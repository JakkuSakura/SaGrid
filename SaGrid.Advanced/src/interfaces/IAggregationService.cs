using System;
using System.Collections.Generic;
using SaGrid.Core;

namespace SaGrid.Advanced.Interfaces;

public interface IAggregationService
{
    void RegisterFunction(string key, Func<IEnumerable<object?>, object?> aggregator);

    bool TryGetFunction(string key, out Func<IEnumerable<object?>, object?> aggregator);

    AggregationSnapshot GetSnapshot<TData>(SaGrid<TData> grid);

    AggregationComputation<TData> BuildAggregationModel<TData>(SaGrid<TData> grid, IReadOnlyList<Row<TData>> sortedRows);
}

public sealed class AggregationSnapshot
{
    public AggregationSnapshot(IReadOnlyDictionary<string, object?> totals, IReadOnlyList<string> groupedColumns)
    {
        ColumnTotals = totals;
        GroupedColumns = groupedColumns;
    }

    public IReadOnlyDictionary<string, object?> ColumnTotals { get; }

    public IReadOnlyList<string> GroupedColumns { get; }
}

public sealed class AggregationComputation<TData>
{
    public AggregationComputation(RowModel<TData>? rowModel, AggregationSnapshot snapshot)
    {
        RowModel = rowModel;
        Snapshot = snapshot;
    }

    public RowModel<TData>? RowModel { get; }

    public AggregationSnapshot Snapshot { get; }
}
