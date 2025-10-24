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
using SaGrid.Advanced;
using SaGrid.Advanced.Interfaces;
// using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Advanced.Modules.StatusBar;
using SaGrid.Advanced.RowModel;
using SaGrid.Core;
using SaGrid.Core.Models;

namespace Examples.Examples.ServerSide;

internal sealed class ServerSideAnalyticsExample : IExample
{
    private const string ColumnPanelId = "columnManager";

    public string Name => "Server-side Analytics (Server Data)";
    public string Description => "Server-driven row model with infinite scroll and status bar.";

    public ExampleHost Create()
    {
        var people = ExampleData.GenerateLargeDataset(1500); // TODO: investigate virtualization performance for larger data sets
        var columns = ExampleData.CreateDefaultColumns();
        var dataSource = new InMemoryPersonServerDataSource(people, columns, blockSize: 150);

        Action<TableState<Person>>? onStateChange = null;

        var options = new TableOptions<Person>
        {
            Data = Array.Empty<Person>(),
            Columns = columns,
            EnableSorting = true,
            EnableGlobalFilter = true,
            EnableColumnFilters = true,
            EnablePagination = false,
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
                // Auto-fit star columns to viewport like other examples
                ColumnSizing = new ColumnSizingState()
            }
        };

        var grid = new SaGrid<Person>(options);
        grid.SetServerSideDataSource(dataSource);
        ConfigureAdvancedFeatures(grid);
        Action refresh = null!;

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

        var gridComponent = grid.Component;
        var tableArea = new Grid
        {
            RowDefinitions = new RowDefinitions("*")
        };
        Grid.SetRow(gridComponent, 0);
        tableArea.Children.Add(gridComponent);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 8
        };

        var title = new TextBlock
        {
            Text = "Server-side Analytics",
            FontSize = 18,
            FontWeight = FontWeight.Bold
        };
        layout.Children.Add(title);

        Grid.SetRow(controls, 2);
        layout.Children.Add(controls);

        // Instructions in row 1 to match client layout
        Grid.SetRow(infoText, 1);
        layout.Children.Add(infoText);

        // Wrap the table area in a border like the client layout (row 3 star)
        var gridHost = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Background = Brushes.White,
            Child = tableArea
        };
        Grid.SetRow(gridHost, 3);
        layout.Children.Add(gridHost);

        refresh();

        return new ExampleHost(layout, Cleanup);

        void OnRowDataChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(refresh);
        }

        void Cleanup()
        {
            grid.RowDataChanged -= OnRowDataChanged;
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

        var resetFilters = CreateButton(() => { grid.ClearGlobalFilter(); grid.ClearColumnFilters(); refresh(); });
        var resetSorting = CreateButton(() => { grid.SetSorting(Array.Empty<ColumnSort>()); refresh(); });

        buttonRow.Children.Add(resetFilters);
        buttonRow.Children.Add(resetSorting);

        panel.Children.Add(new TextBlock
        {
            Text = "Quick Controls",
            FontWeight = FontWeight.Bold,
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 6)
        });

        panel.Children.Add(buttonRow);
        return new ControlsPanelContext(panel, resetFilters, resetSorting);

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

        context.ResetFilters.Content = $"🧹 Reset Filters ({filterCount})";
        context.ResetSorting.Content = $"↕️ Reset Sorting ({sortCount})";
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
               $"Filters: Global {(hasGlobalFilter ? "✅" : "❌")}, Column {(hasColumnFilters ? "✅" : "❌")} | {selectionInfo}";
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
        private readonly string[] _depts = { "Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "Support" };
        private readonly IReadOnlyList<ColumnDef<Person>> _columns;

        public InMemoryPersonServerDataSource(IEnumerable<Person> rows, IReadOnlyList<ColumnDef<Person>> columns, int blockSize)
        {
            _rows = rows.ToList();
            _columns = columns;
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

        private void EnsureRows(int count)
        {
            lock (_sync)
            {
                while (_rows.Count < count)
                {
                    var i = _rows.Count + 1;
                    var dept = _depts[i % _depts.Length];
                    var first = $"User{i}";
                    var last = $"Demo{i}";
                    var age = 20 + (i % 45);
                    var email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.com";
                    var isActive = (i % 3) != 0;
                    _rows.Add(new Person(i, first, last, age, email, dept, isActive));
                }
            }
        }

        public async Task<ServerSideRowsResult<Person>> GetRowsAsync(ServerSideRowsRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(120, cancellationToken);

            // Grow backing data to cover the requested window for infinite scrolling
            var start = Math.Max(0, request.StartRow);
            var end = Math.Max(start, request.EndRow);
            EnsureRows(end);

            // Use core Table to compute filtered/sorted rows for exact parity with client pipeline
            var state = BuildStateFromRequest(request);
            var options = new TableOptions<Person>
            {
                Data = _rows,
                Columns = _columns,
                EnableSorting = true,
                EnableGlobalFilter = true,
                EnableColumnFilters = true,
                State = state
            };

            var table = new Table<Person>(options);
            var filtered = table.RowModel.FlatRows.Select(r => r.Original).ToList();
            start = Math.Clamp(start, 0, filtered.Count);
            end = Math.Clamp(end, start, filtered.Count);
            var rows = filtered.Skip(start).Take(Math.Max(0, end - start)).ToList();

            lock (_sync)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    _loadedIndexes.Add(start + i);
                }
            }

            // Infinite scrolling: never set lastRow to force unbounded scroll in both directions
            return new ServerSideRowsResult<Person>(rows, null);
        }

        private static TableState<Person> BuildStateFromRequest(ServerSideRowsRequest request)
        {
            var filters = new List<ColumnFilter>();
            object? global = null;
            foreach (var kv in request.FilterModel)
            {
                if (string.Equals(kv.Key, "__global", StringComparison.OrdinalIgnoreCase))
                {
                    global = kv.Value;
                }
                else
                {
                    filters.Add(new ColumnFilter(kv.Key, kv.Value));
                }
            }

            var sorting = new SortingState(request.SortModel?.ToList() ?? new List<ColumnSort>());
            var columnFilters = filters.Count > 0 ? new ColumnFiltersState(filters) : null;
            var globalFilter = global != null ? new GlobalFilterState(global) : null;

            return new TableState<Person>
            {
                Sorting = sorting,
                ColumnFilters = columnFilters,
                GlobalFilter = globalFilter
            };
        }
    }

    private sealed record ControlsPanelContext(
        StackPanel Panel,
        Button ResetFilters,
        Button ResetSorting);
}
