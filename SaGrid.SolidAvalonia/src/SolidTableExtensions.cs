using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using SaGrid.Core;
using static SolidAvalonia.Solid;

namespace SaGrid.SolidAvalonia;

public static class SolidTableExtensions
{
    public static Control SortableHeader<TData>(
        this SolidTable<TData> solidTable,
        IHeader<TData> header,
        Action<string, SortDirection?>? onSort = null)
    {
        return new Button()
            .Content(solidTable.GetHeaderContent(header))
            .Background(header.Column.SortDirection.HasValue
                ? Brushes.LightYellow
                : Brushes.LightBlue)
            .OnClick(_ =>
            {
                SortDirection? nextDirection = header.Column.SortDirection switch
                {
                    null => SortDirection.Ascending,
                    SortDirection.Ascending => SortDirection.Descending,
                    SortDirection.Descending => null,
                    _ => SortDirection.Ascending
                };

                onSort?.Invoke(header.Column.Id, nextDirection);

                if (nextDirection.HasValue)
                {
                    solidTable.Table.SetSorting(header.Column.Id, nextDirection.Value);
                }
                else
                {
                    solidTable.Table.SetSorting(Array.Empty<ColumnSort>());
                }
            });
    }

    public static Control FilterableHeader<TData>(
        this SolidTable<TData> solidTable,
        IHeader<TData> header,
        Action<string, object?>? onFilter = null)
    {
        var (filterValue, setFilterValue) = CreateSignal(header.Column.FilterValue?.ToString() ?? string.Empty);

        return new StackPanel()
            .Orientation(Orientation.Vertical)
            .Children(
                new TextBlock()
                    .Text(solidTable.GetHeaderContent(header))
                    .FontWeight(FontWeight.Bold),
                new TextBox()
                    .Text(() => filterValue())
                    .Width(120)
                    .OnTextChanged(_ =>
                    {
                        var value = filterValue();
                        onFilter?.Invoke(header.Column.Id, value);
                        header.Column.SetFilterValue(string.IsNullOrWhiteSpace(value) ? null : value);
                    })
            );
    }

