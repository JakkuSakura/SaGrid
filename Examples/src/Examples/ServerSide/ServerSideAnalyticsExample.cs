using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Examples.Models;
using SaGrid;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Advanced.Modules.StatusBar;
using SaGrid.Advanced.RowModel;
using SaGrid.Core;

namespace Examples.Examples.ServerSide;

internal sealed class ServerSideAnalyticsExample : IExample
{
    private const string ColumnPanelId = "columnManager";

    public string Name => "Server-Side Analytics Demo";
    public string Description => "Enterprise-style configuration with server-side row model, side bar, status bar, and analytics tooling.";

    public ExampleHost Create()
    {
        var people = ExampleData.GenerateLargeDataset(1500); // TODO: investigate virtualization performance for larger data sets
        var dataSource = new InMemoryPersonServerDataSource(people, blockSize: 150);
        var columns = ExampleData.CreateDefaultColumns();

        Action<TableState<Person>>? onStateChange = null;

        var options = new TableOptions<Person>
        {
            Data = Array.Empty<Person>(),
            Columns = columns,
            EnableSorting = true,
            EnableGlobalFilter = true,
            EnableColumnFilters = true,
            EnablePagination = true,
            EnableRowSelection = true,
            EnableCellSelection = true,
            EnableColumnResizing = true,
            Meta = new Dictionary<string, object>
            {
                ["rowModelType"] = RowModelType.ServerSide,
                ["serverSideBlockSize"] = dataSource.BlockSize
            },
            OnStateChange = state => onStateChange?.Invoke(state),
            State = new TableState<Person>
            {
                Pagination = new PaginationState { PageIndex = 0, PageSize = 15 }
            }
        };

        var grid = new SaGrid<Person>(options);
        grid.SetServerSideDataSource(dataSource);
        ConfigureAdvancedFeatures(grid);
        Action refresh = null!;

        var sideBarService = grid.GetSideBarService();
        sideBarService.StateChanged += OnSideBarStateChanged;
        grid.RowDataChanged += OnRowDataChanged;

        var infoText = new TextBlock
        {
            Text = "Loading row statistics…",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(20, 10),
            FontSize = 14
        };

        ControlsPanelContext controlsContext = default;

        refresh = () =>
        {
            infoText.Text = BuildInfoText(grid, dataSource);
            UpdateControlButtons(grid, controlsContext);
        };

        onStateChange = _ => Dispatcher.UIThread.Post(refresh);

        controlsContext = BuildControlsPanel(grid, refresh);
        var controls = controlsContext.Panel;

        var sideBarHost = new SideBarHost();
        sideBarHost.Initialize(sideBarService, grid);

        var statusBarHost = new StatusBarHost();
        statusBarHost.Initialize(grid.GetStatusBarService(), grid);

        var gridComponent = new SaGridComponent<Person>(grid);

        var tableArea = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(20, 10, 20, 20)
        };

        tableArea.Children.Add(sideBarHost);
        Grid.SetColumn(sideBarHost, 0);
        Grid.SetRow(sideBarHost, 0);
        Grid.SetRowSpan(sideBarHost, 2);

        tableArea.Children.Add(gridComponent);
        Grid.SetColumn(gridComponent, 1);
        Grid.SetRow(gridComponent, 0);

        tableArea.Children.Add(statusBarHost);
        Grid.SetColumn(statusBarHost, 1);
        Grid.SetRow(statusBarHost, 1);

