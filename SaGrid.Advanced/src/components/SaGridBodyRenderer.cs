using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
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
        var renderedRows = FlattenRows(saGrid.RowModel.Rows);

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        foreach (var row in renderedRows)
        {
            stackPanel.Children.Add(CreateRow(saGrid, row, gridSignalGetter, selectionSignalGetter));
        }

        var scroller = new ScrollViewer
        {
            Focusable = false,
            Content = stackPanel
        };
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

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        foreach (var cell in cells)
        {
            panel.Children.Add(cell);
        }

        return panel;
    }

    private IEnumerable<Row<TData>> FlattenRows(IReadOnlyList<Row<TData>> rows)
    {
        foreach (var row in rows)
        {
            yield return row;

            if (row.SubRows.Count > 0)
            {
                var children = row.SubRows
                    .OfType<Row<TData>>()
                    .ToList();

                foreach (var child in FlattenRows(children))
                {
                    yield return child;
                }
            }
        }
    }
}
