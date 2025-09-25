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
    public Control CreateCell(SaGrid<TData> saGrid, Row<TData> row, Column<TData> column)
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
            .Background(Brushes.White)
            .Width(column.Size)
            .Height(30);

        border.Child = new EditingCellPresenter<TData>(saGrid, row, column, displayFactory);
        return border;
    }

    public Control CreateReactiveCell(SaGrid<TData> saGrid, Row<TData> row, Column<TData> column, Func<SaGrid<TData>> gridSignalGetter, Func<int>? selectionSignalGetter = null)
    {
        return Reactive(() =>
        {
            // Access both the grid signal and selection signal to detect state changes
            var currentGrid = gridSignalGetter(); // Get current grid from reactive signal
            var selectionCounter = selectionSignalGetter?.Invoke() ?? 0; // This ensures reactivity when selection changes
            
            var isSelected = currentGrid?.IsCellSelected(row.Index, column.Id) ?? false;
            var activeCell = currentGrid?.GetActiveCell();
            var isActiveCell = activeCell?.RowIndex == row.Index && activeCell?.ColumnId == column.Id;
            
            var background = GetCellBackground(isSelected, isActiveCell, row.Index);
            var firstColumnId = currentGrid?.VisibleLeafColumns.FirstOrDefault()?.Id;
            var indent = column.Id == firstColumnId ? row.Depth * 16 : 0;

            var displayFactory = new Func<Control>(() => new TextBlock()
                .Text(SaGridContentHelper<TData>.GetCellContent(row, column))
                .VerticalAlignment(VerticalAlignment.Center)
                .HorizontalAlignment(HorizontalAlignment.Left)
                .Margin(new Thickness(8 + indent, 0, 0, 0)));

            var border = new Border()
                .BorderThickness(0, 0, 1, 1)
                .BorderBrush(Brushes.LightGray)
                .Background(background)
                .Width(column.Size)
                .Height(30);

            border.Child = new EditingCellPresenter<TData>(saGrid, row, column, displayFactory);

            border.PointerPressed += (sender, e) =>
            {
                var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                currentGrid?.SelectCell(row.Index, column.Id, isCtrlPressed);
                e.Handled = true;
            };

            return border;
        });
    }

    private IBrush GetCellBackground(bool isSelected, bool isActiveCell, int rowIndex)
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