        var layout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new TextBlock
                {
                    Text = "Server-Side Analytics Demo",
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(20, 10)
                },
                controls,
                infoText,
                tableArea
            }
        };

        refresh();

        return new ExampleHost(new ScrollViewer { Content = layout }, Cleanup);

        void OnRowDataChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(refresh);
        }

        void OnSideBarStateChanged(object? sender, SideBarChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Grid, grid))
            {
                return;
            }

            Dispatcher.UIThread.Post(refresh);
        }

        void Cleanup()
        {
            grid.RowDataChanged -= OnRowDataChanged;
            sideBarService.StateChanged -= OnSideBarStateChanged;
        }
    }

    private static ControlsPanelContext BuildControlsPanel(SaGrid<Person> grid, Action refresh)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(20, 0, 20, 10)
        };

        var buttonRow = new WrapPanel();

        var multiSort = CreateButton(() => { grid.ToggleMultiSortOverride(); refresh(); });
        var resetFilters = CreateButton(() => { grid.ClearGlobalFilter(); grid.ClearColumnFilters(); refresh(); });
        var resetSorting = CreateButton(() => { grid.SetSorting(Array.Empty<ColumnSort>()); refresh(); });
        var toggleSideBar = CreateButton(() => { grid.ToggleSideBarVisible(); refresh(); });
        var toggleStatusBar = CreateButton(() => { grid.ToggleStatusBarVisible(); refresh(); });
        var openColumnsPanel = CreateButton(() => OpenColumnsPanel(grid));

        buttonRow.Children.Add(multiSort);
        buttonRow.Children.Add(resetFilters);
        buttonRow.Children.Add(resetSorting);
        buttonRow.Children.Add(toggleSideBar);
        buttonRow.Children.Add(toggleStatusBar);
        buttonRow.Children.Add(openColumnsPanel);

        panel.Children.Add(new TextBlock
        {
            Text = "Quick Controls",
            FontWeight = FontWeight.Bold,
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 6)
        });

        panel.Children.Add(buttonRow);
        return new ControlsPanelContext(panel, multiSort, resetFilters, resetSorting, toggleSideBar, toggleStatusBar, openColumnsPanel);

        static Button CreateButton(Action action)
        {
            var button = new Button
            {
                Padding = new Thickness(12, 6),
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Content = ""
            };
            button.Click += (_, _) => action();
            return button;
        }
    }

    private static void UpdateControlButtons(SaGrid<Person> grid, ControlsPanelContext context)
    {
        var filterCount = grid.State.ColumnFilters?.Filters.Count ?? 0;
        var sortCount = grid.State.Sorting?.Columns.Count ?? 0;

        context.MultiSort.Content = $"⇅ Multi-Sort: {(grid.IsMultiSortEnabled() ? "ON" : "OFF")}";
        context.ResetFilters.Content = $"🧹 Reset Filters ({filterCount})";
        context.ResetSorting.Content = $"↕️ Reset Sorting ({sortCount})";
        context.ToggleSideBar.Content = $"☰ Side Bar: {(grid.IsSideBarVisible() ? "Shown" : "Hidden")}";
        context.ToggleStatusBar.Content = $"📊 Status Bar: {(grid.IsStatusBarVisible() ? "Shown" : "Hidden")}";
        context.ColumnsPanel.Content = "📋 Open Columns Panel";
    }

    private static string BuildInfoText(SaGrid<Person> grid, InMemoryPersonServerDataSource dataSource)
    {
        var approxRows = grid.GetApproximateRowCount();
        var loadedRows = dataSource.LoadedRowCount;
        var totalColumns = grid.AllLeafColumns.Count;
        var visibleColumns = grid.VisibleLeafColumns.Count;
        var hasGlobalFilter = grid.State.GlobalFilter != null;
        var hasColumnFilters = grid.State.ColumnFilters?.Filters.Count > 0;
        var multiSort = grid.IsMultiSortEnabled() ? "ON" : "OFF";
        var sideBarState = grid.IsSideBarVisible() ? "Visible" : "Hidden";
        var statusBarState = grid.IsStatusBarVisible() ? "Visible" : "Hidden";
        var activePanel = grid.GetOpenedToolPanel() ?? "None";

        var selectedCells = grid.GetSelectedCells();
        var activeCell = grid.GetActiveCell();
        var selectionInfo = selectedCells.Count > 0
            ? $"Selected: {selectedCells.Count} cells"
            : "No selection";

        if (activeCell != null)
        {
            selectionInfo += $" | Active: ({activeCell.Value.RowIndex},{activeCell.Value.ColumnId})";
        }

        return $"📊 Rows: ~{approxRows} (loaded {loadedRows}) | Columns: {visibleColumns}/{totalColumns} | Multi-Sort: {multiSort}\n" +
               $"Filters: Global {(hasGlobalFilter ? "✅" : "❌")}, Column {(hasColumnFilters ? "✅" : "❌")} | Side Bar: {sideBarState} (Panel: {activePanel}) | Status Bar: {statusBarState} | {selectionInfo}";
    }

    private static void OpenColumnsPanel(SaGrid<Person> grid)
    {
        if (string.Equals(grid.GetOpenedToolPanel(), ColumnPanelId, StringComparison.OrdinalIgnoreCase))
        {
            grid.CloseToolPanel();
        }
        else
        {
            grid.OpenToolPanel(ColumnPanelId);
            grid.SetSideBarVisible(true);
        }
    }

    private static void ConfigureAdvancedFeatures(SaGrid<Person> grid)
    {
        grid.AddRowAction("edit", "Edit", row =>
        {
            Debug.WriteLine($"Edit clicked for {row.Original.FirstName} {row.Original.LastName}");
        });

        grid.AddRowAction("delete", "Delete", row =>
        {
            Debug.WriteLine($"Delete clicked for {row.Original.FirstName} {row.Original.LastName}");
        });

        grid.SetHeaderRenderer("age", _ => "📅 Age");
        grid.SetHeaderRenderer("department", _ => "🏢 Dept");
        grid.SetHeaderRenderer("isActive", _ => "✅ Status");

        grid.SetCellRenderer((row, columnId) => columnId switch
        {
            "isActive" => row.Original.IsActive ? "✅ Active" : "❌ Inactive",
            "age" => $"{row.Original.Age} years",
            "department" => $"[{row.Original.Department}]",
            _ => row.GetCell(columnId).Value?.ToString() ?? string.Empty
        });
    }

    private sealed class InMemoryPersonServerDataSource : IServerSideDataSource<Person>
    {
        private readonly List<Person> _rows;
        private readonly int _blockSize;
        private readonly HashSet<int> _loadedIndexes = new();
        private readonly object _sync = new();

        public InMemoryPersonServerDataSource(IEnumerable<Person> rows, int blockSize)
        {
            _rows = rows.ToList();
            _blockSize = Math.Max(1, blockSize);
        }

        public int BlockSize => _blockSize;

        public int LoadedRowCount
        {
            get
            {
                lock (_sync)
                {
                    return _loadedIndexes.Count;
                }
            }
        }

        public async Task<ServerSideRowsResult<Person>> GetRowsAsync(ServerSideRowsRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(120, cancellationToken);

            IEnumerable<Person> query = _rows;

            if (request.FilterModel.TryGetValue("__global", out var globalFilter) &&
                globalFilter is string term && !string.IsNullOrWhiteSpace(term))
            {
                var lowered = term.Trim().ToLowerInvariant();
                query = query.Where(p => p.FirstName.ToLowerInvariant().Contains(lowered)
                                         || p.LastName.ToLowerInvariant().Contains(lowered)
                                         || p.Email.ToLowerInvariant().Contains(lowered)
                                         || p.Department.ToLowerInvariant().Contains(lowered));
            }

            query = ApplyColumnFilters(query, request.FilterModel);
            query = ApplySorts(query, request.SortModel);

            var filtered = query.ToList();
            var start = Math.Clamp(request.StartRow, 0, filtered.Count);
            var end = Math.Clamp(request.EndRow, start, filtered.Count);
            var rows = filtered.Skip(start).Take(end - start).ToList();

            lock (_sync)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    _loadedIndexes.Add(start + i);
                }
            }

            var lastRow = end >= filtered.Count ? filtered.Count : (int?)null;
            return new ServerSideRowsResult<Person>(rows, lastRow);
        }

        private static IEnumerable<Person> ApplyColumnFilters(IEnumerable<Person> source, IReadOnlyDictionary<string, object?> filters)
        {
            if (filters == null)
            {
                return source;
            }

            IEnumerable<Person> query = source;

            foreach (var filter in filters)
            {
                if (string.Equals(filter.Key, "__global", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (filter.Value is null)
                {
                    continue;
                }

                var acceptedValues = ExtractFilterValues(filter.Value);
                if (acceptedValues == null || acceptedValues.Count == 0)
                {
                    continue;
                }

                query = query.Where(row => acceptedValues.Contains(GetFieldValue(row, filter.Key)?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase));
            }

            return query;
        }

        private static IReadOnlyCollection<string>? ExtractFilterValues(object value)
        {
            return value switch
            {
                string s => new[] { s },
                bool b => new[] { b.ToString() },
                IEnumerable<string> enumerable => enumerable.ToList(),
                IEnumerable<object?> objects => objects.Select(o => o?.ToString() ?? string.Empty).ToList(),
                _ => null
            };
        }

        private static IEnumerable<Person> ApplySorts(IEnumerable<Person> source, IReadOnlyList<ColumnSort> sortModel)
        {
            if (sortModel == null || sortModel.Count == 0)
            {
                return source;
            }

            IOrderedEnumerable<Person>? ordered = null;

            foreach (var sort in sortModel)
            {
                Func<Person, object?> keySelector = row => GetFieldValue(row, sort.Id);

                ordered = ordered == null
                    ? (sort.Direction == SortDirection.Descending
                        ? source.OrderByDescending(keySelector)
                        : source.OrderBy(keySelector))
                    : (sort.Direction == SortDirection.Descending
                        ? ordered.ThenByDescending(keySelector)
                        : ordered.ThenBy(keySelector));
            }

            return ordered ?? source;
        }

        private static object? GetFieldValue(Person row, string columnId)
        {
            return columnId switch
            {
                "id" => row.Id,
                "firstName" => row.FirstName,
                "lastName" => row.LastName,
                "age" => row.Age,
                "email" => row.Email,
                "department" => row.Department,
                "isActive" => row.IsActive,
                _ => null
            };
        }
    }

    private sealed record ControlsPanelContext(
        StackPanel Panel,
        Button MultiSort,
        Button ResetFilters,
        Button ResetSorting,
        Button ToggleSideBar,
        Button ToggleStatusBar,
        Button ColumnsPanel);
}
