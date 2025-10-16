using System.Linq.Expressions;
using System.Reflection;
using SaGrid.Core.Models;

namespace SaGrid.Core;

public class Column<TData> : IColumn<TData>
{
    private readonly Table<TData> _table;
    private readonly List<Column<TData>> _columns = new();

    public string Id { get; }
    public ColumnDef<TData> ColumnDef { get; }
    public int Depth { get; }
    public Column<TData>? Parent { get; }
    public IReadOnlyList<IColumn<TData>> Columns => _columns.Cast<IColumn<TData>>().ToList().AsReadOnly();
    public IReadOnlyList<IColumn<TData>> FlatColumns => GetFlatColumns();
    public IReadOnlyList<IColumn<TData>> LeafColumns => GetLeafColumns();

    // 便于调试的访问器属性 - 通过反射读取正确的值
    public string? AccessorKey 
    { 
        get 
        {
            // 如果是泛型 ColumnDef<TData,TValue>，尝试获取其 AccessorKey
            var columnDefType = ColumnDef.GetType();
            if (columnDefType.IsGenericType && 
                columnDefType.GetGenericTypeDefinition() == typeof(ColumnDef<,>))
            {
                // 使用 DeclaredOnly 只获取派生类中声明的属性，避免歧义
                var accessorKeyProp = columnDefType.GetProperty("AccessorKey",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (accessorKeyProp != null)
                {
                    return accessorKeyProp.GetValue(ColumnDef) as string;
                }
            }
            return ColumnDef.AccessorKey;
        }
    }
    
    public bool HasAccessorFn 
    { 
        get 
        {
            // 如果是泛型 ColumnDef<TData,TValue>，尝试获取其 AccessorFn
            var columnDefType = ColumnDef.GetType();
            if (columnDefType.IsGenericType && 
                columnDefType.GetGenericTypeDefinition() == typeof(ColumnDef<,>))
            {
                // 使用 DeclaredOnly 只获取派生类中声明的属性，避免歧义
                var accessorFnProp = columnDefType.GetProperty("AccessorFn",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (accessorFnProp != null)
                {
                    return accessorFnProp.GetValue(ColumnDef) != null;
                }
            }
            return ColumnDef.AccessorFn != null;
        }
    }

    public bool IsVisible => GetVisibility();
    public bool CanSort => GetCanSort();
    public bool CanFilter => GetCanFilter();
    public bool CanGroup => GetCanGroup();
    public bool CanResize => GetCanResize();

    private bool GetVisibility()
    {
        var visibilityState = _table.State.ColumnVisibility ?? new ColumnVisibilityState();
        return visibilityState.GetValueOrDefault(Id, true);
    }
    

    public SortDirection? SortDirection => GetSortDirection();
    public int? SortIndex => GetSortIndex();
    public bool IsFiltered => GetIsFiltered();
    public object? FilterValue => GetFilterValue();
    public bool IsGrouped => GetIsGrouped();
    public int? GroupIndex => GetGroupIndex();
    public double Size => GetSize();
    public bool IsPinned => GetIsPinned();
    public string? PinnedPosition => GetPinnedPosition();

    public Column(Table<TData> table, ColumnDef<TData> columnDef, Column<TData>? parent, int depth)
    {
        _table = table;
        ColumnDef = columnDef;
        Parent = parent;
        Depth = depth;

        Id = columnDef.Id ?? GenerateId(columnDef, parent);

        parent?.AddChild(this);
    }

    private string GenerateId(ColumnDef<TData> columnDef, Column<TData>? parent)
    {
        if (columnDef is ColumnDef<TData, object> typedColumnDef && typedColumnDef.AccessorKey != null)
            return typedColumnDef.AccessorKey;

        var parentId = parent?.Id ?? "";
        var index = parent?._columns.Count ?? 0;
        return $"{parentId}_{index}";
    }

    private void AddChild(Column<TData> child)
    {
        _columns.Add(child);
    }

    private IReadOnlyList<IColumn<TData>> GetFlatColumns()
    {
        var result = new List<IColumn<TData>> { this };
        foreach (var column in _columns)
        {
            result.AddRange(column.FlatColumns);
        }
        return result.AsReadOnly();
    }

    private IReadOnlyList<IColumn<TData>> GetLeafColumns()
    {
        if (!_columns.Any())
            return new List<IColumn<TData>> { this }.AsReadOnly();

        var result = new List<IColumn<TData>>();
        foreach (var column in _columns)
        {
            result.AddRange(column.LeafColumns);
        }
        return result.AsReadOnly();
    }


    private bool GetCanSort()
    {
        return ColumnDef.EnableSorting ?? _table.Options.EnableSorting;
    }

    private bool GetCanFilter()
    {
        return ColumnDef.EnableColumnFilter ?? _table.Options.EnableColumnFilters;
    }

    private bool GetCanGroup()
    {
        return ColumnDef.EnableGrouping ?? _table.Options.EnableGrouping;
    }

    private bool GetCanResize()
    {
        return ColumnDef.EnableResizing ?? _table.Options.EnableColumnResizing;
    }

    private SortDirection? GetSortDirection()
    {
        var sortingState = _table.State.Sorting;
        if (sortingState == null) return null;

        var sort = sortingState.FirstOrDefault(s => s.Id == Id);
        return sort?.Direction;
    }

    private int? GetSortIndex()
    {
        var sortingState = _table.State.Sorting;
        if (sortingState == null) return null;

        var index = sortingState.FindIndex(s => s.Id == Id);
        return index >= 0 ? index : null;
    }

    private bool GetIsFiltered()
    {
        var filtersState = _table.State.ColumnFilters;
        return filtersState?.Any(f => f.Id == Id) ?? false;
    }

    private object? GetFilterValue()
    {
        var filtersState = _table.State.ColumnFilters;
        return filtersState?.FirstOrDefault(f => f.Id == Id)?.Value;
    }

    private bool GetIsGrouped()
    {
        var groupingState = _table.State.Grouping;
        return groupingState?.Contains(Id) ?? false;
    }

    private int? GetGroupIndex()
    {
        var groupingState = _table.State.Grouping;
        if (groupingState == null) return null;

        var index = groupingState.FindIndex(g => g == Id);
        return index >= 0 ? index : null;
    }

    private double GetSize()
    {
        var sizingState = _table.State.ColumnSizing;
        if (sizingState != null && sizingState.Items.TryGetValue(Id, out var stored))
        {
            return ClampWidth(stored);
        }

        if (ColumnDef.Width.HasValue)
        {
            var definition = ColumnDef.Width.Value;
            if (definition.Mode == ColumnWidthMode.Fixed)
            {
                return ClampWidth(definition.Value);
            }

            var fallback = ColumnDef.MinSize.HasValue ? Math.Max(ColumnDef.MinSize.Value, 1) : 150;
            return ClampWidth(fallback);
        }

        if (ColumnDef.Size.HasValue)
        {
            return ClampWidth(ColumnDef.Size.Value);
        }

        return ClampWidth(150);
    }

    private bool GetIsPinned()
    {
        var pinningState = _table.State.ColumnPinning;
        return pinningState?.Left?.Contains(Id) == true ||
               pinningState?.Right?.Contains(Id) == true;
    }

    private string? GetPinnedPosition()
    {
        var pinningState = _table.State.ColumnPinning;
        if (pinningState?.Left?.Contains(Id) == true) return "left";
        if (pinningState?.Right?.Contains(Id) == true) return "right";
        return null;
    }

    private (double Min, double Max) GetWidthBounds()
    {
        var min = ColumnDef.MinSize.HasValue
            ? Math.Max(ColumnDef.MinSize.Value, 1)
            : 40;

        var max = ColumnDef.MaxSize.HasValue
            ? Math.Max(ColumnDef.MaxSize.Value, min)
            : double.PositiveInfinity;

        return (min, max);
    }

    private double ClampWidth(double width)
    {
        var (min, max) = GetWidthBounds();
        var candidate = double.IsNaN(width) ? min : Math.Max(width, min);
        if (!double.IsPositiveInfinity(max))
        {
            candidate = Math.Min(candidate, max);
        }

        return candidate;
    }

    public void ToggleSorting(SortDirection? direction = null)
    {
        var currentSorting = _table.State.Sorting ?? new SortingState();
        var existing = currentSorting.FirstOrDefault(s => s.Id == Id);

        // Determine if multi-sort is enabled (table options only)
        var allowMulti = _table.Options.EnableMultiSort;

        if (existing != null)
        {
            if (direction.HasValue)
            {
                // Replace this column's direction
                var replaced = currentSorting.Where(s => s.Id != Id);
                if (allowMulti)
                {
                    replaced = replaced.Add(new ColumnSort(Id, direction.Value));
                    _table.SetState(state => state with { Sorting = replaced });
                }
                else
                {
                    _table.SetState(state => state with { Sorting = new SortingState(new List<ColumnSort>{ new ColumnSort(Id, direction.Value) }) });
                }
            }
            else
            {
                // Cycle this column: asc -> desc -> remove
                var remaining = currentSorting.Where(s => s.Id != Id);
                if (existing.Direction == SaGrid.Core.SortDirection.Ascending)
                {
                    if (allowMulti)
                    {
                        remaining = remaining.Add(new ColumnSort(Id, SaGrid.Core.SortDirection.Descending));
                        _table.SetState(state => state with { Sorting = remaining });
                    }
                    else
                    {
                        _table.SetState(state => state with { Sorting = new SortingState(new List<ColumnSort>{ new ColumnSort(Id, SaGrid.Core.SortDirection.Descending) }) });
                    }
                }
                else
                {
                    // remove this column from sorting
                    _table.SetState(state => state with { Sorting = remaining.Count() > 0 ? remaining : null });
                }
            }
        }
        else
        {
            // Not sorted yet: add as ascending
            var dir = direction ?? SaGrid.Core.SortDirection.Ascending;
            if (allowMulti)
            {
                var appended = currentSorting.Add(new ColumnSort(Id, dir));
                _table.SetState(state => state with { Sorting = appended });
            }
            else
            {
                _table.SetState(state => state with { Sorting = new SortingState(new List<ColumnSort>{ new ColumnSort(Id, dir) }) });
            }
        }
    }

    public void ClearSorting()
    {
        var currentSorting = _table.State.Sorting ?? new SortingState();
        var newSorting = currentSorting.Where(s => s.Id != Id);
        _table.SetState(state => state with { Sorting = newSorting });
    }

    public void SetFilterValue(object? value)
    {
        var currentFilters = _table.State.ColumnFilters ?? new ColumnFiltersState();
        var newFilters = currentFilters.Where(f => f.Id != Id);

        if (value != null)
            newFilters = newFilters.Add(new ColumnFilter(Id, value));

        _table.SetState(state => state with { ColumnFilters = newFilters });
    }

    public void ToggleGrouping()
    {
        var currentGrouping = _table.State.Grouping ?? new GroupingState();
        var newGrouping = currentGrouping;

        if (newGrouping.Contains(Id))
            newGrouping = newGrouping.Remove(Id);
        else
            newGrouping = newGrouping.Add(Id);

        _table.SetState(state => state with { Grouping = newGrouping });
    }

    public void ToggleVisibility()
    {
        var currentVisibility = _table.State.ColumnVisibility ?? new ColumnVisibilityState();
        var newVisibility = currentVisibility.With(Id,
            !currentVisibility.GetValueOrDefault(Id, true));

        _table.SetState(state => state with { ColumnVisibility = newVisibility });
    }

    public void ResetSize()
    {
        var currentSizing = _table.State.ColumnSizing ?? new ColumnSizingState();
        var newSizing = currentSizing.Remove(Id);

        _table.SetState(state => state with { ColumnSizing = newSizing });
    }

    public void SetSize(double size)
    {
        var currentSizing = _table.State.ColumnSizing ?? new ColumnSizingState();
        var newSizing = currentSizing.With(Id, size);

        _table.SetState(state => state with { ColumnSizing = newSizing });
    }
}

public class Column<TData, TValue> : Column<TData>, IColumn<TData, TValue>
{
    public new ColumnDef<TData, TValue> ColumnDef { get; }
    public AccessorFn<TData, TValue>? AccessorFn { get; }

    public Column(Table<TData> table, ColumnDef<TData, TValue> columnDef, Column<TData>? parent, int depth)
        : base(table, columnDef, parent, depth)
    {
        ColumnDef = columnDef;
        AccessorFn = columnDef.AccessorFn ?? CreateAccessorFromKey(columnDef.AccessorKey);
    }

    private AccessorFn<TData, TValue>? CreateAccessorFromKey(string? accessorKey)
    {
        if (string.IsNullOrEmpty(accessorKey))
            return null;

        var parameter = Expression.Parameter(typeof(TData), "data");
        var property = Expression.PropertyOrField(parameter, accessorKey);
        var lambda = Expression.Lambda<AccessorFn<TData, TValue>>(property, parameter);
        return lambda.Compile();
    }

    public TValue GetValue(Row<TData> row)
    {
        if (AccessorFn == null)
            return default(TValue)!;

        return AccessorFn(row.Original);
    }
}
