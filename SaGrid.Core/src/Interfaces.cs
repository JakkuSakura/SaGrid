namespace SaGrid.Core;

public interface ITable<TData>
{
    TableOptions<TData> Options { get; }
    TableState<TData> State { get; }
    IReadOnlyList<Column<TData>> AllColumns { get; }
    IReadOnlyList<Column<TData>> AllLeafColumns { get; }
    IReadOnlyList<Column<TData>> VisibleLeafColumns { get; }
    IReadOnlyList<HeaderGroup<TData>> HeaderGroups { get; }
    IReadOnlyList<HeaderGroup<TData>> FooterGroups { get; }
    RowModel<TData> RowModel { get; }
    RowModel<TData> PreFilteredRowModel { get; }
    RowModel<TData> PreSortedRowModel { get; }
    RowModel<TData> PreGroupedRowModel { get; }
    RowModel<TData> PreExpandedRowModel { get; }
    RowModel<TData> PrePaginationRowModel { get; }
    
    void SetState(TableState<TData> state);
    void SetState(Updater<TableState<TData>> updater);
    Column<TData>? GetColumn(string columnId);
    Row<TData>? GetRow(string rowId);
    IReadOnlyList<Row<TData>> GetSelectedRowModel();
    void ResetColumnFilters();
    void ResetGlobalFilter();
    void ResetSorting();
    void ResetRowSelection();
    void ResetColumnOrder();
    void ResetColumnSizing();
    void ResetColumnVisibility();
    void ResetExpanded();
    void ResetGrouping();
    void ResetPagination();
    
    // Pagination methods
    int GetPageCount();
    bool GetCanPreviousPage();
    bool GetCanNextPage();
    void NextPage();
    void PreviousPage();
    void FirstPage();
    void LastPage();
    void SetPageIndex(int pageIndex);
    void SetPageSize(int pageSize);
    
    // Row selection methods
    bool GetIsAllRowsSelected();
    bool GetIsSomeRowsSelected();
    void SelectAllRows();
    void DeselectAllRows();
    void ToggleAllRowsSelected();
    void SetRowSelection(string rowId, bool selected);
    void SelectRowRange(int startIndex, int endIndex);
    int GetSelectedRowCount();
    int GetTotalRowCount();
}

public interface IColumn<TData, TValue> : IColumn<TData>
{
    new ColumnDef<TData, TValue> ColumnDef { get; }
    AccessorFn<TData, TValue>? AccessorFn { get; }
    TValue GetValue(Row<TData> row);
}

public interface IColumn<TData>
{
    string Id { get; }
    ColumnDef<TData> ColumnDef { get; }
    int Depth { get; }
    IReadOnlyList<IColumn<TData>> Columns { get; }
    IReadOnlyList<IColumn<TData>> FlatColumns { get; }
    IReadOnlyList<IColumn<TData>> LeafColumns { get; }
    bool IsVisible { get; }
    bool CanSort { get; }
    bool CanFilter { get; }
    bool CanGroup { get; }
    bool CanResize { get; }
    SortDirection? SortDirection { get; }
    int? SortIndex { get; }
    bool IsFiltered { get; }
    object? FilterValue { get; }
    bool IsGrouped { get; }
    int? GroupIndex { get; }
    double Size { get; }
    bool IsPinned { get; }
    string? PinnedPosition { get; }
    
    void ToggleSorting(SortDirection? direction = null);
    void ClearSorting();
    void SetFilterValue(object? value);
    void ToggleGrouping();
    void ToggleVisibility();
    void ResetSize();
    void SetSize(double size);
}

public interface IRow<TData>
{
    string Id { get; }
    int Index { get; }
    TData Original { get; }
    int Depth { get; }
    IReadOnlyList<IRow<TData>> SubRows { get; }
    IReadOnlyList<IRow<TData>> LeafRows { get; }
    IReadOnlyDictionary<string, ICell<TData>> Cells { get; }
    bool IsSelected { get; }
    bool IsExpanded { get; }
    bool IsGrouped { get; }
    object? GroupingValue { get; }
    
    TValue GetValue<TValue>(string columnId);
    ICell<TData> GetCell(string columnId);
    void ToggleSelected();
    void ToggleExpanded();
    IReadOnlyList<IRow<TData>> GetParentRows();
}

public interface ICell<TData, TValue> : ICell<TData>
{
    new IColumn<TData, TValue> Column { get; }
    TValue Value { get; }
    TValue RenderValue { get; }
}

public interface ICell<TData>
{
    string Id { get; }
    IColumn<TData> Column { get; }
    IRow<TData> Row { get; }
    object? Value { get; }
    object? RenderValue { get; }
    bool IsGrouped { get; }
    bool IsAggregated { get; }
    bool IsPlaceholder { get; }
}

public interface IHeader<TData>
{
    string Id { get; }
    IColumn<TData> Column { get; }
    int Index { get; }
    bool IsPlaceholder { get; }
    int ColSpan { get; }
    int RowSpan { get; }
    IReadOnlyList<IHeader<TData>> SubHeaders { get; }
    double Size { get; }
}

public interface IHeaderGroup<TData>
{
    string Id { get; }
    int Depth { get; }
    IReadOnlyList<IHeader<TData>> Headers { get; }
}

public interface ITableFeature<TData>
{
    string Name { get; }
    void Initialize(ITable<TData> table);
    TableState<TData> GetInitialState(TableOptions<TData> options);
    void OnStateChange(ITable<TData> table, TableState<TData> state);
}

public interface ICellSelectable<TData>
{
    // Cell selection capabilities
    bool IsCellSelected(int rowIndex, string columnId);
    void SelectCell(int rowIndex, string columnId, bool multiSelect = false);
    void ClearCellSelection();
    (int RowIndex, string ColumnId)? GetActiveCell();
    bool NavigateCell(CellNavigationDirection direction);
    int GetSelectedCellCount();
    string CopySelectedCells();
}

public enum CellNavigationDirection
{
    Up,
    Down,
    Left,
    Right
}

public interface IAdvancedTable<TData> : ITable<TData>
{
    // Advanced filtering capabilities
    void SetGlobalFilter(object? value);
    object? GetGlobalFilterValue();
    void ClearGlobalFilter();
    
    // Advanced search and filtering
    void SetQuickFilter(string? searchTerm);
    string? GetQuickFilter();
    
    // Advanced column operations
    void SetColumnVisibility(string columnId, bool visible);
    bool GetColumnVisibility(string columnId);
    int GetVisibleColumnCount();
    int GetTotalColumnCount();
    int GetHiddenColumnCount();
    
    // Keyboard navigation
    void HandleKeyDown(string key);
    Cell<TData>? GetCurrentCell();
    Row<TData>? GetCurrentRow();
}

public interface ISaGrid<TData> : IAdvancedTable<TData>, ICellSelectable<TData>
{
    // Export functionality
    Task<string> ExportToCsvAsync();
    Task<string> ExportToJsonAsync();
    string ExportToCsv();
    string ExportToJson();
}