using SaGrid.Core.Models;

namespace SaGrid.Core;

public class SortingFeature<TData> : ITableFeature<TData>
{
    public string Name => "Sorting";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>();
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

public class ColumnFilteringFeature<TData> : ITableFeature<TData>
{
    public string Name => "ColumnFiltering";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>();
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

public class GlobalFilteringFeature<TData> : ITableFeature<TData>
{
    public string Name => "GlobalFiltering";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>();
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

public class GroupingFeature<TData> : ITableFeature<TData>
{
    public string Name => "Grouping";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>();
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

public class ExpandingFeature<TData> : ITableFeature<TData>
{
    public string Name => "Expanding";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>();
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

public class RowSelectionFeature<TData> : ITableFeature<TData>
{
    public string Name => "RowSelection";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>();
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

public class PaginationFeature<TData> : ITableFeature<TData>
{
    public string Name => "Pagination";

    public void Initialize(ITable<TData> table)
    {
        // Feature initialization logic
    }

    public TableState<TData> GetInitialState(TableOptions<TData> options)
    {
        return new TableState<TData>
        {
            Pagination = options.EnablePagination ? new PaginationState() : null
        };
    }

    public void OnStateChange(ITable<TData> table, TableState<TData> state)
    {
        // React to state changes
    }
}

// Extension methods for features
public static class GlobalFilterExtensions
{
    public static void SetGlobalFilter<TData>(this Table<TData> table, object? value)
    {
        object? normalized = value is string s && string.IsNullOrWhiteSpace(s)
            ? null
            : value;

        table.SetState(state => state with
        {
            GlobalFilter = normalized != null ? new GlobalFilterState(normalized) : null
        });
    }

    public static void ClearGlobalFilter<TData>(this Table<TData> table)
    {
        table.SetGlobalFilter(null);
    }

    public static object? GetGlobalFilterValue<TData>(this Table<TData> table)
    {
        return table.State.GlobalFilter?.Value;
    }
}

public static class ColumnFilterExtensions
{
    public static void SetColumnFilter<TData>(this Table<TData> table, string columnId, object? value)
    {
        var currentFilters = table.State.ColumnFilters ?? new ColumnFiltersState();
        var filters = currentFilters.Filters.Where(f => f.Id != columnId).ToList();
        
        if (value != null)
        {
            filters.Add(new ColumnFilter(columnId, value));
        }
        
        table.SetState(state => state with 
        { 
            ColumnFilters = filters.Count > 0 ? new ColumnFiltersState(filters) : null
        });
    }

    public static void ClearColumnFilter<TData>(this Table<TData> table, string columnId)
    {
        table.SetColumnFilter(columnId, null);
    }

    public static object? GetColumnFilterValue<TData>(this Table<TData> table, string columnId)
    {
        var filters = table.State.ColumnFilters?.Filters;
        return filters?.FirstOrDefault(f => f.Id == columnId)?.Value;
    }
}

public static class GroupingExtensions
{
    public static void SetGrouping<TData>(this Table<TData> table, IEnumerable<string> columnIds)
    {
        var groups = columnIds.ToList();
        table.SetState(state => state with 
        { 
            Grouping = groups.Count > 0 ? new GroupingState(groups) : null
        });
    }

    public static void ToggleGrouping<TData>(this Table<TData> table, string columnId)
    {
        var currentGrouping = table.State.Grouping?.Groups ?? new List<string>();
        var newGroups = currentGrouping.ToList();
        
        if (newGroups.Contains(columnId))
        {
            newGroups.Remove(columnId);
        }
        else
        {
            newGroups.Add(columnId);
        }
        
        table.SetGrouping(newGroups);
    }

    public static bool IsGrouped<TData>(this Table<TData> table, string columnId)
    {
        return table.State.Grouping?.Groups.Contains(columnId) ?? false;
    }
}

public static class ExpandingExtensions
{
    public static void SetExpanded<TData>(this Table<TData> table, string rowId, bool expanded)
    {
        var currentExpanded = table.State.Expanded ?? new ExpandedState();
        var newItems = new Dictionary<string, bool>(currentExpanded.Items);
        
        if (expanded)
        {
            newItems[rowId] = true;
        }
        else
        {
            newItems.Remove(rowId);
        }
        
        table.SetState(state => state with 
        { 
            Expanded = newItems.Count > 0 ? new ExpandedState(newItems) : null
        });
    }

    public static void ToggleExpanded<TData>(this Table<TData> table, string rowId)
    {
        var isExpanded = table.IsExpanded(rowId);
        table.SetExpanded(rowId, !isExpanded);
    }

    public static bool IsExpanded<TData>(this Table<TData> table, string rowId)
    {
        return table.State.Expanded?.Items.GetValueOrDefault(rowId, false) ?? false;
    }

    public static void ExpandAll<TData>(this Table<TData> table)
    {
        var allRowIds = table.PrePaginationRowModel.FlatRows.Select(r => r.Id);
        var expandedItems = allRowIds.ToDictionary(id => id, _ => true);
        
        table.SetState(state => state with 
        { 
            Expanded = new ExpandedState(expandedItems)
        });
    }

