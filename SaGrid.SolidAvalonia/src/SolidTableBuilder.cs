using SaGrid.Core;
using static SolidAvalonia.Solid;
using Avalonia.Markup.Declarative;

namespace SaGrid.SolidAvalonia;

public static class SolidTableBuilder
{
    /// <summary>
    /// Creates a reactive SolidTable component
    /// </summary>
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

        // Ensure data and columns are set
        tableOptions = tableOptions with
        {
            Data = data,
            Columns = columns
        };

        return new SolidTable<TData>(tableOptions);
    }

    /// <summary>
    /// Creates a reactive SolidTable component using an existing table instance
    /// </summary>
    public static SolidTable<TData> CreateSolidTable<TData>(
        Table<TData> table,
        TableOptions<TData> options)
    {
        return new SolidTable<TData>(options, table);
    }

    /// <summary>
    /// Creates a reactive table with sorting enabled
    /// </summary>
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

    /// <summary>
    /// Creates a reactive table with filtering enabled
    /// </summary>
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

    /// <summary>
    /// Creates a reactive table with pagination enabled
    /// </summary>
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

    /// <summary>
    /// Creates a reactive table with row selection enabled
    /// </summary>
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

    /// <summary>
    /// Creates a fully featured reactive table
    /// </summary>
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

/// <summary>
/// Column helper specifically for SolidAvalonia tables
/// </summary>
public static class SolidColumnHelper
{
    /// <summary>
    /// Creates an accessor column with reactive cell content
    /// </summary>
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

    /// <summary>
    /// Creates a display column that doesn't bind to data
    /// </summary>
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
                ? (object)new Func<Row<TData>, object>(cellRenderer)
                : null
        };
    }

    /// <summary>
    /// Creates an action column (e.g., for buttons)
    /// </summary>
    public static ColumnDef<TData, object> ActionColumn<TData>(
        string id,
        string buttonText,
        Action<TData> onAction,
        object? header = null)
    {
        return new ColumnDef<TData, object>
        {
            Id = id,
            Header = header ?? id,
            Cell = (object)new Func<Row<TData>, object>(row =>
                Reactive(() => new Avalonia.Controls.Button()
                    .Content(buttonText)
                    .OnClick(_ => onAction(row.Original))
                )
            )
        };
    }
}
