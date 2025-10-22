using Avalonia.Controls;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableBodyRenderer<TData>
{
    private readonly TableCellRenderer<TData> _cellRenderer = new();

    public Control CreateBody(Table<TData> table)
    {
        var layoutManager = TableColumnLayoutManagerRegistry.GetOrCreate(table);
        return CreateBody(table, layoutManager);
    }

    public Control CreateBody(Table<TData> table, TableColumnLayoutManager<TData> layoutManager)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };

        foreach (var row in table.RowModel.Rows)
        {
            stack.Children.Add(CreateRow(table, row, layoutManager));
        }

        return new ScrollViewer { Content = stack };
    }

    private Control CreateRow(Table<TData> table, Row<TData> row, TableColumnLayoutManager<TData> layoutManager)
    {
        var panel = layoutManager.CreatePanel();
        panel.Height = 30;

        var displayIndex = row.DisplayIndex >= 0 ? row.DisplayIndex : row.Index;

        foreach (var column in table.VisibleLeafColumns)
        {
            var cell = _cellRenderer.CreateCell(table, row, (Column<TData>)column, displayIndex);
            ColumnLayoutPanel.SetColumnId(cell, column.Id);
            panel.Children.Add(cell);
        }

        return panel;
    }
}
