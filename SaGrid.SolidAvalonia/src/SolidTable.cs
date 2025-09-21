using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SolidAvalonia;
using SaGrid.Core;
using static SolidAvalonia.Solid;
using Avalonia.LogicalTree;
using Avalonia;

namespace SaGrid.SolidAvalonia;

public class SolidTable<TData> : Component
{
    private readonly TableOptions<TData> _options;
    private Table<TData>? _table;
    private (Func<Table<TData>>, Action<Table<TData>>)? _tableSignal;
    private Table<TData>? _externalTable;
    private (Func<int>, Action<int>)? _selectionSignal;

    // Renderers
    private readonly TableHeaderRenderer<TData> _headerRenderer;
    private readonly TableBodyRenderer<TData> _bodyRenderer;
    private readonly TableFooterRenderer<TData> _footerRenderer;

    public Table<TData> Table => _tableSignal?.Item1() ?? throw new InvalidOperationException("Table not initialized. Call Build() first.");

    public SolidTable(TableOptions<TData> options, Table<TData>? externalTable = null) : base(true)
    {
        _options = options;
        _externalTable = externalTable;
        
        // Initialize renderers
        _headerRenderer = new TableHeaderRenderer<TData>();
        _bodyRenderer = new TableBodyRenderer<TData>();
        _footerRenderer = new TableFooterRenderer<TData>();
        
        OnCreatedCore(); // 推送 reactive owner
        Initialize(); // 触发 Build()
    }

    protected override object Build()
    {
        Console.WriteLine("SolidTable.Build invoked");
        // Initialize table and signals here in the proper component lifecycle
        if (_table == null)
        {
            _table = _externalTable ?? new Table<TData>(_options);
            _tableSignal = CreateSignal(_table);
            _selectionSignal = CreateSignal(0); // Signal for cell selection count

            // Extract the signal setter for use in the callback
            var signalSetter = _tableSignal.Value.Item2;

            // Subscribe to table state changes to trigger UI updates
            var originalOnStateChange = _options.OnStateChange;
            
            // Update the table options to include state change callback
            var newOptions = _options with
            {
                OnStateChange = state =>
                {
                    originalOnStateChange?.Invoke(state);
                    // Update the reactive signal to trigger UI update
                    signalSetter.Invoke(_table);
                }
            };

            // For external tables with UI update capability, we need to handle state changes differently
            if (_externalTable != null && _externalTable is IAdvancedTable<TData> advancedTable)
            {
                var (selectionGetter, selectionSetter) = _selectionSignal.Value;
                var updateCounter = 0;

                // Try to set up UI update callback if the table supports it
                if (advancedTable.GetType().GetMethod("SetUIUpdateCallback") != null)
                {
                    var method = advancedTable.GetType().GetMethod("SetUIUpdateCallback");
                    method?.Invoke(advancedTable, new object[] { new Action(() =>
                    {
                        updateCounter++;
                        signalSetter?.Invoke(advancedTable as Table<TData>);
                        selectionSetter?.Invoke(updateCounter);
                    })});
                }
            }
            else
            {
                // For external tables, the state change callback is handled through the original options
                // We'll need to recreate the external table with new options if we want to change the callback
                _table = new Table<TData>(newOptions with { Data = _table.Options.Data, Columns = _table.Options.Columns });
            }
        }

        var table = Table; // Access the reactive signal once to get the initial table
        var header = _headerRenderer.CreateHeader(table); // Create header once, outside reactive context
        
        return Reactive(() =>
        {
            var currentTable = Table; // Access the reactive signal for body updates

            var mainBorder = new Border()
                .BorderThickness(1)
                .BorderBrush(Brushes.Gray)
                .Child(
                    new StackPanel()
                        .Children(
                            // Header (stable, created once)
                            header,
                            // Body (reactive, updates with table changes)
                            _bodyRenderer.CreateBody(currentTable, () => Table, _selectionSignal?.Item1), // Pass both table and selection signal getters
                            // Footer (reactive)
                            _footerRenderer.CreateFooter(currentTable)
                        )
                );

            // Add keyboard navigation for cell selection
            if (currentTable is ICellSelectable<TData> cellSelectable)
            {
                mainBorder.KeyDown += (sender, e) =>
                {
                    try
                    {
                        var direction = e.Key switch
                        {
                            Avalonia.Input.Key.Up => CellNavigationDirection.Up,
                            Avalonia.Input.Key.Down => CellNavigationDirection.Down,
                            Avalonia.Input.Key.Left => CellNavigationDirection.Left,
                            Avalonia.Input.Key.Right => CellNavigationDirection.Right,
                            _ => (CellNavigationDirection?)null
                        };

                        if (direction.HasValue)
                        {
                            Console.WriteLine($"Keyboard navigation: {direction.Value}");
                            cellSelectable.NavigateCell(direction.Value);
                            // UI update callback will be triggered automatically
                            e.Handled = true;
                        }
                        else if (e.Key == Avalonia.Input.Key.C && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
                        {
                            // Ctrl+C to copy selected cells
                            var copiedText = cellSelectable.CopySelectedCells();
                            if (!string.IsNullOrEmpty(copiedText))
                            {
                                Console.WriteLine($"Copied to clipboard:\n{copiedText}");
                                // In a real app, you'd copy to the system clipboard
                            }
                            e.Handled = true;
                        }
                        else if (e.Key == Avalonia.Input.Key.Escape)
                        {
                            // Escape to clear selection
                            cellSelectable.ClearCellSelection();
                            // UI update callback will be triggered automatically
                            e.Handled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling keyboard navigation: {ex.Message}");
                    }
                };

                // Focus the border when clicked to enable keyboard navigation, but not if clicking on a TextBox
                mainBorder.PointerPressed += (sender, e) =>
                {
                    // Don't steal focus if the user clicked on a TextBox
                    if (e.Source is not TextBox)
                    {
                        mainBorder.Focus();
                    }
                };
            }

            return mainBorder;
        });
    }

    // Extension support methods
    public void SetSorting(string columnId, SortDirection? direction)
    {
        if (direction.HasValue)
        {
            Table.SetSorting(columnId, direction.Value);
        }
        else
        {
            // Clear sorting for this column by setting all sorts without this column
            var currentSorts = Table.State.Sorting?.Columns ?? new List<ColumnSort>();
            var newSorts = currentSorts.Where(s => s.Id != columnId).ToList();
            Table.SetSorting(newSorts);
        }
    }

    public void SetPageIndex(int pageIndex)
    {
        Table.SetPageIndex(pageIndex);
    }

    public void SetPageSize(int pageSize)
    {
        Table.SetPageSize(pageSize);
    }

    public string GetHeaderContent(IHeader<TData> header)
    {
        return TableContentHelper<TData>.GetHeaderContent(header);
    }
}