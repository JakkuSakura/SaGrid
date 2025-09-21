using System.Collections.Concurrent;
using System.Reflection;

namespace SaGrid.Core;

public class Table<TData> : ITable<TData>
{
    private readonly Dictionary<string, Column<TData>> _columnMap = new();
    private readonly Dictionary<string, Row<TData>> _rowMap = new();
    private readonly List<ITableFeature<TData>> _features = new();
    private TableState<TData> _state;

    public TableOptions<TData> Options { get; }
    public TableState<TData> State => _state;
    public IReadOnlyList<Column<TData>> AllColumns { get; private set; } = Array.Empty<Column<TData>>();
    public IReadOnlyList<Column<TData>> AllLeafColumns { get; private set; } = Array.Empty<Column<TData>>();
    public IReadOnlyList<Column<TData>> VisibleLeafColumns { get; private set; } = Array.Empty<Column<TData>>();
    public IReadOnlyList<HeaderGroup<TData>> HeaderGroups { get; private set; } = Array.Empty<HeaderGroup<TData>>();
    public IReadOnlyList<HeaderGroup<TData>> FooterGroups { get; private set; } = Array.Empty<HeaderGroup<TData>>();
    public RowModel<TData> RowModel { get; private set; }
    public RowModel<TData> PreFilteredRowModel { get; private set; }
    public RowModel<TData> PreSortedRowModel { get; private set; }
    public RowModel<TData> PreGroupedRowModel { get; private set; }
    public RowModel<TData> PreExpandedRowModel { get; private set; }
    public RowModel<TData> PrePaginationRowModel { get; private set; }

    public Table(TableOptions<TData> options)
    {
        Options = options;
        _state = options.State ?? new TableState<TData>();

        InitializeColumns();
        InitializeFeatures();
        UpdateRowModel();
    }

    private void InitializeColumns()
    {
        var allColumns = new List<Column<TData>>();
        var leafColumns = new List<Column<TData>>();

        BuildColumnTree(Options.Columns, null, 0, allColumns, leafColumns);

        AllColumns = allColumns.AsReadOnly();
        AllLeafColumns = leafColumns.AsReadOnly();

        foreach (var column in allColumns)
        {
            _columnMap[column.Id] = column;
        }

        UpdateVisibleColumns();
        UpdateHeaderGroups();
    }

    private void BuildColumnTree(
        IEnumerable<ColumnDef<TData>> columnDefs,
        Column<TData>? parent,
        int depth,
        List<Column<TData>> allColumns,
        List<Column<TData>> leafColumns)
    {
        foreach (var columnDef in columnDefs)
        {
            Column<TData> column;

            // 检测是否为泛型 ColumnDef<TData, TValue>
            var columnDefType = columnDef.GetType();
            if (columnDefType.IsGenericType &&
                columnDefType.GetGenericTypeDefinition() == typeof(ColumnDef<,>))
            {
                // 通过反射创建对应的 Column<TData, TValue>
                var valueType = columnDefType.GetGenericArguments()[1]; // TValue
                var columnType = typeof(Column<,>).MakeGenericType(typeof(TData), valueType);

                try
                {
                    column = (Column<TData>)Activator.CreateInstance(columnType, this, columnDef, parent, depth)!;
                }
                catch
                {
                    // 如果泛型创建失败，回退到基础 Column<TData>
                    column = new Column<TData>(this, columnDef, parent, depth);
                }
            }
            else
            {
                // 非泛型或 GroupColumnDef<TData> 等，使用基础 Column<TData>
                column = new Column<TData>(this, columnDef, parent, depth);
            }

            allColumns.Add(column);

            if (columnDef is GroupColumnDef<TData> groupDef)
            {
                BuildColumnTree(groupDef.Columns, column, depth + 1, allColumns, leafColumns);
            }
            else
            {
                leafColumns.Add(column);
            }
        }
    }

