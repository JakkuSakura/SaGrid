using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SaGrid.Advanced.DragDrop;
using SaGrid.Avalonia;
using SaGrid.Core;
using SaGrid.Core.Models;
using SaGrid.SolidAvalonia;
using SolidAvalonia;
using static SolidAvalonia.Solid;

using GridControl = Avalonia.Controls.Grid;

namespace SaGrid.Advanced.Components;

public class SaGridComponent<TData> : SolidTable<TData>
{
    private readonly ISaGridComponentHost<TData> _host;
    private readonly TableOptions<TData> _options;
    private (Func<int>, Action<int>)? _selectionSignal;
    private Grid? _rootGrid;
    private Border? _rootBorder;
    private StackPanel? _headerContainer;
    private ContentControl? _bodyHost;
    private ContentControl? _footerHost;
    private readonly Dictionary<string, TextBox> _filterTextBoxes = new();
    private int _themeTick;
    private string _headerStructureVersion = string.Empty;
    private string _filterVersion = string.Empty;
    private string _bodyStructureVersion = string.Empty;
    private string _columnWidthVersion = string.Empty;
    private ISelectionAwareRowsControl? _virtualizedRowsControl;
    private bool _callbacksConnected;
    private int _selectionCounter;
    private TableColumnLayoutManager<TData>? _layoutManager;

    // Renderers
    private readonly SaGridHeaderRenderer<TData> _headerRenderer;
    private readonly SaGridBodyRenderer<TData> _bodyRenderer;
    private readonly SaGridFooterRenderer<TData> _footerRenderer;
    private DragDropManager<TData>? _dragDropManager;
    private DragValidationService<TData>? _dragValidationService;

    private (Func<Table<TData>> Getter, Action<Table<TData>> Setter) TableSignalValue =>
        TableSignal ?? throw new InvalidOperationException("Table signal not initialized.");

    #region Table facade

    internal TableOptions<TData> GetOptions() => _options;

    internal TableState<TData> GetState() => Table.State;

    internal IReadOnlyList<Column<TData>> GetAllColumns() => Table.AllColumns;

    internal IReadOnlyList<Column<TData>> GetAllLeafColumns() => Table.AllLeafColumns;

    internal IReadOnlyList<Column<TData>> GetVisibleLeafColumns() => Table.VisibleLeafColumns;

    internal IReadOnlyList<HeaderGroup<TData>> GetHeaderGroups() => Table.HeaderGroups;

    internal IReadOnlyList<HeaderGroup<TData>> GetFooterGroups() => Table.FooterGroups;

    internal RowModel<TData> GetRowModel() => Table.RowModel;

    internal RowModel<TData> GetPreFilteredRowModel() => Table.PreFilteredRowModel;

    internal RowModel<TData> GetPreSortedRowModel() => Table.PreSortedRowModel;

    internal RowModel<TData> GetPreGroupedRowModel() => Table.PreGroupedRowModel;

    internal RowModel<TData> GetPreExpandedRowModel() => Table.PreExpandedRowModel;

    internal RowModel<TData> GetPrePaginationRowModel() => Table.PrePaginationRowModel;

    internal void ApplyState(TableState<TData> state, bool updateRowModel) => Table.SetState(state, updateRowModel);

    internal void ApplyState(Updater<TableState<TData>> updater, bool updateRowModel) => Table.SetState(updater, updateRowModel);

    internal Column<TData>? FindColumn(string columnId) => Table.GetColumn(columnId);

    internal Row<TData>? FindRow(string rowId) => Table.GetRow(rowId);

    internal IReadOnlyList<Row<TData>> GetSelectedRows() => Table.GetSelectedRowModel();

    internal void ResetColumnFilters() => Table.ResetColumnFilters();

    internal void ResetGlobalFilter() => Table.ResetGlobalFilter();

    internal void ResetSorting() => Table.ResetSorting();

    internal void ResetRowSelection() => Table.ResetRowSelection();

    internal void ResetColumnOrder() => Table.ResetColumnOrder();

    internal void ResetColumnSizing() => Table.ResetColumnSizing();

    internal void ResetColumnVisibility() => Table.ResetColumnVisibility();

    internal void ResetExpanded() => Table.ResetExpanded();

