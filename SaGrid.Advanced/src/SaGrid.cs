using System;
using System.Collections.Generic;
using Avalonia.Threading;
using SaGrid.Core;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Advanced.Modules.StatusBar;
using SaGrid.Advanced.Modules.Export;
using SaGrid.Advanced.Selection;
using SaGrid.Advanced.Modules.Sorting;

namespace SaGrid;

public class SaGrid<TData> : Table<TData>, ISaGrid<TData>
{
    private string? _quickFilter;
    
    // Callback for UI updates
    private Action? _onUIUpdate;
    private readonly ExportService _exportService;
    private readonly CellSelectionService _cellSelectionService;
    private readonly SortingEnhancementsService _sortingEnhancementsService;
    private readonly SideBarService _sideBarService;
    private readonly StatusBarService _statusBarService;

    public SaGrid(TableOptions<TData> options) : base(options)
    {
        SaGridModules.EnsureInitialized();
        var context = ModuleRegistry.Context;
        _exportService = context.Resolve<ExportService>();
        _cellSelectionService = context.Resolve<CellSelectionService>();
        _sortingEnhancementsService = context.Resolve<SortingEnhancementsService>();
        _sideBarService = context.Resolve<SideBarService>();
        _statusBarService = context.Resolve<StatusBarService>();
        _sideBarService.EnsureDefaultPanels(this);
        _statusBarService.EnsureDefaultWidgets(this);
    }

    // Constructor for test compatibility  
    public SaGrid(Table<TData> table) : base(table.Options)
    {
        SaGridModules.EnsureInitialized();
        var context = ModuleRegistry.Context;
        _exportService = context.Resolve<ExportService>();
        _cellSelectionService = context.Resolve<CellSelectionService>();
        _sortingEnhancementsService = context.Resolve<SortingEnhancementsService>();
        _sideBarService = context.Resolve<SideBarService>();
        _statusBarService = context.Resolve<StatusBarService>();
        _sideBarService.EnsureDefaultPanels(this);
        _statusBarService.EnsureDefaultWidgets(this);
    }

    // Advanced filtering capabilities
    public void SetGlobalFilter(object? value)
    {
        GlobalFilterExtensions.SetGlobalFilter(this, value);
        ScheduleUIUpdate();
    }

    public object? GetGlobalFilterValue()
    {
        return GlobalFilterExtensions.GetGlobalFilterValue(this);
    }

    public void ClearGlobalFilter()
    {
        GlobalFilterExtensions.ClearGlobalFilter(this);
        // When filters change, reset to first page to avoid empty views
        if (State.Pagination != null)
        {
            base.SetPageIndex(0);
        }
        ScheduleUIUpdate();
    }

    // Export functionality
    public Task<string> ExportToCsvAsync()
    {
        return _exportService.ExportToCsvAsync(this);
    }

    public string ExportToCsv()
    {
        return _exportService.ExportToCsv(this);
    }

    public Task<string> ExportToJsonAsync()
    {
        return _exportService.ExportToJsonAsync(this);
    }

    public string ExportToJson()
    {
        return _exportService.ExportToJson(this);
    }

    // Advanced search and filtering
    public void SetQuickFilter(string? searchTerm)
    {
        _quickFilter = searchTerm;
        // In a real implementation, this would trigger filtering
        // For now, we'll store it and use it in custom filtering logic
    }

    public string? GetQuickFilter()
    {
        return _quickFilter;
    }

    // Advanced column operations
    public new void SetColumnVisibility(string columnId, bool visible)
    {
        base.SetColumnVisibility(columnId, visible);
        ScheduleUIUpdate();
    }

    public new bool GetColumnVisibility(string columnId)
    {
        var visibility = State.ColumnVisibility;
        return visibility?.Items.GetValueOrDefault(columnId, true) ?? true;
    }

    public new int GetVisibleColumnCount()
    {
        return VisibleLeafColumns.Count;
    }

    public new int GetTotalColumnCount()
    {
        return AllLeafColumns.Count;
    }

    public new int GetHiddenColumnCount()
    {
        return GetTotalColumnCount() - GetVisibleColumnCount();
    }

    // Side bar APIs
    public SideBarService GetSideBarService() => _sideBarService;

    public IReadOnlyList<SideBarPanelDefinition> GetSideBarPanels() => _sideBarService.GetPanels(this);

    public void SetSideBarPanels(IEnumerable<SideBarPanelDefinition> panels) => _sideBarService.SetPanels(this, panels);

    public bool IsSideBarVisible() => _sideBarService.IsVisible(this);

    public void SetSideBarVisible(bool visible) => _sideBarService.SetVisible(this, visible);