    private void InitializeFeatures()
    {
        if (Options.EnableColumnFilters)
            _features.Add(new ColumnFilteringFeature<TData>());
        if (Options.EnableGlobalFilter)
            _features.Add(new GlobalFilteringFeature<TData>());
        if (Options.EnableSorting)
            _features.Add(new SortingFeature<TData>());
        if (Options.EnableGrouping)
            _features.Add(new GroupingFeature<TData>());
        if (Options.EnableExpanding)
            _features.Add(new ExpandingFeature<TData>());
        if (Options.EnableRowSelection)
            _features.Add(new RowSelectionFeature<TData>());
        if (Options.EnablePagination)
            _features.Add(new PaginationFeature<TData>());

        foreach (var feature in _features)
        {
            feature.Initialize(this);
        }
    }

    private void UpdateRowModel()
    {
        var coreRowModel = GetCoreRowModel();
        PreFilteredRowModel = coreRowModel;

        var filteredRowModel = Options.GetFilteredRowModel?.Invoke(this) ?? GetFilteredRowModel(coreRowModel);
        PreSortedRowModel = filteredRowModel;

        var sortedRowModel = Options.GetSortedRowModel?.Invoke(this) ?? GetSortedRowModel(filteredRowModel);
        PreGroupedRowModel = sortedRowModel;

        var groupedRowModel = Options.GetGroupedRowModel?.Invoke(this) ?? sortedRowModel;
        PreExpandedRowModel = groupedRowModel;

        var expandedRowModel = Options.GetExpandedRowModel?.Invoke(this) ?? groupedRowModel;
        PrePaginationRowModel = expandedRowModel;

        RowModel = Options.GetPaginationRowModel?.Invoke(this) ?? GetPaginatedRowModel(expandedRowModel);

        UpdateRowMap();
    }

    private RowModel<TData> GetCoreRowModel()
    {
        if (Options.GetCoreRowModel != null)
            return Options.GetCoreRowModel(Options.Data.ToArray());

        var rows = new List<Row<TData>>();
        var flatRows = new List<Row<TData>>();
        var rowsById = new Dictionary<string, Row<TData>>();

        var index = 0;
        foreach (var data in Options.Data)
        {
            var row = new Row<TData>(this, $"{index}", index, data, 0, null);
            rows.Add(row);
            flatRows.Add(row);
            rowsById[row.Id] = row;
            index++;
        }

        return new RowModel<TData>
        {
            Rows = rows.AsReadOnly(),
            FlatRows = flatRows.AsReadOnly(),
            RowsById = rowsById.AsReadOnly()
        };
    }

    private RowModel<TData> GetPaginatedRowModel(RowModel<TData> rowModel)
    {
        var pagination = _state.Pagination;
        if (pagination == null)
            return rowModel;

        var startIndex = pagination.PageIndex * pagination.PageSize;
        var endIndex = Math.Min(startIndex + pagination.PageSize, rowModel.Rows.Count);

        var paginatedRows = new List<Row<TData>>();

        for (int i = startIndex; i < endIndex; i++)
        {
            if (i < rowModel.Rows.Count)
            {
                paginatedRows.Add(rowModel.Rows[i]);
            }
        }

        return new RowModel<TData>
        {
            Rows = paginatedRows.AsReadOnly(),
            FlatRows = rowModel.FlatRows, // Keep all flat rows for reference
            RowsById = rowModel.RowsById   // Keep all rows by ID for lookups
        };
    }

    private void UpdateRowMap()
    {
        _rowMap.Clear();
        foreach (var row in RowModel.FlatRows)
        {
            _rowMap[row.Id] = row;
        }
    }

    private void UpdateVisibleColumns()
    {
        var visibilityState = State.ColumnVisibility ?? new ColumnVisibilityState();

        VisibleLeafColumns = AllLeafColumns
            .Where(column => visibilityState.GetValueOrDefault(column.Id, true))
            .ToList()
            .AsReadOnly();
    }