    public static Control PaginationControls<TData>(
        this SolidTable<TData> solidTable,
        Action<int>? onPageChange = null,
        Action<int>? onPageSizeChange = null)
    {
        var table = solidTable.Table;
        var pagination = table.State.Pagination ?? new PaginationState();
        var totalRows = table.PrePaginationRowModel.Rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRows / pagination.PageSize));

        return new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(10)
            .Children(
                new Button()
                    .Content("Previous")
                    .IsEnabled(pagination.PageIndex > 0)
                    .OnClick(_ =>
                    {
                        var newIndex = Math.Max(0, pagination.PageIndex - 1);
                        onPageChange?.Invoke(newIndex);
                        table.SetPageIndex(newIndex);
                    }),
                new TextBlock()
                    .Text($"Page {pagination.PageIndex + 1} of {totalPages}")
                    .VerticalAlignment(VerticalAlignment.Center),
                new Button()
                    .Content("Next")
                    .IsEnabled(pagination.PageIndex < totalPages - 1)
                    .OnClick(_ =>
                    {
                        var newIndex = Math.Min(totalPages - 1, pagination.PageIndex + 1);
                        onPageChange?.Invoke(newIndex);
                        table.SetPageIndex(newIndex);
                    }),
                new ComboBox()
                    .ItemsSource(new[] { 5, 10, 20, 50, 100 })
                    .SelectedItem(pagination.PageSize)
                    .OnSelectionChanged(args =>
                    {
                        if (args.AddedItems?.Count > 0 && args.AddedItems[0] is int pageSize)
                        {
                            onPageSizeChange?.Invoke(pageSize);
                            table.SetPageSize(pageSize);
                        }
                    })
            );
    }

    public static Control GlobalFilterInput<TData>(
        this SolidTable<TData> solidTable,
        string placeholder = "Search...",
        Action<string?>? onFilterChange = null)
    {
        var table = solidTable.Table;
        var (filterValue, setFilterValue) = CreateSignal(
            table.State.GlobalFilter?.Value?.ToString() ?? string.Empty);

        return new TextBox()
            .Watermark(placeholder)
            .Text(() => filterValue())
            .Width(200)
            .OnTextChanged(tb =>
            {
                var value = filterValue();
                onFilterChange?.Invoke(value);
                table.SetGlobalFilter(string.IsNullOrWhiteSpace(value) ? null : value);
            });
    }

    public static Control ColumnVisibilityPanel<TData>(
        this SolidTable<TData> solidTable,
        Action<string, bool>? onVisibilityChange = null)
    {
        var container = new WrapPanel();

        var table = solidTable.Table;

        foreach (var column in table.AllLeafColumns)
        {
            container.Children.Add(new CheckBox()
                .Content(column.Id)
                .IsChecked(column.IsVisible)
                .Margin(5)
                .OnChecked(_ =>
                {
                    onVisibilityChange?.Invoke(column.Id, true);
                    table.ToggleColumnVisibility(column.Id, true);
                })
                .OnUnchecked(_ =>
                {
                    onVisibilityChange?.Invoke(column.Id, false);
                    table.ToggleColumnVisibility(column.Id, false);
                }));
        }

        return container;
    }

    public static Control InteractiveHeader<TData>(
        this SolidTable<TData> solidTable,
        IHeader<TData> header,
        Action<string, SortDirection?>? onSort = null,
        Action<string, double>? onResize = null,
        Action<string, int>? onMove = null)
        where TData : class
    {
        var headerText = solidTable.GetHeaderContent(header);
        
        // Create a container for the header with interactive features
        var container = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                // Main header content (clickable for sorting)
                new Button()
                    .Content(headerText)
                    .Background(header.Column.SortDirection.HasValue
                        ? Brushes.LightYellow
                        : Brushes.LightBlue)
                    .OnClick(_ =>
                    {
                        SortDirection? nextDirection = header.Column.SortDirection switch
                        {
                            null => SortDirection.Ascending,
                            SortDirection.Ascending => SortDirection.Descending,
                            SortDirection.Descending => null,
                            _ => SortDirection.Ascending
                        };

                        onSort?.Invoke(header.Column.Id, nextDirection);

                        if (nextDirection.HasValue)
                        {
                            solidTable.Table.SetSorting(header.Column.Id, nextDirection.Value);
                        }
                        else
                        {
                            solidTable.Table.SetSorting(Array.Empty<ColumnSort>());
                        }
                    }),
                
                // Auto-size button
                new Button()
                    .Content("⚏")
                    .Width(20)
                    .ToolTip("Auto-size column")
                    .OnClick(_ =>
                    {
                        // Auto-size functionality requires SaGrid.Advanced
                        // if (solidTable.Table is SaGrid<TData> saGrid)
                        // {
                        //     saGrid.AutoSizeColumn(header.Column.Id);
                        // }
                    }),
                
                // Column menu button
                new Button()
                    .Content("⋮")
                    .Width(20)
                    .ToolTip("Column options")
                    .OnClick(_ =>
                    {
                        // Column menu functionality requires SaGrid.Advanced
                        // if (solidTable.Table is SaGrid<TData> saGrid)
                        // {
                        //     saGrid.ToggleColumnVisibility(header.Column.Id);
                        // }
                    })
            );

        return container;
    }

    public static Control DraggableHeader<TData>(
        this SolidTable<TData> solidTable,
        IHeader<TData> header,
        Action<string, int>? onMove = null)
        where TData : class
    {
        var headerButton = new Button()
            .Content(solidTable.GetHeaderContent(header))
            .Background(header.Column.SortDirection.HasValue
                ? Brushes.LightYellow
                : Brushes.LightBlue)
            .Cursor(new Cursor(StandardCursorType.Hand));

        // Visual feedback for draggable header
        headerButton.PointerEntered += (s, e) =>
        {
            if (s is Button btn)
                btn.Background = new SolidColorBrush(Colors.LightCyan);
        };

        headerButton.PointerExited += (s, e) =>
        {
            if (s is Button btn)
                btn.Background = header.Column.SortDirection.HasValue
                    ? Brushes.LightYellow
                    : Brushes.LightBlue;
        };

        // Add tooltip to indicate draggable
        ToolTip.SetTip(headerButton, "Drag to reorder column");

        // Click handler for basic interaction
        headerButton.OnClick(_ =>
        {
            onMove?.Invoke(header.Column.Id, 0);
        });

        return headerButton;
    }

    public static Control ColumnManagementPanel<TData>(
        this SolidTable<TData> solidTable)
        where TData : class
    {
        var panel = new StackPanel()
            .Orientation(Orientation.Vertical)
            .Spacing(10);

        // Column management functionality requires SaGrid.Advanced
        // if (solidTable.Table is SaGrid<TData> saGrid)
        // {
        //     panel.Children.Add(new TextBlock().Text("Column Visibility:").FontWeight(FontWeight.Bold));
        //     
        //     foreach (var column in solidTable.Table.AllLeafColumns)
        //     {
        //         var checkBox = new CheckBox()
        //             .Content(column.Id)
        //             .IsChecked(column.IsVisible)
        //             .OnChecked(_ => saGrid.ToggleColumnVisibility(column.Id))
        //             .OnUnchecked(_ => saGrid.ToggleColumnVisibility(column.Id));
        //         
        //         panel.Children.Add(checkBox);
        //     }
        //
        //     panel.Children.Add(new Button()
        //         .Content("Auto-size All Columns")
        //         .OnClick(_ =>
        //         {
        //             foreach (var column in solidTable.Table.AllLeafColumns)
        //             {
        //                 saGrid.AutoSizeColumn(column.Id);
        //             }
        //         }));
        // }

        return new ScrollViewer()
            .Content(panel);
    }

    internal static string GetHeaderContent<TData>(this SolidTable<TData> solidTable, IHeader<TData> header)
    {
        return header.Column.ColumnDef.Header?.ToString() ?? header.Column.Id;
    }
}
