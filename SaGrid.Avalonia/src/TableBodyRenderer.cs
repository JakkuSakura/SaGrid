using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        var panel = new VirtualizedBodyPanel<TData>(table, layoutManager);
        var viewer = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        return viewer;
    }
}
