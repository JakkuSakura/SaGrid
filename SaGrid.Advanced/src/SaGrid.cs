using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using SaGrid.Core;

namespace SaGrid;

public class SaGrid<TData> : Table<TData>, ISaGrid<TData>
{
    private string? _quickFilter;
    
    // Callback for UI updates
    private Action? _onUIUpdate;

    public SaGrid(TableOptions<TData> options) : base(options)
    {
        SaGridModules.EnsureInitialized();
    }

    // Constructor for test compatibility  
    public SaGrid(Table<TData> table) : base(table.Options)
    {
        SaGridModules.EnsureInitialized();
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
    public async Task<string> ExportToCsvAsync()
    {
        return await Task.Run(() =>
        {
            var csv = new StringBuilder();
            
            // Headers
            var headers = VisibleLeafColumns.Select(c => EscapeCsvValue(c.Id)).ToList();
            csv.AppendLine(string.Join(",", headers));
            
            // Data rows
            foreach (var row in RowModel.Rows)
            {
                var values = VisibleLeafColumns.Select(column =>
                {
                    var cell = row.GetCell(column.Id);
                    var value = cell.Value?.ToString() ?? "";
                    return EscapeCsvValue(value);
                });
                csv.AppendLine(string.Join(",", values));
            }
            
            return csv.ToString();
        });
    }

    public string ExportToCsv()
    {
        return ExportToCsvAsync().GetAwaiter().GetResult();
    }

    public async Task<string> ExportToJsonAsync()
    {
        return await Task.Run(() =>
        {
            var data = RowModel.Rows.Select(row =>
            {
                var obj = new Dictionary<string, object?>();
                foreach (var column in VisibleLeafColumns)
                {
                    var cell = row.GetCell(column.Id);
                    obj[column.Id] = cell.Value;
                }
                return obj;
            }).ToList();
            
            return JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        });
    }

    public string ExportToJson()
    {
        return ExportToJsonAsync().GetAwaiter().GetResult();
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
            
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }
        
        return value;
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
        this.ToggleColumnVisibility(columnId, visible); // Use extension method from base
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

    // Multi-sort helpers for the example
    private bool? _multiSortOverride;
    public bool IsMultiSortEnabled()
    {
        return _multiSortOverride ?? Options.EnableMultiSort;
    }
    public void ToggleMultiSortOverride()
    {
        _multiSortOverride = !IsMultiSortEnabled();
        ScheduleUIUpdate();
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

    private void ScheduleUIUpdate()
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
        _onUIUpdate?.Invoke();
    }

    public void SelectCell(int rowIndex, string columnId, bool addToSelection = false)
    {
        if (!Options.EnableCellSelection) return;

        var cellPosition = new CellPosition(rowIndex, columnId);
        var currentSelection = State.CellSelection ?? new CellSelectionState();
        
        Console.WriteLine($"DEBUG SelectCell: Row {rowIndex}, Col {columnId}, AddToSelection: {addToSelection}, CurrentSelected: {currentSelection.SelectedCells.Count}");
        
        HashSet<CellPosition> newSelectedCells;
        
        if (addToSelection)
        {
            newSelectedCells = new HashSet<CellPosition>(currentSelection.SelectedCells) { cellPosition };
        }
        else
        {
            newSelectedCells = new HashSet<CellPosition> { cellPosition };
        }

        Console.WriteLine($"DEBUG SelectCell: NewSelectedCount: {newSelectedCells.Count}, Setting active to Row {rowIndex}, Col {columnId}");

        SetState(state => state with 
        { 
            CellSelection = new CellSelectionState(newSelectedCells, cellPosition, null)
        });

        Console.WriteLine($"DEBUG SelectCell: After SetState, Selected count: {State.CellSelection?.SelectedCells.Count ?? 0}");

        // Trigger UI update
        _onUIUpdate?.Invoke();
    }

    public void SelectCellRange(int startRowIndex, string startColumnId, int endRowIndex, string endColumnId)
    {
        if (!Options.EnableCellSelection) return;

        var startPos = new CellPosition(startRowIndex, startColumnId);
        var endPos = new CellPosition(endRowIndex, endColumnId);
        var range = new CellRange(startPos, endPos);

        // Calculate all cells in the range
        var selectedCells = new HashSet<CellPosition>();
        var startRow = Math.Min(startRowIndex, endRowIndex);
        var endRow = Math.Max(startRowIndex, endRowIndex);
        
        // Get column indices for proper range selection
        var visibleColumns = VisibleLeafColumns.ToList();
        var startColIndex = visibleColumns.FindIndex(c => c.Id == startColumnId);
        var endColIndex = visibleColumns.FindIndex(c => c.Id == endColumnId);
        
        if (startColIndex >= 0 && endColIndex >= 0)
        {
            var minColIndex = Math.Min(startColIndex, endColIndex);
            var maxColIndex = Math.Max(startColIndex, endColIndex);
            
            for (int rowIndex = startRow; rowIndex <= endRow; rowIndex++)
            {
                for (int colIndex = minColIndex; colIndex <= maxColIndex; colIndex++)
                {
                    selectedCells.Add(new CellPosition(rowIndex, visibleColumns[colIndex].Id));
                }
            }
        }

        SetState(state => state with 
        { 
            CellSelection = new CellSelectionState(selectedCells, startPos, range)
        });

        // Trigger UI update
        _onUIUpdate?.Invoke();
    }

    public void ClearCellSelection()
    {
        SetState(state => state with 
        { 
            CellSelection = new CellSelectionState()
        });

        // Trigger UI update
        _onUIUpdate?.Invoke();
    }

    public bool IsCellSelected(int rowIndex, string columnId)
    {
        var cellSelection = State.CellSelection;
        return cellSelection?.IsCellSelected(rowIndex, columnId) == true;
    }

    public (int RowIndex, string ColumnId)? GetActiveCell()
    {
        var activeCell = State.CellSelection?.ActiveCell;
        return activeCell != null ? (activeCell.RowIndex, activeCell.ColumnId) : null;
    }

    public IReadOnlyCollection<CellPosition> GetSelectedCells()
    {
        return State.CellSelection?.SelectedCells ?? new HashSet<CellPosition>();
    }

    public int GetSelectedCellCount()
    {
        return State.CellSelection?.SelectedCells.Count ?? 0;
    }

    // Copy selected cells to clipboard (as text)
    public string CopySelectedCells()
    {
        var selection = State.CellSelection;
        if (selection == null || selection.SelectedCells.Count == 0)
            return "";

        // Group by row and sort
        var cellsByRow = selection.SelectedCells
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key)
            .ToList();

        var result = new StringBuilder();
        
        foreach (var rowGroup in cellsByRow)
        {
            var rowIndex = rowGroup.Key;
            if (rowIndex >= 0 && rowIndex < RowModel.Rows.Count)
            {
                var row = RowModel.Rows[rowIndex];
                var sortedCells = rowGroup.OrderBy(c => 
                {
                    var colIndex = VisibleLeafColumns.ToList().FindIndex(col => col.Id == c.ColumnId);
                    return colIndex >= 0 ? colIndex : int.MaxValue;
                }).ToList();

                var cellValues = sortedCells.Select(cellPos =>
                {
                    var cell = row.GetCell(cellPos.ColumnId);
                    return cell.Value?.ToString() ?? "";
                });

                result.AppendLine(string.Join("\t", cellValues));
            }
        }

        return result.ToString().TrimEnd();
    }

