using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SaGrid.Avalonia;
using SaGrid.Core.Models;
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
    private readonly Table<TData>? _externalTable;
    private (Func<Table<TData>>, Action<Table<TData>>)? _tableSignal;

    public SolidTable(TableOptions<TData> options, Table<TData>? externalTable = null) : base(true)
    {
        _options = options;
        _externalTable = externalTable;
        OnCreatedCore();
        Initialize();
    }

    public Table<TData> Table => _table ?? throw new InvalidOperationException("Table not initialized");

    protected TableOptions<TData> Options => _options;

    protected (Func<Table<TData>> Getter, Action<Table<TData>> Setter)? TableSignal => _tableSignal;

    protected override object Build()
    {
        EnsureTableAndSignal();

        return Reactive(() =>
        {
            var currentTable = _tableSignal!.Value.Item1();
            var content = BuildContent(currentTable);
            return WrapContent(currentTable, content);
        });
    }

    protected virtual Control BuildContent(Table<TData> table)
    {
        return new StackPanel()
            .Children(
                CreateHeader(table),
                CreateBody(table),
                CreateFooter(table));
    }

    protected virtual Control WrapContent(Table<TData> table, Control content)
    {
        return new Border()
            .BorderThickness(1)
            .BorderBrush(Brushes.Gray)
            .Child(content);
    }

    protected virtual Control CreateHeader(Table<TData> table) => _headerRenderer.CreateHeader(table);

    protected virtual Control CreateBody(Table<TData> table) => _bodyRenderer.CreateBody(table);

    protected virtual Control CreateFooter(Table<TData> table) => _footerRenderer.CreateFooter(table);

    protected virtual void OnTableInitialized(Table<TData> table)
    {
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

        OnTableInitialized(_table);
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
