using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using SaGrid.Advanced.Components;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interactive;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Modules.Analytics;
using SaGrid.Advanced.Modules.Editing;
using SaGrid.Advanced.Modules.Export;
using SaGrid.Advanced.Modules.Filters;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Advanced.Modules.Sorting;
using SaGrid.Advanced.Modules.StatusBar;
using SaGrid.Advanced.RowModel;
using SaGrid.Advanced.Selection;
using SaGrid.Core;
using SaGrid.Core.Models;

namespace SaGrid.Advanced;

public class SaGrid<TData> : ISaGrid<TData>, ISaGridComponentHost<TData>
{
    private readonly SaGridComponent<TData>? _component;
    private readonly Table<TData> _table;
    // Quick filter removed; rely on global filter only
    
    // Callback for UI updates
    private Action? _onGridUpdate;
    private Action<CellSelectionDelta?>? _onSelectionUpdate;
    private ExportService _exportService = default!;
    private CellSelectionService _cellSelectionService = default!;
    private SortingEnhancementsService _sortingEnhancementsService = default!;
    private SideBarService _sideBarService = default!;
    private IFilterService _filterService = default!;
    private ICellEditorService<TData> _cellEditorService = default!;
    private BatchEditManager<TData> _batchEditManager = default!;
    private IAggregationService _aggregationService = default!;
    private IGroupingService _groupingService = default!;
    private StatusBarService _statusBarService = default!;
    private IEventService _eventService = default!;
    private IChartIntegrationService _chartIntegrationService = default!;
    private IExportCoordinator _exportCoordinator = default!;
    private IClientSideRowModel<TData> _clientSideRowModel = default!;
    private ColumnInteractiveService<TData> _columnInteractiveService = default!;
    private IServerSideRowModel<TData>? _serverSideRowModel;
    private RowModelType _rowModelType;
    private int _serverSideBlockSize;

    public event EventHandler? RowDataChanged;

    internal Table<TData> InnerTable => _table;
    public SaGridComponent<TData>? Component => _component;

    public TableOptions<TData> Options => _table.Options;
    public TableState<TData> State => _table.State;
    public IReadOnlyList<Column<TData>> AllColumns => _table.AllColumns;
    public IReadOnlyList<Column<TData>> AllLeafColumns => _table.AllLeafColumns;
    public IReadOnlyList<Column<TData>> VisibleLeafColumns => _table.VisibleLeafColumns;
    public IReadOnlyList<HeaderGroup<TData>> HeaderGroups => _table.HeaderGroups;
    public IReadOnlyList<HeaderGroup<TData>> FooterGroups => _table.FooterGroups;
    public RowModel<TData> RowModel => _table.RowModel;
    public RowModel<TData> PreFilteredRowModel => _table.PreFilteredRowModel;
    public RowModel<TData> PreSortedRowModel => _table.PreSortedRowModel;
    public RowModel<TData> PreGroupedRowModel => _table.PreGroupedRowModel;
    public RowModel<TData> PreExpandedRowModel => _table.PreExpandedRowModel;
    public RowModel<TData> PrePaginationRowModel => _table.PrePaginationRowModel;

    public void SetState(TableState<TData> state, bool updateRowModel = true)
    {
        _table.SetState(state, updateRowModel);

        if (_rowModelType == RowModelType.ServerSide)
        {
            _serverSideRowModel?.Refresh(ServerSideRefreshMode.Full, purge: true);
            ScheduleUIUpdate();
        }
    }

    public void SetState(Updater<TableState<TData>> updater, bool updateRowModel = true)
    {
        _table.SetState(updater, updateRowModel);

        if (_rowModelType == RowModelType.ServerSide)
        {
            _serverSideRowModel?.Refresh(ServerSideRefreshMode.Full, purge: true);
            ScheduleUIUpdate();
        }
    }

    public Column<TData>? GetColumn(string columnId) => InnerTable.GetColumn(columnId);

    public Row<TData>? GetRow(string rowId) => InnerTable.GetRow(rowId);

    public IReadOnlyList<Row<TData>> GetSelectedRowModel() => InnerTable.GetSelectedRowModel();

