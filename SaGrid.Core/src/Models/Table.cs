namespace SaGrid.Core.Models;

public class Table<TData> : ITable<TData>
{
    private readonly Dictionary<string, Column<TData>> _columnMap = new();
    private readonly Dictionary<string, Row<TData>> _rowMap = new();
    private readonly List<ITableFeature<TData>> _features = new();
    private readonly Dictionary<string, int> _initialColumnOrder = new();
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
            _initialColumnOrder[column.Id] = _initialColumnOrder.Count;

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
        var filteredRowModel = Options.GetFilteredRowModel?.Invoke(this) ?? GetFilteredRowModel(coreRowModel);
        PreFilteredRowModel = filteredRowModel;

        var sortedRowModel = Options.GetSortedRowModel?.Invoke(this) ?? GetSortedRowModel(filteredRowModel);
        PreSortedRowModel = sortedRowModel;

        var groupedRowModel = Options.GetGroupedRowModel?.Invoke(this) ?? GetGroupedRowModel(sortedRowModel);
        PreGroupedRowModel = groupedRowModel;

        var expandedRowModel = Options.GetExpandedRowModel?.Invoke(this) ?? GetExpandedRowModel(groupedRowModel);
        PreExpandedRowModel = expandedRowModel;

        AssignDisplayIndices(expandedRowModel);

        var paginatedRowModel = Options.GetPaginationRowModel?.Invoke(this) ?? GetPaginatedRowModel(expandedRowModel);
        PrePaginationRowModel = expandedRowModel;

