using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;
using SaGrid;

namespace SaGrid.Advanced.RowModel;

/// <summary>
/// Client-side row model implementation following AG Grid's ClientSideRowModel pattern.
/// Handles all data processing pipeline: filter → sort → group → aggregate → pivot
/// </summary>
public class ClientSideRowModel<TData> : IClientSideRowModel<TData>
{
    private readonly SaGrid<TData> _grid;
    private readonly Table<TData> _table;
    private readonly IAggregationService _aggregationService;
    private List<Row<TData>> _rootRows = new();
    private List<Row<TData>> _filteredRows = new();
    private List<Row<TData>> _sortedRows = new();
    private List<Row<TData>> _groupTopLevelRows = new();
    private List<Row<TData>> _groupFlatRows = new();
    private List<Row<TData>> _finalRows = new();

    public ClientSideRowModel(SaGrid<TData> grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _table = grid;
        _aggregationService = grid.GetAggregationService();
    }

    public IReadOnlyList<Row<TData>> RootRows => _rootRows.AsReadOnly();

    public Row<TData>? GetRow(int index)
    {
        return index >= 0 && index < _finalRows.Count ? _finalRows[index] : null;
    }

    public Row<TData>? GetRowById(string id)
    {
        return _finalRows.FirstOrDefault(row => row.Id == id);
    }

    public int GetRowCount()
    {
        return _finalRows.Count;
    }

    public int GetTopLevelRowCount()
    {
        return _groupTopLevelRows.Count > 0 ? _groupTopLevelRows.Count : GetRowCount();
    }

    public bool IsEmpty()
    {
        return _rootRows.Count == 0;
    }

    public bool IsRowsToRender()
    {
        return _finalRows.Count > 0;
    }

    public void ForEachRow(Action<Row<TData>, int> callback)
    {
        for (int i = 0; i < _finalRows.Count; i++)
        {
            callback(_finalRows[i], i);
        }
    }

    public RowModelType GetRowModelType()
    {
        return RowModelType.ClientSide;
    }

    public bool IsLastRowIndexKnown()
    {
        return true; // Client-side always knows the last row
    }