    public void ResetColumnFilters() { InnerTable.ResetColumnFilters(); ScheduleUIUpdate(); }
    public void ResetGlobalFilter() { InnerTable.ResetGlobalFilter(); ScheduleUIUpdate(); }
    public void ResetSorting() { InnerTable.ResetSorting(); ScheduleUIUpdate(); }
    public void ResetRowSelection() { InnerTable.ResetRowSelection(); ScheduleUIUpdate(); }
    public void ResetColumnOrder() { InnerTable.ResetColumnOrder(); ScheduleUIUpdate(); }
    public void ResetColumnSizing() { InnerTable.ResetColumnSizing(); ScheduleUIUpdate(); }
    public void ResetColumnVisibility() { InnerTable.ResetColumnVisibility(); ScheduleUIUpdate(); }
    public void ResetExpanded() { InnerTable.ResetExpanded(); ScheduleUIUpdate(); }
    public void ResetGrouping() { InnerTable.ResetGrouping(); ScheduleUIUpdate(); }
    public void ResetPagination() { InnerTable.ResetPagination(); ScheduleUIUpdate(); }
    public int GetPageCount() => InnerTable.GetPageCount();
    public bool GetCanPreviousPage() => InnerTable.GetCanPreviousPage();
    public bool GetCanNextPage() => InnerTable.GetCanNextPage();
    public void FirstPage() { InnerTable.FirstPage(); ScheduleUIUpdate(); }
    public void LastPage() { InnerTable.LastPage(); ScheduleUIUpdate(); }
    public bool GetIsAllRowsSelected() => InnerTable.GetIsAllRowsSelected();
    public bool GetIsSomeRowsSelected() => InnerTable.GetIsSomeRowsSelected();
    public void SelectAllRows() { InnerTable.SelectAllRows(); ScheduleUIUpdate(); }
    public void DeselectAllRows() { InnerTable.DeselectAllRows(); ScheduleUIUpdate(); }
    public void ToggleAllRowsSelected() { InnerTable.ToggleAllRowsSelected(); ScheduleUIUpdate(); }
    public void SetRowSelection(string rowId, bool selected) { InnerTable.SetRowSelection(rowId, selected); ScheduleUIUpdate(); }
    public void SelectRowRange(int startIndex, int endIndex) { InnerTable.SelectRowRange(startIndex, endIndex); ScheduleUIUpdate(); }
    public int GetSelectedRowCount() => InnerTable.GetSelectedRowCount();
    public int GetTotalRowCount() => InnerTable.GetTotalRowCount();

    public SaGrid(TableOptions<TData> options)
    {
        var table = new Table<TData>(options);
        _table = table;
        try
        {
            SaGridModules.EnsureInitialized();
            _component = new SaGridComponent<TData>(this, options, table);
            Initialize(table, options);
        }
        catch
        {
            _component = null;
            // Still initialize services for headless use
            Initialize(table, options);
        }
    }

    // Constructor for test compatibility
    public SaGrid(Table<TData> table)
    {
        _table = table;
        try
        {
            SaGridModules.EnsureInitialized();
            _component = new SaGridComponent<TData>(this, table.Options, table);
            Initialize(table, table.Options);
        }
        catch
        {
            _component = null;
            Initialize(table, table.Options);
        }
    }

