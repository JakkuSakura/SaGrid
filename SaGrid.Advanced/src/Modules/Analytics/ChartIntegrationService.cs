using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Analytics;

internal sealed class ChartIntegrationService : IChartIntegrationService
{
    private const string DefaultContextMenuId = "charts.quickCreate";

    private readonly IEventService _eventService;
    private readonly ConditionalWeakTable<object, ChartAttachment> _attachments = new();

    public ChartIntegrationService(IEventService eventService)
    {
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    public void AttachToGrid<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        var attachment = _attachments.GetValue(grid, _ => new ChartAttachment());
        if (attachment.ContextMenuAttached)
        {
            return;
        }

        EnsureContextMenu(grid, attachment);
    }

    public ChartRequest BuildDefaultRequest<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        var categoryColumn = FindDefaultCategoryColumn(grid);
        var valueColumns = FindNumericColumns(grid).Select(c => c.Id).Take(4).ToList();

        if (valueColumns.Count == 0 && categoryColumn != null)
        {
            valueColumns = grid.VisibleLeafColumns
                .Where(c => !string.Equals(c.Id, categoryColumn.Id, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id)
                .Take(1)
                .ToList();
        }

        var chartType = valueColumns.Count == 1 ? ChartType.Column : ChartType.Column;
        var categoryId = categoryColumn?.Id ?? grid.VisibleLeafColumns.FirstOrDefault()?.Id ?? "index";

        return new ChartRequest(chartType, categoryId, new ReadOnlyCollection<string>(valueColumns), IncludeLeafRows: true, IncludeGroupRows: true);
    }

    public ChartData BuildChartData<TData>(SaGrid<TData> grid, ChartRequest request)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var rows = ResolveSourceRows(grid, request).ToList();
        if (rows.Count == 0)
        {
            return new ChartData(request.ChartType, Array.Empty<string>(), Array.Empty<ChartSeries>(), "No data", null);
        }

        var categories = new List<string>();
        var seriesResults = request.ValueColumnIds
            .Select(id => new ChartSeriesBuilder(id, ResolveSeriesName(grid, id), rows.Count))
            .ToList();

        foreach (var row in rows)
        {
            var categoryValue = ExtractCellDisplay(row, request.CategoryColumnId);
            categories.Add(categoryValue);

            foreach (var builder in seriesResults)
            {
                var numeric = ExtractNumericValue(row, builder.ColumnId);
                builder.AddPoint(numeric);
            }
        }

        var series = seriesResults
            .Where(s => s.HasAnyData)
            .Select(s => s.Build())
            .ToList();

        var title = categories.Count > 0 ? $"{request.ChartType} Chart" : "Chart";
        var subtitle = $"{series.Count} series · {categories.Count} categories";