    public void Start()
    {
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything, NewData: true));
    }

    public void ResetRowHeights()
    {
        // Implementation for row height reset
        // This would typically invalidate cached heights and trigger re-measurement
    }

    public void OnRowHeightChanged()
    {
        // Implementation for when row heights change
        // This would typically update the virtual scrolling calculations
    }

    public void RefreshModel(RefreshModelParams parameters)
    {
        switch (parameters.Stage)
        {
            case ClientSideRowModelStage.Everything:
                ExecuteFilterStage();
                ExecuteSortStage();
                ExecuteGroupStage();
                ExecuteAggregateStage();
                ExecuteMapStage();
                break;
            case ClientSideRowModelStage.Filter:
                ExecuteFilterStage();
                ExecuteSortStage();
                ExecuteGroupStage();
                ExecuteAggregateStage();
                ExecuteMapStage();
                break;
            case ClientSideRowModelStage.Sort:
                ExecuteSortStage();
                ExecuteGroupStage();
                ExecuteAggregateStage();
                ExecuteMapStage();
                break;
            case ClientSideRowModelStage.Map:
                ExecuteMapStage();
                break;
            case ClientSideRowModelStage.Group:
                ExecuteGroupStage();
                ExecuteAggregateStage();
                break;
            case ClientSideRowModelStage.Aggregate:
                ExecuteAggregateStage();
                break;
            case ClientSideRowModelStage.Nothing:
                // Do nothing
                break;
        }
    }

    public void ForEachLeafRow(Action<Row<TData>, int> callback)
    {
        // For non-grouped data, all rows are leaf rows
        ForEachRow(callback);
    }

    public void ForEachRowAfterFilter(Action<Row<TData>, int> callback)
    {
        for (int i = 0; i < _filteredRows.Count; i++)
        {
            callback(_filteredRows[i], i);
        }
    }

    public void ForEachRowAfterFilterAndSort(Action<Row<TData>, int> callback)
    {
        for (int i = 0; i < _sortedRows.Count; i++)
        {
            callback(_sortedRows[i], i);
        }
    }

    public IReadOnlyList<Row<TData>>? GetTopLevelRows()
    {
        if (_groupTopLevelRows.Count > 0)
        {
            return new ReadOnlyCollection<Row<TData>>(_groupTopLevelRows);
        }

        return new ReadOnlyCollection<Row<TData>>(_finalRows);
    }

    public bool IsRowDataLoaded()
    {
        return _rootRows.Count > 0;
    }

    public RowTransaction<TData>? UpdateRowData(RowDataTransaction<TData> transaction)
    {
        var addedRows = new List<Row<TData>>();
        var removedRows = new List<Row<TData>>();
        var updatedRows = new List<Row<TData>>();

        // Handle additions
        if (transaction.Add != null)
        {
            foreach (var data in transaction.Add)
            {
                // Create a new row - for now we'll use a simple approach
                var row = new Row<TData>(_table, _rootRows.Count.ToString(), _rootRows.Count, data, 0, null);
                _rootRows.Add(row);
                addedRows.Add(row);
            }
        }

        // Handle removals
        if (transaction.Remove != null)
        {
            foreach (var data in transaction.Remove)
            {
                var rowToRemove = _rootRows.FirstOrDefault(r => ReferenceEquals(r.Original, data));
                if (rowToRemove != null)
                {
                    _rootRows.Remove(rowToRemove);
                    removedRows.Add(rowToRemove);
                }
            }
        }

        // Handle updates
        if (transaction.Update != null)
        {
            foreach (var data in transaction.Update)
            {
                var rowToUpdate = _rootRows.FirstOrDefault(r => ReferenceEquals(r.Original, data));
                if (rowToUpdate != null)
                {
                    // Update the row data
                    updatedRows.Add(rowToUpdate);
                }
            }
        }

        // Refresh the model after transaction
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything, RowDataUpdated: true));

        return new RowTransaction<TData>(addedRows, removedRows, updatedRows);
    }

    public void BatchUpdateRowData(RowDataTransaction<TData> transaction, Action<RowTransaction<TData>>? callback = null)
    {
        var result = UpdateRowData(transaction);
        callback?.Invoke(result!);
    }

    public void DoAggregate()
    {
        ExecuteAggregateStage();
    }

    // Internal methods for processing stages

    private void ExecuteFilterStage()
    {
        _filteredRows = _table.PreFilteredRowModel.Rows.ToList();
    }

    private void ExecuteSortStage()
    {
        _sortedRows = _table.PreSortedRowModel.Rows.ToList();
    }

    private void ExecuteMapStage()
    {
        var currentModel = _table.RowModel;
        _finalRows = currentModel.Rows.ToList();
    }

    private void ExecuteGroupStage()
    {
        var computation = _aggregationService.BuildAggregationModel(_grid, _table.PreSortedRowModel.Rows);
        _grid.DispatchEvent(GridEventTypes.AggregationChanged, new AggregationChangedEventArgs(_grid, computation.Snapshot));

        if (computation.RowModel != null)
        {
            _table.ReplaceFinalRowModel(computation.RowModel);
            _groupTopLevelRows = computation.RowModel.Rows.ToList();
            _groupFlatRows = computation.RowModel.FlatRows.ToList();
        }
        else
        {
            _groupTopLevelRows = _sortedRows.ToList();
            _groupFlatRows = _sortedRows.ToList();
        }
    }

    private void ExecuteAggregateStage()
    {
        if (_groupFlatRows.Count > 0)
        {
            _finalRows = _groupFlatRows.ToList();
        }
        else
        {
            _finalRows = _sortedRows.ToList();
        }
    }

    public void SetRootRows(IEnumerable<Row<TData>> rows)
    {
        _rootRows = rows.ToList();
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything, NewData: true));
    }
}