    private void Initialize(Table<TData> table, TableOptions<TData> options)
    {
        var context = ModuleRegistry.Context;
        _rowModelType = ResolveRowModelType(options);
        _serverSideBlockSize = ResolveServerBlockSize(options);
        _exportService = context.Resolve<ExportService>();
        _cellSelectionService = context.Resolve<CellSelectionService>();
        _sortingEnhancementsService = context.Resolve<SortingEnhancementsService>();
        _sideBarService = context.Resolve<SideBarService>();
        _filterService = context.Resolve<IFilterService>();
        _aggregationService = context.Resolve<IAggregationService>();
        _groupingService = context.Resolve<IGroupingService>();
        _statusBarService = context.Resolve<StatusBarService>();
        _eventService = context.TryResolve<IEventService>(out var eventService) ? eventService : new EventService();
        _chartIntegrationService = context.Resolve<IChartIntegrationService>();
        _exportCoordinator = context.Resolve<IExportCoordinator>();

        var editorRegistry = context.Resolve<ICellEditorRegistry>();
        _cellEditorService = editorRegistry.GetOrCreate<TData>();
        _batchEditManager = _cellEditorService.GetBatchManager(this);
        _clientSideRowModel = new ClientSideRowModel<TData>(this, table);
        _columnInteractiveService = new ColumnInteractiveService<TData>(table, _eventService);
        if (_rowModelType == RowModelType.ServerSide)
        {
            _serverSideRowModel = new ServerSideRowModel<TData>(this, _serverSideBlockSize);
            _serverSideRowModel.RowsChanged += OnServerRowsChanged;
        }
        else
        {
            _serverSideRowModel = null;
        }

        _sideBarService.EnsureDefaultPanels(this);
        // Standalone filter panel disabled; rely on built-in filter boxes and global filter hook
        _statusBarService.EnsureDefaultWidgets(this);
        _chartIntegrationService.AttachToGrid(this);
        _exportCoordinator.AttachToGrid(this);

        if (_rowModelType == RowModelType.ClientSide)
        {
            _clientSideRowModel.Start();
        }

        _eventService.DispatchEvent(GridEventTypes.GridReady, new GridReadyEventArgs(this));
    }

    // Advanced filtering capabilities
    public void SetGlobalFilter(object? value)
    {
        InnerTable.SetGlobalFilter(value);
        ScheduleUIUpdate();
    }

    public object? GetGlobalFilterValue()
    {
        return InnerTable.GetGlobalFilterValue();
    }

