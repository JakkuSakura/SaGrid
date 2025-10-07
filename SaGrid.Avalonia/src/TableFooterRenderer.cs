using Avalonia.Controls;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableFooterRenderer<TData>
{
    public Control CreateFooter(Table<TData> table)
    {
        if (!table.Options.EnablePagination)
        {
            return new StackPanel();
        }

        var pagination = table.State.Pagination ?? new PaginationState();
        var totalRows = table.PrePaginationRowModel.Rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRows / pagination.PageSize));

        var infoText = new TextBlock
        {
            Text = BuildPageInfo(pagination.PageIndex, pagination.PageSize, totalPages, totalRows),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0)
        };

        var previousButton = new Button
        {
            Content = "Previous",
            IsEnabled = pagination.PageIndex > 0,
            Margin = new Thickness(0, 0, 10, 0)
        };

        previousButton.Click += (_, __) =>
        {
            table.SetPageIndex(Math.Max(0, pagination.PageIndex - 1));
        };

        var nextButton = new Button
        {
            Content = "Next",
            IsEnabled = pagination.PageIndex < totalPages - 1
        };

        nextButton.Click += (_, __) =>
        {
            table.SetPageIndex(Math.Min(totalPages - 1, pagination.PageIndex + 1));
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0)
        };

        panel.Children.Add(infoText);
        panel.Children.Add(previousButton);
        panel.Children.Add(nextButton);

        return new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Brushes.LightGray,
            Background = Brushes.LightGray,
            Height = 40,
            Child = panel
        };
    }

    private static string BuildPageInfo(int pageIndex, int pageSize, int totalPages, int totalRows)
    {
        return $"Page {pageIndex + 1} of {totalPages} ({totalRows} total rows, page size {pageSize})";
    }
}