        RowModel = paginatedRowModel;
        AssignDisplayIndices(RowModel);

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
            row.SetDisplayIndex(index);
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
        UpdateRowMap(RowModel);
    }

    private void UpdateRowMap(RowModel<TData> model)
    {
        _rowMap.Clear();
        foreach (var row in model.FlatRows)
        {
            _rowMap[row.Id] = row;
        }
    }

    private bool IsDebugFilteringEnabled()
    {
        if (Options.Meta != null && Options.Meta.TryGetValue("debugFiltering", out var v))
        {
            return v switch
            {
                bool b => b,
                string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        return false;
    }

    private static void AssignDisplayIndices(RowModel<TData> model)
    {
        var flatRows = model.FlatRows;
        if (flatRows == null || flatRows.Count == 0)
        {
            return;
        }

        for (var i = 0; i < flatRows.Count; i++)
        {
            flatRows[i].SetDisplayIndex(i);
        }
    }

    internal void ReplaceFinalRowModel(RowModel<TData> rowModel)
    {
        PrePaginationRowModel = rowModel;
        RowModel = rowModel;
        UpdateRowMap(rowModel);
    }

    // Rebuild full pipeline from externally supplied core rows (e.g., server-side data source)
    public void RebuildFromExternalRows(IReadOnlyList<Row<TData>> coreRows)
    {
        // Build a core model from provided rows
        var coreModel = new RowModel<TData>
        {
            Rows = coreRows.ToList().AsReadOnly(),
            FlatRows = coreRows.ToList().AsReadOnly(),
            RowsById = coreRows.ToDictionary(r => r.Id, r => r).AsReadOnly()
        };

        // Apply pipeline stages using existing stage methods
        var filteredRowModel = Options.GetFilteredRowModel?.Invoke(this) ?? GetFilteredRowModel(coreModel);
        PreFilteredRowModel = filteredRowModel;

        var sortedRowModel = Options.GetSortedRowModel?.Invoke(this) ?? GetSortedRowModel(filteredRowModel);
        PreSortedRowModel = sortedRowModel;

        var groupedRowModel = Options.GetGroupedRowModel?.Invoke(this) ?? GetGroupedRowModel(sortedRowModel);
        PreGroupedRowModel = groupedRowModel;

        var expandedRowModel = Options.GetExpandedRowModel?.Invoke(this) ?? GetExpandedRowModel(groupedRowModel);
        PreExpandedRowModel = expandedRowModel;

        AssignDisplayIndices(expandedRowModel);

        var paginatedRowModel = Options.GetPaginationRowModel?.Invoke(this) ?? GetPaginatedRowModel(expandedRowModel);
        PrePaginationRowModel = expandedRowModel;

        RowModel = paginatedRowModel;
        AssignDisplayIndices(RowModel);

        UpdateRowMap();
    }

    private void UpdateVisibleColumns()
    {
        var visibilityState = State.ColumnVisibility ?? new ColumnVisibilityState();

        var visibleColumns = AllLeafColumns
            .Where(column => visibilityState.GetValueOrDefault(column.Id, true));

        VisibleLeafColumns = OrderColumnsByState(visibleColumns);
    }

    private void UpdateHeaderGroups()
    {
        var headerGroups = new List<HeaderGroup<TData>>();
        var footerGroups = new List<HeaderGroup<TData>>();

        var maxDepth = AllColumns.Any() ? AllColumns.Max(c => c.Depth) : 0;

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            var orderedColumns = OrderColumnsByState(
                AllColumns.Where(c => c.Depth == depth && c.IsVisible));

            var headers = orderedColumns
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

    private IReadOnlyList<Column<TData>> OrderColumnsByState(IEnumerable<Column<TData>> columns)
    {
        var columnList = columns.ToList();
        if (columnList.Count == 0)
        {
            return columnList.AsReadOnly();
        }

        var columnOrder = _state.ColumnOrder?.Order;
        if (columnOrder == null || columnOrder.Count == 0)
        {
            return columnList
                .OrderBy(c => _initialColumnOrder.GetValueOrDefault(c.Id))
                .ToList()
                .AsReadOnly();
        }

        var orderLookup = columnOrder
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return columnList
            .Select((column, originalIndex) => new
            {
                Column = column,
                OrderIndex = TryGetStateOrderIndex(column, orderLookup),
                FallbackIndex = _initialColumnOrder.GetValueOrDefault(column.Id, originalIndex),
                OriginalIndex = originalIndex
            })
            .OrderBy(x => x.OrderIndex.HasValue ? 0 : 1)
            .ThenBy(x => x.OrderIndex ?? int.MaxValue)
            .ThenBy(x => x.FallbackIndex)
            .ThenBy(x => x.OriginalIndex)
            .Select(x => x.Column)
            .ToList()
            .AsReadOnly();
    }

    private int? TryGetStateOrderIndex(Column<TData> column, Dictionary<string, int> orderLookup)
    {
        if (orderLookup.TryGetValue(column.Id, out var index))
        {
            return index;
        }

        var leafIndexes = column.LeafColumns
            .Where(leaf => leaf.IsVisible)
            .Select(leaf => orderLookup.TryGetValue(leaf.Id, out var leafIndex) ? (int?)leafIndex : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (leafIndexes.Count == 0)
        {
            return null;
        }

        return leafIndexes.Min();
    }

    public void SetState(TableState<TData> state, bool updateRowModel = true)
    {
        var oldState = _state;
        _state = state;

        Options.OnStateChange?.Invoke(state);

        foreach (var feature in _features)
        {
            feature.OnStateChange(this, state);
        }

        var visibilityChanged = !ReferenceEquals(oldState.ColumnVisibility, state.ColumnVisibility);
        var orderChanged = !ReferenceEquals(oldState.ColumnOrder, state.ColumnOrder);

        if (visibilityChanged || orderChanged)
        {
            UpdateVisibleColumns();
            UpdateHeaderGroups();
        }

        if (updateRowModel)
        {
            UpdateRowModel();
        }
    }

    public void SetState(Updater<TableState<TData>> updater, bool updateRowModel = true)
    {
        SetState(updater(_state), updateRowModel);
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
        // Delegate to shared engine for consistency across client/server
        return BaseRowModel<TData>.ApplyFilter(this, sourceRowModel.Rows);
    }

    private RowModel<TData> GetSortedRowModel(RowModel<TData> sourceRowModel)
    {
        // Delegate to shared engine for consistency across client/server
        return BaseRowModel<TData>.ApplySort(this, sourceRowModel.Rows);
    }

    private RowModel<TData> GetGroupedRowModel(RowModel<TData> sourceRowModel)
    {
        var grouping = State.Grouping;
        if (grouping == null || grouping.Groups.Count == 0)
        {
            return sourceRowModel;
        }

        var groups = grouping.Groups.ToList();

        var flatRows = new List<Row<TData>>();
        var rowsById = new Dictionary<string, Row<TData>>();

        int nextSyntheticIndex = sourceRowModel.Rows.Count; // start after data rows

        List<Row<TData>> BuildLevel(IEnumerable<Row<TData>> inputRows, int depth, Row<TData>? parent, int level)
        {
            var columnId = groups[level];
            var lookup = inputRows.GroupBy(r => r.GetCell(columnId).Value);
            var result = new List<Row<TData>>();

            foreach (var grp in lookup)
            {
                var preset = new Dictionary<string, object?> { { columnId, grp.Key } };
                var groupId = $"g_{level}_{columnId}_{grp.Key}_{result.Count}";
                var groupRow = new Row<TData>(this, groupId, nextSyntheticIndex++, default!, depth, parent, preset, isGroupRow: true);
                groupRow.SetGroupInfo(columnId, grp.Key);

                rowsById[groupId] = groupRow;
                flatRows.Add(groupRow);
                result.Add(groupRow);

                if (level + 1 < groups.Count)
                {
                    var child = BuildLevel(grp, depth + 1, groupRow, level + 1);
                    // child rows are attached to parent via Row constructor
                }
                else
                {
                    foreach (var row in grp)
                    {
                        // attach leaf row under the group
                        var attach = new Row<TData>(this, row.Id, row.Index, row.Original, depth + 1, groupRow);
                        rowsById[row.Id] = attach;
                        flatRows.Add(attach);
                    }
                }
            }

            return result;
        }

        var top = BuildLevel(sourceRowModel.Rows, 0, null, 0);
        return new RowModel<TData>
        {
            Rows = top.AsReadOnly(),
            FlatRows = flatRows.AsReadOnly(),
            RowsById = rowsById.AsReadOnly()
        };
    }

    private RowModel<TData> GetExpandedRowModel(RowModel<TData> grouped)
    {
        // If there is no grouping or expanded state, return grouped as-is
        var grouping = State.Grouping;
        if (grouping == null || grouping.Groups.Count == 0)
        {
            return grouped;
        }

        var expanded = State.Expanded ?? new ExpandedState();
        var visible = new List<Row<TData>>();

        void Walk(Row<TData> row)
        {
            visible.Add(row);
            // Only traverse children when expanded
            if (row.SubRows.Count > 0 && expanded.Items.GetValueOrDefault(row.Id, false))
            {
                foreach (var child in row.SubRows.OfType<Row<TData>>())
                {
                    Walk(child);
                }
            }
        }

        foreach (var top in grouped.Rows)
        {
            Walk(top);
        }

        return new RowModel<TData>
        {
            Rows = visible.AsReadOnly(),
            FlatRows = grouped.FlatRows,
            RowsById = grouped.RowsById
        };
    }
}
