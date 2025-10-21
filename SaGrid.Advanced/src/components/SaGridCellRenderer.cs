using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Avalonia.VisualTree;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Modules.Editing;
using SaGrid.Advanced.Utils;
using SaGrid.Core;
using SaGrid.Core.Models;
using SolidAvalonia;
using static SolidAvalonia.Solid;

namespace SaGrid.Advanced.Components;

internal interface IReusableCellVisual<TData>
{
    void Update(Row<TData> row, int displayIndex, bool force = false);
}

internal class SaGridCellRenderer<TData>
{
    public Control CreateCell(ISaGridComponentHost<TData> host, Table<TData> table, Row<TData> row, Column<TData> column, int displayIndex)
    {
        var firstColumnId = table.VisibleLeafColumns.FirstOrDefault()?.Id;
        var indent = column.Id == firstColumnId ? row.Depth * 16 : 0;

        var displayFactory = new Func<Control>(() => new TextBlock()
            .Text(SaGridContentHelper<TData>.GetCellContent(row, column))
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Margin(new Thickness(8 + indent, 0, 0, 0)));

        var border = new Border()
            .BorderThickness(0, 0, 1, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(GetCellBackground(false, false, displayIndex))
            .Height(30);

        border.Child = new EditingCellPresenter<TData>(host, row, column, displayFactory);
        return border;
    }

    public Control CreateReactiveCell(
        ISaGridComponentHost<TData> host,
        Table<TData> table,
        Row<TData> row,
        Column<TData> column,
        int displayIndex,
        Func<ISaGridComponentHost<TData>> hostSignalGetter,
        Func<int>? selectionSignalGetter = null)
    {
        var hostSignal = hostSignalGetter ?? (() => host);
        return new ReactiveCellComponent<TData>(host, table, row, column, displayIndex, hostSignal, selectionSignalGetter, this);
    }

    internal IBrush GetCellBackground(bool isSelected, bool isActiveCell, int rowIndex)
    {
        if (isActiveCell)
        {
            return new SolidColorBrush(Colors.Orange);
        }

        if (isSelected)
        {
            return new SolidColorBrush(Colors.LightBlue);
        }

        return rowIndex % 2 == 0
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(248, 248, 248));
    }
}

internal sealed class ReactiveCellComponent<TData> : Component, IReusableCellVisual<TData>
{
    private readonly ISaGridComponentHost<TData> _host;
    private readonly Row<TData> _row;
    private readonly Column<TData> _column;
    private int _displayIndex;
    private readonly Func<ISaGridComponentHost<TData>> _hostSignalGetter;
    private readonly Func<int>? _selectionSignalGetter;
    private readonly SaGridCellRenderer<TData> _renderer;
    private readonly Table<TData> _table;

    private Border? _border;
    private EditingCellPresenter<TData>? _presenter;
    private double _indent;

    public ReactiveCellComponent(
        ISaGridComponentHost<TData> host,
        Table<TData> table,
        Row<TData> row,
        Column<TData> column,
        int displayIndex,
        Func<ISaGridComponentHost<TData>> hostSignalGetter,
        Func<int>? selectionSignalGetter,
        SaGridCellRenderer<TData> renderer) : base(true)
    {
        _host = host;
        _table = table;
        _row = row;
        _column = column;
        _displayIndex = displayIndex;
        _hostSignalGetter = hostSignalGetter;
        _selectionSignalGetter = selectionSignalGetter;
        _renderer = renderer;
        _ = _selectionSignalGetter;

        OnCreatedCore();
        Initialize();
    }

    protected override object Build()
    {
        _border = new Border()
            .BorderThickness(0, 0, 1, 1)
            .BorderBrush(Brushes.LightGray)
            .Height(30);

        Func<Control> displayFactory = () => new TextBlock()
            .Text(SaGridContentHelper<TData>.GetCellContent(_row, _column))
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Margin(new Thickness(8 + _indent, 0, 0, 0));

        _presenter = new EditingCellPresenter<TData>(_host, _row, _column, displayFactory);
        _border.Child = _presenter;

        _border.PointerPressed += OnPointerPressed;
        _border.Background = _renderer.GetCellBackground(false, false, _displayIndex);

        CreateEffect(UpdateVisualState);

        return _border;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _host.SelectCell(_displayIndex, _column.Id, isCtrlPressed);
        e.Handled = true;
    }

    private void UpdateVisualState()
    {
        var currentHost = _hostSignalGetter();

        if (_border == null)
        {
            return;
        }

        var isSelected = currentHost?.IsCellSelected(_displayIndex, _column.Id) ?? false;
        var activeCell = currentHost?.GetActiveCell();
        var isActiveCell = activeCell?.RowIndex == _displayIndex && activeCell?.ColumnId == _column.Id;

        var firstColumnId = _table.VisibleLeafColumns.FirstOrDefault()?.Id;
        _indent = _column.Id == firstColumnId ? _row.Depth * 16 : 0;

        _border.Background = _renderer.GetCellBackground(isSelected, isActiveCell, _displayIndex);

        if (_presenter?.Content is Control content)
        {
            content.Margin = new Thickness(8 + _indent, 0, 0, 0);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_border != null)
        {
            _border.PointerPressed -= OnPointerPressed;
        }
    }

    void IReusableCellVisual<TData>.Update(Row<TData> row, int displayIndex, bool force)
    {
        if (!force && _displayIndex == displayIndex)
        {
            return;
        }

        _displayIndex = displayIndex;
        UpdateVisualState();
    }
}

internal sealed class EditingCellPresenter<TData> : ContentControl
{
    private readonly ISaGridComponentHost<TData> _host;
    private readonly Row<TData> _row;
    private readonly Column<TData> _column;
    private readonly Func<Control> _displayFactory;
    private readonly ICellEditorService<TData> _editingService;

    public EditingCellPresenter(ISaGridComponentHost<TData> host, Row<TData> row, Column<TData> column, Func<Control> displayFactory)
    {
        _host = host;
        _row = row;
        _column = column;
        _displayFactory = displayFactory;
        _editingService = host.GetEditingService();
        _editingService.EditingStateChanged += OnEditingStateChanged;

        SetDisplay();

        DoubleTapped += (_, e) =>
        {
            _host.BeginCellEdit(_row, _column);
            e.Handled = true;
        };
    }

    private void OnEditingStateChanged(object? sender, CellEditingChangedEventArgs<TData> e)
    {
        if (!_host.IsSameGrid(e.Grid))
        {
            return;
        }

        if (e.Session != null && ReferenceEquals(e.Session.Row, _row) && ReferenceEquals(e.Session.Column, _column))
        {
            Content = e.Session.EditorControl;
        }
        else
        {
            SetDisplay();
        }
    }

    private void SetDisplay()
    {
        Content = _displayFactory();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _editingService.EditingStateChanged -= OnEditingStateChanged;
    }
}