    internal void ResetGrouping() => Table.ResetGrouping();

    internal void ResetPagination() => Table.ResetPagination();

    internal int GetPageCount() => Table.GetPageCount();

    internal bool CanGoPreviousPage() => Table.GetCanPreviousPage();

    internal bool CanGoNextPage() => Table.GetCanNextPage();

    internal void GoToFirstPage() => Table.FirstPage();

    internal void GoToLastPage() => Table.LastPage();

    internal bool AreAllRowsSelected() => Table.GetIsAllRowsSelected();

    internal bool AreSomeRowsSelected() => Table.GetIsSomeRowsSelected();

    internal void SelectAllRows() => Table.SelectAllRows();

    internal void DeselectAllRows() => Table.DeselectAllRows();

    internal void ToggleAllRowsSelected() => Table.ToggleAllRowsSelected();

    internal void SetRowSelection(string rowId, bool selected) => Table.SetRowSelection(rowId, selected);

    internal void SelectRowRange(int startIndex, int endIndex) => Table.SelectRowRange(startIndex, endIndex);

    internal int GetSelectedRowCount() => Table.GetSelectedRowCount();

    internal int GetTotalRowCount() => Table.GetTotalRowCount();

    internal void SetColumnVisibility(string columnId, bool visible) => Table.SetColumnVisibility(columnId, visible);

    internal bool GetColumnVisibility(string columnId)
    {
        var visibility = Table.State.ColumnVisibility;
        return visibility?.Items.GetValueOrDefault(columnId, true) ?? true;
    }

    internal int GetVisibleColumnCount() => Table.VisibleLeafColumns.Count;

    internal int GetTotalColumnCount() => Table.AllLeafColumns.Count;

    internal int GetHiddenColumnCount() => GetTotalColumnCount() - GetVisibleColumnCount();

    internal void SetPageIndex(int pageIndex) => Table.SetPageIndex(pageIndex);

    internal void SetPageSize(int pageSize) => Table.SetPageSize(pageSize);

    internal void GoToNextPage() => Table.NextPage();

    internal void GoToPreviousPage() => Table.PreviousPage();

    internal void SetSorting(IEnumerable<ColumnSort> sorts) => Table.SetSorting(sorts);

    internal void SetSorting(string columnId, SortDirection direction) => Table.SetSorting(columnId, direction);

    internal void ToggleSort(string columnId) => Table.ToggleSort(columnId);

    internal Table<TData> GetUnderlyingTable() => Table;

    internal void SetGlobalFilterValue(object? value) => Table.SetGlobalFilter(value);

    internal object? GetGlobalFilterValue() => Table.GetGlobalFilterValue();

    internal void ClearGlobalFilterValue() => Table.ClearGlobalFilter();

    #endregion

    internal SaGridComponent(ISaGridComponentHost<TData> host, TableOptions<TData> options, Table<TData> table)
        : base(options, table)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _options = options;

