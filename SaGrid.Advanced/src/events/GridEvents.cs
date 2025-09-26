using System.Collections.Generic;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Modules.Analytics;
using SaGrid.Advanced.Modules.Editing;
using SaGrid.Advanced.Modules.Export;
using SaGrid.Core;

namespace SaGrid.Advanced.Events;

/// <summary>
/// Event arguments for grid lifecycle events
/// </summary>
public class GridReadyEventArgs : AgEventArgs
{
    public GridReadyEventArgs(object source) : base(GridEventTypes.GridReady, source) { }
}

public class GridPreDestroyedEventArgs : AgEventArgs
{
    public GridPreDestroyedEventArgs(object source) : base(GridEventTypes.GridPreDestroyed, source) { }
}

/// <summary>
/// Event arguments for row-related events
/// </summary>
public class RowDataChangedEventArgs<TData> : AgEventArgs
{
    public IReadOnlyList<Row<TData>> Rows { get; }

    public RowDataChangedEventArgs(object source, IReadOnlyList<Row<TData>> rows) 
        : base(GridEventTypes.RowDataChanged, source)
    {
        Rows = rows;
    }
}

public class RowSelectedEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public bool Selected { get; }

    public RowSelectedEventArgs(object source, Row<TData> row, bool selected) 
        : base(GridEventTypes.RowSelected, source)
    {
        Row = row;
        Selected = selected;
    }
}

public class RowClickedEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public int RowIndex { get; }
    public string? ColumnId { get; }

    public RowClickedEventArgs(object source, Row<TData> row, int rowIndex, string? columnId = null) 
        : base(GridEventTypes.RowClicked, source)
    {
        Row = row;
        RowIndex = rowIndex;
        ColumnId = columnId;
    }
}

/// <summary>
/// Event arguments for cell-related events
/// </summary>
public class CellClickedEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public int RowIndex { get; }
    public string ColumnId { get; }
    public object? Value { get; }

    public CellClickedEventArgs(object source, Row<TData> row, int rowIndex, string columnId, object? value) 
        : base(GridEventTypes.CellClicked, source)
    {
        Row = row;
        RowIndex = rowIndex;
        ColumnId = columnId;
        Value = value;
    }
}

