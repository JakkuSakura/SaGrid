using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SaGrid.Core;
using Avalonia;

namespace SaGrid.SolidAvalonia;

internal class TableFooterRenderer<TData>
{
    public Control CreateFooter(Table<TData> table)
    {
        if (!table.Options.EnablePagination)
        {
            return new StackPanel(); // Empty footer if pagination is disabled
        }

        var pageIndex = table.State.Pagination?.PageIndex ?? 0;
        var pageSize = table.State.Pagination?.PageSize ?? 10;
        var totalRows = table.Options.Data.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRows / pageSize));
        var currentPage = pageIndex + 1; // Convert to 1-based

        return new Border()
            .BorderThickness(0, 1, 0, 0)
            .BorderBrush(Brushes.LightGray)
            .Background(Brushes.LightGray)
            .Height(40)
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(10, 0))
                    .Children(
                        new TextBlock()
                            .Text($"Page {currentPage} of {totalPages} ({totalRows} total rows)")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(new Thickness(0, 0, 20, 0)),
                        
                        new Button()
                            .Content("Previous")
                            .IsEnabled(pageIndex > 0)
                            .Margin(new Thickness(0, 0, 10, 0)),
                            
                        new Button()
                            .Content("Next")
                            .IsEnabled(currentPage < totalPages)
                    )
            );
    }
}