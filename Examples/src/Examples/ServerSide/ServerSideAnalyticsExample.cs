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

namespace Examples.Examples.ServerSide;

internal sealed class ServerSideAnalyticsExample : IExample
{
    private const string ColumnPanelId = "columnManager";

    public string Name => "Server-side Analytics (Server Data)";
    public string Description => "Server-driven row model with infinite scroll and status bar.";

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

                // Text contains semantics for scalar string filter values
                if (filter.Value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    var term = s.Trim();
                    query = query.Where(row =>
                    {
                        var value = GetFieldValue(row, filter.Key);
                        return (value?.ToString() ?? string.Empty)
                            .IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
                    continue;
                }

                // Boolean equality
                if (filter.Value is bool b)
                {
                    query = query.Where(row =>
                    {
                        var value = GetFieldValue(row, filter.Key);
                        return value is bool vb && vb == b;
                    });
                    continue;
                }

                // Numeric range: support { min, max } object/dictionary
                if (filter.Value is IReadOnlyDictionary<string, object?> dict)
                {
                    double minParsed = 0, maxParsed = 0;
                    var hasMin = dict.TryGetValue("min", out var minObj) && TryToDouble(minObj, out minParsed);
                    var hasMax = dict.TryGetValue("max", out var maxObj) && TryToDouble(maxObj, out maxParsed);
                    if (hasMin || hasMax)
                    {
                        double? minVal = hasMin ? minParsed : null;
                        double? maxVal = hasMax ? maxParsed : null;
                        query = query.Where(row =>
                        {
                            var value = GetFieldValue(row, filter.Key);
                            if (!TryToDouble(value, out var v)) return false;
                            if (minVal.HasValue && v < minVal.Value) return false;
                            if (maxVal.HasValue && v > maxVal.Value) return false;
                            return true;
                        });
                        continue;
                    }
                }

                // Set membership (multi-select) semantics
                var acceptedValues = ExtractFilterValues(filter.Value);
                if (acceptedValues != null && acceptedValues.Count > 0)
                {
                    query = query.Where(row => acceptedValues.Contains(GetFieldValue(row, filter.Key)?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase));
                }
            }

            return query;
        }

        private static bool TryToDouble(object? value, out double result)
        {
            switch (value)
            {
                case null:
                    result = 0; return false;
                case double d:
                    result = d; return true;
                case float f:
                    result = f; return true;
                case int i:
                    result = i; return true;
                case long l:
                    result = l; return true;
                case string s when double.TryParse(s, out var parsed):
                    result = parsed; return true;
                default:
                    result = 0; return false;
            }
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
        Button ResetFilters,
        Button ResetSorting);
}
