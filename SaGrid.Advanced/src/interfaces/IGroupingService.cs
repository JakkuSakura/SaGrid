using System.Collections.Generic;

namespace SaGrid.Advanced.Interfaces;

/// <summary>
/// Describes the grouping contract exposed by the row grouping module. Mirrors the
/// responsibilities of AG Grid's column group service in a C# friendly form.
/// </summary>
public interface IGroupingService
{
    IReadOnlyList<string> GetGroupedColumnIds<TData>(SaGrid<TData> grid);

    void SetGrouping<TData>(SaGrid<TData> grid, IEnumerable<string> columnIds);

    void AddGroupingColumn<TData>(SaGrid<TData> grid, string columnId, int? insertAtIndex = null);

    void RemoveGroupingColumn<TData>(SaGrid<TData> grid, string columnId);

    void MoveGroupingColumn<TData>(SaGrid<TData> grid, string columnId, int targetIndex);

    void ClearGrouping<TData>(SaGrid<TData> grid);

    GroupingConfiguration GetConfiguration<TData>(SaGrid<TData> grid);
}

/// <summary>
/// Metadata describing the current grouping configuration. The structure is intentionally
/// future-proofed so pivot mode and value aggregations can be layered on without breaking
/// existing consumers.
/// </summary>
public sealed class GroupingConfiguration
{
    public GroupingConfiguration(IReadOnlyList<GroupingColumnState> columns, bool pivotModeEnabled)
    {
        Columns = columns;
        PivotModeEnabled = pivotModeEnabled;
    }

    public IReadOnlyList<GroupingColumnState> Columns { get; }

    public bool PivotModeEnabled { get; }
}

/// <summary>
/// Describes an individual grouped column. Additional flags (pivot/value) align with the
/// concepts used inside AG Grid's enterprise row grouping module.
/// </summary>
public sealed class GroupingColumnState
{
    public GroupingColumnState(string columnId, bool isPivotColumn = false, string? aggregationFunction = null)
    {
        ColumnId = columnId;
        IsPivotColumn = isPivotColumn;
        AggregationFunction = aggregationFunction;
    }

    public string ColumnId { get; }

    public bool IsPivotColumn { get; }

    public string? AggregationFunction { get; }

    public GroupingColumnState WithPivot(bool pivot)
    {
        return pivot == IsPivotColumn ? this : new GroupingColumnState(ColumnId, pivot, AggregationFunction);
    }

    public GroupingColumnState WithAggregation(string? aggregation)
    {
        return aggregation == AggregationFunction ? this : new GroupingColumnState(ColumnId, IsPivotColumn, aggregation);
    }
}
