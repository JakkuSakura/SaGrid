using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SaGrid.Core;
using Avalonia;
using SaGrid.Advanced;
using SolidAvalonia;
using static SolidAvalonia.Solid;

namespace SaGrid;

internal class SaGridFooterRenderer<TData>
{
    public Control CreateFooter(SaGrid<TData> saGrid)
    {
        if (!saGrid.Options.EnablePagination)
        {
            return new StackPanel(); // Empty footer if pagination is disabled
        }

        return (Control)Reactive(() =>
        {
            // Depend on grid reactive signal via visible row count
            var pageIndex = saGrid.State.Pagination?.PageIndex ?? 0;
            var pageSize = saGrid.State.Pagination?.PageSize ?? 10;
            // Use filtered total (PrePaginationRowModel) for page count consistency
            var totalRows = saGrid.PrePaginationRowModel.Rows.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRows / pageSize));
            var currentPage = pageIndex + 1; // Convert to 1-based

            var previousBtn = new Button()
                .Content("Previous")
                .IsEnabled(pageIndex > 0)
                .Margin(new Thickness(0, 0, 10, 0));

            previousBtn.Click += (s, e) =>
            {
                var newIndex = Math.Max(0, pageIndex - 1);
                saGrid.SetPageIndex(newIndex);
            };

            var nextBtn = new Button()
                .Content("Next")
                .IsEnabled(currentPage < totalPages);

            nextBtn.Click += (s, e) =>
            {
                var newIndex = Math.Min(totalPages - 1, pageIndex + 1);
                saGrid.SetPageIndex(newIndex);
            };

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
                            previousBtn,
                            nextBtn
                        )
                );
        });
    }
}
