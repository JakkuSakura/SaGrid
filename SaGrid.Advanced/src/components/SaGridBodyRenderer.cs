using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced.Components;
using SaGrid.Advanced.Interfaces;
using SaGrid.Avalonia;
using SaGrid.Core;
using SaGrid.Core.Models;

namespace SaGrid;

internal interface ISelectionAwareRowsControl
{
    void ApplySelectionDelta(CellSelectionDelta delta);
}

internal class SaGridBodyRenderer<TData>
{
    private readonly SaGridCellRenderer<TData> _cellRenderer;

    public SaGridBodyRenderer()
    {
        _cellRenderer = new SaGridCellRenderer<TData>();
    }

    public Control CreateBody(
        ISaGridComponentHost<TData> host,
        Table<TData> table,
        TableColumnLayoutManager<TData> layoutManager,
        Func<ISaGridComponentHost<TData>>? hostSignalGetter = null,
        Func<int>? selectionSignalGetter = null)
    {
        var initialRows = FlattenRows(table.RowModel.Rows).ToList();
        return new VirtualizedRowsControl(host, table, initialRows, _cellRenderer, layoutManager, hostSignalGetter, selectionSignalGetter);
    }

    private static IEnumerable<Row<TData>> FlattenRows(IReadOnlyList<Row<TData>> rows)
    {
        foreach (var row in rows)
        {
            yield return row;

            if (row.SubRows.Count > 0)
            {
                var children = row.SubRows
                    .OfType<Row<TData>>()
                    .ToList();

                foreach (var child in FlattenRows(children))
                {
                    yield return child;
                }
            }
        }
    }

    private sealed class VirtualizedRowsControl : ContentControl, ISelectionAwareRowsControl
    {
        private const double RowHeight = 30;
        private const int Overscan = 4;

        private readonly ISaGridComponentHost<TData> _host;
        private readonly SaGridCellRenderer<TData> _cellRenderer;
        private readonly Func<ISaGridComponentHost<TData>>? _hostSignalGetter;
        private readonly Func<int>? _selectionSignalGetter;
        private readonly ScrollViewer _scrollViewer;
        private readonly Canvas _canvas;
        private readonly IReadOnlyList<Row<TData>> _initialRows;
        private readonly TableColumnLayoutManager<TData> _layoutManager;
        private bool _hasRealRows;
        private int _lastKnownRowCount = -1;
        private readonly Dictionary<string, RowControlContainer> _rowControlsById = new();
        private readonly Table<TData> _table;

        public VirtualizedRowsControl(
            ISaGridComponentHost<TData> host,
            Table<TData> table,
            IReadOnlyList<Row<TData>> initialRows,
            SaGridCellRenderer<TData> cellRenderer,
            TableColumnLayoutManager<TData> layoutManager,
            Func<ISaGridComponentHost<TData>>? hostSignalGetter,
            Func<int>? selectionSignalGetter)
        {
            _host = host;
            _table = table;
            _initialRows = initialRows;
            _cellRenderer = cellRenderer;
            _layoutManager = layoutManager;
            _hostSignalGetter = hostSignalGetter;
            _selectionSignalGetter = selectionSignalGetter;

            _canvas = new Canvas();
            _scrollViewer = new ScrollViewer
            {
                Focusable = false,
                Content = _canvas
            };

            Content = _scrollViewer;

            _scrollViewer.PropertyChanged += ScrollViewerOnPropertyChanged;
            AttachedToVisualTree += OnAttached;
            DetachedFromVisualTree += OnDetached;
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _host.RowDataChanged += OnRowDataChanged;
            _ = _host.EnsureDataRangeAsync(0, _host.GetPreferredFetchSize());
            UpdateViewport(force: true);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _host.RowDataChanged -= OnRowDataChanged;
        }