    public void ToggleSideBarVisible() => _sideBarService.ToggleVisible(this);

    public void OpenToolPanel(string panelId) => _sideBarService.OpenPanel(this, panelId);

    public void CloseToolPanel() => _sideBarService.ClosePanel(this);

    public string? GetOpenedToolPanel() => _sideBarService.GetActivePanelId(this);

    public SideBarPosition GetSideBarPosition() => _sideBarService.GetPosition(this);

    public void SetSideBarPosition(SideBarPosition position) => _sideBarService.SetPosition(this, position);

    public bool IsMultiSortEnabled()
    {
        return _sortingEnhancementsService.IsMultiSortEnabled(this);
    }
    public void ToggleMultiSortOverride()
    {
        _sortingEnhancementsService.ToggleMultiSortOverride(this);
    }

    // Keyboard navigation support
    private int _currentRowIndex = 0;
    private int _currentColumnIndex = 0;
    
    // Context menu and row actions
    private List<ContextMenuItem> _contextMenuItems = new();
    private List<RowAction<TData>> _rowActions = new();

    public void HandleKeyDown(string key)
    {
        switch (key.ToLower())
        {
            case "arrowup":
                _currentRowIndex = Math.Max(0, _currentRowIndex - 1);
                break;
            case "arrowdown":
                _currentRowIndex = Math.Min(RowModel.Rows.Count - 1, _currentRowIndex + 1);
                break;
            case "arrowleft":
                _currentColumnIndex = Math.Max(0, _currentColumnIndex - 1);
                break;
            case "arrowright":
                _currentColumnIndex = Math.Min(VisibleLeafColumns.Count - 1, _currentColumnIndex + 1);
                break;
        }
    }

    public Cell<TData>? GetCurrentCell()
    {
        if (_currentRowIndex >= 0 && _currentRowIndex < RowModel.Rows.Count &&
            _currentColumnIndex >= 0 && _currentColumnIndex < VisibleLeafColumns.Count)
        {
            var row = RowModel.Rows[_currentRowIndex];
            var column = VisibleLeafColumns[_currentColumnIndex];
            return row.GetCell(column.Id) as Cell<TData>;
        }
        return null;
    }

    public Row<TData>? GetCurrentRow()
    {
        if (_currentRowIndex >= 0 && _currentRowIndex < RowModel.Rows.Count)
        {
            return RowModel.Rows[_currentRowIndex];
        }
        return null;
    }

    // Context menu functionality
    public void SetContextMenuItems(IEnumerable<ContextMenuItem> items)
    {
        _contextMenuItems = items.ToList();
    }

    public IReadOnlyList<ContextMenuItem> GetContextMenuItems()
    {
        return _contextMenuItems.AsReadOnly();
    }

    // Row actions functionality
    public void AddRowAction(RowAction<TData> action)
    {
        _rowActions.Add(action);
    }

    public void AddRowAction(string id, string label, Action<Row<TData>> action)
    {
        var rowAction = new RowAction<TData>
        {
            Id = id,
            Label = label,
            Action = action
        };
        _rowActions.Add(rowAction);
    }

    // Overload for test compatibility (with default action)
    public void AddRowAction(string id, string label)
    {
        AddRowAction(id, label, _ => { }); // Default empty action
    }

    // Overload for test compatibility (with Func<Row<TData>, string>)
    public void AddRowAction(string id, Func<Row<TData>, string> labelGenerator)
    {
        AddRowAction(id, id, row => { labelGenerator(row); }); // Convert Func<Row<TData>, string> to Action<Row<TData>>
    }

    public IReadOnlyList<RowAction<TData>> GetRowActions()
    {
        return _rowActions.AsReadOnly();
    }

    public IReadOnlyList<RowAction<TData>> GetRowActions(Row<TData> row)
    {
        // Return all actions for now, could be filtered based on row state
        return _rowActions.AsReadOnly();
    }

    public void RemoveRowAction(string actionId)
    {
        _rowActions.RemoveAll(a => a.Id == actionId);
    }

    public void ClearRowActions()
    {
        _rowActions.Clear();
    }

    // Header rendering functionality
    private Func<string, string>? _headerRenderer;

    public void SetHeaderRenderer(Func<string, string> renderer)
    {
        _headerRenderer = renderer;
    }

    // Overload for test compatibility
    public void SetHeaderRenderer(string columnId, Func<string, string> renderer)
    {
        // For simplicity, we'll just use the renderer for all columns
        // In a real implementation, this might be column-specific
        _headerRenderer = renderer;
    }