    private void UpdateHeaderGroups()
    {
        var headerGroups = new List<HeaderGroup<TData>>();
        var footerGroups = new List<HeaderGroup<TData>>();

        var maxDepth = AllColumns.Any() ? AllColumns.Max(c => c.Depth) : 0;

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            var headers = AllColumns
                .Where(c => c.Depth == depth && c.IsVisible)
                .Select(c => new Header<TData>(c, depth))
                .Cast<Header<TData>>()
                .ToList();

            if (headers.Any())
            {
                headerGroups.Add(new HeaderGroup<TData>($"headerGroup_{depth}", depth, headers));
                footerGroups.Insert(0, new HeaderGroup<TData>($"footerGroup_{depth}", depth, headers));
            }
        }

        HeaderGroups = headerGroups.AsReadOnly();
        FooterGroups = footerGroups.AsReadOnly();
    }

    public void SetState(TableState<TData> state)
    {
        var oldState = _state;
        _state = state;

        Options.OnStateChange?.Invoke(state);

        foreach (var feature in _features)
        {
            feature.OnStateChange(this, state);
        }

        if (!ReferenceEquals(oldState.ColumnVisibility, state.ColumnVisibility))
        {
            UpdateVisibleColumns();
            UpdateHeaderGroups();
        }

        UpdateRowModel();
    }

    public void SetState(Updater<TableState<TData>> updater)
    {
        SetState(updater(_state));
    }

    public Column<TData>? GetColumn(string columnId)
    {
        return _columnMap.GetValueOrDefault(columnId);
    }

    public Row<TData>? GetRow(string rowId)
    {
        return _rowMap.GetValueOrDefault(rowId);
    }

    public IReadOnlyList<Row<TData>> GetSelectedRowModel()
    {
        var selectionState = State.RowSelection ?? new RowSelectionState();
        return RowModel.FlatRows
            .Where(row => selectionState.GetValueOrDefault(row.Id, false))
            .ToList()
            .AsReadOnly();
    }

    public void ResetColumnFilters()
    {
        SetState(state => state with { ColumnFilters = null });
    }

    public void ResetGlobalFilter()
    {
        SetState(state => state with { GlobalFilter = null });
    }

    public void ResetSorting()
    {
        SetState(state => state with { Sorting = null });
    }

    public void ResetRowSelection()
    {
        SetState(state => state with { RowSelection = null });
    }

    public void ResetColumnOrder()
    {
        SetState(state => state with { ColumnOrder = null });
    }

    public void ResetColumnSizing()
    {
        SetState(state => state with { ColumnSizing = null });
    }

    public void ResetColumnVisibility()
    {
        SetState(state => state with { ColumnVisibility = null });
    }

    public void ResetExpanded()
    {
        SetState(state => state with { Expanded = null });
    }

    public void ResetGrouping()
    {
        SetState(state => state with { Grouping = null });
    }

    public void ResetPagination()
    {
        SetState(state => state with { Pagination = null });
    }

    // Sorting methods
    public void SetSorting(IEnumerable<ColumnSort> sorts)
    {
        var sortList = sorts.ToList();
        SetState(state => state with
        {
            Sorting = sortList.Count > 0 ? new SortingState(sortList) : null
        });
    }

    public void SetSorting(string columnId, SortDirection direction)
    {
        SetSorting(new[] { new ColumnSort(columnId, direction) });
    }

    public void AddSort(string columnId, SortDirection direction)
    {
        var currentSorting = State.Sorting?.Columns ?? new List<ColumnSort>();
        var newSorting = currentSorting.Where(s => s.Id != columnId).ToList();
        newSorting.Add(new ColumnSort(columnId, direction));
        SetSorting(newSorting);
    }

    public void RemoveSort(string columnId)
    {
        var currentSorting = State.Sorting?.Columns ?? new List<ColumnSort>();
        var newSorting = currentSorting.Where(s => s.Id != columnId).ToList();
        SetSorting(newSorting);
    }

    public void ToggleSort(string columnId)
    {
        var currentSorting = State.Sorting?.Columns ?? new List<ColumnSort>();
        var existingSort = currentSorting.FirstOrDefault(s => s.Id == columnId);

        if (existingSort == null)
        {
            AddSort(columnId, SortDirection.Ascending);
        }
        else if (existingSort.Direction == SortDirection.Ascending)
        {
            var newSorting = currentSorting.Where(s => s.Id != columnId).ToList();
            newSorting.Add(new ColumnSort(columnId, SortDirection.Descending));
            SetSorting(newSorting);
        }
        else
        {
            RemoveSort(columnId);
        }
    }


    public int GetPageCount()
    {
        var pagination = _state.Pagination;
        if (pagination == null) return 1;

        var totalRows = PrePaginationRowModel.Rows.Count;
        return (int)Math.Ceiling((double)totalRows / pagination.PageSize);
    }

    public bool GetCanPreviousPage()
    {
        var pagination = _state.Pagination;
        return pagination != null && pagination.PageIndex > 0;
    }

    public bool GetCanNextPage()
    {
        var pagination = _state.Pagination;
        if (pagination == null) return false;

        return pagination.PageIndex < GetPageCount() - 1;
    }

    public void NextPage()
    {
        var pagination = _state.Pagination;
        if (pagination == null || !GetCanNextPage()) return;

        SetState(state => state with
        {
            Pagination = pagination with { PageIndex = pagination.PageIndex + 1 }
        });
    }

    public void PreviousPage()
    {
        var pagination = _state.Pagination;
        if (pagination == null || !GetCanPreviousPage()) return;

        SetState(state => state with
        {
            Pagination = pagination with { PageIndex = pagination.PageIndex - 1 }
        });
    }

    public void FirstPage()
    {
        var pagination = _state.Pagination;
        if (pagination == null) return;

        SetState(state => state with
        {
            Pagination = pagination with { PageIndex = 0 }
        });
    }

    public void LastPage()
    {
        var pagination = _state.Pagination;
        if (pagination == null) return;

        var lastPageIndex = Math.Max(0, GetPageCount() - 1);
        SetState(state => state with
        {
            Pagination = pagination with { PageIndex = lastPageIndex }
        });
    }

    public void SetPageIndex(int pageIndex)
    {
        var pagination = _state.Pagination ?? new PaginationState();
        var maxPageIndex = Math.Max(0, GetPageCount() - 1);
        var clampedPageIndex = Math.Max(0, Math.Min(pageIndex, maxPageIndex));

        SetState(state => state with
        {
            Pagination = pagination with { PageIndex = clampedPageIndex }
        });
    }

    public void SetPageSize(int pageSize)
    {
        var pagination = _state.Pagination ?? new PaginationState();
        var normalizedPageSize = Math.Max(1, pageSize);

        SetState(state => state with
        {
            Pagination = pagination with { PageSize = normalizedPageSize }
        });
    }

    public bool GetIsAllRowsSelected()
    {
        var selection = _state.RowSelection;
        if (selection == null) return false;

        var totalRows = PrePaginationRowModel.Rows;
        return totalRows.Count > 0 && totalRows.All(row =>
            selection.Items.GetValueOrDefault(row.Id, false));
    }

    public bool GetIsSomeRowsSelected()
    {
        var selection = _state.RowSelection;
        if (selection == null) return false;

        return PrePaginationRowModel.Rows.Any(row =>
            selection.Items.GetValueOrDefault(row.Id, false));
    }

    public void SelectAllRows()
    {
        var selection = _state.RowSelection ?? new RowSelectionState();
        var newItems = new Dictionary<string, bool>(selection.Items);

        foreach (var row in PrePaginationRowModel.Rows)
        {
            newItems[row.Id] = true;
        }

        SetState(state => state with
        {
            RowSelection = new RowSelectionState(newItems)
        });
    }

    public void DeselectAllRows()
    {
        SetState(state => state with { RowSelection = null });
    }

    public void ToggleAllRowsSelected()
    {
        if (GetIsAllRowsSelected())
        {
            DeselectAllRows();
        }
        else
        {
            SelectAllRows();
        }
    }

    public void SetRowSelection(string rowId, bool selected)
    {
        var selection = _state.RowSelection ?? new RowSelectionState();
        var newItems = new Dictionary<string, bool>(selection.Items);

        if (selected)
        {
            newItems[rowId] = true;
        }
        else
        {
            newItems.Remove(rowId);
        }

        SetState(state => state with
        {
            RowSelection = newItems.Count > 0 ? new RowSelectionState(newItems) : null
        });
    }

    public void SelectRowRange(int startIndex, int endIndex)
    {
        var selection = _state.RowSelection ?? new RowSelectionState();
        var newItems = new Dictionary<string, bool>(selection.Items);

        var rows = PrePaginationRowModel.Rows;
        var actualStartIndex = Math.Max(0, Math.Min(startIndex, endIndex));
        var actualEndIndex = Math.Min(rows.Count - 1, Math.Max(startIndex, endIndex));

        for (int i = actualStartIndex; i <= actualEndIndex; i++)
        {
            if (i < rows.Count)
            {
                newItems[rows[i].Id] = true;
            }
        }

        SetState(state => state with
        {
            RowSelection = new RowSelectionState(newItems)
        });
    }

    public int GetSelectedRowCount()
    {
        var selection = _state.RowSelection;
        if (selection == null) return 0;

        return PrePaginationRowModel.Rows.Count(row =>
            selection.Items.GetValueOrDefault(row.Id, false));
    }

    public int GetTotalRowCount()
    {
        return PrePaginationRowModel.Rows.Count;
    }

    public void ToggleAllColumnsVisible(bool? visible = null)
    {
        var currentVisibility = _state.ColumnVisibility ?? new ColumnVisibilityState();
        var newVisibility = new Dictionary<string, bool>();

        // If visible is null, toggle based on current state
        bool targetVisibility;
        if (visible.HasValue)
        {
            targetVisibility = visible.Value;
        }
        else
        {
            // Check if all columns are currently visible
            var allVisible = AllLeafColumns.All(c => currentVisibility.Items.GetValueOrDefault(c.Id, true));
            targetVisibility = !allVisible;
        }

        foreach (var column in AllLeafColumns)
        {
            newVisibility[column.Id] = targetVisibility;
        }

        SetState(state => state with
        {
            ColumnVisibility = new ColumnVisibilityState(newVisibility)
        });
    }

    public void ToggleColumnVisibility(string columnId, bool? visible = null)
    {
        var currentVisibility = _state.ColumnVisibility ?? new ColumnVisibilityState();
        var newVisibility = new Dictionary<string, bool>(currentVisibility.Items);

        bool targetVisibility;
        if (visible.HasValue)
        {
            targetVisibility = visible.Value;
        }
        else
        {
            var currentValue = currentVisibility.Items.GetValueOrDefault(columnId, true);
            targetVisibility = !currentValue;
        }

        if (targetVisibility)
        {
            newVisibility.Remove(columnId); // Default is visible
        }
        else
        {
            newVisibility[columnId] = false;
        }

        SetState(state => state with
        {
            ColumnVisibility = newVisibility.Count > 0 ? new ColumnVisibilityState(newVisibility) : null
        });
    }

    public void SetColumnVisibility(string columnId, bool visible)
    {
        ToggleColumnVisibility(columnId, visible);
    }

    public void SetColumnVisibility(ColumnVisibilityState visibilityState)
    {
        SetState(state => state with { ColumnVisibility = visibilityState });
    }

    public void SetColumnVisibility(Dictionary<string, bool> visibilityMap)
    {
        var visibilityState = new ColumnVisibilityState(visibilityMap);
        SetColumnVisibility(visibilityState);
    }

    public bool GetColumnVisibility(string columnId)
    {
        var visibility = _state.ColumnVisibility;
        return visibility?.Items.GetValueOrDefault(columnId, true) ?? true;
    }

    public int GetVisibleColumnCount()
    {
        return VisibleLeafColumns.Count;
    }

    public int GetTotalColumnCount()
    {
        return AllLeafColumns.Count;
    }

    public int GetHiddenColumnCount()
    {
        return GetTotalColumnCount() - GetVisibleColumnCount();
    }

    public Row<TData>? GetRowAtIndex(int index)
    {
        if (index < 0 || index >= RowModel.Rows.Count)
            return null;
        return RowModel.Rows[index];
    }

    // Virtualization support methods for tests
    public IReadOnlyList<Row<TData>> GetVirtualRows()
    {
        return this.GetViewportRows();
    }

    public double GetEstimatedRowSize()
    {
        var viewport = this.GetViewport();
        return viewport?.ItemHeight ?? 25.0;
    }

    public double GetEstimatedTotalSize()
    {
        var rowCount = RowModel.Rows.Count;
        var estimatedRowSize = GetEstimatedRowSize();
        return rowCount * estimatedRowSize;
    }

    public double ScrollToRow(int rowIndex)
    {
        var viewport = this.GetViewport();
        if (viewport == null)
        {
            // Initialize a default viewport if none exists
            this.SetViewport(0, Math.Min(19, RowModel.Rows.Count - 1), 400, 25);
            viewport = this.GetViewport()!;
        }

        var viewportSize = viewport.ViewportHeight / viewport.ItemHeight;
        var startIndex = Math.Max(0, rowIndex - viewportSize / 2);
        var endIndex = Math.Min(RowModel.Rows.Count - 1, startIndex + viewportSize - 1);

        this.SetViewport(startIndex, endIndex, viewport.ViewportHeight, viewport.ItemHeight);

        // Return the scroll offset (estimated)
        return rowIndex * viewport.ItemHeight;
    }

    private RowModel<TData> GetFilteredRowModel(RowModel<TData> sourceRowModel)
    {
        var globalFilter = State.GlobalFilter;
        var columnFilters = State.ColumnFilters;

        // If no filters are active, return the source model
        if (globalFilter == null && (columnFilters == null || columnFilters.Filters.Count == 0))
        {
            return sourceRowModel;
        }

        Console.WriteLine($"DEBUG Filter: global={(globalFilter?.Value?.ToString() ?? "<none>")}, columnFilters={(columnFilters?.Filters.Count ?? 0)}");

        var filteredRows = sourceRowModel.Rows.Where(row =>
        {
            // Apply global filter
            if (globalFilter != null && !PassesGlobalFilter(row, globalFilter.Value))
            {
                return false;
            }

            // Apply column filters
            if (columnFilters != null)
            {
                foreach (var filter in columnFilters.Filters)
                {
                    if (!PassesColumnFilter(row, filter))
                    {
                        return false;
                    }
                }
            }

            return true;
        }).ToList();

        Console.WriteLine($"DEBUG Filter result: {filteredRows.Count} / {sourceRowModel.Rows.Count} rows");

        return new RowModel<TData>
        {
            Rows = filteredRows.AsReadOnly(),
            FlatRows = filteredRows.AsReadOnly(),
            RowsById = filteredRows.ToDictionary(r => r.Id, r => r).AsReadOnly()
        };
    }

    private bool PassesGlobalFilter(Row<TData> row, object filterValue)
    {
        var filterText = filterValue?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrEmpty(filterText)) return true;

        // Check all visible columns for the filter text
        foreach (var column in VisibleLeafColumns)
        {
            var cell = row.GetCell(column.Id);
            var cellValue = cell.Value?.ToString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(cellValue) && cellValue.Contains(filterText))
            {
                return true;
            }
        }

        return false;
    }

    private bool PassesColumnFilter(Row<TData> row, ColumnFilter filter)
    {
        var column = GetColumn(filter.Id);
        if (column == null) return true;

        var cell = row.GetCell(filter.Id);
        var cellValue = cell.Value;
        var filterValue = filter.Value;

        // Handle null values
        if (filterValue == null) return true;
        if (cellValue == null) return false;

        // Handle different filter types based on value types
        if (filterValue is string stringFilter)
        {
            // If the cell is numeric/bool and the filter parses to that type, do typed comparison
            if (cellValue is int cellInt && int.TryParse(stringFilter, out var parsedInt))
            {
                return cellInt == parsedInt;
            }
            if (cellValue is double cellDouble && double.TryParse(stringFilter, out var parsedDouble))
            {
                return Math.Abs(cellDouble - parsedDouble) < 0.000_001;
            }
            if (cellValue is float cellFloat && float.TryParse(stringFilter, out var parsedFloat))
            {
                return Math.Abs(cellFloat - parsedFloat) < 0.000_001f;
            }
            if (cellValue is decimal cellDecimal && decimal.TryParse(stringFilter, out var parsedDecimal))
            {
                return cellDecimal == parsedDecimal;
            }
            if (cellValue is bool cellBool && bool.TryParse(stringFilter, out var parsedBool))
            {
                return cellBool == parsedBool;
            }

            // Fallback to string contains (case-insensitive)
            var cellString = cellValue.ToString() ?? "";
            return cellString.IndexOf(stringFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (filterValue is bool boolFilter)
        {
            return cellValue is bool cellBool && cellBool == boolFilter;
        }

        if (filterValue is int intFilter)
        {
            return cellValue is int cellInt && cellInt == intFilter;
        }

        if (filterValue is double doubleFilter)
        {
            return cellValue is double cellDouble && Math.Abs(cellDouble - doubleFilter) < 0.001;
        }

        // Default: convert both to strings and compare
        return cellValue.ToString()?.Equals(filterValue.ToString(), StringComparison.OrdinalIgnoreCase) == true;
    }

    private RowModel<TData> GetSortedRowModel(RowModel<TData> sourceRowModel)
    {
        var sorting = State.Sorting;
        if (sorting == null || sorting.Columns.Count == 0)
        {
            return sourceRowModel;
        }

        var sortedRows = sourceRowModel.Rows.ToList();

        // Composite comparer: apply sort columns in defined order
        sortedRows.Sort((row1, row2) =>
        {
            foreach (var sortColumn in sorting.Columns)
            {
                var column = GetColumn(sortColumn.Id);
                if (column == null) continue;

                var cell1 = row1.GetCell(sortColumn.Id);
                var cell2 = row2.GetCell(sortColumn.Id);

                var value1 = cell1.Value;
                var value2 = cell2.Value;

                if (value1 == null && value2 == null) continue; // equal, continue to next key
                if (value1 == null) return 1; // nulls last
                if (value2 == null) return -1;

                int comparison;
                if (value1 is IComparable comparable1 && value2 is IComparable)
                {
                    comparison = comparable1.CompareTo(value2);
                }
                else
                {
                    comparison = string.Compare(value1.ToString(), value2.ToString(), StringComparison.Ordinal);
                }

                if (comparison != 0)
                {
                    if (sortColumn.Direction == SortDirection.Descending)
                    {
                        comparison = -comparison;
                    }
                    return comparison;
                }
            }
            return 0;
        });

        return new RowModel<TData>
        {
            Rows = sortedRows.AsReadOnly(),
            FlatRows = sortedRows.AsReadOnly(),
            RowsById = sortedRows.ToDictionary(r => r.Id, r => r).AsReadOnly()
        };
    }
}
