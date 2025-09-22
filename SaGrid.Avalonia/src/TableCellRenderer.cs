using Avalonia.Controls;

namespace SaGrid.Avalonia;

public class TableCellRenderer<TData>
{
    public Control CreateCell(Table<TData> table, Row<TData> row, Column<TData> column)
    {
        var content = TableContentHelper<TData>.GetCellContent(row, column);

        return new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = Brushes.LightGray,
            Background = Brushes.White,
            Height = 30,
            Child = new TextBlock
            {
                Text = content,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 0)
            }
        };
    }
}
