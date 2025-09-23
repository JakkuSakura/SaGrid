using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using SaGrid.Core;

namespace SaGrid;

internal class SaGridBodyRenderer<TData>
{
    private readonly SaGridCellRenderer<TData> _cellRenderer;

    public SaGridBodyRenderer()
    {
        _cellRenderer = new SaGridCellRenderer<TData>();
    }

    public Control CreateBody(SaGrid<TData> saGrid, Func<SaGrid<TData>>? gridSignalGetter = null, Func<int>? selectionSignalGetter = null)
    {
        var scroller = new ScrollViewer()
            .Focusable(false)
            .Content(
                new StackPanel()
                    .Orientation(Orientation.Vertical)
                    .Children(
                        saGrid.RowModel.Rows.Select(row =>
                            CreateRow(saGrid, row, gridSignalGetter, selectionSignalGetter)
                        ).ToArray()
                    )
            );
        return scroller;
    }

    private Control CreateRow(SaGrid<TData> saGrid, Row<TData> row, Func<SaGrid<TData>>? gridSignalGetter = null, Func<int>? selectionSignalGetter = null)
    {
        var cells = saGrid.VisibleLeafColumns.Select(column =>
        {
            // Use reactive cells to support cell selection
            if (gridSignalGetter != null)
            {
                return _cellRenderer.CreateReactiveCell(saGrid, row, column, gridSignalGetter, selectionSignalGetter);
            }
            else
            {
                return _cellRenderer.CreateCell(saGrid, row, column);
            }
        }).ToArray();

        return new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(cells);
    }
}
