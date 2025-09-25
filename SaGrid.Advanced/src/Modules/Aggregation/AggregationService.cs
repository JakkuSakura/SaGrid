using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SaGrid.Advanced.Interfaces;
using SaGrid;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Aggregation;

public sealed class AggregationService : IAggregationService
{
    private sealed class AggregationSnapshotState
    {
        public IReadOnlyDictionary<string, object?> ColumnTotals { get; set; } = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
        public IReadOnlyList<string> GroupColumns { get; set; } = Array.Empty<string>();

        public AggregationSnapshot ToSnapshot()
        {
            return new AggregationSnapshot(ColumnTotals, GroupColumns);
        }
    }

    private sealed class GroupBucket<TData>
    {
        public GroupBucket(int index, string key, object? rawKey)
        {
            Index = index;
            Key = key;
            RawKey = rawKey;
            Rows = new List<Row<TData>>();
        }

        public int Index { get; }
        public string Key { get; }
        public object? RawKey { get; }
        public List<Row<TData>> Rows { get; }
    }

    private readonly Dictionary<string, Func<IEnumerable<object?>, object?>> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConditionalWeakTable<object, AggregationSnapshotState> _snapshots = new();

    public AggregationService()
    {
        RegisterBuiltIns();
    }

    public void RegisterFunction(string key, Func<IEnumerable<object?>, object?> aggregator)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required", nameof(key));
        if (aggregator == null) throw new ArgumentNullException(nameof(aggregator));
        _functions[key] = aggregator;
    }

    public bool TryGetFunction(string key, out Func<IEnumerable<object?>, object?> aggregator)
    {
        return _functions.TryGetValue(key, out aggregator!);
    }

    public AggregationSnapshot GetSnapshot<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        var state = _snapshots.GetValue(grid, _ => new AggregationSnapshotState());
        return state.ToSnapshot();
    }

    public AggregationComputation<TData> BuildAggregationModel<TData>(SaGrid<TData> grid, IReadOnlyList<Row<TData>> sortedRows)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (sortedRows == null) throw new ArgumentNullException(nameof(sortedRows));

        var grouping = grid.State.Grouping?.Groups ?? new List<string>();
        var totals = ComputeColumnTotals(grid, sortedRows);
        var snapshot = UpdateSnapshot(grid, totals, grouping);

        if (grouping.Count == 0)
        {
            return new AggregationComputation<TData>(null, snapshot);
        }

        var rowModel = BuildGroupedRowModel(grid, sortedRows, grouping);
        return new AggregationComputation<TData>(rowModel, snapshot);
    }

    private AggregationSnapshot UpdateSnapshot<TData>(SaGrid<TData> grid, IReadOnlyDictionary<string, object?> totals, IReadOnlyList<string> grouping)
    {
        var state = _snapshots.GetValue(grid, _ => new AggregationSnapshotState());
        state.ColumnTotals = totals;
        state.GroupColumns = grouping.ToArray();
        return state.ToSnapshot();
    }

    private RowModel<TData> BuildGroupedRowModel<TData>(SaGrid<TData> grid, IReadOnlyList<Row<TData>> rows, IReadOnlyList<string> grouping)
    {
        var flatRows = new List<Row<TData>>();
        var rowsById = new Dictionary<string, Row<TData>>();
        var runningIndex = 0;
        var topLevel = BuildLevel(grid, rows, grouping, 0, null, flatRows, rowsById, ref runningIndex);

        return new RowModel<TData>
        {
            Rows = new ReadOnlyCollection<Row<TData>>(topLevel),
            FlatRows = new ReadOnlyCollection<Row<TData>>(flatRows),
            RowsById = new ReadOnlyDictionary<string, Row<TData>>(rowsById)
        };
    }

    private List<Row<TData>> BuildLevel<TData>(SaGrid<TData> grid,
        IEnumerable<Row<TData>> source,
        IReadOnlyList<string> grouping,
        int level,
        Row<TData>? parent,
        List<Row<TData>> flatRows,
        Dictionary<string, Row<TData>> rowsById,
        ref int runningIndex)
    {
        if (level >= grouping.Count)
        {
            var leafRows = new List<Row<TData>>();
            foreach (var row in source)
            {
                var clone = CloneLeafRow(grid, row, level, parent, runningIndex++);
                leafRows.Add(clone);
                flatRows.Add(clone);
                rowsById[clone.Id] = clone;
            }
            return leafRows;
        }

        var columnId = grouping[level];
        var buckets = CreateBuckets<TData>(source, columnId);
        var result = new List<Row<TData>>();

        foreach (var bucket in buckets)
        {
            var aggregatedValues = ComputeAggregations(grid, bucket.Rows);
            aggregatedValues[columnId] = bucket.RawKey;

            var groupId = CreateGroupRowId(parent, columnId, bucket.Index);
            var groupRow = new Row<TData>(grid, groupId, runningIndex++, default!, level, parent, aggregatedValues, isGroupRow: true);
            groupRow.SetGroupInfo(columnId, bucket.RawKey);

            flatRows.Add(groupRow);
            rowsById[groupRow.Id] = groupRow;
            result.Add(groupRow);

            BuildLevel(grid, bucket.Rows, grouping, level + 1, groupRow, flatRows, rowsById, ref runningIndex);
        }

        return result;
    }

    private static string CreateGroupRowId<TData>(Row<TData>? parent, string columnId, int index)
    {
        var prefix = parent?.Id ?? "root";
        return $"{prefix}|group|{columnId}|{index}";
    }

    private Row<TData> CloneLeafRow<TData>(SaGrid<TData> grid, Row<TData> original, int depth, Row<TData>? parent, int position)
    {
        return new Row<TData>(grid, original.Id, position, original.Original, depth, parent);
    }

    private IReadOnlyDictionary<string, object?> ComputeColumnTotals<TData>(SaGrid<TData> grid, IEnumerable<Row<TData>> rows)
    {
        var totals = new Dictionary<string, object?>();
        var rowList = rows.ToList();
        foreach (var column in grid.VisibleLeafColumns)
        {
            var values = rowList.Select(r => r.GetCell(column.Id).Value).ToList();
            var aggregator = ResolveAggregator(column);
            var result = SafeAggregate(aggregator, values);

            if (result == null && !ReferenceEquals(aggregator, _functions["count"]))
            {
                result = SafeAggregate(_functions["count"], values);
            }

            if (result != null)
            {
                totals[column.Id] = result;
            }
        }

        return new ReadOnlyDictionary<string, object?>(totals);
    }

    private Dictionary<string, object?> ComputeAggregations<TData>(SaGrid<TData> grid, IEnumerable<Row<TData>> rows)
    {
        var aggregations = new Dictionary<string, object?>();
        var rowList = rows.ToList();

        foreach (var column in grid.VisibleLeafColumns)
        {
            var values = rowList.Select(r => r.GetCell(column.Id).Value).ToList();
            var aggregator = ResolveAggregator(column);
            var result = SafeAggregate(aggregator, values);

            if (result == null && !ReferenceEquals(aggregator, _functions["count"]))
            {
                result = SafeAggregate(_functions["count"], values);
            }

            if (result != null)
            {
                aggregations[column.Id] = result;
            }
        }

        aggregations["__childCount"] = rowList.Count;
        return aggregations;
    }

    private Func<IEnumerable<object?>, object?> ResolveAggregator<TData>(Column<TData> column)
    {
        if (TryResolveTypedAggregation(column, out var typed))
        {
            return typed;
        }

        if (column.ColumnDef.Meta != null &&
            column.ColumnDef.Meta.TryGetValue("aggregation", out var keyObj) &&
            keyObj is string key &&
            _functions.TryGetValue(key, out var fn))
        {
            return fn;
        }

        return _functions["sum"];
    }

    private bool TryResolveTypedAggregation<TData>(Column<TData> column, out Func<IEnumerable<object?>, object?> aggregator)
    {
        aggregator = default!;
        var columnDef = column.ColumnDef;
        var type = columnDef.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ColumnDef<,>))
        {
            return false;
        }

        var aggregationProp = type.GetProperty("AggregationFn", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (aggregationProp == null)
        {
            return false;
        }

        if (aggregationProp.GetValue(columnDef) is not Delegate del)
        {
            return false;
        }

        var valueType = type.GetGenericArguments()[1];

        aggregator = values =>
        {
            var listType = typeof(List<>).MakeGenericType(valueType);
            var typedList = (IList)Activator.CreateInstance(listType)!;

            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }

                try
                {
                    typedList.Add(ConvertValue(value, valueType));
                }
                catch
                {
                    // Skip values that cannot be converted to the target type
                }
            }

            if (typedList.Count == 0)
            {
                return null;
            }

            return del.DynamicInvoke(typedList);
        };

        return true;
    }

    private static object? ConvertValue(object value, Type targetType)
    {
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private object? SafeAggregate(Func<IEnumerable<object?>, object?> aggregator, IEnumerable<object?> values)
    {
        try
        {
            return aggregator(values);
        }
        catch
        {
            return null;
        }
    }

    private IReadOnlyList<GroupBucket<TData>> CreateBuckets<TData>(IEnumerable<Row<TData>> rows, string columnId)
    {
        var buckets = new List<GroupBucket<TData>>();
        var lookup = new Dictionary<string, GroupBucket<TData>>();
        var index = 0;

        foreach (var row in rows)
        {
            var value = row.GetCell(columnId).Value;
            var key = MakeGroupKey(value);

            if (!lookup.TryGetValue(key, out var bucket))
            {
                bucket = new GroupBucket<TData>(index++, key, value);
                lookup[key] = bucket;
                buckets.Add(bucket);
            }

            bucket.Rows.Add(row);
        }

        return buckets;
    }

    private static string MakeGroupKey(object? value)
    {
        if (value == null)
        {
            return "__null__";
        }

        return $"{value.GetType().FullName}:{value}";
    }

    private void RegisterBuiltIns()
    {
        _functions["count"] = values => values.Count(v => v != null);
        _functions["sum"] = values =>
        {
            decimal total = 0;
            var count = 0;
            foreach (var value in values)
            {
                if (TryConvertDecimal(value, out var number))
                {
                    total += number;
                    count++;
                }
            }
            return count > 0 ? (object)total : null;
        };
        _functions["avg"] = values =>
        {
            decimal total = 0;
            var count = 0;
            foreach (var value in values)
            {
                if (TryConvertDecimal(value, out var number))
                {
                    total += number;
                    count++;
                }
            }
            return count > 0 ? total / count : null;
        };
        _functions["min"] = values =>
        {
            IComparable? best = null;
            foreach (var value in values)
            {
                if (value is IComparable comparable)
                {
                    if (best == null || comparable.CompareTo(best) < 0)
                    {
                        best = comparable;
                    }
                }
            }
            return best;
        };
        _functions["max"] = values =>
        {
            IComparable? best = null;
            foreach (var value in values)
            {
                if (value is IComparable comparable)
                {
                    if (best == null || comparable.CompareTo(best) > 0)
                    {
                        best = comparable;
                    }
                }
            }
            return best;
        };
    }

    private static bool TryConvertDecimal(object? value, out decimal result)
    {
        if (value == null)
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
