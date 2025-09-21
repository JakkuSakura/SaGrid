using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SaGrid.Core;
using Avalonia;

namespace SaGrid.SolidAvalonia;

internal class TableHeaderRenderer<TData>
{
    public Control CreateHeader(Table<TData> table)
    {
        var headerControls = new List<Control>();
        
        // Add header title rows
        headerControls.AddRange(table.HeaderGroups.Select(headerGroup =>
            new StackPanel()
                .Orientation(Orientation.Horizontal)
                .Children(
                    headerGroup.Headers.Select(header =>
                        new Border()
                            .BorderThickness(0, 0, 1, 1)
                            .BorderBrush(Brushes.LightGray)
                            .Background(Brushes.LightBlue)
                            .Width(header.Size)
                            .Height(40)
                            .Child(
                                new TextBlock()
                                    .Text(TableContentHelper<TData>.GetHeaderContent(header))
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .FontWeight(FontWeight.Bold)
                            )
                    ).ToArray()
                )
        ));
        
        // Add filter row if column filtering is enabled
        if (table.Options.EnableColumnFilters)
        {
            var filterRow = CreateFilterRow(table);
            Console.WriteLine($"Adding filter row to header - Total header controls: {headerControls.Count + 1}");
            headerControls.Add(filterRow);
        }
        else
        {
            Console.WriteLine("Column filtering is disabled - no filter row will be added");
        }
        
        return new StackPanel()
            .Orientation(Orientation.Vertical)
            .Children(headerControls.ToArray());
    }

    private Control CreateFilterRow(Table<TData> table)
    {
        Console.WriteLine($"Creating filter row with {table.VisibleLeafColumns.Count} columns");
        
        var filterControls = table.VisibleLeafColumns.Select(column =>
        {
            Console.WriteLine($"Creating filter for column: {column.Id}");
            var textBox = CreateFilterTextBox(table, column);
            return new Border()
                .BorderThickness(0, 0, 1, 1)
                .BorderBrush(Brushes.LightGray)
                .Background(Brushes.White)
                .Width(column.Size)
                .Height(35)
                .Padding(new Thickness(2))
                .Child(textBox);
        }).ToArray();

        return new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(filterControls);
    }

    private Control CreateFilterTextBox(Table<TData> table, Column<TData> column)
    {
        Console.WriteLine($"Creating TextBox for column {column.Id}");
        
        var textBox = new TextBox
        {
            Watermark = $"Filter {column.Id}...",
            Width = double.NaN, // Auto width
            Height = double.NaN, // Auto height
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        
        Console.WriteLine($"TextBox created for {column.Id} - Focusable: {textBox.Focusable}, IsEnabled: {textBox.IsEnabled}");

        // Add multiple event handlers to debug what's happening
        textBox.GotFocus += (sender, args) =>
        {
            Console.WriteLine($"TextBox for column {column.Id} got focus");
        };

        textBox.LostFocus += (sender, args) =>
        {
            Console.WriteLine($"TextBox for column {column.Id} lost focus");
        };

        textBox.PointerPressed += (sender, args) =>
        {
            Console.WriteLine($"TextBox for column {column.Id} pointer pressed");
            textBox.Focus();
        };

        textBox.PointerEntered += (sender, args) =>
        {
            Console.WriteLine($"TextBox for column {column.Id} pointer entered");
        };

        textBox.KeyDown += (sender, args) =>
        {
            Console.WriteLine($"TextBox for column {column.Id} key down: {args.Key}");
        };

        textBox.TextChanging += (sender, args) =>
        {
            Console.WriteLine($"TextBox for column {column.Id} text changing");
        };

        textBox.TextChanged += (sender, args) =>
        {
            if (table is IAdvancedTable<TData> advancedTable && sender is TextBox tb)
            {
                var filterValue = string.IsNullOrWhiteSpace(tb.Text) ? (object?)null : tb.Text;
                Console.WriteLine($"Filter changed for column {column.Id}: '{tb.Text}' -> {(filterValue == null ? "null" : filterValue)}");
                if (filterValue != null)
                {
                    // Use base table SetColumnFilter method if available, or try reflection
                    if (table is Table<TData> baseTable)
                    {
                        baseTable.SetColumnFilter(column.Id, filterValue);
                    }
                }
                else
                {
                    if (table is Table<TData> baseTable)
                    {
                        baseTable.SetColumnFilter(column.Id, ""); // Use empty string instead of null
                    }
                }
            }
        };

        return textBox;
    }
}