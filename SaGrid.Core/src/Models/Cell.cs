using System.Reflection;

namespace SaGrid.Core;

public class Cell<TData> : ICell<TData>
{
    public string Id { get; }
    public Column<TData> Column { get; }
    public Row<TData> Row { get; }
    public object? Value { get; private set; }
    public object? RenderValue { get; private set; }
    public bool IsGrouped { get; }
    public bool IsAggregated { get; private set; }
    public bool IsPlaceholder { get; private set; }

    IColumn<TData> ICell<TData>.Column => Column;
    IRow<TData> ICell<TData>.Row => Row;

    public Cell(Row<TData> row, Column<TData> column, object? presetValue = null, bool isAggregated = false, bool isPlaceholder = false)
    {
        Row = row;
        Column = column;
        Id = $"{row.Id}_{column.Id}";

        Value = presetValue ?? GetCellValue();
        RenderValue = Value;

        IsGrouped = row.IsGroupRow || (row.IsGrouped && column.IsGrouped);
        IsAggregated = isAggregated;
        IsPlaceholder = isPlaceholder;
    }

    private object? GetCellValue()
    {
        var columnDef = Column.ColumnDef;
        
        // 1. 优先使用 AccessorFn（支持所有类型，包括计算属性）
        // 如果是泛型 ColumnDef<TData,TValue>，获取其特定的 AccessorFn
        var columnDefType = columnDef.GetType();
        if (columnDefType.IsGenericType && 
            columnDefType.GetGenericTypeDefinition() == typeof(ColumnDef<,>))
        {
            // 使用 DeclaredOnly 只获取派生类中声明的属性，避免歧义
            var accessorFnProp = columnDefType.GetProperty("AccessorFn", 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (accessorFnProp != null)
            {
                var accessorFn = accessorFnProp.GetValue(columnDef);
                if (accessorFn != null && accessorFn is Delegate del)
                {
                    try
                    {
                        return del.DynamicInvoke(Row.Original);
                    }
                    catch { /* 吞掉单元格级别异常，继续尝试下一策略 */ }
                }
            }
            
            // 尝试 AccessorKey - 同样使用 DeclaredOnly
            var accessorKeyProp = columnDefType.GetProperty("AccessorKey",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (accessorKeyProp != null)
            {
                var accessorKey = accessorKeyProp.GetValue(columnDef) as string;
                if (!string.IsNullOrEmpty(accessorKey))
                {
                    try
                    {
                        var dataType = typeof(TData);
                        var prop = dataType.GetProperty(accessorKey);
                        if (prop != null)
                            return prop.GetValue(Row.Original);

                        var field = dataType.GetField(accessorKey);
                        if (field != null)
                            return field.GetValue(Row.Original);
                    }
                    catch { /* 吞掉单元格级别异常 */ }
                }
            }
        }
        
        // 回退到基类属性
        if (columnDef.AccessorFn != null)
        {
            try
            {
                if (columnDef.AccessorFn is Delegate del)
                {
                    return del.DynamicInvoke(Row.Original);
                }
            }
            catch { /* 吞掉单元格级别异常，继续尝试下一策略 */ }
        }
        
        if (!string.IsNullOrEmpty(columnDef.AccessorKey))
        {
            try
            {
                var dataType = typeof(TData);
                var prop = dataType.GetProperty(columnDef.AccessorKey);
                if (prop != null)
                    return prop.GetValue(Row.Original);

                var field = dataType.GetField(columnDef.AccessorKey);
                if (field != null)
                    return field.GetValue(Row.Original);
            }
            catch { /* 吞掉单元格级别异常 */ }
        }

        return null;
    }

    public void ApplyAggregatedValue(object? value)
    {
        Value = value;
        RenderValue = value;
        IsAggregated = true;
    }

    public void UpdateValue(object? value)
    {
        Value = value;
        RenderValue = value;
        IsAggregated = false;
    }
}

public class Cell<TData, TValue> : Cell<TData>, ICell<TData, TValue>
{
    public new Column<TData, TValue> Column { get; }
    public new TValue Value { get; private set; }
    public new TValue RenderValue { get; private set; }

    IColumn<TData, TValue> ICell<TData, TValue>.Column => Column;

    public Cell(Row<TData> row, Column<TData, TValue> column, object? presetValue = null, bool isAggregated = false, bool isPlaceholder = false)
        : base(row, column, presetValue, isAggregated, isPlaceholder)
    {
        Column = column;
        // 对于泛型列可直接使用列的 AccessorFn 以避免二次反射
        if (column.AccessorFn != null)
        {
            try
            {
                Value = column.AccessorFn(row.Original);
            }
            catch
            {
                Value = default!;
            }
        }
        else
        {
            // 退回基类已算好的 object? Value
            var baseVal = base.Value;
            Value = baseVal is TValue tv ? tv : default!;
        }
        RenderValue = Value;
    }

    public new void ApplyAggregatedValue(object? value)
    {
        base.ApplyAggregatedValue(value);
        if (value is TValue typed)
        {
            Value = typed;
            RenderValue = typed;
        }
        else
        {
            Value = default!;
            RenderValue = default!;
        }
    }

    public void UpdateValue(TValue value)
    {
        base.UpdateValue(value);
        Value = value;
        RenderValue = value;
    }
}
