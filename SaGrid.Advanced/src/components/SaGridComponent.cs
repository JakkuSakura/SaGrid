using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SolidAvalonia;
using SaGrid.Core;
using SaGrid.Advanced;
using SaGrid.Advanced.DragDrop;
using static SolidAvalonia.Solid;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;

using GridControl = Avalonia.Controls.Grid;

namespace SaGrid;

public class SaGridComponent<TData> : Component
{
    private readonly SaGrid<TData> _saGrid;
    private (Func<SaGrid<TData>>, Action<SaGrid<TData>>)? _gridSignal;
    private (Func<int>, Action<int>)? _selectionSignal;
    private Grid? _rootGrid;
    private Border? _rootBorder;
    private StackPanel? _headerContainer;
    private ContentControl? _bodyHost;
    private ContentControl? _footerHost;
    private readonly Dictionary<string, TextBox> _filterTextBoxes = new();
    private int _themeTick = 0;
    private string _headerStructureVersion = string.Empty;
    private string _filterVersion = string.Empty;
    private string _bodyStructureVersion = string.Empty;
    private ISelectionAwareRowsControl? _virtualizedRowsControl;

    // Renderers
    private readonly SaGridHeaderRenderer<TData> _headerRenderer;
    private readonly SaGridBodyRenderer<TData> _bodyRenderer;
    private readonly SaGridFooterRenderer<TData> _footerRenderer;
    private DragDropManager<TData>? _dragDropManager;
    private DragValidationService<TData>? _dragValidationService;

    public SaGrid<TData> Grid => _gridSignal?.Item1() ?? throw new InvalidOperationException("Grid not initialized. Call Build() first.");

    public SaGridComponent(SaGrid<TData> saGrid) : base(true)
    {
        _saGrid = saGrid;
        
        // Initialize renderers
        _headerRenderer = new SaGridHeaderRenderer<TData>(
            _ => { },
            (columnId, textBox) =>
            {
                _filterTextBoxes[columnId] = textBox;
            });
        _bodyRenderer = new SaGridBodyRenderer<TData>();
        _footerRenderer = new SaGridFooterRenderer<TData>();
        
        OnCreatedCore(); // 推送 reactive owner
        Initialize(); // 触发 Build()
    }

    protected override object Build()
    {
        // Initialize reactive signals and wire SaGrid.Advanced to trigger them
        if (_gridSignal == null)
        {
            _gridSignal = CreateSignal(_saGrid);
            _selectionSignal = CreateSignal(0);

            var (gridGetter, gridSetter) = _gridSignal.Value;
            var (selectionGetter, selectionSetter) = _selectionSignal.Value;
            var selectionCounter = 0;

            _saGrid.SetUIUpdateCallbacks(
                () =>
                {
                    _themeTick++;
                    gridSetter?.Invoke(_saGrid);
                    selectionSetter?.Invoke(++selectionCounter);
                    HandleHeaderStateChanges();
                },
                delta =>
                {
                    if (delta != null)
                    {
                        _virtualizedRowsControl?.ApplySelectionDelta(delta);
                    }

                    selectionSetter?.Invoke(++selectionCounter);
                });
        }

        // Build the root container once to avoid reparenting and keep header TextBoxes stable
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

            RebuildHeaderStructure();

            EnsureBodyControl(force: true);

            _footerHost.Content = Reactive(() =>
            {
                var currentGrid = Grid;
                var selTick = _selectionSignal?.Item1();
                
                // Don't show footer if status bar is visible (to avoid duplicate pagination)
                if (currentGrid.IsStatusBarVisible())
                {
                    return new StackPanel(); // Empty footer
                }
                
                return _footerRenderer.CreateFooter(currentGrid);
            });
        }
        EnsureBodyControl();
        return _rootBorder!;
    }

    private void EnsureDragDropInfrastructure()
    {
        if (_rootGrid == null)
        {
            return;
        }

        _dragValidationService ??= new DragValidationService<TData>(_saGrid);
        _dragDropManager ??= new DragDropManager<TData>(_saGrid.GetEventService(), _rootGrid, _dragValidationService);

        if (_dragDropManager != null)
        {
            _headerRenderer.EnableInteractivity(_dragDropManager, _saGrid.GetColumnInteractiveService());
        }
    }

    private void HandleHeaderStateChanges()
    {
        var newStructureVersion = ComputeHeaderStructureVersion();
        var newFilterVersion = ComputeFilterVersion();
        var newBodyVersion = ComputeBodyStructureVersion();

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
    }

    private string ComputeHeaderStructureVersion()
    {
        var columnsSignature = string.Join("|", _saGrid.VisibleLeafColumns.Select(c => $"{c.Id}:{c.Size}").ToArray());
        var sortingSignature = _saGrid.State.Sorting != null
            ? string.Join("|", _saGrid.State.Sorting.Columns.Select(c => $"{c.Id}:{c.Direction}").ToArray())
            : string.Empty;
        var groupingSignature = string.Join("|", _saGrid.GetGroupedColumnIds());
        var multiSort = _saGrid.IsMultiSortEnabled() ? "1" : "0";
        return $"cols={columnsSignature};sort={sortingSignature};group={groupingSignature};multi={multiSort}";
    }

    private string ComputeBodyStructureVersion()
    {
        var columnSignature = string.Join(
            "|",
            _saGrid.VisibleLeafColumns
                .Select(c => $"{c.Id}:{c.Size.ToString(CultureInfo.InvariantCulture)}:{c.PinnedPosition ?? "-"}")
                .ToArray());

        return columnSignature;
    }

    private string ComputeFilterVersion()
    {
        var filters = _saGrid.State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        return string.Join("|", filters.OrderBy(f => f.Id).Select(f => $"{f.Id}:{f.Value}").ToArray());
    }

    private void RebuildHeaderStructure()
    {
        if (_headerContainer == null)
        {
            return;
        }

        EnsureDragDropInfrastructure();

        var header = _headerRenderer.CreateHeader(_saGrid, null, null);
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
        if (_gridSignal == null || _bodyHost == null)
        {
            return;
        }

        var bodyVersion = ComputeBodyStructureVersion();
        if (!force && string.Equals(bodyVersion, _bodyStructureVersion, StringComparison.Ordinal))
        {
            return;
        }

        _bodyStructureVersion = bodyVersion;
        var bodyControl = _bodyRenderer.CreateBody(Grid, () => Grid, _selectionSignal?.Item1);
        _virtualizedRowsControl = bodyControl as ISelectionAwareRowsControl;
        _bodyHost.Content = bodyControl;
    }

    private string GetFilterText(string columnId)
    {
        var filters = _saGrid.State.ColumnFilters?.Filters;
        var value = filters?.FirstOrDefault(f => f.Id == columnId)?.Value;
        return value?.ToString() ?? string.Empty;
    }

}