        return new ChartData(request.ChartType, new ReadOnlyCollection<string>(categories), new ReadOnlyCollection<ChartSeries>(series), title, subtitle);
    }

    public bool ShowChart<TData>(SaGrid<TData> grid, ChartRequest request)
    {
        var data = BuildChartData(grid, request);
        if (!data.HasData)
        {
            return false;
        }

        _eventService.DispatchEvent(GridEventTypes.ChartCreated, new ChartCreatedEventArgs(grid, data, request));

        Dispatcher.UIThread.Post(() => PresentChartWindow(data));
        return true;
    }

    public bool TryShowDefaultChart<TData>(SaGrid<TData> grid)
    {
        var request = BuildDefaultRequest(grid);
        if (request.ValueColumnIds.Count == 0)
        {
            return false;
        }

        return ShowChart(grid, request);
    }

    private void EnsureContextMenu<TData>(SaGrid<TData> grid, ChartAttachment attachment)
    {
        var request = BuildDefaultRequest(grid);
        if (request.ValueColumnIds.Count == 0)
        {
            return;
        }

        var existing = grid.GetContextMenuItems().ToList();
        if (existing.Any(item => string.Equals(item.Id, DefaultContextMenuId, StringComparison.OrdinalIgnoreCase)))
        {
            attachment.ContextMenuAttached = true;
            return;
        }

        existing.Add(new ContextMenuItem
        {
            Id = DefaultContextMenuId,
            Label = "Create Quick Chart",
            Action = _ => TryShowDefaultChart(grid)
        });

        grid.SetContextMenuItems(existing);
        attachment.ContextMenuAttached = true;
    }

    private void PresentChartWindow(ChartData data)
    {
        var window = new ChartHostWindow(data)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var lifetime = Application.Current?.ApplicationLifetime;
        if (lifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.Icon = desktop.MainWindow?.Icon;
            window.Show(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }

    private static Column<TData>? FindDefaultCategoryColumn<TData>(SaGrid<TData> grid)
    {
        foreach (var column in grid.VisibleLeafColumns)
        {
            if (!IsNumericColumn(grid, column))
            {
                return column;
            }
        }

        return grid.VisibleLeafColumns.FirstOrDefault();
    }

    private static IEnumerable<Column<TData>> FindNumericColumns<TData>(SaGrid<TData> grid)
    {
        return grid.VisibleLeafColumns.Where(column => IsNumericColumn(grid, column));
    }

    private static bool IsNumericColumn<TData>(SaGrid<TData> grid, Column<TData> column)
    {
        foreach (var row in grid.RowModel.FlatRows.OfType<Row<TData>>())
        {
            if (row.IsGroupRow)
            {
                if (row.TryGetAggregatedValue(column.Id, out var aggregated) && TryConvertToDouble(aggregated, out _))
                {
                    return true;
                }

                continue;
            }

            var value = row.GetCell(column.Id).Value;
            if (TryConvertToDouble(value, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Row<TData>> ResolveSourceRows<TData>(SaGrid<TData> grid, ChartRequest request)
    {
        var flatRows = grid.RowModel.FlatRows.OfType<Row<TData>>();
        var grouping = grid.State.Grouping?.Groups ?? new List<string>();

        if (grouping.Count > 0 && request.IncludeGroupRows)
        {
            var targetGroupId = grouping.Last();
            var grouped = flatRows.Where(row => row.IsGroupRow && string.Equals(row.GroupColumnId, targetGroupId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (grouped.Count > 0)
            {
                return grouped;
            }
        }

        if (!request.IncludeLeafRows)
        {
            return Array.Empty<Row<TData>>();
        }

        return flatRows.Where(row => !row.IsGroupRow);
    }

    private static string ResolveSeriesName<TData>(SaGrid<TData> grid, string columnId)
    {
        var column = grid.AllLeafColumns.FirstOrDefault(c => string.Equals(c.Id, columnId, StringComparison.OrdinalIgnoreCase));
        return column?.ColumnDef.Header?.ToString() ?? columnId;
    }

    private static string ExtractCellDisplay<TData>(Row<TData> row, string columnId)
    {
        try
        {
            var cell = row.GetCell(columnId);
            return cell.Value?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static double ExtractNumericValue<TData>(Row<TData> row, string columnId)
    {
        if (row.TryGetAggregatedValue(columnId, out var aggregated) && TryConvertToDouble(aggregated, out var aggregatedNumber))
        {
            return aggregatedNumber;
        }

        var cell = row.GetCell(columnId);
        return TryConvertToDouble(cell.Value, out var value) ? value : 0d;
    }

    private static bool TryConvertToDouble(object? value, out double number)
    {
        switch (value)
        {
            case null:
                number = 0;
                return false;
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case decimal m:
                number = (double)m;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case short s:
                number = s;
                return true;
            case byte b:
                number = b;
                return true;
            case string text when double.TryParse(text, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private sealed class ChartAttachment
    {
        public bool ContextMenuAttached;
    }

    private sealed class ChartSeriesBuilder
    {
        private readonly List<double> _points;

        public string ColumnId { get; }
        public string DisplayName { get; }

        public bool HasAnyData { get; private set; }

        public ChartSeriesBuilder(string columnId, string displayName, int capacity)
        {
            ColumnId = columnId;
            DisplayName = displayName;
            _points = new List<double>(capacity);
        }

        public void AddPoint(double value)
        {
            if (Math.Abs(value) > double.Epsilon)
            {
                HasAnyData = true;
            }

            _points.Add(value);
        }

        public ChartSeries Build()
        {
            return new ChartSeries(ColumnId, DisplayName, new ReadOnlyCollection<double>(_points));
        }
    }
}

internal sealed class ChartHostWindow : Window
{
    private static readonly IReadOnlyList<SolidColorBrush> Palette = new[]
    {
        new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0xF0)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x65)),
        new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84)),
        new SolidColorBrush(Color.FromRgb(0xBA, 0x68, 0xC8)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F)),
        new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE))
    };

    public ChartHostWindow(ChartData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        Title = data.Title ?? "Chart";
        Width = 720;
        Height = 480;
        Content = BuildContent(data);
    }

    private Control BuildContent(ChartData data)
    {
        var root = new DockPanel
        {
            Margin = new Thickness(12)
        };

        if (!string.IsNullOrWhiteSpace(data.Subtitle))
        {
            var subtitle = new TextBlock
            {
                Text = data.Subtitle,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = Brushes.Gray
            };
            DockPanel.SetDock(subtitle, Dock.Top);
            root.Children.Add(subtitle);
        }

        var legend = BuildLegend(data.Series);
        if (legend != null)
        {
            DockPanel.SetDock(legend, Dock.Top);
            root.Children.Add(legend);
        }

        var scrollViewer = new ScrollViewer
        {
            Content = BuildChartBody(data) ?? new TextBlock { Text = "No chart data available." },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        root.Children.Add(scrollViewer);
        return root;
    }

    private Control? BuildLegend(IReadOnlyList<ChartSeries> series)
    {
        if (series == null || series.Count == 0)
        {
            return null;
        }

        var legendPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 12)
        };

        for (var i = 0; i < series.Count; i++)
        {
            var brush = Palette[i % Palette.Count];
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            itemPanel.Children.Add(new Border
            {
                Background = brush,
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(2)
            });

            itemPanel.Children.Add(new TextBlock
            {
                Text = series[i].DisplayName,
                Foreground = Brushes.Gray
            });

            legendPanel.Children.Add(itemPanel);
        }

        return legendPanel;
    }

    private Control? BuildChartBody(ChartData data)
    {
        if (!data.HasData)
        {
            return null;
        }

        var maxValue = data.Series.SelectMany(s => s.Points).DefaultIfEmpty(0d).Max();
        if (Math.Abs(maxValue) < double.Epsilon)
        {
            maxValue = 1d;
        }

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12
        };

        for (var categoryIndex = 0; categoryIndex < data.Categories.Count; categoryIndex++)
        {
            stack.Children.Add(BuildCategoryRow(data, categoryIndex, maxValue));
        }

        return stack;
    }

    private Control BuildCategoryRow(ChartData data, int categoryIndex, double maxValue)
    {
        var border = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 8)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8
        };

        panel.Children.Add(new TextBlock
        {
            Text = data.Categories[categoryIndex],
            FontWeight = FontWeight.Bold
        });

        var barPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6
        };

        for (var seriesIndex = 0; seriesIndex < data.Series.Count; seriesIndex++)
        {
            var value = data.Series[seriesIndex].Points[categoryIndex];
            var width = Math.Max(6, (Math.Abs(value) / maxValue) * 320);
            var brush = Palette[seriesIndex % Palette.Count];

            var seriesStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2
            };

            seriesStack.Children.Add(new TextBlock
            {
                Text = data.Series[seriesIndex].DisplayName,
                FontSize = 12,
                Foreground = Brushes.Gray
            });

            seriesStack.Children.Add(new Border
            {
                Background = brush,
                Width = width,
                Height = 20,
                CornerRadius = new CornerRadius(3)
            });

            seriesStack.Children.Add(new TextBlock
            {
                Text = value.ToString("0.##"),
                FontSize = 11,
                Foreground = Brushes.Gray
            });

            barPanel.Children.Add(seriesStack);
        }

        panel.Children.Add(barPanel);
        border.Child = panel;
        return border;
    }
}