        _headerRenderer = new SaGridHeaderRenderer<TData>(
            _ => { },
            (columnId, textBox) => { _filterTextBoxes[columnId] = textBox; });
        _bodyRenderer = new SaGridBodyRenderer<TData>();
        _footerRenderer = new SaGridFooterRenderer<TData>();
    }

    protected override Control BuildContent(Table<TData> currentTable)
    {
        EnsureSelectionSignal();
        EnsureCallbacks();

        if (_rootGrid == null)
        {
            _rootGrid = new GridControl
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto")
            };

            EnsureDragDropInfrastructure();

            _headerContainer = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            GridControl.SetRow(_headerContainer, 0);
            _rootGrid.Children.Add(_headerContainer);

            _bodyHost = new ContentControl();
            GridControl.SetRow(_bodyHost, 1);
            _rootGrid.Children.Add(_bodyHost);

            _footerHost = new ContentControl();
            GridControl.SetRow(_footerHost, 2);
            _rootGrid.Children.Add(_footerHost);

            _rootBorder = new Border()
                .BorderThickness(1)
                .BorderBrush(Brushes.Gray)
                .Child(_rootGrid);

            _rootBorder.AttachedToVisualTree += RootBorderOnAttached;
            _rootBorder.DetachedFromVisualTree += RootBorderOnDetached;

            RebuildHeaderStructure();
            EnsureBodyControl(force: true);

            _footerHost.Content = Reactive(() =>
            {
                // Ensure dependency on selection ticks so footer reacts to state changes
                var selTick = _selectionSignal?.Item1();
                _ = selTick;

                if (_host.IsStatusBarVisible())
                {
                    return new StackPanel();
                }

                return _footerRenderer.CreateFooter(_host, Table);
            });
        }

        EnsureBodyControl();
        ReportAvailableWidth();
        return _rootBorder!;
    }

    protected override Control WrapContent(Table<TData> table, Control content)
    {
        return content;
    }

    private (Func<int> Getter, Action<int> Setter) EnsureSelectionSignal()
    {
        if (_selectionSignal == null)
        {
            _selectionSignal = CreateSignal(0);
        }

        return _selectionSignal.Value;
    }

    private void EnsureCallbacks()
    {
        if (_callbacksConnected || TableSignal == null || _selectionSignal == null)
        {
            return;
        }

        var tableSignal = TableSignalValue;
        var (_, selectionSetter) = _selectionSignal.Value;

        _host.SetUIUpdateCallbacks(
            () =>
            {
                _themeTick++;
                tableSignal.Setter(Table);
                selectionSetter(++_selectionCounter);
                HandleHeaderStateChanges();
            },
            delta =>
            {
                if (delta != null)
                {
                    _virtualizedRowsControl?.ApplySelectionDelta(delta);
                }

                selectionSetter(++_selectionCounter);
            });

        _callbacksConnected = true;
    }

    private void RootBorderOnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Border border)
        {
            border.PropertyChanged += RootBorderOnPropertyChanged;
            ReportAvailableWidth();
        }
    }

    private void RootBorderOnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Border border)
        {
            border.PropertyChanged -= RootBorderOnPropertyChanged;
        }
    }

    private void RootBorderOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            ReportAvailableWidth();
        }
    }

    private void ReportAvailableWidth()
    {
        if (_rootBorder == null)
        {
            return;
        }

        var bounds = _rootBorder.Bounds;
        var width = bounds.Width;
        if (double.IsNaN(width) || width <= 0)
        {
            return;
        }

        var borderWidth = _rootBorder.BorderThickness.Left + _rootBorder.BorderThickness.Right;
        var paddingWidth = _rootBorder.Padding.Left + _rootBorder.Padding.Right;
        var effectiveWidth = width - borderWidth - paddingWidth;
        if (effectiveWidth <= 0)
        {
            return;
        }

        if (_layoutManager != null)
        {
            _layoutManager.ReportViewportWidth(effectiveWidth);
            _layoutManager.Refresh();
        }
    }

    private void EnsureDragDropInfrastructure()
    {
        if (_rootGrid == null)
        {
            return;
        }

        _dragValidationService ??= new DragValidationService<TData>(Table);
        _dragDropManager ??= new DragDropManager<TData>(_host.GetEventService(), _rootGrid, _dragValidationService);

        if (_dragDropManager != null)
        {
            _headerRenderer.EnableInteractivity(_dragDropManager, _host.GetColumnInteractiveService());
        }
    }

    private void HandleHeaderStateChanges()
    {
        var newStructureVersion = ComputeHeaderStructureVersion();
        var newFilterVersion = ComputeFilterVersion();
        var newBodyVersion = ComputeBodyStructureVersion();
        var newWidthVersion = ComputeColumnWidthVersion();

        if (!string.Equals(newStructureVersion, _headerStructureVersion, StringComparison.Ordinal))
        {
            _headerStructureVersion = newStructureVersion;
            _filterVersion = newFilterVersion;
            RebuildHeaderStructure();
        }
        else if (!string.Equals(newFilterVersion, _filterVersion, StringComparison.Ordinal))
        {
            _filterVersion = newFilterVersion;
            RefreshFilterTextBoxes();
        }

        if (!string.Equals(newBodyVersion, _bodyStructureVersion, StringComparison.Ordinal))
        {
            EnsureBodyControl(force: true);
        }

        if (!string.Equals(newWidthVersion, _columnWidthVersion, StringComparison.Ordinal))
        {
            _columnWidthVersion = newWidthVersion;
            _layoutManager?.Refresh();
        }
    }

    private string ComputeHeaderStructureVersion()
    {
        var table = Table;
        var columnsSignature = string.Join(
            "|",
            table.VisibleLeafColumns.Select(c => c.Id).ToArray());

        var sortingSignature = table.State.Sorting != null
            ? string.Join("|", table.State.Sorting.Columns.Select(c => $"{c.Id}:{c.Direction}").ToArray())
            : string.Empty;

        var groupingSignature = string.Join("|", _host.GetGroupedColumnIds());
        var multiSort = _host.IsMultiSortEnabled() ? "1" : "0";

        return $"cols={columnsSignature};sort={sortingSignature};group={groupingSignature};multi={multiSort}";
    }

    private string ComputeBodyStructureVersion()
    {
        var table = Table;
        var columnSignature = string.Join(
            "|",
            table.VisibleLeafColumns
                .Select(c => $"{c.Id}:{c.PinnedPosition ?? "-"}")
                .ToArray());

        return columnSignature;
    }

    private string ComputeColumnWidthVersion()
    {
        var table = Table;
        return string.Join(
            "|",
            table.VisibleLeafColumns
                .Select(c => $"{c.Id}:{c.Size.ToString(CultureInfo.InvariantCulture)}")
                .ToArray());
    }

    private string ComputeFilterVersion()
    {
        var filters = Table.State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        return string.Join("|", filters.OrderBy(f => f.Id).Select(f => $"{f.Id}:{f.Value}").ToArray());
    }

    private void RebuildHeaderStructure()
    {
        if (_headerContainer == null)
        {
            return;
        }

        EnsureDragDropInfrastructure();

        _layoutManager ??= new TableColumnLayoutManager<TData>(Table);
        _layoutManager.Refresh();

        var header = _headerRenderer.CreateHeader(
            _host,
            Table,
            _layoutManager,
            () => _host,
            _selectionSignal?.Item1);

        if (header is Control hdrCtrl)
        {
            hdrCtrl.SetValue(Panel.ZIndexProperty, 1);
            if (hdrCtrl is Panel hdrPanel)
            {
                hdrPanel.Background = Brushes.White;
            }
        }

        _headerContainer.Children.Clear();
        _headerContainer.Children.Add(header);

        _filterTextBoxes.Clear();
        foreach (var textBox in header.GetVisualDescendants().OfType<TextBox>())
        {
            if (textBox.Tag is string columnId)
            {
                _filterTextBoxes[columnId] = textBox;
            }
        }

        _headerStructureVersion = ComputeHeaderStructureVersion();
        _filterVersion = ComputeFilterVersion();
        _columnWidthVersion = ComputeColumnWidthVersion();
        RefreshFilterTextBoxes(force: true);
    }

    private void RefreshFilterTextBoxes(bool force = false)
    {
        foreach (var kvp in _filterTextBoxes.ToList())
        {
            var columnId = kvp.Key;
            var textBox = kvp.Value;
            var expected = GetFilterText(columnId);

            if (!force && textBox.IsFocused)
            {
                continue;
            }

            if (!string.Equals(textBox.Text, expected, StringComparison.Ordinal))
            {
                textBox.Text = expected;
            }
        }
    }

    private void EnsureBodyControl(bool force = false)
    {
        if (TableSignal == null || _bodyHost == null)
        {
            return;
        }

        var bodyVersion = ComputeBodyStructureVersion();
        if (!force && string.Equals(bodyVersion, _bodyStructureVersion, StringComparison.Ordinal))
        {
            return;
        }

        _bodyStructureVersion = bodyVersion;
        _layoutManager ??= new TableColumnLayoutManager<TData>(Table);

        var bodyControl = _bodyRenderer.CreateBody(_host, Table, _layoutManager, () => _host, _selectionSignal?.Item1);
        _virtualizedRowsControl = bodyControl as ISelectionAwareRowsControl;
        _bodyHost.Content = bodyControl;
        _columnWidthVersion = ComputeColumnWidthVersion();
    }

    private string GetFilterText(string columnId)
    {
        var filters = Table.State.ColumnFilters?.Filters;
        var value = filters?.FirstOrDefault(f => f.Id == columnId)?.Value;
        return value?.ToString() ?? string.Empty;
    }
}