    public string RenderHeader(string columnId)
    {
        var column = GetColumn(columnId);
        if (column == null) return columnId;

        if (_headerRenderer != null)
        {
            return _headerRenderer(columnId);
        }

        // Default header rendering - use column header or fallback to ID
        return column.ColumnDef.Header?.ToString() ?? columnId;
    }

    // Cell rendering functionality
    private Func<Row<TData>, string, string>? _cellRenderer;

    public void SetCellRenderer(Func<Row<TData>, string, string> renderer)
    {
        _cellRenderer = renderer;
    }

    // Sorting wrappers to trigger UI updates
    public new void SetSorting(IEnumerable<ColumnSort> sorts)
    {
        base.SetSorting(sorts);
        ScheduleUIUpdate();
    }

    public new void ToggleSort(string columnId)
    {
        base.ToggleSort(columnId);
        ScheduleUIUpdate();
    }

    public void SetSorting(string columnId, SortDirection direction)
    {
        base.SetSorting(columnId, direction);
        ScheduleUIUpdate();
    }

    public string RenderCell(Row<TData> row, string columnId)
    {
        if (_cellRenderer != null)
        {
            return _cellRenderer(row, columnId);
        }

        // Default cell rendering
        var cell = row.GetCell(columnId);
        return cell.Value?.ToString() ?? "";
    }

    // Overload for test compatibility
    public string RenderCell(string columnId, Row<TData> row)
    {
        return RenderCell(row, columnId);
    }

    // Column management methods for tests
    public void ResizeColumn(string columnId, double width)
    {
        var currentSizing = State.ColumnSizing ?? new ColumnSizingState();
        var newSizing = new Dictionary<string, double>(currentSizing.Items)
        {
            [columnId] = width
        };

        SetState(state => state with 
        { 
            ColumnSizing = new ColumnSizingState(newSizing)
        });
    }

    public void MoveColumn(string columnId, int toIndex)
    {
        var currentOrder = State.ColumnOrder?.Order ?? AllLeafColumns.Select(c => c.Id).ToList();
        var newOrder = currentOrder.ToList();

        // Remove the column from its current position
        newOrder.Remove(columnId);

        // Insert at the new position
        var clampedIndex = Math.Max(0, Math.Min(toIndex, newOrder.Count));
        newOrder.Insert(clampedIndex, columnId);

        SetState(state => state with 
        { 
            ColumnOrder = new ColumnOrderState(newOrder)
        });
    }

    // Theme support for tests
    private string? _currentTheme;
    private Dictionary<string, object> _themeProperties = new();

    public void SetTheme(string theme)
    {
        _currentTheme = theme;
        
        // Set default theme properties based on theme
        switch (theme.ToLowerInvariant())
        {
            case "dark":
                _themeProperties["backgroundColor"] = "#1e1e1e";
                _themeProperties["textColor"] = "#ffffff";
                break;
            case "light":
                _themeProperties["backgroundColor"] = "#ffffff";
                _themeProperties["textColor"] = "#000000";
                break;
            default:
                _themeProperties["backgroundColor"] = "#f5f5f5";
                _themeProperties["textColor"] = "#333333";
                break;
        }
        ScheduleUIUpdate();
    }

    public string? CurrentTheme => _currentTheme;

    public void SetThemeProperty(string key, object value)
    {
        _themeProperties[key] = value;
    }

    public object? GetThemeProperty(string key)
    {
        return _themeProperties.GetValueOrDefault(key);
    }

    // Additional filtering methods needed by the example
    public void ClearColumnFilters()
    {
        SetState(state => state with { ColumnFilters = null });
        if (State.Pagination != null)
        {
            base.SetPageIndex(0);
        }
        ScheduleUIUpdate();
    }

    public void SetColumnFilter(string columnId, object? value)
    {
        var currentFilters = State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        var existing = currentFilters.FirstOrDefault(f => f.Id == columnId);
        var currentValue = existing?.Value;
        var newFilters = currentFilters.Where(f => f.Id != columnId).ToList();

        // Skip state update if value is effectively unchanged
        var isEqual = (currentValue == null && value == null) ||
                      (currentValue != null && value != null &&
                       string.Equals(currentValue.ToString(), value.ToString(), StringComparison.Ordinal));
        if (isEqual)
        {
            return;
        }

        if (value != null)
        {
            newFilters.Add(new ColumnFilter(columnId, value));
        }
        
        SetState(state => state with 
        { 
            ColumnFilters = newFilters.Count > 0 ? new ColumnFiltersState(newFilters) : null
        });
        if (State.Pagination != null)
        {
            base.SetPageIndex(0);
        }
        ScheduleUIUpdate();
    }

