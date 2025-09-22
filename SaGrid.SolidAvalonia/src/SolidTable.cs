using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SaGrid.Avalonia;
using SolidAvalonia;
using static SolidAvalonia.Solid;

namespace SaGrid.SolidAvalonia;

public class SolidTable<TData> : Component
{
    private readonly TableOptions<TData> _options;
    private readonly TableHeaderRenderer<TData> _headerRenderer = new();
    private readonly TableBodyRenderer<TData> _bodyRenderer = new();
    private readonly TableFooterRenderer<TData> _footerRenderer = new();

    private Table<TData>? _table;
    private Table<TData>? _externalTable;
    private (Func<Table<TData>>, Action<Table<TData>>)? _tableSignal;

    public SolidTable(TableOptions<TData> options, Table<TData>? externalTable = null) : base(true)
    {
        _options = options;
        _externalTable = externalTable;
        OnCreatedCore();
        Initialize();
    }

    public Table<TData> Table => _table ?? throw new InvalidOperationException("Table not initialized");

    protected override object Build()
    {
        EnsureTableAndSignal();

        return Reactive(() =>
        {
            var currentTable = _tableSignal!.Value.Item1();

            var container = new StackPanel()
                .Children(
                    _headerRenderer.CreateHeader(currentTable),
                    _bodyRenderer.CreateBody(currentTable),
                    _footerRenderer.CreateFooter(currentTable)
                );

            return new Border()
                .BorderThickness(1)
                .BorderBrush(Brushes.Gray)
                .Child(container);
        });
    }

    private void EnsureTableAndSignal()
    {
        if (_table != null && _tableSignal != null)
        {
            return;
        }

        if (_externalTable != null)
        {
            _table = _externalTable;
        }
        else
        {
            _table = CreateTable(_options);
        }

        _tableSignal = CreateSignal(_table);
    }

    private Table<TData> CreateTable(TableOptions<TData> options)
    {
        var originalOnStateChange = options.OnStateChange;

        Table<TData>? table = null;

        var adaptedOptions = options with
        {
            OnStateChange = state =>
            {
                originalOnStateChange?.Invoke(state);
                if (table != null)
                {
                    _tableSignal?.Item2(table);
                }
            }
        };

        table = new Table<TData>(adaptedOptions);
        return table;
    }
}