        private void OnRowDataChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => UpdateViewport(force: true));
        }

        private void ScrollViewerOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ViewportProperty)
            {
                UpdateViewport();
            }
        }

        private void UpdateViewport(bool force = false)
        {
            var rowCount = _host.GetApproximateRowCount();
            var totalRows = Math.Max(0, rowCount);

            if (totalRows == 0 && !_hasRealRows && _initialRows.Count > 0)
            {
                totalRows = _initialRows.Count;
            }

            if (totalRows != _lastKnownRowCount)
            {
                force = true;
                _lastKnownRowCount = totalRows;
            }

            var viewport = _scrollViewer.Viewport;
            var viewportHeight = viewport.Height;
            if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
            {
                viewportHeight = _scrollViewer.Bounds.Height;
            }
            if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
            {
                viewportHeight = RowHeight * 20;
            }

            if (totalRows == 0)
            {
                foreach (var container in _rowControlsById.Values)
                {
                    container.Detach(_canvas);
                }

                _rowControlsById.Clear();
                _canvas.Children.Clear();
                _canvas.Height = 0;
                return;
            }

            if (rowCount > 0)
            {
                _hasRealRows = true;
            }

            _canvas.Height = totalRows * RowHeight;

            if (force)
            {
                PruneStaleControlsIfNeeded();
            }

            var verticalOffset = _scrollViewer.Offset.Y;
            var startIndex = Math.Max(0, (int)(verticalOffset / RowHeight) - Overscan);
            var maxVisible = (int)System.Math.Ceiling(viewportHeight / RowHeight) + (Overscan * 2);
            if (maxVisible <= 0)
            {
                maxVisible = Overscan * 2 + _host.GetPreferredFetchSize();
            }
            var endIndex = System.Math.Min(totalRows, startIndex + System.Math.Max(maxVisible, 0));

            if (endIndex <= startIndex)
            {
                endIndex = System.Math.Min(totalRows, startIndex + _host.GetPreferredFetchSize());
            }

            var visibleRowIds = new HashSet<string>();

            for (var rowIndex = startIndex; rowIndex < endIndex; rowIndex++)
            {
                var row = _host.TryGetDisplayedRow(rowIndex) ?? GetFallbackRow(rowIndex);
                if (row == null)
                {
                    _ = _host.EnsureDataRangeAsync(rowIndex, rowIndex + _host.GetPreferredFetchSize());
                    continue;
                }

                var displayIndex = row.DisplayIndex >= 0 ? row.DisplayIndex : rowIndex;
                var rowId = row.Id ?? displayIndex.ToString();

                visibleRowIds.Add(rowId);

                if (!_rowControlsById.TryGetValue(rowId, out var container))
                {
                    container = CreateRowControl(row, displayIndex, rowId);
                    _rowControlsById[rowId] = container;
                }

                if (!container.IsAttached)
                {
                    container.Attach(_canvas);
                }

                Canvas.SetTop(container.Control, rowIndex * RowHeight);
                Canvas.SetLeft(container.Control, 0);
                container.Update(row, displayIndex, force);
            }

            var stale = _rowControlsById.Keys
                .Where(id => !visibleRowIds.Contains(id))
                .ToList();

            foreach (var staleId in stale)
            {
                if (_rowControlsById.TryGetValue(staleId, out var container))
                {
                    container.Detach(_canvas);
                }
            }
        }

        private Row<TData>? GetFallbackRow(int index)
        {
            if (index >= 0 && index < _initialRows.Count)
            {
                return _initialRows[index];
            }

            return null;
        }

        public void ApplySelectionDelta(CellSelectionDelta delta)
        {
            _ = delta;
            UpdateViewport(force: true);
        }

        private void PruneStaleControlsIfNeeded()
        {
            if (!_hasRealRows || _rowControlsById.Count == 0)
            {
                return;
            }

            if (_host.GetActiveRowModelType() == RowModelType.ServerSide)
            {
                return;
            }

            var currentRowIds = _table.RowModel.FlatRows.Select(r => r.Id).ToHashSet();
            if (currentRowIds.Count == 0)
            {
                return;
            }

            var staleIds = _rowControlsById.Keys
                .Where(id => !currentRowIds.Contains(id))
                .ToList();

            foreach (var staleId in staleIds)
            {
                if (_rowControlsById.TryGetValue(staleId, out var container))
                {
                    container.Detach(_canvas);
                }

                _rowControlsById.Remove(staleId);
            }
        }

        private RowControlContainer CreateRowControl(Row<TData> row, int displayIndex, string rowId)
        {
            var panel = _layoutManager.CreatePanel();
            panel.Height = RowHeight;

            var cellVisuals = new List<IReusableCellVisual<TData>>();
            var cellVisualsByColumn = new Dictionary<string, IReusableCellVisual<TData>>();

            foreach (var column in _table.VisibleLeafColumns)
            {
                var control = _hostSignalGetter != null
                    ? _cellRenderer.CreateReactiveCell(_host, _table, row, column, displayIndex, _hostSignalGetter, _selectionSignalGetter)
                    : _cellRenderer.CreateCell(_host, _table, row, column, displayIndex);

                if (control is IReusableCellVisual<TData> reusable)
                {
                    cellVisuals.Add(reusable);
                    cellVisualsByColumn[column.Id] = reusable;
                }

                ColumnLayoutPanel.SetColumnId(control, column.Id);
                panel.Children.Add(control);
            }

            panel.Tag = rowId;
            return new RowControlContainer(panel, cellVisuals, cellVisualsByColumn);
        }

        private sealed class RowControlContainer
        {
            private readonly Control _control;
            private readonly List<IReusableCellVisual<TData>> _cellVisuals;
            private readonly Dictionary<string, IReusableCellVisual<TData>> _cellVisualsByColumn;

            public RowControlContainer(
                Control control,
                List<IReusableCellVisual<TData>> cellVisuals,
                Dictionary<string, IReusableCellVisual<TData>> cellVisualsByColumn)
            {
                _control = control;
                _cellVisuals = cellVisuals;
                _cellVisualsByColumn = cellVisualsByColumn;
            }

            public Control Control => _control;
            public bool IsAttached { get; private set; }

            public void Attach(Canvas canvas)
            {
                if (IsAttached)
                {
                    return;
                }

                canvas.Children.Add(_control);
                IsAttached = true;
            }

            public void Detach(Canvas canvas)
            {
                if (!IsAttached)
                {
                    return;
                }

                canvas.Children.Remove(_control);
                IsAttached = false;
            }

            public void Update(Row<TData> row, int displayIndex, bool force = false)
            {
                for (var i = 0; i < _cellVisuals.Count; i++)
                {
                    _cellVisuals[i].Update(row, displayIndex, force);
                }
            }

            public bool TryGetCell(string columnId, out IReusableCellVisual<TData>? cell)
            {
                return _cellVisualsByColumn.TryGetValue(columnId, out cell);
            }
        }

        private static Control CreatePlaceholderRow()
        {
            return new Border
            {
                Height = RowHeight,
                Background = Brushes.Transparent
            };
        }
    }
}
