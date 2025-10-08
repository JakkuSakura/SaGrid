using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableHeaderRenderer<TData>
{
    private const double HeaderHeight = 40;
    private const double FilterHeight = 35;
    private const double ResizeHandleWidth = 8d;

    private TableColumnLayoutManager<TData>? _currentLayoutManager;

    public Control CreateHeader(Table<TData> table)
    {
        var layoutManager = new TableColumnLayoutManager<TData>(table);
        return CreateHeader(table, layoutManager);
    }

    public Control CreateHeader(Table<TData> table, TableColumnLayoutManager<TData> layoutManager)
    {
        _currentLayoutManager = layoutManager;

        var container = new StackPanel { Orientation = Orientation.Vertical };

        foreach (var headerGroup in table.HeaderGroups)
        {
            var rowPanel = layoutManager.CreatePanel();
            rowPanel.Height = HeaderHeight;

            foreach (var header in headerGroup.Headers)
            {
                var column = (Column<TData>)header.Column;
                var headerCell = CreateHeaderCell(table, column, header);

                if (header.SubHeaders.Count > 0)
                {
                    var spanIds = column.LeafColumns
                        .OfType<Column<TData>>()
                        .Where(c => c.IsVisible)
                        .Select(c => c.Id)
                        .ToArray();

                    if (spanIds.Length > 0)
                    {
                        ColumnLayoutPanel.SetColumnSpan(headerCell, spanIds);
                    }
                    else
                    {
                        ColumnLayoutPanel.SetColumnId(headerCell, column.Id);
                    }
                }
                else
                {
                    ColumnLayoutPanel.SetColumnId(headerCell, column.Id);
                }

                rowPanel.Children.Add(headerCell);
            }

            container.Children.Add(rowPanel);
        }

        if (table.Options.EnableColumnFilters)
        {
            container.Children.Add(CreateFilterRow(table, layoutManager));
        }

        return container;
    }

    private Control CreateHeaderCell(Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Brushes.LightGray,
            Background = Brushes.LightBlue,
            Padding = new Thickness(0),
            Height = HeaderHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var headerGrid = new Grid();

        var hasResizer = ShouldRenderResizer(column);

        var contentHost = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var content = CreateHeaderContent(column, header, out var sortIndicator);
        contentHost.Child = content;

        headerGrid.Children.Add(contentHost);

        if (hasResizer)
        {
            var resizer = CreateResizeThumb(column);
            headerGrid.Children.Add(resizer);
        }

        border.Child = headerGrid;

        AttachSortingInteraction(contentHost, table, column, sortIndicator);

        return border;
    }

    private Control CreateHeaderContent(
        Column<TData> column,
        IHeader<TData> header,
        out TextBlock sortIndicator)
    {
        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4
        };

        var title = new TextBlock
        {
            Text = TableContentHelper<TData>.GetHeaderContent(header),
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        sortIndicator = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        stack.Children.Add(title);
        stack.Children.Add(sortIndicator);

        Grid.SetColumn(stack, 1);
        layout.Children.Add(stack);

        UpdateSortIndicator(column, sortIndicator);

        return layout;
    }

    private static void UpdateSortIndicator(Column<TData> column, TextBlock sortIndicator)
    {
        var sortText = column.SortDirection switch
        {
            SortDirection.Ascending => "▲",
            SortDirection.Descending => "▼",
            _ => string.Empty
        };

        sortIndicator.Text = sortText;
        sortIndicator.IsVisible = !string.IsNullOrEmpty(sortText);
    }

    private static void ToggleSorting(Table<TData> table, Column<TData> column)
    {
        var currentDirection = column.SortDirection;

        if (currentDirection == null)
        {
            table.SetSorting(column.Id, SortDirection.Ascending);
        }
        else if (currentDirection == SortDirection.Ascending)
        {
            table.SetSorting(column.Id, SortDirection.Descending);
        }
        else
        {
            table.SetSorting(Array.Empty<ColumnSort>());
        }
    }

    private static bool ShouldRenderResizer(Column<TData> column)
    {
        return !column.Columns.Any() && column.CanResize;
    }

    private Control CreateResizeThumb(Column<TData> column)
    {
        var thumb = new Thumb
        {
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        thumb.DragDelta += (_, e) =>
        {
            var delta = e.Vector.X;
            if (Math.Abs(delta) < double.Epsilon)
            {
                return;
            }

            var currentWidth = column.Size;
            var minWidth = (double)(column.ColumnDef.MinSize ?? 40);
            var maxWidth = column.ColumnDef.MaxSize;

            var updatedWidth = currentWidth + delta;
            updatedWidth = Math.Max(updatedWidth, minWidth);
            if (maxWidth.HasValue)
            {
                updatedWidth = Math.Min(updatedWidth, maxWidth.Value);
            }

            column.SetSize(updatedWidth);
            _currentLayoutManager?.Refresh();
            e.Handled = true;
        };

        thumb.DoubleTapped += (_, e) =>
        {
            column.ResetSize();
            _currentLayoutManager?.Refresh();
            e.Handled = true;
        };

        var resizerContainer = new Grid
        {
            Width = ResizeHandleWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            IsHitTestVisible = true
        };

        resizerContainer.SetValue(Panel.ZIndexProperty, 1);

        var line = new Border
        {
            Width = 1,
            Background = Brushes.LightGray,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        resizerContainer.Children.Add(line);

        thumb.SetValue(Panel.ZIndexProperty, 1);
        resizerContainer.Children.Add(thumb);

        thumb.PointerEntered += (_, _) =>
        {
            line.Background = new SolidColorBrush(Colors.SteelBlue);
        };

        thumb.PointerExited += (_, _) =>
        {
            line.Background = Brushes.LightGray;
        };

        return resizerContainer;
    }

    private void AttachSortingInteraction(
        Control target,
        Table<TData> table,
        Column<TData> column,
        TextBlock sortIndicator)
    {
        if (!column.CanSort)
        {
            return;
        }

        target.Cursor = new Cursor(StandardCursorType.Hand);

        var pressed = false;

        target.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
            {
                pressed = true;
            }
        };

        target.PointerReleased += (_, e) =>
        {
            if (!pressed || e.InitialPressMouseButton != MouseButton.Left)
            {
                pressed = false;
                return;
            }

            pressed = false;

            ToggleSorting(table, column);
            UpdateSortIndicator(column, sortIndicator);
            e.Handled = true;
        };

        target.PointerCaptureLost += (_, __) => pressed = false;
        target.PointerExited += (_, __) => pressed = false;
    }

    private Control CreateFilterRow(Table<TData> table, TableColumnLayoutManager<TData> layoutManager)
    {
        var rowPanel = layoutManager.CreatePanel();
        rowPanel.Height = FilterHeight;

        foreach (var column in table.VisibleLeafColumns)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.LightGray,
                Background = Brushes.White,
                Padding = new Thickness(4)
            };

            var textBox = new TextBox
            {
                Watermark = $"Filter {column.Id}...",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            textBox.Text = column.FilterValue?.ToString() ?? string.Empty;
            textBox.TextChanged += (_, __) =>
            {
                column.SetFilterValue(string.IsNullOrEmpty(textBox.Text) ? null : textBox.Text);
            };

            textBox.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    table.SetState(table.State);
                }
            };

            border.Child = textBox;
            ColumnLayoutPanel.SetColumnId(border, column.Id);
            rowPanel.Children.Add(border);
        }

        return rowPanel;
    }
}
