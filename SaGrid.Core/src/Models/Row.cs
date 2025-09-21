namespace SaGrid.Core;

public class Row<TData> : IRow<TData>
{
    private readonly Table<TData> _table;
    private readonly Dictionary<string, Cell<TData>> _cells;
    private readonly List<Row<TData>> _subRows = new();
    
    public string Id { get; }
    public int Index { get; }
    public TData Original { get; }
    public int Depth { get; }
    public Row<TData>? Parent { get; }
    public IReadOnlyList<IRow<TData>> SubRows => _subRows.Cast<IRow<TData>>().ToList().AsReadOnly();
    public IReadOnlyList<IRow<TData>> LeafRows => GetLeafRows();
    public IReadOnlyDictionary<string, ICell<TData>> Cells => _cells.ToDictionary(kvp => kvp.Key, kvp => (ICell<TData>)kvp.Value).AsReadOnly();
    
    public bool IsSelected => GetIsSelected();
    public bool IsExpanded => GetIsExpanded();
    public bool IsGrouped => GetIsGrouped();
    public object? GroupingValue => GetGroupingValue();

    public Row(Table<TData> table, string id, int index, TData original, int depth, Row<TData>? parent)
    {
        _table = table;
        Id = id;
        Index = index;
        Original = original;
        Depth = depth;
        Parent = parent;
        
        _cells = new Dictionary<string, Cell<TData>>();
        CreateCells();
        
        parent?.AddSubRow(this);
    }

    private void CreateCells()
    {
        foreach (var column in _table.AllLeafColumns)
        {
            var cell = new Cell<TData>(this, column);
            _cells[column.Id] = cell;
        }
    }

    private void AddSubRow(Row<TData> subRow)
    {
        _subRows.Add(subRow);
    }

    private IReadOnlyList<IRow<TData>> GetLeafRows()
    {
        if (!_subRows.Any())
            return new List<IRow<TData>> { this }.AsReadOnly();
            
        var result = new List<IRow<TData>>();
        foreach (var subRow in _subRows)
        {
            result.AddRange(subRow.LeafRows);
        }
        return result.AsReadOnly();
    }

    private bool GetIsSelected()
    {
        var selectionState = _table.State.RowSelection ?? new RowSelectionState();
        return selectionState.GetValueOrDefault(Id, false);
    }

    private bool GetIsExpanded()
    {
        var expandedState = _table.State.Expanded ?? new ExpandedState();
        return expandedState.GetValueOrDefault(Id, false);
    }

    private bool GetIsGrouped()
    {
        var groupingState = _table.State.Grouping ?? new GroupingState();
        return groupingState.Any();
    }

    private object? GetGroupingValue()
    {
        var groupingState = _table.State.Grouping ?? new GroupingState();
        if (!groupingState.Any()) return null;

        var firstGroupColumn = groupingState.First();
        return GetValue<object>(firstGroupColumn);
    }

    public TValue GetValue<TValue>(string columnId)
    {
        var column = _table.GetColumn(columnId);
        if (column == null)
            return default(TValue)!;
            
        if (column is Column<TData, TValue> typedColumn)
            return typedColumn.GetValue(this);
            
        var cell = _cells.GetValueOrDefault(columnId);
        return cell?.Value is TValue value ? value : default(TValue)!;
    }

    public ICell<TData> GetCell(string columnId)
    {
        return _cells.GetValueOrDefault(columnId) ?? throw new ArgumentException($"Cell not found for column: {columnId}");
    }

    public void ToggleSelected()
    {
        var currentSelection = _table.State.RowSelection ?? new RowSelectionState();
        var newSelection = currentSelection.With(Id,
            !currentSelection.GetValueOrDefault(Id, false));

        _table.SetState(state => state with { RowSelection = newSelection });
    }

    public void ToggleExpanded()
    {
        var currentExpanded = _table.State.Expanded ?? new ExpandedState();
        var newExpanded = currentExpanded.With(Id,
            !currentExpanded.GetValueOrDefault(Id, false));

        _table.SetState(state => state with { Expanded = newExpanded });
    }

    public IReadOnlyList<IRow<TData>> GetParentRows()
    {
        var parents = new List<IRow<TData>>();
        var current = Parent;
        
        while (current != null)
        {
            parents.Insert(0, current);
            current = current.Parent;
        }
        
        return parents.AsReadOnly();
    }
}