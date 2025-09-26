using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Advanced.Modules.StatusBar;
using SaGrid.Advanced.RowModel;
using SaGrid.Core;
using SolidAvalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SolidAvalonia.Solid;

namespace Examples;

public record Person(int Id, string FirstName, string LastName, int Age, string Email, string Department, bool IsActive);


public class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
}

internal sealed class InMemoryPersonServerDataSource : IServerSideDataSource<Person>
{
    private readonly List<Person> _allRows;
    private readonly int _blockSize;
    private readonly HashSet<int> _loadedIndexes = new();
    private readonly object _sync = new();

    public InMemoryPersonServerDataSource(IEnumerable<Person> rows, int blockSize)
    {
        _allRows = rows.ToList();
        _blockSize = Math.Max(1, blockSize);
    }

    public int TotalCount => _allRows.Count;

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
        await Task.Delay(150, cancellationToken); // simulate latency

        IEnumerable<Person> query = _allRows;

        if (request.FilterModel.TryGetValue("__global", out var globalFilter) &&
            globalFilter is string term && !string.IsNullOrWhiteSpace(term))
        {
            query = ApplyGlobalFilter(query, term);
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

    private static IEnumerable<Person> ApplyGlobalFilter(IEnumerable<Person> source, string term)
    {
        var lowered = term.Trim().ToLowerInvariant();
        return source.Where(p => p.FirstName.ToLowerInvariant().Contains(lowered)
                                 || p.LastName.ToLowerInvariant().Contains(lowered)
                                 || p.Email.ToLowerInvariant().Contains(lowered)
                                 || p.Department.ToLowerInvariant().Contains(lowered));
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

    private void OnRowDataChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateInfoText);
    }

    private const string ColumnPanelId = "columnManager";

    private SaGrid<Person> saGrid = null!;
    private InMemoryPersonServerDataSource? _dataSource;
    private TextBlock infoTextBlock = null!;
    private Button _multiSortBtn = null!;
    private Button _resetFiltersBtn = null!;
    private Button _resetSortingBtn = null!;
    private Button _toggleSideBarBtn = null!;
    private Button _toggleStatusBarBtn = null!;
    private Button _openColumnsPanelBtn = null!;
    private SideBarService _sideBarService = null!;

