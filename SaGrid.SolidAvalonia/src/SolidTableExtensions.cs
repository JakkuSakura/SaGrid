using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Declarative;
using SolidAvalonia;
using SaGrid.Core;
using static SolidAvalonia.Solid;

namespace SaGrid.SolidAvalonia;

public static class SolidTableExtensions
{
    /// <summary>
    /// Creates a sortable header cell with click handling
    /// </summary>
    public static Control SortableHeader<TData>(
        this SolidTable<TData> solidTable,
        IHeader<TData> header,
        Action<string, SortDirection?>? onSort = null)
    {
        return new Button()
            .Content(solidTable.GetHeaderContent(header))
            .Background(header.Column.SortDirection.HasValue
                ? Avalonia.Media.Brushes.LightYellow
                : Avalonia.Media.Brushes.LightBlue)
            .OnClick(_ =>
            {
                SortDirection? nextDirection;
                if (header.Column.SortDirection == null)
                    nextDirection = SaGrid.Core.SortDirection.Ascending;
                else if (header.Column.SortDirection.Value == SaGrid.Core.SortDirection.Ascending)
                    nextDirection = SaGrid.Core.SortDirection.Descending;
                else if (header.Column.SortDirection.Value == SaGrid.Core.SortDirection.Descending)
                    nextDirection = null;
                else
                    nextDirection = SaGrid.Core.SortDirection.Ascending;

                onSort?.Invoke(header.Column.Id, nextDirection);
                solidTable.SetSorting(header.Column.Id, nextDirection);
            });
    }

    /// <summary>
    /// Creates a filterable column header with text input
    /// </summary>
    public static Control FilterableHeader<TData>(
        this SolidTable<TData> solidTable,
        IHeader<TData> header,
        Action<string, object?>? onFilter = null)
    {
        var (filterValue, setFilterValue) = CreateSignal(header.Column.FilterValue?.ToString() ?? "");

        var container = new StackPanel() { Orientation = Orientation.Vertical };

        container.Children.Add(new TextBlock()
            .Text(solidTable.GetHeaderContent(header))
            .FontWeight(Avalonia.Media.FontWeight.Bold));

        container.Children.Add(new TextBox()
            .Text(() => filterValue())
            .Width(100)
            .Height(25));

        return container;
    }

    /// <summary>
    /// Creates a selectable row with checkbox
    /// </summary>
    public static Control SelectableRow<TData>(
        this SolidTable<TData> solidTable,
        Row<TData> row,
        Action<string, bool>? onSelectionChanged = null,
        params Control[] cells)
    {
        var container = new StackPanel()
        {
            Orientation = Orientation.Horizontal,
            Background = row.Index % 2 == 0
                ? Avalonia.Media.Brushes.White
                : Avalonia.Media.Brushes.AliceBlue
        };

        // Selection checkbox
        container.Children.Add(new CheckBox()
            .IsChecked(row.IsSelected)
            .Margin(5));

        // Cell content
        foreach (var cell in cells)
            container.Children.Add(cell);

        return container;
    }

    /// <summary>
    /// Creates pagination controls
    /// </summary>
    public static Control PaginationControls<TData>(
        this SolidTable<TData> solidTable,
        Action<int>? onPageChange = null,
        Action<int>? onPageSizeChange = null)
    {
        var pagination = solidTable.Table.State.Pagination ?? new PaginationState();
        var totalRows = solidTable.Table.PrePaginationRowModel.Rows.Count;
        var totalPages = (int)Math.Ceiling((double)totalRows / pagination.PageSize);

        var container = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 10 };

        // Previous page button
        container.Children.Add(new Button()
            .Content("Previous")
            .IsEnabled(pagination.PageIndex > 0)
            .OnClick(_ =>
            {
                var newIndex = Math.Max(0, pagination.PageIndex - 1);
                onPageChange?.Invoke(newIndex);
                solidTable.SetPageIndex(newIndex);
            }));

        // Page info
        container.Children.Add(new TextBlock()
            .Text($"Page {pagination.PageIndex + 1} of {totalPages}")
            .VerticalAlignment(Avalonia.Layout.VerticalAlignment.Center));

        // Next page button
        container.Children.Add(new Button()
            .Content("Next")
            .IsEnabled(pagination.PageIndex < totalPages - 1)
            .OnClick(_ =>
            {
                var newIndex = Math.Min(totalPages - 1, pagination.PageIndex + 1);
                onPageChange?.Invoke(newIndex);
                solidTable.SetPageIndex(newIndex);
            }));

        // Page size selector
        container.Children.Add(new ComboBox()
            .ItemsSource(new[] { 5, 10, 20, 50, 100 })
            .SelectedItem(pagination.PageSize)
            .OnSelectionChanged(args =>
            {
                if (args.AddedItems?.Count > 0 && args.AddedItems[0] is int pageSize)
                {
                    onPageSizeChange?.Invoke(pageSize);
                    solidTable.SetPageSize(pageSize);
                }
            }));

        return container;
    }

    /// <summary>
    /// Creates a global filter search box
    /// </summary>
    public static Control GlobalFilterInput<TData>(
        this SolidTable<TData> solidTable,
        string placeholder = "Search...",
        Action<string?>? onFilterChange = null)
    {
        var (filterValue, setFilterValue) = CreateSignal(
            solidTable.Table.State.GlobalFilter?.Value?.ToString() ?? "");

        return new TextBox()
            .Watermark(placeholder)
            .Text(() => filterValue())
            .Width(200);
    }

    /// <summary>
    /// Creates a column visibility toggle panel
    /// </summary>
    public static Control ColumnVisibilityPanel<TData>(
        this SolidTable<TData> solidTable,
        Action<string, bool>? onVisibilityChange = null)
    {
        var container = new WrapPanel();

        foreach (var column in solidTable.Table.AllLeafColumns)
        {
            container.Children.Add(new CheckBox()
                .Content(column.Id)
                .IsChecked(column.IsVisible)
                .Margin(5));
        }

        return container;
    }

    // Helper method to get header content (internal use)
    internal static string GetHeaderContent<TData>(this SolidTable<TData> solidTable, IHeader<TData> header)
    {
        return header.Column.ColumnDef.Header?.ToString() ?? header.Column.Id;
    }
}
