using System;
using System.Collections.Generic;
using SaGrid.Core;

namespace SaGrid.Advanced.Interfaces;

/// <summary>
/// Client-side row model interface following AG Grid's IClientSideRowModel pattern.
/// Handles all data processing stages: filter, sort, group, aggregate, pivot
/// </summary>
public interface IClientSideRowModel<TData> : IRowModel<TData>
{
    /// <summary>
    /// All rows before any processing
    /// </summary>
    IReadOnlyList<Row<TData>> RootRows { get; }

    /// <summary>
    /// Refresh the model with specified stage
    /// </summary>
    void RefreshModel(RefreshModelParams parameters);

    /// <summary>
    /// Iterate through leaf nodes only
    /// </summary>
    void ForEachLeafRow(Action<Row<TData>, int> callback);

    /// <summary>
    /// Iterate through rows after filtering
    /// </summary>
    void ForEachRowAfterFilter(Action<Row<TData>, int> callback);

    /// <summary>
    /// Iterate through rows after filtering and sorting
    /// </summary>
    void ForEachRowAfterFilterAndSort(Action<Row<TData>, int> callback);

    /// <summary>
    /// Get top level rows (for grouping)
    /// </summary>
    IReadOnlyList<Row<TData>>? GetTopLevelRows();

    /// <summary>
    /// Returns true if row data is loaded
    /// </summary>
    bool IsRowDataLoaded();

    /// <summary>
    /// Update row data with transaction
    /// </summary>
    RowTransaction<TData>? UpdateRowData(RowDataTransaction<TData> transaction);

    /// <summary>
    /// Batch update row data
    /// </summary>
    void BatchUpdateRowData(RowDataTransaction<TData> transaction, Action<RowTransaction<TData>>? callback = null);

    /// <summary>
    /// Execute aggregation
    /// </summary>
    void DoAggregate();
}

/// <summary>
/// Client-side row model processing stages following AG Grid's pattern
/// </summary>
public enum ClientSideRowModelStage
{
    Everything,
    Group,
    Filter,
    Sort,
    Map,
    Aggregate,
    FilterAggregates,
    Pivot,
    Nothing
}

/// <summary>
/// Parameters for refreshing the model
/// </summary>
public record RefreshModelParams(
    ClientSideRowModelStage Stage,
    bool RowDataUpdated = false,
    bool NewData = false
);

/// <summary>
/// Row data transaction for adding/removing/updating rows
/// </summary>
public record RowDataTransaction<TData>(
    IEnumerable<TData>? Add = null,
    IEnumerable<TData>? Remove = null,
    IEnumerable<TData>? Update = null,
    int? AddIndex = null
);

/// <summary>
/// Result of a row transaction
/// </summary>
public record RowTransaction<TData>(
    IReadOnlyList<Row<TData>> Add,
    IReadOnlyList<Row<TData>> Remove,
    IReadOnlyList<Row<TData>> Update
);