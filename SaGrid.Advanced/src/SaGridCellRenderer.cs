using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SolidAvalonia;
using SaGrid.Core;
using static SolidAvalonia.Solid;
using Avalonia;

namespace SaGrid;

internal class SaGridCellRenderer<TData>
{
    public Control CreateCell(SaGrid<TData> saGrid, Row<TData> row, Column<TData> column)
    {
        var content = SaGridContentHelper<TData>.GetCellContent(row, column);
        
        return new Border()
            .BorderThickness(0, 0, 1, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(Brushes.White)
            .Width(column.Size)
            .Height(30)
            .Child(
                new TextBlock()
                    .Text(content)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Margin(new Thickness(8, 0))
            );
    }

    public Control CreateReactiveCell(SaGrid<TData> saGrid, Row<TData> row, Column<TData> column, Func<SaGrid<TData>> gridSignalGetter, Func<int>? selectionSignalGetter = null)
    {
        Console.WriteLine($"Creating reactive cell for row {row.Index}, column {column.Id}");
        
        return Reactive(() =>
        {
            // Access both the grid signal and selection signal to detect state changes
            var currentGrid = gridSignalGetter(); // Get current grid from reactive signal
            var selectionCounter = selectionSignalGetter?.Invoke() ?? 0; // This ensures reactivity when selection changes
            
            var isSelected = currentGrid?.IsCellSelected(row.Index, column.Id) ?? false;
            var activeCell = currentGrid?.GetActiveCell();
            var isActiveCell = activeCell?.RowIndex == row.Index && activeCell?.ColumnId == column.Id;
            
            var background = GetCellBackground(isSelected, isActiveCell, row.Index);
            var content = SaGridContentHelper<TData>.GetCellContent(row, column);
            
            Console.WriteLine($"Reactive cell render - Row {row.Index}, Col {column.Id}: Selected={isSelected}, Active={isActiveCell}");
            
            var border = new Border()
                .BorderThickness(0, 0, 1, 1)
                .BorderBrush(Brushes.LightGray)
                .Background(background)
                .Width(column.Size)
                .Height(30)
                .Child(
                    new TextBlock()
                        .Text(content)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .Margin(new Thickness(8, 0))
                );

            // Add click handler for cell selection
            border.PointerPressed += (sender, e) =>
            {
                var isCtrlPressed = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
                Console.WriteLine($"Cell clicked: Row {row.Index}, Column {column.Id}, Ctrl: {isCtrlPressed}");
                
                currentGrid?.SelectCell(row.Index, column.Id, isCtrlPressed);
                e.Handled = true; // Prevent event from bubbling up
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