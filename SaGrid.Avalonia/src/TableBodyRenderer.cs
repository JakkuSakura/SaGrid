using Avalonia.Controls;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableBodyRenderer<TData>
{
    private readonly TableCellRenderer<TData> _cellRenderer = new();

    public Control CreateBody(Table<TData> table)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };

        foreach (var row in table.RowModel.Rows)
        {
            stack.Children.Add(CreateRow(table, row));
        }

        return new ScrollViewer { Content = stack };
    }

    private Control CreateRow(Table<TData> table, Row<TData> row)
    {
        var grid = new Grid();

        foreach (var _ in table.VisibleLeafColumns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var displayIndex = row.DisplayIndex >= 0 ? row.DisplayIndex : row.Index;

        for (var index = 0; index < table.VisibleLeafColumns.Count; index++)
        {
            var column = table.VisibleLeafColumns[index];
            var cell = _cellRenderer.CreateCell(table, row, column, displayIndex);
            Grid.SetColumn(cell, index);
            grid.Children.Add(cell);
        }

        return grid;
    }
}
