using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableHeaderRenderer<TData>
{
    private const double HeaderHeight = 40;
    private const double FilterHeight = 35;
    private const double ResizeHandleWidth = 8d;

    public Control CreateHeader(Table<TData> table)
    {
        var container = new StackPanel { Orientation = Orientation.Vertical };

        foreach (var headerGroup in table.HeaderGroups)
        {
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var header in headerGroup.Headers)
            {
                var column = (Column<TData>)header.Column;
                var border = new Border
                {
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = Brushes.LightGray,
                    Background = Brushes.LightBlue,
                    Width = header.Size,
                    Height = HeaderHeight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var hasResizer = ShouldRenderResizer(column);

                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

                if (hasResizer)
                {
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = new GridLength(ResizeHandleWidth, GridUnitType.Pixel)
                    });
                }

                var contentHost = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(8, 0, 8, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var content = CreateHeaderContent(column, header, out var sortIndicator);
                contentHost.Child = content;

                Grid.SetColumn(contentHost, 0);
                headerGrid.Children.Add(contentHost);

                if (hasResizer)
                {
                    var resizer = CreateResizeThumb(column, border);
                    Grid.SetColumn(resizer, 1);
                    headerGrid.Children.Add(resizer);
                }

                border.Child = headerGrid;

                AttachSortingInteraction(contentHost, table, column, sortIndicator);

                rowPanel.Children.Add(border);
            }

            container.Children.Add(rowPanel);
        }

        if (table.Options.EnableColumnFilters)
        {
            container.Children.Add(CreateFilterRow(table));
        }

        return container;
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

    private Control CreateResizeThumb(Column<TData> column, Border headerBorder)
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

            var currentWidth = headerBorder.Width;
            if (double.IsNaN(currentWidth) || double.IsInfinity(currentWidth))
            {
                currentWidth = column.Size;
            }

            var minWidth = (double)(column.ColumnDef.MinSize ?? 40);
            var maxWidth = column.ColumnDef.MaxSize;

            var updatedWidth = currentWidth + delta;
            updatedWidth = Math.Max(updatedWidth, minWidth);
            if (maxWidth.HasValue)
            {
                updatedWidth = Math.Min(updatedWidth, maxWidth.Value);
            }

            headerBorder.Width = updatedWidth;
            column.SetSize(updatedWidth);
            e.Handled = true;
        };

        thumb.DoubleTapped += (_, e) =>
        {
            column.ResetSize();
            headerBorder.Width = column.Size;
            e.Handled = true;
        };

        var resizerContainer = new Grid
        {
            Width = ResizeHandleWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = true
        };

        resizerContainer.ZIndex = 1;

        resizerContainer.Children.Add(new Border
        {
            Width = 1,
            Background = Brushes.LightGray,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch
        });

        resizerContainer.Children.Add(thumb);

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

    private Control CreateFilterRow(Table<TData> table)
    {
        var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };

        foreach (var column in table.VisibleLeafColumns)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.LightGray,
                Background = Brushes.White,
                Width = column.Size,
                Height = FilterHeight,
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
            rowPanel.Children.Add(border);
        }

        return rowPanel;
    }
}
