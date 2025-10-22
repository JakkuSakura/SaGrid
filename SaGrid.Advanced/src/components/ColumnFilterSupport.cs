using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using SaGrid.Core;

namespace SaGrid.Advanced.Components;

public enum ColumnFilterKind
{
    Text,
    BooleanTriState,
    Custom
}

public static class ColumnFilterMetaKeys
{
    public const string FilterKind = "SaGrid.Advanced.FilterKind";
    public const string FilterFactory = "SaGrid.Advanced.FilterFactory";
}

public sealed class ColumnFilterContext<TData>
{
    private readonly ISaGridComponentHost<TData> _host;

    public ColumnFilterContext(ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Table = table ?? throw new ArgumentNullException(nameof(table));
        Column = column ?? throw new ArgumentNullException(nameof(column));
    }

    public Table<TData> Table { get; }
    public Column<TData> Column { get; }

    public object? CurrentValue => Table.State.ColumnFilters?.Filters
        ?.FirstOrDefault(f => f.Id == Column.Id)?.Value;

    public void SetFilterValue(object? value) => _host.SetColumnFilter(Column.Id, value);
}

public sealed class ColumnFilterRegistration
{
    public ColumnFilterRegistration(Control control, Action<object?> applyState, Func<bool>? isFocused = null)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        ApplyState = applyState ?? throw new ArgumentNullException(nameof(applyState));
        IsFocused = isFocused ?? (() => control.IsKeyboardFocusWithin);
    }

    public Control Control { get; }
    public Action<object?> ApplyState { get; }
    public Func<bool> IsFocused { get; }
}

public delegate ColumnFilterRegistration ColumnFilterFactory<TData>(ColumnFilterContext<TData> context);

public static class AdvancedColumnDefExtensions
{
    public static ColumnDef<TData, TValue> WithTextFilter<TData, TValue>(this ColumnDef<TData, TValue> column)
    {
        return column.WithFilterMeta(ColumnFilterKind.Text);
    }

    public static ColumnDef<TData, TValue> WithBooleanFilter<TData, TValue>(this ColumnDef<TData, TValue> column)
    {
        return column.WithFilterMeta(ColumnFilterKind.BooleanTriState);
    }

    public static ColumnDef<TData, TValue> WithCustomFilter<TData, TValue>(
        this ColumnDef<TData, TValue> column,
        ColumnFilterFactory<TData> factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var metaColumn = column.WithFilterMeta(ColumnFilterKind.Custom);
        return metaColumn.WithFilterFactoryMeta(factory);
    }

    private static ColumnDef<TData, TValue> WithFilterMeta<TData, TValue>(
        this ColumnDef<TData, TValue> column,
        ColumnFilterKind kind)
    {
        var meta = column.Meta != null
            ? new Dictionary<string, object>(column.Meta)
            : new Dictionary<string, object>();
        meta[ColumnFilterMetaKeys.FilterKind] = kind;
        return column with { Meta = meta };
    }

    private static ColumnDef<TData, TValue> WithFilterFactoryMeta<TData, TValue>(
        this ColumnDef<TData, TValue> column,
        ColumnFilterFactory<TData> factory)
    {
        var meta = column.Meta != null
            ? new Dictionary<string, object>(column.Meta)
            : new Dictionary<string, object>();
        meta[ColumnFilterMetaKeys.FilterFactory] = factory;
        return column with { Meta = meta };
    }
}