    public static void CollapseAll<TData>(this Table<TData> table)
    {
        table.SetState(state => state with { Expanded = null });
    }
}

public static class RowSelectionExtensions
{
    public static void ToggleRowSelected<TData>(this Table<TData> table, string rowId)
    {
        var currentSelection = table.State.RowSelection?.Items.GetValueOrDefault(rowId, false) ?? false;
        table.SetRowSelection(rowId, !currentSelection);
    }

    public static bool IsRowSelected<TData>(this Table<TData> table, string rowId)
    {
        return table.State.RowSelection?.Items.GetValueOrDefault(rowId, false) ?? false;
    }

    public static IEnumerable<string> GetSelectedRowIds<TData>(this Table<TData> table)
    {
        var selection = table.State.RowSelection;
        if (selection == null) return Enumerable.Empty<string>();
        
        return selection.Items.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }
}

public static class PaginationExtensions
{
    public static void EnablePagination<TData>(this Table<TData> table, int pageSize = 10)
    {
        table.SetState(state => state with 
        { 
            Pagination = new PaginationState { PageIndex = 0, PageSize = pageSize }
        });
    }

    public static void DisablePagination<TData>(this Table<TData> table)
    {
        table.SetState(state => state with { Pagination = null });
    }

    public static bool IsPaginationEnabled<TData>(this Table<TData> table)
    {
        return table.State.Pagination != null;
    }

    public static int GetCurrentPageIndex<TData>(this Table<TData> table)
    {
        return table.State.Pagination?.PageIndex ?? 0;
    }

    public static int GetCurrentPageSize<TData>(this Table<TData> table)
    {
        return table.State.Pagination?.PageSize ?? 10;
    }
}

// Virtualization support
public class VirtualizationViewport
{
    public int StartIndex { get; init; }
    // Internally we keep EndIndex for compatibility, but all public APIs treat the
    // second parameter as a COUNT. EndIndex is derived from StartIndex + Count - 1.
    public int EndIndex { get; init; }
    public int ViewportHeight { get; init; }
    public int ItemHeight { get; init; }
}

public static class VirtualizationExtensions
{
    private static readonly Dictionary<object, VirtualizationViewport> _viewports = new();
    private static readonly Dictionary<object, double> _rowHeights = new();

    // The second parameter is a COUNT (how many rows), not an end index.
    public static void SetViewport<TData>(this Table<TData> table, int startIndex, int count, int viewportHeight = 400, int itemHeight = 25)
    {
        var safeStart = Math.Max(0, startIndex);
        var safeCount = Math.Max(0, count);
        var endIndex = safeCount <= 0
            ? Math.Min(table.RowModel.Rows.Count - 1, safeStart)
            : Math.Min(table.RowModel.Rows.Count - 1, safeStart + safeCount - 1);

        var viewport = new VirtualizationViewport
        {
            StartIndex = safeStart,
            EndIndex = endIndex,
            ViewportHeight = viewportHeight,
            ItemHeight = itemHeight
        };
        
        _viewports[table] = viewport;
    }

    public static IReadOnlyList<Row<TData>> GetViewportRows<TData>(this Table<TData> table)
    {
        if (!_viewports.TryGetValue(table, out var viewport))
        {
            return table.RowModel.Rows;
        }

        var startIndex = Math.Max(0, viewport.StartIndex);
        var endIndex = Math.Min(table.RowModel.Rows.Count - 1, viewport.EndIndex);
        
        var viewportRows = new List<Row<TData>>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (i < table.RowModel.Rows.Count)
            {
                viewportRows.Add(table.RowModel.Rows[i]);
            }
        }
        
        return viewportRows.AsReadOnly();
    }

    // Overload for tests that expect 2 parameters (startIndex, count)
    public static IReadOnlyList<Row<TData>> GetVirtualRows<TData>(this Table<TData> table, int startIndex, int count)
    {
        table.SetViewport(startIndex, count);
        return table.GetViewportRows();
    }

    public static VirtualizationViewport? GetViewport<TData>(this Table<TData> table)
    {
        return _viewports.GetValueOrDefault(table);
    }

    public static void ClearViewport<TData>(this Table<TData> table)
    {
        _viewports.Remove(table);
    }

    // Row height management for virtualization
    private static readonly Dictionary<object, Dictionary<int, double>> _individualRowHeights = new();

    public static void SetRowHeight<TData>(this Table<TData> table, double height)
    {
        _rowHeights[table] = height;
    }

    public static double GetRowHeight<TData>(this Table<TData> table)
    {
        return _rowHeights.GetValueOrDefault(table, 25.0);
    }

    // Individual row height management for tests
    public static void SetRowHeight<TData>(this Table<TData> table, int rowIndex, double height)
    {
        if (!_individualRowHeights.TryGetValue(table, out var rowHeights))
        {
            rowHeights = new Dictionary<int, double>();
            _individualRowHeights[table] = rowHeights;
        }
        
        rowHeights[rowIndex] = height;
    }

    public static double GetRowHeight<TData>(this Table<TData> table, int rowIndex)
    {
        if (_individualRowHeights.TryGetValue(table, out var rowHeights) && 
            rowHeights.TryGetValue(rowIndex, out var height))
        {
            return height;
        }
        
        return _rowHeights.GetValueOrDefault(table, 25.0);
    }
}
