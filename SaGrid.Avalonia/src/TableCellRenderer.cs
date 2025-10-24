using Avalonia.Controls;
using Avalonia.Media;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableCellRenderer<TData>
{
    public Control CreateCell(Table<TData> table, Row<TData> row, Column<TData> column, int? displayIndex = null)
    {
        var content = TableContentHelper<TData>.GetCellContent(row, column);
        var index = displayIndex ?? row.DisplayIndex;
        if (index < 0)
        {
            index = row.Index;
        }

        IBrush background = index % 2 == 0
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(248, 248, 248));

        return new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = Brushes.LightGray,
            Background = background,
            Height = double.NaN,
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
