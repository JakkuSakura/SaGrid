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
    public const string InfoPanelId = "infoPanel";

    public static IReadOnlyList<SideBarPanelDefinition> CreateDefaultPanels<TData>(SaGrid<TData> grid)
    {
        return new List<SideBarPanelDefinition>
        {
            new SideBarPanelDefinition(ColumnManagerId, "Columns", () => new ColumnManagerView<TData>(grid)),
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
