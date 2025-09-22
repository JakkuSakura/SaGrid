using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using SaGrid.Avalonia;
using SaGrid.Core;
using static SolidAvalonia.Solid;

namespace SaGrid.SolidAvalonia;

public static class SolidTableBuilder
{
    public static SolidTable<TData> CreateSolidTable<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns,
        TableOptions<TData>? options = null)
    {
        var tableOptions = options ?? new TableOptions<TData>
        {
            Data = data,
            Columns = columns
        };

        tableOptions = tableOptions with
        {
            Data = data,
            Columns = columns
        };

        return new SolidTable<TData>(tableOptions);
    }

    public static SolidTable<TData> CreateSolidTable<TData>(
        Table<TData> table,
        TableOptions<TData> options)
    {
        return new SolidTable<TData>(options, table);
    }

    public static SolidTable<TData> CreateSortableTable<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns,
        Action<TableState<TData>>? onStateChange = null)
    {
        var options = new TableOptions<TData>
        {
            Data = data,
            Columns = columns,
            EnableSorting = true,
            EnableMultiSort = true,
            OnStateChange = onStateChange
        };

        return new SolidTable<TData>(options);
    }

    public static SolidTable<TData> CreateFilterableTable<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns,
        Action<TableState<TData>>? onStateChange = null)
    {
        var options = new TableOptions<TData>
        {
            Data = data,
            Columns = columns,
            EnableColumnFilters = true,
            EnableGlobalFilter = true,
            OnStateChange = onStateChange
        };

        return new SolidTable<TData>(options);
    }

    public static SolidTable<TData> CreatePaginatedTable<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns,
        int initialPageSize = 10,
        Action<TableState<TData>>? onStateChange = null)
    {
        var options = new TableOptions<TData>
        {
            Data = data,
            Columns = columns,
            EnablePagination = true,
            State = new TableState<TData>
            {
                Pagination = new PaginationState
                {
                    PageIndex = 0,
                    PageSize = initialPageSize
                }
            },
            OnStateChange = onStateChange
        };

        return new SolidTable<TData>(options);
    }

    public static SolidTable<TData> CreateSelectableTable<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns,
        Action<TableState<TData>>? onStateChange = null)
    {
        var options = new TableOptions<TData>
        {
            Data = data,
            Columns = columns,
            EnableRowSelection = true,
            OnStateChange = onStateChange
        };

        return new SolidTable<TData>(options);
    }

    public static SolidTable<TData> CreateFullFeaturedTable<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns,
        int initialPageSize = 10,
        Action<TableState<TData>>? onStateChange = null)
    {
        var options = new TableOptions<TData>
        {
            Data = data,
            Columns = columns,
            EnableSorting = true,
            EnableMultiSort = true,
            EnableColumnFilters = true,
            EnableGlobalFilter = true,
            EnableRowSelection = true,
            EnableColumnResizing = true,
            EnableColumnReordering = true,
            EnableColumnPinning = true,
            EnablePagination = true,
            State = new TableState<TData>
            {
                Pagination = new PaginationState
                {
                    PageIndex = 0,
                    PageSize = initialPageSize
                }
            },
            OnStateChange = onStateChange
        };

        return new SolidTable<TData>(options);
    }
}

public static class SolidColumnHelper
{
    public static ColumnDef<TData, TValue> ReactiveAccessor<TData, TValue>(
        string accessorKey,
        string? id = null,
        object? header = null,
        Func<TValue, object>? cellRenderer = null)
    {
        return new ColumnDef<TData, TValue>
        {
            Id = id ?? accessorKey,
            AccessorKey = accessorKey,
            Header = header ?? accessorKey,
            Cell = cellRenderer != null
                ? (object)new Func<object?, object>(value =>
                    value is TValue typedValue ? cellRenderer(typedValue) : value ?? "")
                : null
        };
    }

    public static ColumnDef<TData, object> ReactiveDisplay<TData>(
        string id,
        object? header = null,
        Func<Row<TData>, object>? cellRenderer = null)
    {
        return new ColumnDef<TData, object>
        {
            Id = id,
            Header = header ?? id,
            Cell = cellRenderer != null
                ? (object)new Func<Row<TData>, object>(row => cellRenderer(row))
                : null
        };
    }

    public static ColumnDef<TData, object> CommandButton<TData>(
        string id,
        string caption,
        Action<Row<TData>> onClick)
    {
        return new ColumnDef<TData, object>
        {
            Id = id,
            Header = caption,
            Cell = new Func<Row<TData>, object>(row => Reactive(() =>
                new Button()
                    .Content(caption)
                    .OnClick(_ => onClick(row))))
        };
    }
}
