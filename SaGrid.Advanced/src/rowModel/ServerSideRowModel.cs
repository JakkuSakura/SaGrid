using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;
using SaGrid;

namespace SaGrid.Advanced.RowModel;

/// <summary>
/// Lightweight server-side row model that mirrors AG Grid's server-side row model contracts.
/// Handles block-based lazy loading and caches rows provided by an <see cref="IServerSideDataSource{TData}"/>.
/// </summary>
public sealed class ServerSideRowModel<TData> : IServerSideRowModel<TData>
{
    private readonly SaGrid<TData> _grid;
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<int, Task> _pendingBlocks = new();
    private readonly Dictionary<int, Row<TData>> _rowCache = new();
    private readonly HashSet<int> _loadedBlocks = new();
    private int _maxLoadedRowIndex = -1;
    private int _maxRequestedRowIndex = 0;
    private int? _lastRow;
    private IServerSideDataSource<TData>? _dataSource;

    public event EventHandler? RowsChanged;

    public int BlockSize { get; }

    public bool HasDataSource => _dataSource != null;

    public ServerSideRowModel(SaGrid<TData> grid, int blockSize)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        BlockSize = Math.Max(1, blockSize);
    }

    public void SetDataSource(IServerSideDataSource<TData> dataSource, bool refresh = true)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ClearCache();
        if (refresh)
        {
            _ = EnsureRangeAsync(0, BlockSize);
        }
    }

    public void Refresh(ServerSideRefreshMode mode = ServerSideRefreshMode.Full, bool purge = false)
    {
        if (purge)
        {
            ClearCache();
        }

        if (mode == ServerSideRefreshMode.Full)
        {
            _ = EnsureRangeAsync(0, BlockSize);
        }
    }

    public async Task EnsureRangeAsync(int startRow, int endRow, CancellationToken cancellationToken = default)
    {
        if (_dataSource == null)
        {
            return;
        }

        if (startRow < 0)
        {
            startRow = 0;
        }

        if (endRow <= startRow)
        {
            endRow = startRow + BlockSize;
        }

        int startBlock = startRow / BlockSize;
        int endBlock = (endRow - 1) / BlockSize;

        var tasks = new List<Task>();
        for (int block = startBlock; block <= endBlock; block++)
        {
            if (IsBlockLoaded(block))
            {
                continue;
            }

            tasks.Add(LoadBlockAsync(block, cancellationToken));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    public Row<TData>? GetRow(int index)
    {
        lock (_sync)
        {
            return _rowCache.TryGetValue(index, out var row) ? row : null;
        }
    }

    public Row<TData>? GetRowById(string id)
    {
        lock (_sync)
        {
            return _rowCache.Values.FirstOrDefault(r => r.Id == id);
        }
    }

    public int GetRowCount()
    {
        lock (_sync)
        {
            if (_lastRow.HasValue)
            {
                return _lastRow.Value;
            }

            if (_maxLoadedRowIndex >= 0)
            {
                return Math.Max(_maxLoadedRowIndex + 1 + BlockSize, _maxRequestedRowIndex);
            }

            return BlockSize;
        }
    }

    public int GetTopLevelRowCount()
    {
        return GetRowCount();
    }

    public bool IsEmpty()
    {
        lock (_sync)
        {
            return _lastRow == 0 || (_rowCache.Count == 0 && !_lastRow.HasValue);
        }
    }

    public bool IsRowsToRender()
    {
        lock (_sync)
        {
            return _rowCache.Count > 0;
        }
    }

    public void ForEachRow(Action<Row<TData>, int> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));

        lock (_sync)
        {
            foreach (var kvp in _rowCache.OrderBy(k => k.Key))
            {
                callback(kvp.Value, kvp.Key);
            }
        }
    }

    public RowModelType GetRowModelType()
    {
        return RowModelType.ServerSide;
    }

    public bool IsLastRowIndexKnown()
    {
        lock (_sync)
        {
            return _lastRow.HasValue;
        }
    }

    public void Start()
    {
        if (_dataSource != null)
        {
            _ = EnsureRangeAsync(0, BlockSize);
        }
    }

    public void ResetRowHeights()
    {
        // No-op for server-side row model (heights handled by the viewport control)
    }

    public void OnRowHeightChanged()
    {
        // No-op for server-side row model
    }

    public IReadOnlyList<Row<TData>> RootRows => _rowCache.OrderBy(k => k.Key).Select(k => k.Value).ToList();

    private bool IsBlockLoaded(int blockIndex)
    {
        lock (_sync)
        {
            return _loadedBlocks.Contains(blockIndex);
        }
    }

    private async Task LoadBlockAsync(int blockIndex, CancellationToken cancellationToken)
    {
        if (_dataSource == null)
        {
            return;
        }

        var task = _pendingBlocks.GetOrAdd(blockIndex, _ => LoadBlockCoreAsync(blockIndex, cancellationToken));

        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            _pendingBlocks.TryRemove(blockIndex, out _);
        }
    }

    private async Task LoadBlockCoreAsync(int blockIndex, CancellationToken cancellationToken)
    {
        if (_dataSource == null)
        {
            return;
        }

        var startRow = blockIndex * BlockSize;
        var endRow = startRow + BlockSize;

        lock (_sync)
        {
            _maxRequestedRowIndex = Math.Max(_maxRequestedRowIndex, endRow);
        }

        var request = CreateRequest(startRow, endRow);
        var result = await _dataSource.GetRowsAsync(request, cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            var rowIndex = startRow;
            foreach (var data in result.Rows)
            {
                var id = $"server_{rowIndex}";
                var row = new Row<TData>(_grid.InnerTable, id, rowIndex, data, 0, null);
                _rowCache[rowIndex] = row;
                _maxLoadedRowIndex = Math.Max(_maxLoadedRowIndex, rowIndex);
                rowIndex++;
            }

            if (result.LastRow.HasValue)
            {
                _lastRow = result.LastRow.Value;
            }

            _loadedBlocks.Add(blockIndex);
        }

        RowsChanged?.Invoke(this, EventArgs.Empty);
    }

    private ServerSideRowsRequest CreateRequest(int startRow, int endRow)
    {
        var sortModel = (_grid.State.Sorting?.Columns ?? new List<ColumnSort>()).ToList();

        var filters = new Dictionary<string, object?>();
        var columnFilters = _grid.State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        foreach (var filter in columnFilters)
        {
            filters[filter.Id] = filter.Value;
        }

        if (_grid.State.GlobalFilter?.Value != null)
        {
            filters["__global"] = _grid.State.GlobalFilter.Value;
        }

        return new ServerSideRowsRequest(startRow, endRow, sortModel, filters);
    }

    private void ClearCache()
    {
        lock (_sync)
        {
            _rowCache.Clear();
            _loadedBlocks.Clear();
            _lastRow = null;
            _maxLoadedRowIndex = -1;
            _maxRequestedRowIndex = 0;
        }

        RowsChanged?.Invoke(this, EventArgs.Empty);
    }
}