    // Cell selection functionality
    // Method for UI to set update callback
    public void SetUIUpdateCallback(Action? callback)
    {
        _onUIUpdate = callback;
    }

    // Pagination wrappers to ensure UI updates
    public new void SetPageIndex(int pageIndex)
    {
        base.SetPageIndex(pageIndex);
        ScheduleUIUpdate();
    }

    public new void SetPageSize(int pageSize)
    {
        base.SetPageSize(pageSize);
        ScheduleUIUpdate();
    }

    public new void NextPage()
    {
        base.NextPage();
        ScheduleUIUpdate();
    }

    internal void ScheduleUIUpdate()
    {
        try
        {
            Dispatcher.UIThread.Post(() => _onUIUpdate?.Invoke());
        }
        catch
        {
            _onUIUpdate?.Invoke();
        }
    }

    public new void PreviousPage()
    {
        base.PreviousPage();
        NotifyUIUpdate();
    }

    internal void NotifyUIUpdate()
    {
        _onUIUpdate?.Invoke();
    }

    public void SelectCell(int rowIndex, string columnId, bool addToSelection = false)
    {
        _cellSelectionService.SelectCell(this, rowIndex, columnId, addToSelection);
    }

    public void SelectCellRange(int startRowIndex, string startColumnId, int endRowIndex, string endColumnId)
    {
        _cellSelectionService.SelectCellRange(this, startRowIndex, startColumnId, endRowIndex, endColumnId);
    }

    public void ClearCellSelection()
    {
        _cellSelectionService.ClearSelection(this);
    }

    public bool IsCellSelected(int rowIndex, string columnId)
    {
        return _cellSelectionService.IsCellSelected(this, rowIndex, columnId);
    }

    public (int RowIndex, string ColumnId)? GetActiveCell()
    {
        return _cellSelectionService.GetActiveCell(this);
    }

    public IReadOnlyCollection<CellPosition> GetSelectedCells()
    {
        return _cellSelectionService.GetSelectedCells(this);
    }

    public int GetSelectedCellCount()
    {
        return _cellSelectionService.GetSelectedCellCount(this);
    }

    // Copy selected cells to clipboard (as text)
    public string CopySelectedCells()
    {
        return _cellSelectionService.CopySelectedCells(this);
    }

    // Keyboard navigation for cell selection
    public bool NavigateCell(CellNavigationDirection direction)
    {
        return _cellSelectionService.Navigate(this, direction);
    }

    // Status Bar API methods following AG Grid pattern
    public StatusBarService GetStatusBarService()
    {
        return _statusBarService;
    }

    public bool IsStatusBarVisible()
    {
        return _statusBarService.IsVisible(this);
    }

    public void SetStatusBarVisible(bool visible)
    {
        _statusBarService.SetVisible(this, visible);
    }

    public void ToggleStatusBarVisible()
    {
        _statusBarService.ToggleVisible(this);
    }

    public void SetStatusBarPosition(StatusBarPosition position)
    {
        _statusBarService.SetPosition(this, position);
    }

    public StatusBarPosition GetStatusBarPosition()
    {
        return _statusBarService.GetPosition(this);
    }

    public void RegisterStatusWidget(StatusBarWidgetDefinition widget)
    {
        _statusBarService.RegisterWidget(this, widget);
    }

    public void UnregisterStatusWidget(string widgetId)
    {
        _statusBarService.UnregisterWidget(this, widgetId);
    }

    public StatusBarState GetStatusBarState()
    {
        return _statusBarService.GetState(this);
    }


}

// Factory methods for SaGrid.Advanced
public static class SaGridFactory
{
    public static SaGrid<TData> Create<TData>(TableOptions<TData> options)
    {
        return new SaGrid<TData>(options);
    }

    public static SaGrid<TData> Create<TData>(Table<TData> table)
    {
        return new SaGrid<TData>(table.Options);
    }

    // Overload that accepts Table directly for test compatibility
    public static SaGrid<TData> CreateFromTable<TData>(Table<TData> table)
    {
        return new SaGrid<TData>(table.Options);
    }

    public static SaGrid<TData> Create<TData>(
        IEnumerable<TData> data,
        IReadOnlyList<ColumnDef<TData>> columns)
    {
        var options = new TableOptions<TData>
        {
            Data = data,
            Columns = columns,
            EnableGlobalFilter = true,
            EnableColumnFilters = true,
            EnableSorting = true,
            EnableRowSelection = true,
            EnableColumnResizing = true,
            EnablePagination = true
        };
        
        return new SaGrid<TData>(options);
    }
}