    private void InitializeComponent()
    {
        // Window properties
        Title = "SaGrid.Advanced Advanced Table - Full Featured Demo";
        Width = 1400;
        Height = 900;

        // Generate larger sample data but keep it on the server-side datasource
        var allPeople = GenerateLargeDataset(5000).ToList();
        _dataSource = new InMemoryPersonServerDataSource(allPeople, blockSize: 200);

        // Define advanced columns with sorting and filtering capabilities
        var columns = new List<ColumnDef<Person>>
        {
            ColumnHelper.Accessor<Person, int>(accessorFn: p => p.Id, id: "id", header: "ID"),
            ColumnHelper.Accessor<Person, string>(accessorFn: p => p.FirstName, id: "firstName", header: "First Name"),
            ColumnHelper.Accessor<Person, string>(accessorFn: p => p.LastName, id: "lastName", header: "Last Name"),
            ColumnHelper.Accessor<Person, int>(accessorFn: p => p.Age, id: "age", header: "Age"),
            ColumnHelper.Accessor<Person, string>(accessorFn: p => p.Email, id: "email", header: "Email"),
            ColumnHelper.Accessor<Person, string>(accessorFn: p => p.Department, id: "department", header: "Department"),
            ColumnHelper.Accessor<Person, bool>(accessorFn: p => p.IsActive, id: "isActive", header: "Active")
        };

        // Create SaGrid.Advanced with advanced features enabled
        var options = new TableOptions<Person>
        {
            Data = Array.Empty<Person>(),
            Columns = columns.AsReadOnly(),
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
                ["serverSideBlockSize"] = 200
            },
            OnStateChange = state =>
            {
                // Keep info bar and control button labels in sync with state
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateInfoText();
                    UpdateControlButtons();
                });
            },
            State = new TableState<Person>
            {
                Pagination = new PaginationState { PageIndex = 0, PageSize = 10 }
            }
        };

        saGrid = new SaGrid<Person>(options);
        saGrid.RowDataChanged += OnRowDataChanged;
        _sideBarService = saGrid.GetSideBarService();
        _sideBarService.StateChanged += OnSideBarStateChanged;

        if (_dataSource != null)
        {
            saGrid.SetServerSideDataSource(_dataSource);
        }

        // Configure SaGrid.Advanced advanced features
        ConfigureSaGridFeatures();
        
        // Start with no programmatic filters; user can filter via headers

        // Create UI with advanced SaGrid.Advanced features
        var ui = CreateAdvancedUI();
        Content = ui;
        
        // Update info display
        UpdateInfoText();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_sideBarService != null)
        {
            _sideBarService.StateChanged -= OnSideBarStateChanged;
        }

        if (saGrid != null)
        {
            saGrid.RowDataChanged -= OnRowDataChanged;
        }
    }

    private IEnumerable<Person> GenerateLargeDataset(int count)
    {
        var random = new Random(42);
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "Support" };
        var firstNames = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry", "Jane", "John" };
        var lastNames = new[] { "Anderson", "Brown", "Davis", "Garcia", "Johnson", "Jones", "Miller", "Smith", "Taylor", "Williams" };

        for (int i = 1; i <= count; i++)
        {
            var firstName = firstNames[random.Next(firstNames.Length)];
            var lastName = lastNames[random.Next(lastNames.Length)];
            yield return new Person(
                i,
                $"{firstName}{i}",
                $"{lastName}{i}",
                random.Next(22, 65),
                $"{firstName.ToLower()}.{lastName.ToLower()}{i}@example.com",
                departments[random.Next(departments.Length)],
                random.Next(0, 10) < 8 // 80% active
            );
        }
    }

    private void ConfigureSaGridFeatures()
    {
        // No theming toggle in example; focus on table features
        
        // Add row actions
        saGrid.AddRowAction("edit", "Edit", row => 
        {
            Debug.WriteLine($"Edit clicked for {row.Original.FirstName} {row.Original.LastName}");
        });
        
        saGrid.AddRowAction("delete", "Delete", row => 
        {
            Debug.WriteLine($"Delete clicked for {row.Original.FirstName} {row.Original.LastName}");
        });

        // Set up custom header renderers for some columns
        saGrid.SetHeaderRenderer("age", columnId => $"📅 Age");
        saGrid.SetHeaderRenderer("department", columnId => $"🏢 Dept");
        saGrid.SetHeaderRenderer("isActive", columnId => $"✅ Status");

        // Set up custom cell renderers
        saGrid.SetCellRenderer((row, columnId) =>
        {
            return columnId switch
            {
                "isActive" => row.Original.IsActive ? "✅ Active" : "❌ Inactive",
                "age" => $"{row.Original.Age} years",
                "department" => $"[{row.Original.Department}]",
                _ => row.GetCell(columnId).Value?.ToString() ?? ""
            };
        });
    }

    private Control CreateAdvancedUI()
    {
        var container = new StackPanel { Orientation = Orientation.Vertical };

        // Simple header
        var header = new TextBlock
        {
            Text = "SaGrid.Advanced Advanced Table Demo",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(20, 10),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        container.Children.Add(header);

        // Removed test TextBox used during debugging

        // Controls section
        var controlsPanel = CreateControlsPanel();
        container.Children.Add(controlsPanel);

        // Info panel
        infoTextBlock = new TextBlock
        {
            Text = "SaGrid.Advanced Information: Initializing...",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(20, 10),
            FontSize = 14
        };
        container.Children.Add(infoTextBlock);

        // Create SaGrid.Advanced host area with side bar + table + status bar
        var sideBarHost = new SideBarHost();
        sideBarHost.Initialize(saGrid.GetSideBarService(), saGrid);

        var statusBarHost = new StatusBarHost();
        statusBarHost.Initialize(saGrid.GetStatusBarService(), saGrid);

        var saGridComponent = new SaGridComponent<Person>(saGrid);

        var tableArea = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(20, 10, 20, 20)
        };

        // Side bar (left column, full height)
        tableArea.Children.Add(sideBarHost);
        Grid.SetColumn(sideBarHost, 0);
        Grid.SetRow(sideBarHost, 0);
        Grid.SetRowSpan(sideBarHost, 2);

        // Grid component (right column, top row)
        tableArea.Children.Add(saGridComponent);
        Grid.SetColumn(saGridComponent, 1);
        Grid.SetRow(saGridComponent, 0);

        // Status bar (right column, bottom row)
        tableArea.Children.Add(statusBarHost);
        Grid.SetColumn(statusBarHost, 1);
        Grid.SetRow(statusBarHost, 1);

        container.Children.Add(tableArea);

        return new ScrollViewer { Content = container };
    }

    private Control CreateControlsPanel()
    {
        var panel = new StackPanel 
        { 
            Orientation = Orientation.Vertical,
            Margin = new Thickness(20, 10)
        };

        // Minimal, reliable actions
        var buttonPanel = new WrapPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _multiSortBtn = new Button 
        { 
            Content = $"⇅ Multi‑Sort: {(saGrid.IsMultiSortEnabled() ? "ON" : "OFF")}", 
            Padding = new Thickness(12, 6),
            Height = 32,
            MinWidth = 140,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _multiSortBtn.Click += (sender, e) =>
        {
            saGrid.ToggleMultiSortOverride();
            UpdateInfoText();
            UpdateControlButtons();
        };

        _resetFiltersBtn = new Button 
        { 
            Content = $"🧹 Reset Filters (0)", 
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6),
            Height = 32,
            MinWidth = 140,
            VerticalAlignment = VerticalAlignment.Center
        };
        _resetFiltersBtn.Click += (sender, e) =>
        {
            saGrid.ClearGlobalFilter();
            saGrid.ClearColumnFilters();
            UpdateInfoText();
            UpdateControlButtons();
        };

        _resetSortingBtn = new Button 
        { 
            Content = $"↕️ Reset Sorting (0)", 
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6),
            Height = 32,
            MinWidth = 140,
            VerticalAlignment = VerticalAlignment.Center
        };
        _resetSortingBtn.Click += (sender, e) =>
        {
            saGrid.SetSorting(Array.Empty<ColumnSort>());
            UpdateInfoText();
            UpdateControlButtons();
        };

        _toggleSideBarBtn = new Button
        {
            Content = $"☰ Side Bar: {(saGrid.IsSideBarVisible() ? "Shown" : "Hidden")}",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6),
            Height = 32,
            MinWidth = 160,
            VerticalAlignment = VerticalAlignment.Center
        };
        _toggleSideBarBtn.Click += (sender, e) =>
        {
            saGrid.ToggleSideBarVisible();
            UpdateInfoText();
            UpdateControlButtons();
        };

        _toggleStatusBarBtn = new Button
        {
            Content = $"📊 Status Bar: {(saGrid.IsStatusBarVisible() ? "Shown" : "Hidden")}",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6),
            Height = 32,
            MinWidth = 160,
            VerticalAlignment = VerticalAlignment.Center
        };
        _toggleStatusBarBtn.Click += (sender, e) =>
        {
            saGrid.ToggleStatusBarVisible();
            UpdateInfoText();
            UpdateControlButtons();
        };

        _openColumnsPanelBtn = new Button
        {
            Content = "📋 Open Columns Panel",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6),
            Height = 32,
            MinWidth = 160,
            VerticalAlignment = VerticalAlignment.Center
        };
        _openColumnsPanelBtn.Click += (sender, e) =>
        {
            if (string.Equals(saGrid.GetOpenedToolPanel(), ColumnPanelId, StringComparison.OrdinalIgnoreCase))
            {
                saGrid.CloseToolPanel();
            }
            else
            {
                saGrid.OpenToolPanel(ColumnPanelId);
                saGrid.SetSideBarVisible(true);
            }
            UpdateControlButtons();
        };

        buttonPanel.Children.Add(_multiSortBtn);
        buttonPanel.Children.Add(_resetFiltersBtn);
        buttonPanel.Children.Add(_resetSortingBtn);
        buttonPanel.Children.Add(_toggleSideBarBtn);
        buttonPanel.Children.Add(_toggleStatusBarBtn);
        buttonPanel.Children.Add(_openColumnsPanelBtn);
        panel.Children.Add(buttonPanel);

        // Initialize button labels based on current state
        UpdateControlButtons();

        return panel;
    }

    private void UpdateInfoText()
    {
        if (infoTextBlock != null && saGrid != null)
        {
            var approxRows = saGrid.GetApproximateRowCount();
            var loadedRows = _dataSource?.LoadedRowCount ?? 0;
            var totalColumns = saGrid.AllLeafColumns.Count;
            var visibleColumns = saGrid.VisibleLeafColumns.Count;
            var hasGlobalFilter = saGrid.State.GlobalFilter != null;
            var hasColumnFilters = saGrid.State.ColumnFilters?.Filters.Count > 0;
            var multiSort = saGrid.IsMultiSortEnabled() ? "ON" : "OFF";
            var sideBarState = saGrid.IsSideBarVisible() ? "Visible" : "Hidden";
            var statusBarState = saGrid.IsStatusBarVisible() ? "Visible" : "Hidden";
            var activePanel = saGrid.GetOpenedToolPanel() ?? "None";

            var selectedCells = saGrid.GetSelectedCells();
            var activeCell = saGrid.GetActiveCell();
            var cellSelectionInfo = selectedCells.Count > 0
                ? $"Selected: {selectedCells.Count} cells"
                : "No selection";

            if (activeCell != null)
            {
                cellSelectionInfo += $" | Active: ({activeCell.Value.RowIndex},{activeCell.Value.ColumnId})";
            }

            infoTextBlock.Text = $"📊 SaGrid.Advanced Stats: ~{approxRows} rows (loaded {loadedRows}) | {visibleColumns}/{totalColumns} columns | " +
                               $"Multi‑Sort: {multiSort} | Global Filter: {(hasGlobalFilter ? "✅" : "❌")} | " +
                               $"Column Filters: {(hasColumnFilters == true ? "✅" : "❌")} | Side Bar: {sideBarState} ({activePanel}) | " +
                               $"Status Bar: {statusBarState} | 🎯 {cellSelectionInfo}";
        }
    }

    private void OnSideBarStateChanged(object? sender, SideBarChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Grid, saGrid))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            UpdateInfoText();
            UpdateControlButtons();
        });
    }

    private void UpdateControlButtons()
    {
        if (_multiSortBtn != null)
        {
            _multiSortBtn.Content = $"⇅ Multi‑Sort: {(saGrid.IsMultiSortEnabled() ? "ON" : "OFF")}";
        }
        if (_resetFiltersBtn != null)
        {
            var cf = saGrid.State.ColumnFilters?.Filters.Count ?? 0;
            var gf = saGrid.State.GlobalFilter != null ? 1 : 0;
            _resetFiltersBtn.Content = $"🧹 Reset Filters ({cf + gf})";
        }
        if (_resetSortingBtn != null)
        {
            var sc = saGrid.State.Sorting?.Columns.Count ?? 0;
            _resetSortingBtn.Content = $"↕️ Reset Sorting ({sc})";
        }
        if (_toggleSideBarBtn != null)
        {
            _toggleSideBarBtn.Content = $"☰ Side Bar: {(saGrid.IsSideBarVisible() ? "Shown" : "Hidden")}";
        }
        if (_toggleStatusBarBtn != null)
        {
            _toggleStatusBarBtn.Content = $"📊 Status Bar: {(saGrid.IsStatusBarVisible() ? "Shown" : "Hidden")}";
        }
        if (_openColumnsPanelBtn != null)
        {
            var opened = saGrid.GetOpenedToolPanel();
            var isActive = string.Equals(opened, ColumnPanelId, StringComparison.OrdinalIgnoreCase);
            _openColumnsPanelBtn.Content = isActive ? "📋 Close Columns Panel" : "📋 Open Columns Panel";
        }
    }
}
