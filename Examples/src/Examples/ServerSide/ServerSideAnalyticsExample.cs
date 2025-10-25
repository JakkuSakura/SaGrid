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
        var people = ExampleData.GenerateLargeDataset(1500);
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
                ["serverSideBlockSize"] = dataSource.BlockSize,
                ["debugFiltering"] = true
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
            infoText.Text = BuildInfoText(grid);
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

    private static string BuildInfoText(SaGrid<Person> grid)
    {
        var approxRows = grid.GetApproximateRowCount();
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

        return $"📊 Rows: ~{approxRows} | Columns: {visibleColumns}/{totalColumns} | Multi-Sort: {multiSort}\n" +
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

        public async Task<ServerSideRowsResult<Person>> GetRowsAsync(ServerSideRowsRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(120, cancellationToken);

            var start = Math.Max(0, request.StartRow);
            var end = Math.Max(start, request.EndRow);

            IEnumerable<Person> query = _rows;

            // Apply column filters
            foreach (var kv in request.FilterModel)
            {
                var key = kv.Key;
                var value = kv.Value;
                if (string.Equals(key, "__global", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // handle global later
                }

                if (value == null)
                {
                    continue;
                }

                if (value is IReadOnlyDictionary<string, object?> dict)
                {
                    // Numeric range
                    dict.TryGetValue("min", out var minObj);
                    dict.TryGetValue("max", out var maxObj);
                    query = ApplyRangeFilter(query, key, minObj, maxObj);
                }
                else
                {
                    query = ApplyColumnFilter(query, key, value);
                }
            }

            // Apply global filter (string contains across visible columns)
            if (request.FilterModel.TryGetValue("__global", out var globalValue))
            {
                var text = globalValue?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    query = query.Where(p => MatchesAnyVisible(p, text!, request.ColumnVisibilityMap));
                }
            }

            // Apply sorting (multi-sort)
            if (request.SortModel != null && request.SortModel.Count > 0)
            {
                IOrderedEnumerable<Person>? ordered = null;
                foreach (var sort in request.SortModel)
                {
                    Func<Person, object?> keySelector = sort.Id switch
                    {
                        "id" => p => p.Id,
                        "firstName" => p => p.FirstName,
                        "lastName" => p => p.LastName,
                        "age" => p => p.Age,
                        "email" => p => p.Email,
                        "department" => p => p.Department,
                        "isActive" => p => p.IsActive,
                        _ => p => null
                    };

                    if (ordered == null)
                    {
                        ordered = sort.Direction == SortDirection.Descending
                            ? query.OrderByDescending(keySelector)
                            : query.OrderBy(keySelector);
                    }
                    else
                    {
                        ordered = sort.Direction == SortDirection.Descending
                            ? ordered.ThenByDescending(keySelector)
                            : ordered.ThenBy(keySelector);
                    }
                }

                if (ordered != null)
                {
                    query = ordered;
                }
            }

            var materialized = query.ToList();
            var rowCount = materialized.Count;
            start = Math.Clamp(start, 0, rowCount);
            end = Math.Clamp(end, start, rowCount);
            var rows = materialized.Skip(start).Take(Math.Max(0, end - start)).ToList();

            return new ServerSideRowsResult<Person>(rows, rowCount);
        }

        private static IEnumerable<Person> ApplyRangeFilter(IEnumerable<Person> source, string columnId, object? minObj, object? maxObj)
        {
            double? min = TryToDouble(minObj);
            double? max = TryToDouble(maxObj);
            if (min == null && max == null) return source;

            return source.Where(p =>
            {
                double value = columnId switch
                {
                    "age" => p.Age,
                    "id" => p.Id,
                    _ => double.NaN
                };

                if (double.IsNaN(value)) return false;
                if (min.HasValue && value < min.Value) return false;
                if (max.HasValue && value > max.Value) return false;
                return true;
            });
        }

        private static IEnumerable<Person> ApplyColumnFilter(IEnumerable<Person> source, string columnId, object filterValue)
        {
            return source.Where(p =>
            {
                object? cell = columnId switch
                {
                    "id" => p.Id,
                    "firstName" => p.FirstName,
                    "lastName" => p.LastName,
                    "age" => p.Age,
                    "email" => p.Email,
                    "department" => p.Department,
                    "isActive" => p.IsActive,
                    _ => null
                };

                if (cell == null) return false;

                // Handle string filters with typed-equality + substring fallback
                if (filterValue is string s)
                {
                    var text = s.Trim();
                    if (text.Length == 0) return true;

                    if (cell is int ci && int.TryParse(text, out var pi)) return ci == pi;
                    if (cell is double cd && double.TryParse(text, out var pd)) return Math.Abs(cd - pd) < 0.000001;
                    if (cell is float cf && float.TryParse(text, out var pf)) return Math.Abs(cf - pf) < 0.000001f;
                    if (cell is decimal cz && decimal.TryParse(text, out var pz)) return cz == pz;
                    if (cell is bool cb && bool.TryParse(text, out var pb)) return cb == pb;

                    return cell.ToString()!.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                // Typed filters
                if (filterValue is bool fb && cell is bool cb2) return fb == cb2;
                if (filterValue is int fi && cell is int ci2) return fi == ci2;
                if (filterValue is double fd && cell is double cd2) return Math.Abs(fd - cd2) < 0.000001;

                return string.Equals(cell.ToString(), filterValue.ToString(), StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool MatchesAnyVisible(Person p, string text, IReadOnlyDictionary<string, bool>? visibility)
        {
            bool IsVisible(string id) => visibility == null || !visibility.TryGetValue(id, out var v) || v;

            if (IsVisible("id") && p.Id.ToString().Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsVisible("firstName") && p.FirstName.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsVisible("lastName") && p.LastName.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsVisible("age") && p.Age.ToString().Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsVisible("email") && p.Email.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsVisible("department") && p.Department.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsVisible("isActive") && p.IsActive.ToString().Contains(text, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static double? TryToDouble(object? o)
        {
            if (o == null) return null;
            var s = o.ToString();
            if (double.TryParse(s, out var d)) return d;
            return null;
        }

        // No quick filter evaluation – rely on Core global filter

        // No manual set-filter evaluation; Core handles it
    }

    private sealed record ControlsPanelContext(
        StackPanel Panel,
        Button ResetFilters,
        Button ResetSorting);
}
