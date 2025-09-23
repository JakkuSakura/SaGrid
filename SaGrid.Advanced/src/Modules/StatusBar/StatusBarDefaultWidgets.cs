using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SaGrid;

namespace SaGrid.Advanced.Modules.StatusBar;

public static class StatusBarDefaultWidgets
{
    public const string TotalRowsId = "totalRows";
    public const string SelectedRowsId = "selectedRows";
    public const string FilteredRowsId = "filteredRows";
    public const string PaginationId = "pagination";

    public static IReadOnlyList<StatusBarWidgetDefinition> CreateDefaultWidgets<TData>()
    {
        return new List<StatusBarWidgetDefinition>
        {
            new StatusBarWidgetDefinition(TotalRowsId, "Total Rows", grid => new TotalRowsWidget<TData>((SaGrid<TData>)grid), 100),
            new StatusBarWidgetDefinition(SelectedRowsId, "Selected", grid => new SelectedRowsWidget<TData>((SaGrid<TData>)grid), 200),
            new StatusBarWidgetDefinition(FilteredRowsId, "Filtered", grid => new FilteredRowsWidget<TData>((SaGrid<TData>)grid), 300),
            new StatusBarWidgetDefinition(PaginationId, "Pagination", grid => new PaginationWidget<TData>((SaGrid<TData>)grid), 400)
        };
    }

    private sealed class TotalRowsWidget<TData> : UserControl
    {
        private readonly SaGrid<TData> _grid;
        private readonly TextBlock _textBlock;

        public TotalRowsWidget(SaGrid<TData> grid)
        {
            _grid = grid;
            _textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4),
                FontSize = 12
            };

            Content = _textBlock;
            UpdateDisplay();

            // Note: For proper implementation, we would need to hook into the grid's state change notification
            // For now, we'll rely on the status bar service to refresh widgets when needed
        }

        private void UpdateDisplay()
        {
            var totalRows = _grid.RowModel.Rows.Count;
            _textBlock.Text = $"Total: {totalRows:N0}";
        }
    }

    private sealed class SelectedRowsWidget<TData> : UserControl
    {
        private readonly SaGrid<TData> _grid;
        private readonly TextBlock _textBlock;

        public SelectedRowsWidget(SaGrid<TData> grid)
        {
            _grid = grid;
            _textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4),
                FontSize = 12
            };

            Content = _textBlock;
            UpdateDisplay();

            // Note: For proper implementation, we would need to hook into the grid's state change notification
            // For now, we'll rely on the status bar service to refresh widgets when needed
        }

        private void UpdateDisplay()
        {
            var selectedCount = _grid.GetSelectedCells().Count;
            var totalRows = _grid.RowModel.Rows.Count;

            if (selectedCount > 0)
            {
                _textBlock.Text = $"Selected: {selectedCount:N0} of {totalRows:N0}";
                IsVisible = true;
            }
            else
            {
                IsVisible = false;
            }
        }
    }

    private sealed class FilteredRowsWidget<TData> : UserControl
    {
        private readonly SaGrid<TData> _grid;
        private readonly TextBlock _textBlock;

        public FilteredRowsWidget(SaGrid<TData> grid)
        {
            _grid = grid;
            _textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4),
                FontSize = 12,
                Foreground = Brushes.Orange
            };

            Content = _textBlock;
            UpdateDisplay();

            // Note: For proper implementation, we would need to hook into the grid's state change notification
            // For now, we'll rely on the status bar service to refresh widgets when needed
        }

        private void UpdateDisplay()
        {
            var visibleRows = _grid.RowModel.Rows.Count;
            var hasGlobalFilter = _grid.State.GlobalFilter != null;
            var hasColumnFilters = _grid.State.ColumnFilters?.Filters.Count > 0;

            if (hasGlobalFilter || hasColumnFilters == true)
            {
                _textBlock.Text = $"Filtered: {visibleRows:N0}";
                IsVisible = true;
            }
            else
            {
                IsVisible = false;
            }
        }
    }

    private sealed class PaginationWidget<TData> : UserControl
    {
        private readonly SaGrid<TData> _grid;
        private readonly StackPanel _container;

        public PaginationWidget(SaGrid<TData> grid)
        {
            _grid = grid;
            _container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4)
            };

            Content = _container;
            UpdateDisplay();

            // Note: For proper implementation, we would need to hook into the grid's state change notification
            // For now, we'll rely on the status bar service to refresh widgets when needed
        }

        private void UpdateDisplay()
        {
            _container.Children.Clear();

            if (!_grid.Options.EnablePagination)
            {
                IsVisible = false;
                return;
            }

            IsVisible = true;

            var pageIndex = _grid.State.Pagination?.PageIndex ?? 0;
            var pageSize = _grid.State.Pagination?.PageSize ?? 10;
            var totalRows = _grid.PrePaginationRowModel.Rows.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRows / pageSize));
            var currentPage = pageIndex + 1; // Convert to 1-based

            // Page info text
            var pageInfo = new TextBlock
            {
                Text = $"Page {currentPage} of {totalPages}",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12
            };
            _container.Children.Add(pageInfo);

            // Previous button
            var previousBtn = new Button
            {
                Content = "◀",
                IsEnabled = pageIndex > 0,
                Padding = new Thickness(8, 2),
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 10
            };
            previousBtn.Click += (s, e) =>
            {
                var newIndex = Math.Max(0, pageIndex - 1);
                _grid.SetPageIndex(newIndex);
                UpdateDisplay(); // Refresh the display
            };
            _container.Children.Add(previousBtn);

            // Next button
            var nextBtn = new Button
            {
                Content = "▶",
                IsEnabled = currentPage < totalPages,
                Padding = new Thickness(8, 2),
                FontSize = 10
            };
            nextBtn.Click += (s, e) =>
            {
                var newIndex = Math.Min(totalPages - 1, pageIndex + 1);
                _grid.SetPageIndex(newIndex);
                UpdateDisplay(); // Refresh the display
            };
            _container.Children.Add(nextBtn);
        }

        public void RefreshDisplay()
        {
            UpdateDisplay();
        }
    }
}