    // Keyboard navigation for cell selection
    public bool NavigateCell(CellNavigationDirection direction)
    {
        var activeCell = GetActiveCell();
        if (activeCell == null) return false;

        var visibleColumns = VisibleLeafColumns.ToList();
        var currentColIndex = visibleColumns.FindIndex(c => c.Id == activeCell.Value.ColumnId);
        var currentRowIndex = activeCell.Value.RowIndex;

        (int RowIndex, string ColumnId)? newActiveCell = direction switch
        {
            CellNavigationDirection.Up when currentRowIndex > 0 => 
                (currentRowIndex - 1, activeCell.Value.ColumnId),
            
            CellNavigationDirection.Down when currentRowIndex < RowModel.Rows.Count - 1 => 
                (currentRowIndex + 1, activeCell.Value.ColumnId),
            
            CellNavigationDirection.Left when currentColIndex > 0 => 
                (currentRowIndex, visibleColumns[currentColIndex - 1].Id),
            
            CellNavigationDirection.Right when currentColIndex < visibleColumns.Count - 1 => 
                (currentRowIndex, visibleColumns[currentColIndex + 1].Id),
            
            _ => null
        };

        if (newActiveCell != null)
        {
            SelectCell(newActiveCell.Value.RowIndex, newActiveCell.Value.ColumnId);
            return true;
        }
        return false;
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