public class CellValueChangedEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public int RowIndex { get; }
    public string ColumnId { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public CellValueChangedEventArgs(object source, Row<TData> row, int rowIndex, string columnId, object? oldValue, object? newValue) 
        : base(GridEventTypes.CellValueChanged, source)
    {
        Row = row;
        RowIndex = rowIndex;
        ColumnId = columnId;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Event arguments for column-related events
/// </summary>
public class ColumnResizedEventArgs<TData> : AgEventArgs
{
    public Column<TData> Column { get; }
    public double OldWidth { get; }
    public double NewWidth { get; }

    public ColumnResizedEventArgs(object source, Column<TData> column, double oldWidth, double newWidth) 
        : base(GridEventTypes.ColumnResized, source)
    {
        Column = column;
        OldWidth = oldWidth;
        NewWidth = newWidth;
    }
}

public class ColumnMovedEventArgs<TData> : AgEventArgs
{
    public Column<TData> Column { get; }
    public int OldIndex { get; }
    public int NewIndex { get; }

    public ColumnMovedEventArgs(object source, Column<TData> column, int oldIndex, int newIndex) 
        : base(GridEventTypes.ColumnMoved, source)
    {
        Column = column;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}

/// <summary>
/// Event arguments for filter and sort events
/// </summary>
public class FilterChangedEventArgs : AgEventArgs
{
    public string? ColumnId { get; }
    public object? FilterModel { get; }

    public FilterChangedEventArgs(object source, string? columnId = null, object? filterModel = null) 
        : base(GridEventTypes.FilterChanged, source)
    {
        ColumnId = columnId;
        FilterModel = filterModel;
    }
}

public class SortChangedEventArgs : AgEventArgs
{
    public string? ColumnId { get; }
    public SortDirection? Direction { get; }

    public SortChangedEventArgs(object source, string? columnId = null, SortDirection? direction = null) 
        : base(GridEventTypes.SortChanged, source)
    {
        ColumnId = columnId;
        Direction = direction;
    }
}

/// <summary>
/// Event arguments for grouping changes
/// </summary>
public class GroupingChangedEventArgs : AgEventArgs
{
    public IReadOnlyList<string> GroupedColumnIds { get; }

    public GroupingChangedEventArgs(object source, IReadOnlyList<string> groupedColumns)
        : base(GridEventTypes.GroupingChanged, source)
    {
        GroupedColumnIds = groupedColumns;
    }
}

/// <summary>
/// Event arguments for aggregation changes
/// </summary>
public class AggregationChangedEventArgs : AgEventArgs
{
    public AggregationSnapshot Snapshot { get; }

    public AggregationChangedEventArgs(object source, AggregationSnapshot snapshot)
        : base(GridEventTypes.AggregationChanged, source)
    {
        Snapshot = snapshot;
    }
}

public class ChartCreatedEventArgs : AgEventArgs
{
    public ChartData ChartData { get; }
    public ChartRequest Request { get; }

    public ChartCreatedEventArgs(object source, ChartData chartData, ChartRequest request)
        : base(GridEventTypes.ChartCreated, source)
    {
        ChartData = chartData;
        Request = request;
    }
}

public class ExportPerformedEventArgs : AgEventArgs
{
    public ExportRequest Request { get; }
    public ExportResult Result { get; }

    public ExportPerformedEventArgs(object source, ExportRequest request, ExportResult result)
        : base(GridEventTypes.ExportPerformed, source)
    {
        Request = request;
        Result = result;
    }
}

/// <summary>
/// Event arguments for cell edit start
/// </summary>
public sealed class CellEditStartedEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public Column<TData> Column { get; }

    public CellEditStartedEventArgs(object source, Row<TData> row, Column<TData> column)
        : base(GridEventTypes.CellEditStarted, source)
    {
        Row = row;
        Column = column;
    }
}

/// <summary>
/// Event arguments for cell edit commit
/// </summary>
public sealed class CellEditCommittedEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public Column<TData> Column { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public CellEditCommittedEventArgs(object source, Row<TData> row, Column<TData> column, object? oldValue, object? newValue)
        : base(GridEventTypes.CellEditCommitted, source)
    {
        Row = row;
        Column = column;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Event arguments for cell edit cancellation
/// </summary>
public sealed class CellEditCancelledEventArgs<TData> : AgEventArgs
{
    public Row<TData> Row { get; }
    public Column<TData> Column { get; }

    public CellEditCancelledEventArgs(object source, Row<TData> row, Column<TData> column)
        : base(GridEventTypes.CellEditCancelled, source)
    {
        Row = row;
        Column = column;
    }
}

/// <summary>
/// Event arguments for batch edit operations
/// </summary>
public sealed class BatchEditEventArgs<TData> : AgEventArgs
{
    public IReadOnlyCollection<CellEditEntry<TData>> Edits { get; }

    public BatchEditEventArgs(object source, string eventType, IReadOnlyCollection<CellEditEntry<TData>> edits)
        : base(eventType, source)
    {
        Edits = edits;
    }
}

/// <summary>
/// Event arguments for selection events
/// </summary>
public class SelectionChangedEventArgs<TData> : AgEventArgs
{
    public IReadOnlyList<Row<TData>> SelectedRows { get; }

    public SelectionChangedEventArgs(object source, IReadOnlyList<Row<TData>> selectedRows) 
        : base(GridEventTypes.SelectionChanged, source)
    {
        SelectedRows = selectedRows;
    }
}

/// <summary>
/// Event arguments for model updates
/// </summary>
public class ModelUpdatedEventArgs : AgEventArgs
{
    public bool Animate { get; }
    public bool KeepRenderedRows { get; }
    public bool NewData { get; }

    public ModelUpdatedEventArgs(object source, bool animate = false, bool keepRenderedRows = false, bool newData = false) 
        : base(GridEventTypes.ModelUpdated, source)
    {
        Animate = animate;
        KeepRenderedRows = keepRenderedRows;
        NewData = newData;
    }
}
