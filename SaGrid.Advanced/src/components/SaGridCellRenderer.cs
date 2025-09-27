using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SolidAvalonia;
using SaGrid.Advanced.Modules.Editing;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;
using static SolidAvalonia.Solid;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SaGrid;

internal class SaGridCellRenderer<TData>
{
    public Control CreateCell(SaGrid<TData> saGrid, Row<TData> row, Column<TData> column, int displayIndex)
    {
        var firstColumnId = saGrid.VisibleLeafColumns.FirstOrDefault()?.Id;
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
            .Width(column.Size)
            .Height(30);

        border.Child = new EditingCellPresenter<TData>(saGrid, row, column, displayFactory);
        return border;
    }

    public Control CreateReactiveCell(
        SaGrid<TData> saGrid,
        Row<TData> row,
        Column<TData> column,
        int displayIndex,
        Func<SaGrid<TData>> gridSignalGetter,
        Func<int>? selectionSignalGetter = null)
    {
        var gridSignal = gridSignalGetter ?? (() => saGrid);
        return new ReactiveCellComponent<TData>(saGrid, row, column, displayIndex, gridSignal, selectionSignalGetter, this);
    }

    internal IBrush GetCellBackground(bool isSelected, bool isActiveCell, int rowIndex)
    {
        if (isActiveCell)
        {
            return new SolidColorBrush(Colors.Orange); // Active cell is orange
        }
        
        if (isSelected)
        {
            return new SolidColorBrush(Colors.LightBlue); // Selected cell is light blue
        }
        
        // Alternate row colors for better readability
        return rowIndex % 2 == 0 
            ? Brushes.White 
            : new SolidColorBrush(Color.FromRgb(248, 248, 248)); // Very light gray
    }
}

internal sealed class ReactiveCellComponent<TData> : Component
{
    private readonly SaGrid<TData> _grid;
    private readonly Row<TData> _row;
    private readonly Column<TData> _column;
    private readonly int _displayIndex;
    private readonly Func<SaGrid<TData>> _gridSignalGetter;
    private readonly Func<int>? _selectionSignalGetter;
    private readonly SaGridCellRenderer<TData> _renderer;

    private Border? _border;
    private EditingCellPresenter<TData>? _presenter;
    private double _indent;

    public ReactiveCellComponent(
        SaGrid<TData> grid,
        Row<TData> row,
        Column<TData> column,
        int displayIndex,
        Func<SaGrid<TData>> gridSignalGetter,
        Func<int>? selectionSignalGetter,
        SaGridCellRenderer<TData> renderer) : base(true)
    {
        _grid = grid;
        _row = row;
        _column = column;
        _displayIndex = displayIndex;
        _gridSignalGetter = gridSignalGetter;
        _selectionSignalGetter = selectionSignalGetter;
        _renderer = renderer;

        // Activate lifecycle now that required state is assigned.
        OnCreatedCore();
        Initialize();
    }

    protected override object Build()
    {
        _border = new Border()
            .BorderThickness(0, 0, 1, 1)
            .BorderBrush(Brushes.LightGray)
            .Width(_column.Size)
            .Height(30);

        Func<Control> displayFactory = () => new TextBlock()
            .Text(SaGridContentHelper<TData>.GetCellContent(_row, _column))
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Margin(new Thickness(8 + _indent, 0, 0, 0));

        _presenter = new EditingCellPresenter<TData>(_grid, _row, _column, displayFactory);
        _border.Child = _presenter;

        _border.PointerPressed += OnPointerPressed;
        _border.Background = _renderer.GetCellBackground(false, false, _displayIndex);

        CreateEffect(UpdateVisualState);

        return _border;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _grid.SelectCell(_displayIndex, _column.Id, isCtrlPressed);
        e.Handled = true;
    }

    private void UpdateVisualState()
    {
        var currentGrid = _gridSignalGetter();
        var selectionToken = _selectionSignalGetter?.Invoke() ?? 0;
        _ = selectionToken; // ensure dependency tracking

        if (_border == null)
        {
            return;
        }

        var isSelected = currentGrid?.IsCellSelected(_displayIndex, _column.Id) ?? false;
        var activeCell = currentGrid?.GetActiveCell();
        var isActiveCell = activeCell?.RowIndex == _displayIndex && activeCell?.ColumnId == _column.Id;

        var firstColumnId = currentGrid?.VisibleLeafColumns.FirstOrDefault()?.Id;
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
}

internal sealed class EditingCellPresenter<TData> : ContentControl
{
    private readonly SaGrid<TData> _grid;
    private readonly Row<TData> _row;
    private readonly Column<TData> _column;
    private readonly Func<Control> _displayFactory;
    private readonly ICellEditorService<TData> _editingService;

    public EditingCellPresenter(SaGrid<TData> grid, Row<TData> row, Column<TData> column, Func<Control> displayFactory)
    {
        _grid = grid;
        _row = row;
        _column = column;
        _displayFactory = displayFactory;
        _editingService = grid.GetEditingService();
        _editingService.EditingStateChanged += OnEditingStateChanged;

        SetDisplay();

        DoubleTapped += (_, e) =>
        {
            _grid.BeginCellEdit(_row, _column);
            e.Handled = true;
        };
    }

    private void OnEditingStateChanged(object? sender, CellEditingChangedEventArgs<TData> e)
    {
        if (!ReferenceEquals(e.Grid, _grid))
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
