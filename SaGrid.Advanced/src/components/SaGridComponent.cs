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
using SaGrid.Advanced.components;
using SaGrid.Advanced.Components;
using SaGrid.Advanced.DragDrop;
using SaGrid.Core;
using SaGrid.Core.Models;
using SaGrid.SolidAvalonia;
using SolidAvalonia;
using static SolidAvalonia.Solid;

using GridControl = Avalonia.Controls.Grid;

namespace SaGrid;

public class SaGridComponent<TData> : SolidTable<TData>
{
    private readonly ISaGridComponentHost<TData> _host;
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
    private ISelectionAwareRowsControl? _virtualizedRowsControl;
    private bool _callbacksConnected;
    private int _selectionCounter;

    // Renderers
    private readonly SaGridHeaderRenderer<TData> _headerRenderer;
    private readonly SaGridBodyRenderer<TData> _bodyRenderer;
    private readonly SaGridFooterRenderer<TData> _footerRenderer;
    private DragDropManager<TData>? _dragDropManager;
    private DragValidationService<TData>? _dragValidationService;

    private (Func<Table<TData>> Getter, Action<Table<TData>> Setter) TableSignalValue =>
        TableSignal ?? throw new InvalidOperationException("Table signal not initialized.");

    internal SaGridComponent(ISaGridComponentHost<TData> host, Table<TData> table)
        : base(host.Options, table)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

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

                return _footerRenderer.CreateFooter(_host);
            });
        }

        EnsureBodyControl();
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
        var columnsSignature = string.Join(
            "|",
            _host.VisibleLeafColumns.Select(c => $"{c.Id}:{c.Size}").ToArray());

        var sortingSignature = _host.State.Sorting != null
            ? string.Join("|", _host.State.Sorting.Columns.Select(c => $"{c.Id}:{c.Direction}").ToArray())
            : string.Empty;

        var groupingSignature = string.Join("|", _host.GetGroupedColumnIds());
        var multiSort = _host.IsMultiSortEnabled() ? "1" : "0";

        return $"cols={columnsSignature};sort={sortingSignature};group={groupingSignature};multi={multiSort}";
    }

    private string ComputeBodyStructureVersion()
    {
        var columnSignature = string.Join(
            "|",
            _host.VisibleLeafColumns
                .Select(c => $"{c.Id}:{c.Size.ToString(CultureInfo.InvariantCulture)}:{c.PinnedPosition ?? "-"}")
                .ToArray());

        return columnSignature;
    }

    private string ComputeFilterVersion()
    {
        var filters = _host.State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
        return string.Join("|", filters.OrderBy(f => f.Id).Select(f => $"{f.Id}:{f.Value}").ToArray());
    }

    private void RebuildHeaderStructure()
    {
        if (_headerContainer == null)
        {
            return;
        }

        EnsureDragDropInfrastructure();

        var header = _headerRenderer.CreateHeader(
            _host,
            Table,
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
        var bodyControl = _bodyRenderer.CreateBody(_host, Table, () => _host, _selectionSignal?.Item1);
        _virtualizedRowsControl = bodyControl as ISelectionAwareRowsControl;
        _bodyHost.Content = bodyControl;
    }

    private string GetFilterText(string columnId)
    {
        var filters = _host.State.ColumnFilters?.Filters;
        var value = filters?.FirstOrDefault(f => f.Id == columnId)?.Value;
        return value?.ToString() ?? string.Empty;
    }
}
