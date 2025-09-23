using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SaGrid;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.SideBar;

public static class SideBarDefaultPanels
{
    public const string ColumnManagerId = "columnManager";
    public const string FilterPanelId = "filterPanel";
    public const string InfoPanelId = "infoPanel";

    public static IReadOnlyList<SideBarPanelDefinition> CreateDefaultPanels<TData>(SaGrid<TData> grid)
    {
        return new List<SideBarPanelDefinition>
        {
            new SideBarPanelDefinition(ColumnManagerId, "Columns", () => new ColumnManagerView<TData>(grid)),
            new SideBarPanelDefinition(FilterPanelId, "Filters", () => new FilterPanelView<TData>(grid)),
            new SideBarPanelDefinition(InfoPanelId, "Info", () => new InfoPanelView<TData>(grid))
        };
    }

    private sealed class ColumnManagerView<TData> : UserControl
    {
        private readonly SaGrid<TData> _grid;
        private readonly StackPanel _container;

        public ColumnManagerView(SaGrid<TData> grid)
        {
            _grid = grid;
            _container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(12)
            };

            Content = new ScrollViewer
            {
                Content = _container
            };

            Build();
        }

        private void Build()
        {
            _container.Children.Clear();

            _container.Children.Add(new TextBlock
            {
                Text = "Column Visibility",
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            foreach (var column in _grid.AllLeafColumns)
            {
                var headerText = column.ColumnDef.Header?.ToString() ?? column.Id;
                var checkBox = new CheckBox
                {
                    Content = headerText,
                    IsChecked = _grid.GetColumnVisibility(column.Id),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                checkBox.Checked += (_, _) => _grid.SetColumnVisibility(column.Id, true);
                checkBox.Unchecked += (_, _) => _grid.SetColumnVisibility(column.Id, false);

                _container.Children.Add(checkBox);
            }

            if (!_grid.AllLeafColumns.Any())
            {
                _container.Children.Add(new TextBlock
                {
                    Text = "No columns available.",
                    FontStyle = FontStyle.Italic
                });
            }
        }
    }

    private sealed class FilterPanelView<TData> : UserControl
    {
        private readonly SaGrid<TData> _grid;
        private readonly StackPanel _container;

        public FilterPanelView(SaGrid<TData> grid)
        {
            _grid = grid;
            _container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(12)
            };

            var scroll = new ScrollViewer { Content = _container };
            Content = scroll;

            Build();
        }

        private void Build()
        {
            _container.Children.Clear();

            _container.Children.Add(new TextBlock
            {
                Text = "Active Filters",
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var filters = _grid.State.ColumnFilters?.Filters ?? new List<ColumnFilter>();
            if (filters.Count == 0)
            {
                _container.Children.Add(new TextBlock
                {
                    Text = "No column filters applied.",
                    FontStyle = FontStyle.Italic
                });
            }
            else
            {
                foreach (var filter in filters)
                {
                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 0, 4)
                    };

                    panel.Children.Add(new TextBlock
                    {
                        Text = filter.Id,
                        FontWeight = FontWeight.SemiBold,
                        Margin = new Thickness(0, 0, 8, 0)
                    });

                    panel.Children.Add(new TextBlock
                    {
                        Text = filter.Value?.ToString() ?? string.Empty,
                        TextWrapping = TextWrapping.Wrap
                    });

                    var clearButton = new Button
                    {
                        Content = "Clear",
                        Margin = new Thickness(8, 0, 0, 0),
                        Padding = new Thickness(6, 2)
                    };

                    clearButton.Click += (_, _) =>
                    {
                        _grid.ClearColumnFilter(filter.Id);
                    };

                    panel.Children.Add(clearButton);
                    _container.Children.Add(panel);
                }
            }

            var clearAllButton = new Button
            {
                Content = "Clear All Filters",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            clearAllButton.Click += (_, _) =>
            {
                _grid.ClearColumnFilters();
                _grid.ClearGlobalFilter();
            };

            _container.Children.Add(clearAllButton);
        }
    }

    private sealed class InfoPanelView<TData> : UserControl
    {
        public InfoPanelView(SaGrid<TData> grid)
        {
            var content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(12),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Grid Information",
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    new TextBlock
                    {
                        Text = $"Rows: {grid.RowModel.Rows.Count}",
                        Margin = new Thickness(0, 0, 0, 2)
                    },
                    new TextBlock
                    {
                        Text = $"Visible Columns: {grid.VisibleLeafColumns.Count}",
                        Margin = new Thickness(0, 0, 0, 2)
                    },
                    new TextBlock
                    {
                        Text = $"Hidden Columns: {grid.GetHiddenColumnCount()}",
                        Margin = new Thickness(0, 0, 0, 2)
                    },
                    new TextBlock
                    {
                        Text = $"Selected Cells: {grid.GetSelectedCellCount()}",
                        Margin = new Thickness(0, 0, 0, 2)
                    }
                }
            };

            Content = content;
        }
    }
}