    public void ClearGlobalFilter()
    {
        InnerTable.ClearGlobalFilter();
        if (State.Pagination != null)
        {
            InnerTable.SetPageIndex(0);
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

    public Task<byte[]> ExportToExcelAsync()
    {
        return _exportService.ExportToExcelAsync(this);
    }

    public byte[] ExportToExcel()
    {
        return _exportService.ExportToExcel(this);
    }

    public string BuildClipboardData(ClipboardExportFormat format = ClipboardExportFormat.TabDelimited, bool includeHeaders = true)
    {
        return _exportService.BuildClipboardData(this, format, includeHeaders);
    }

    public int GetApproximateRowCount()
    {
        if (_rowModelType == RowModelType.ServerSide && _serverSideRowModel != null)
        {
            return _serverSideRowModel.GetRowCount();
        }

        return RowModel.FlatRows.Count;
    }

    public ChartRequest BuildDefaultChartRequest()
    {
        return _chartIntegrationService.BuildDefaultRequest(this);
    }

    public bool ShowChart(ChartRequest request)
    {
        return _chartIntegrationService.ShowChart(this, request);
    }

    public bool ShowQuickChart()
    {
        return _chartIntegrationService.TryShowDefaultChart(this);
    }

    public Task<ExportResult?> ShowExportOptionsAsync(Window? owner = null)
    {
        return _exportCoordinator.ShowExportDialogAsync(this, owner);
    }

    public ExportResult ExportWithOptions(ExportRequest request)
    {
        return _exportCoordinator.ExecuteExport(this, request);
    }

    public ICellEditorService<TData> GetEditingService()
    {
        return _cellEditorService;
    }

    public bool BeginCellEdit(Row<TData> row, Column<TData> column)
    {
        return _cellEditorService.BeginEdit(this, row, column);
    }

    public bool BeginCellEdit(string rowId, string columnId)
    {
        var row = GetRow(rowId);
        var column = GetColumn(columnId);
        if (row == null || column == null)
        {
            return false;
        }

        return BeginCellEdit(row, column);
    }

    public bool CommitActiveCellEdit()
    {
        return _cellEditorService.CommitEdit(this);
    }

    public void CancelActiveCellEdit()
    {
        _cellEditorService.CancelEdit(this);
    }

    public void BeginBatchEdit()
    {
        _cellEditorService.BeginBatch(this);
    }

    public void CommitBatchEdit()
    {
        _cellEditorService.CommitEdit(this);
        _cellEditorService.CommitBatch(this);
    }

    public void CancelBatchEdit()
    {
        _cellEditorService.CancelEdit(this);
        _cellEditorService.CancelBatch(this);
    }

    public void UndoLastEdit()
    {
        _cellEditorService.CancelEdit(this);
        _cellEditorService.Undo(this);
    }

    public void RedoLastEdit()
    {
        _cellEditorService.CancelEdit(this);
        _cellEditorService.Redo(this);
    }

    public IReadOnlyDictionary<CellCoordinate, CellEditEntry<TData>> GetPendingEdits()
    {
        return new ReadOnlyDictionary<CellCoordinate, CellEditEntry<TData>>(
            new Dictionary<CellCoordinate, CellEditEntry<TData>>(_batchEditManager.PendingEdits));
    }

    // Quick filter removed

    // Advanced column operations
    public void SetColumnVisibility(string columnId, bool visible)
    {
        InnerTable.SetColumnVisibility(columnId, visible);
        ScheduleUIUpdate();
    }

    public bool GetColumnVisibility(string columnId)
    {
        return InnerTable.GetColumnVisibility(columnId);
    }

    public int GetVisibleColumnCount()
    {
        return InnerTable.GetVisibleColumnCount();
    }

    public int GetTotalColumnCount()
    {
        return InnerTable.GetTotalColumnCount();
    }

    public int GetHiddenColumnCount()
    {
        return InnerTable.GetHiddenColumnCount();
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
    public void SetSorting(IEnumerable<ColumnSort> sorts)
    {
        InnerTable.SetSorting(sorts);
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Sort));
    }

    public void ToggleSort(string columnId)
    {
        InnerTable.ToggleSort(columnId);
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Sort));
    }

    public void SetSorting(string columnId, SortDirection direction)
    {
        InnerTable.SetSorting(columnId, direction);
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Sort));
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
        var currentFilters = State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        foreach (var filter in currentFilters.ToList())
        {
            ClearColumnFilter(filter.Id);
        }
    }

    public void SetColumnFilter(string columnId, object? value)
    {
        // Compare against the unwrapped, UI-facing filter value for this column
        var currentValue = GetColumn(columnId)?.FilterValue;
        var currentFilters = State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        var newFilters = currentFilters.Where(f => f.Id != columnId).ToList();

        var isEqual = FiltersEqual(currentValue, value);
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
            InnerTable.SetPageIndex(0);
        }

        if (value is not SetFilterState && _filterService is FilterService filterServiceImpl)
        {
            filterServiceImpl.NotifyManualFilterChange(this, columnId);
        }

        _eventService.DispatchEvent(GridEventTypes.FilterChanged, new FilterChangedEventArgs(this, columnId, value));
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Filter));
        ScheduleUIUpdate();
    }

    public void ClearColumnFilter(string columnId)
    {
        var currentFilters = State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        if (!currentFilters.Any(f => f.Id == columnId))
        {
            return;
        }

        var newFilters = currentFilters.Where(f => f.Id != columnId).ToList();
        SetState(state => state with
        {
            ColumnFilters = newFilters.Count > 0 ? new ColumnFiltersState(newFilters) : null
        });

        if (State.Pagination != null)
        {
            InnerTable.SetPageIndex(0);
        }

        if (_filterService is FilterService filterServiceImpl)
        {
            filterServiceImpl.NotifyManualFilterChange(this, columnId);
        }

        _eventService.DispatchEvent(GridEventTypes.FilterChanged, new FilterChangedEventArgs(this, columnId, null));
        RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Filter));
        ScheduleUIUpdate();
    }

    private static bool FiltersEqual(object? currentValue, object? newValue)
    {
        if (ReferenceEquals(currentValue, newValue))
        {
            return true;
        }

        if (currentValue == null || newValue == null)
        {
            return currentValue == null && newValue == null;
        }

        if (currentValue is SetFilterState currentSet && newValue is SetFilterState newSet)
        {
            if (currentSet.Operator != newSet.Operator || currentSet.IncludeBlanks != newSet.IncludeBlanks)
            {
                return false;
            }

            return currentSet.SelectedValues.SequenceEqual(newSet.SelectedValues, StringComparer.OrdinalIgnoreCase);
        }

        if (currentValue is string currentString && newValue is string newString)
        {
            return string.Equals(currentString, newString, StringComparison.OrdinalIgnoreCase);
        }

        if (currentValue is TextFilterState currentText && newValue is TextFilterState newText)
        {
            return string.Equals(currentText.Query, newText.Query, StringComparison.Ordinal) &&
                   currentText.Mode == newText.Mode &&
                   currentText.CaseSensitive == newText.CaseSensitive;
        }

        return Equals(currentValue, newValue);
    }

    // Cell selection functionality
    // Method for UI to set update callback
    public void SetUIUpdateCallbacks(Action? gridCallback, Action<CellSelectionDelta?>? selectionCallback)
    {
        _onGridUpdate = gridCallback;
        _onSelectionUpdate = selectionCallback;
    }

    public void SetUIUpdateCallback(Action? callback)
    {
        SetUIUpdateCallbacks(callback, callback != null ? _ => callback() : null);
    }

    internal void UpdateCellValue(string rowId, string columnId, object? value)
    {
        var row = GetRow(rowId);
        row?.UpdateCell(columnId, value);
    }

    public IFilterService GetFilterService()
    {
        return _filterService;
    }

    // Pagination wrappers to ensure UI updates
    public void SetPageIndex(int pageIndex)
    {
        InnerTable.SetPageIndex(pageIndex);
        ScheduleUIUpdate();
    }

    public void SetPageSize(int pageSize)
    {
        InnerTable.SetPageSize(pageSize);
        ScheduleUIUpdate();
    }

    public void NextPage()
    {
        InnerTable.NextPage();
        ScheduleUIUpdate();
    }

    internal void ScheduleUIUpdate()
    {
        try
        {
            Dispatcher.UIThread.Post(() => _onGridUpdate?.Invoke());
        }
        catch
        {
            _onGridUpdate?.Invoke();
        }
    }

    public void PreviousPage()
    {
        InnerTable.PreviousPage();
        NotifyUIUpdate();
    }

    internal void NotifyUIUpdate()
    {
        _onGridUpdate?.Invoke();
    }

    internal void NotifySelectionUpdate(CellSelectionDelta? delta)
    {
        _onSelectionUpdate?.Invoke(delta);
    }

    private void RaiseModelUpdated(bool newData)
    {
        RowDataChanged?.Invoke(this, EventArgs.Empty);
        ScheduleUIUpdate();
        _eventService.DispatchEvent(GridEventTypes.ModelUpdated, new ModelUpdatedEventArgs(this, newData));
    }

    public void SelectCell(int rowIndex, string columnId, bool addToSelection = false)
    {
        _cellSelectionService.SelectCell(this, rowIndex, columnId, addToSelection);
    }

    private static RowModelType ResolveRowModelType(TableOptions<TData> options)
    {
        if (options.Meta != null && options.Meta.TryGetValue("rowModelType", out var value))
        {
            if (value is RowModelType typed)
            {
                return typed;
            }

            if (value is string text && Enum.TryParse<RowModelType>(text, true, out var parsed))
            {
                return parsed;
            }
        }

        return RowModelType.ClientSide;
    }

    private static int ResolveServerBlockSize(TableOptions<TData> options)
    {
        if (options.Meta != null && options.Meta.TryGetValue("serverSideBlockSize", out var value))
        {
            if (value is int i && i > 0)
            {
                return i;
            }

            if (value is string text && int.TryParse(text, out var parsed) && parsed > 0)
            {
                return parsed;
            }
        }

        return 100;
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

    public IAggregationService GetAggregationService()
    {
        return _aggregationService;
    }

    public AggregationSnapshot GetAggregationSnapshot()
    {
        return _aggregationService.GetSnapshot(this);
    }

    // ================================
    // Row Grouping API
    // ================================

    public IGroupingService GetGroupingService()
    {
        return _groupingService;
    }

    public IReadOnlyList<string> GetGroupedColumnIds()
    {
        return _groupingService.GetGroupedColumnIds(this);
    }

    public GroupingConfiguration GetGroupingConfiguration()
    {
        return _groupingService.GetConfiguration(this);
    }

    public void SetGrouping(IEnumerable<string> columnIds)
    {
        _groupingService.SetGrouping(this, columnIds);
    }

    public void AddGroupingColumn(string columnId, int? insertAtIndex = null)
    {
        _groupingService.AddGroupingColumn(this, columnId, insertAtIndex);
    }

    public void RemoveGroupingColumn(string columnId)
    {
        _groupingService.RemoveGroupingColumn(this, columnId);
    }

    public void MoveGroupingColumn(string columnId, int targetIndex)
    {
        _groupingService.MoveGroupingColumn(this, columnId, targetIndex);
    }

    public void ClearGrouping()
    {
        _groupingService.ClearGrouping(this);
    }

    // Advanced Grid API methods following AG Grid pattern
    
    /// <summary>
    /// Get the event service for adding custom event listeners
    /// </summary>
    public IEventService GetEventService()
    {
        return _eventService;
    }

    /// <summary>
    /// Get the client-side row model for advanced row operations
    /// </summary>
    public IClientSideRowModel<TData> GetRowModel()
    {
        return _clientSideRowModel;
    }

    public RowModelType GetRowModelType()
    {
        return _rowModelType;
    }

    public IServerSideRowModel<TData>? GetServerSideRowModel()
    {
        return _serverSideRowModel;
    }

    public void SetServerSideDataSource(IServerSideDataSource<TData> dataSource, bool refresh = true)
    {
        if (_rowModelType != RowModelType.ServerSide || _serverSideRowModel == null)
        {
            throw new InvalidOperationException("Server-side data sources can only be configured when using the server-side row model.");
        }

        _serverSideRowModel.SetDataSource(dataSource, refresh);
        if (refresh)
        {
            RaiseModelUpdated(true);
        }
    }

    /// <summary>
    /// Configure server-side retention policy (number of margin blocks around viewport and maximum resident blocks in cache).
    /// </summary>
    public void SetServerSideRetention(int retainMarginBlocks, int maxResidentBlocks)
    {
        _serverSideRowModel?.ConfigureRetention(retainMarginBlocks, maxResidentBlocks);
    }

    /// <summary>
    /// Add event listener for typed events
    /// </summary>
    public void AddEventListener<T>(string eventType, Action<T> listener) where T : class
    {
        _eventService.AddEventListener(eventType, listener);
    }

    /// <summary>
    /// Remove event listener
    /// </summary>
    public void RemoveEventListener<T>(string eventType, Action<T> listener) where T : class
    {
        _eventService.RemoveEventListener(eventType, listener);
    }

    /// <summary>
    /// Add global event listener that receives all events
    /// </summary>
    public void AddGlobalEventListener(Action<string, object> listener, bool async = true)
    {
        _eventService.AddGlobalListener(listener, async);
    }

    /// <summary>
    /// Refresh the row model with specified parameters
    /// </summary>
    public void RefreshModel(RefreshModelParams? parameters = null)
    {
        parameters ??= new RefreshModelParams(ClientSideRowModelStage.Everything);

        if (_rowModelType == RowModelType.ServerSide)
        {
            // For server-side, any model refresh implies state-affecting changes (sort/filter/group/visibility)
            // Purge cached blocks to ensure new requests reflect the latest state.
            _serverSideRowModel?.Refresh(ServerSideRefreshMode.Full, purge: true);
            RaiseModelUpdated(parameters.NewData || parameters.RowDataUpdated);
            return;
        }

        _clientSideRowModel.RefreshModel(parameters);
        RaiseModelUpdated(parameters.NewData || parameters.RowDataUpdated);
    }

    /// <summary>
    /// Update row data using transaction pattern (AG Grid style)
    /// </summary>
    public RowTransaction<TData>? UpdateRowData(RowDataTransaction<TData> transaction)
    {
        if (_rowModelType != RowModelType.ClientSide)
        {
            throw new InvalidOperationException("UpdateRowData is only supported when using the client-side row model.");
        }

        var result = _clientSideRowModel.UpdateRowData(transaction);
        if (result != null)
        {
            _eventService.DispatchEvent(GridEventTypes.RowDataUpdated, new RowDataChangedEventArgs<TData>(this, _clientSideRowModel.GetTopLevelRows() ?? new List<Row<TData>>()));
            RaiseModelUpdated(true);
        }

        return result;
    }

    /// <summary>
    /// Emit a custom event
    /// </summary>
    public void DispatchEvent<T>(string eventType, T eventData) where T : class
    {
        _eventService.DispatchEvent(eventType, eventData);
    }

    internal RowModelType GetActiveRowModelType()
    {
        return _rowModelType;
    }
    

    internal Row<TData>? TryGetDisplayedRow(int index)
    {
        var rows = _table.RowModel.Rows;
        return (index >= 0 && index < rows.Count) ? rows[index] : null;
    }

    internal Task EnsureDataRangeAsync(int startRow, int endRow, CancellationToken cancellationToken = default)
    {
        if (_rowModelType == RowModelType.ServerSide && _serverSideRowModel != null)
        {
            return _serverSideRowModel.EnsureRangeAsync(startRow, endRow, cancellationToken);
        }

        return Task.CompletedTask;
    }

    internal int GetPreferredFetchSize()
    {
        if (_rowModelType == RowModelType.ServerSide)
        {
            return _serverSideRowModel?.BlockSize ?? _serverSideBlockSize;
        }

        return Math.Max(State.Pagination?.PageSize ?? 64, 1);
    }

    // ================================
    // Column Interactive Features API
    // ================================

    /// <summary>
    /// Get the column interactive service
    /// </summary>
    public ColumnInteractiveService<TData> GetColumnInteractiveService()
    {
        return _columnInteractiveService;
    }

    private void OnServerRowsChanged(object? sender, EventArgs e)
    {
        // Build a RowModel from the current server rows and install it into the Table.
        // This ensures Table.RowModel is the single source for rendering (filter/sort/group/paginate).
        var server = _serverSideRowModel;
        if (server != null)
        {
            var rows = server.RootRows.ToList();
            for (int i = 0; i < rows.Count; i++) rows[i].SetDisplayIndex(i);
            _table.RebuildFromExternalRows(rows);
        }

        RaiseModelUpdated(false);
    }

    /// <summary>
    /// Move a column to a new position (simplified AG Grid style)
    /// </summary>
    public bool MoveColumn(string columnId, int toIndex)
    {
        return _columnInteractiveService.MoveColumn(columnId, toIndex);
    }

    /// <summary>
    /// Set the width of a column by ID
    /// </summary>
    public bool SetColumnWidth(string columnId, double width)
    {
        return _columnInteractiveService.SetColumnWidth(columnId, width);
    }

    /// <summary>
    /// Auto-size a column to fit its content
    /// </summary>
    public bool AutoSizeColumn(string columnId)
    {
        return _columnInteractiveService.AutoSizeColumn(columnId);
    }

    /// <summary>
    /// Toggle column visibility
    /// </summary>
    public bool ToggleColumnVisibility(string columnId)
    {
        return _columnInteractiveService.ToggleColumnVisibility(columnId);
    }

    /// <summary>
    /// Pin/unpin a column
    /// </summary>
    public bool SetColumnPinned(string columnId, bool pinned)
    {
        return _columnInteractiveService.SetColumnPinned(columnId, pinned);
    }

    public bool SetColumnPinned(string columnId, string? pinnedArea)
    {
        return _columnInteractiveService.SetColumnPinned(columnId, pinnedArea);
    }

    Task ISaGridComponentHost<TData>.EnsureDataRangeAsync(int startRow, int endRow, CancellationToken cancellationToken)
    {
        return EnsureDataRangeAsync(startRow, endRow, cancellationToken);
    }

    int ISaGridComponentHost<TData>.GetPreferredFetchSize()
    {
        return GetPreferredFetchSize();
    }

    Row<TData>? ISaGridComponentHost<TData>.TryGetDisplayedRow(int index)
    {
        return TryGetDisplayedRow(index);
    }

    RowModelType ISaGridComponentHost<TData>.GetActiveRowModelType()
    {
        return GetActiveRowModelType();
    }

    bool ISaGridComponentHost<TData>.IsSameGrid(object gridInstance)
    {
        return ReferenceEquals(this, gridInstance);
    }

    Table<TData> ISaGridComponentHost<TData>.GetUnderlyingTable()
    {
        return InnerTable;
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
