using SaGrid.Core.Models;

namespace SaGrid.Core;

public static class TableBuilder
{
    public static Table<TData> CreateTable<TData>(TableOptions<TData> options)
    {
        return new Table<TData>(options);
    }

    public static TableOptions<TData> CreateOptions<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns)
    {
        return new TableOptions<TData>
        {
            Data = data,
            Columns = columns
        };
    }
}

public static class ColumnHelper
{
    public static ColumnDef<TData, TValue> Accessor<TData, TValue>(
        string accessorKey,
        string? id = null,
        object? header = null,
        object? cell = null,
        ColumnWidthDefinition? width = null)
    {
        return new ColumnDef<TData, TValue>
        {
            Id = id ?? accessorKey,
            AccessorKey = accessorKey,
            Header = header ?? accessorKey,
            Cell = cell,
            Width = width
        };
    }

    public static ColumnDef<TData, TValue> Accessor<TData, TValue>(
        AccessorFn<TData, TValue> accessorFn,
        string id,
        object? header = null,
        object? cell = null,
        ColumnWidthDefinition? width = null)
    {
        return new ColumnDef<TData, TValue>
        {
            Id = id,
            AccessorFn = accessorFn,
            Header = header ?? id,
            Cell = cell,
            Width = width
        };
    }

    public static GroupColumnDef<TData> Group<TData>(
        string id,
        object? header = null,
        params ColumnDef<TData>[] columns)
    {
        return new GroupColumnDef<TData>
        {
            Id = id,
            Header = header ?? id,
            Columns = columns.ToList().AsReadOnly()
        };
    }

    public static ColumnDef<TData> Display<TData>(
        string id,
        object? header = null,
        object? cell = null)
    {
        return new ColumnDef<TData, object>
        {
            Id = id,
            Header = header ?? id,
            Cell = cell
        };
    }
}