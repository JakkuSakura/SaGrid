using Avalonia.Controls;
using Avalonia.Input;

namespace SaGrid.Avalonia;

public class TableHeaderRenderer<TData>
{
    private const double HeaderHeight = 40;
    private const double FilterHeight = 35;

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

                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0)
                };

                UpdateHeaderButtonContent(table, column, header, button);

                button.Click += (_, __) =>
                {
                    ToggleSorting(table, column);
                    UpdateHeaderButtonContent(table, column, header, button);
                };

                border.Child = button;
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

    private static void UpdateHeaderButtonContent(
        Table<TData> table,
        Column<TData> column,
        IHeader<TData> header,
        Button button)
    {
        var title = TableContentHelper<TData>.GetHeaderContent(header);
        var sortSuffix = column.SortDirection switch
        {
            SortDirection.Ascending => " ▲",
            SortDirection.Descending => " ▼",
            _ => string.Empty
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4
        };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrEmpty(sortSuffix))
        {
            stack.Children.Add(new TextBlock
            {
                Text = sortSuffix,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        button.Content = stack;
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
