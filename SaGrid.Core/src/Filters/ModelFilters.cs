using SaGrid.Core.Models;

namespace SaGrid.Core.Filters;

// Row-level text filter applied across visible columns
public sealed class RowTextModelFilter<TData> : IModelFilter<TData>
{
    public RowTextModelFilter(TextFilterState state)
    {
        State = state;
    }

    public TextFilterState State { get; }

    public bool Evaluate(Table<TData> table, Row<TData> row)
    {
        foreach (var column in table.VisibleLeafColumns)
        {
            var cellString = row.GetCell(column.Id).Value?.ToString() ?? string.Empty;
            if (FilterText(cellString))
            {
                return true;
            }
        }
        return false;
    }

    private bool FilterText(string cell)
    {
        var comparison = State.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var text = State.Query ?? string.Empty;
        return State.Mode switch
        {
            TextFilterMode.StartsWith => cell.StartsWith(text, comparison),
            TextFilterMode.EndsWith => cell.EndsWith(text, comparison),
            TextFilterMode.Fuzzy => FuzzyMatch(cell, text, State.CaseSensitive),
            _ => cell.IndexOf(text, comparison) >= 0
        };
    }

    private static bool FuzzyMatch(string text, string pattern, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(pattern)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        var t = caseSensitive ? text : text.ToLowerInvariant();
        var p = caseSensitive ? pattern : pattern.ToLowerInvariant();

        p = new string(p.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (p.Length == 0) return true;

        int i = 0, j = 0;
        while (i < t.Length && j < p.Length)
        {
            if (t[i] == p[j]) j++;
            i++;
        }
        return j == p.Length;
    }
}

// Row-level predicate adapter
public sealed class RowPredicateModelFilter<TData> : IModelFilter<TData>
{
    private readonly RowFilterFn<TData> _predicate;
    public RowPredicateModelFilter(RowFilterFn<TData> predicate) => _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    public bool Evaluate(Table<TData> table, Row<TData> row) => _predicate(table, row);
}

// Column-level text filter
public sealed class TextColumnFilter<TData> : IColumnFilter<TData>
{
    public TextColumnFilter(string columnId, TextFilterState state)
    {
        ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
        State = state;
    }

    public string ColumnId { get; }
    public TextFilterState State { get; }

    public bool Evaluate(Table<TData> table, Row<TData> row)
    {
        var cellString = row.GetCell(ColumnId).Value?.ToString() ?? string.Empty;
        var comparison = State.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var text = State.Query ?? string.Empty;
        return State.Mode switch
        {
            TextFilterMode.StartsWith => cellString.StartsWith(text, comparison),
            TextFilterMode.EndsWith => cellString.EndsWith(text, comparison),
            TextFilterMode.Fuzzy => FuzzyMatch(cellString, text, State.CaseSensitive),
            _ => cellString.IndexOf(text, comparison) >= 0
        };
    }

    private static bool FuzzyMatch(string text, string pattern, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(pattern)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        var t = caseSensitive ? text : text.ToLowerInvariant();
        var p = caseSensitive ? pattern : pattern.ToLowerInvariant();

        p = new string(p.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (p.Length == 0) return true;

        int i = 0, j = 0;
        while (i < t.Length && j < p.Length)
        {
            if (t[i] == p[j]) j++;
            i++;
        }
        return j == p.Length;
    }
}

// Column-level set filter
public sealed class SetColumnFilter<TData> : IColumnFilter<TData>
{
    public SetColumnFilter(string columnId, SetFilterState state)
    {
        ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
        State = state;
    }

    public string ColumnId { get; }
    public SetFilterState State { get; }

    public bool Evaluate(Table<TData> table, Row<TData> row)
    {
        var value = row.GetCell(ColumnId).Value?.ToString() ?? string.Empty;
        var isBlank = string.IsNullOrEmpty(value);
        if (isBlank) return State.IncludeBlanks;

        var tokens = value.Split(new[] { ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var selections = State.SelectedValues;
        if (selections.Count == 0) return true;

        if (State.Operator == SetFilterOperator.All)
        {
            return selections.All(selection => tokens.Contains(selection, StringComparer.OrdinalIgnoreCase));
        }

        return tokens.Any(token => selections.Contains(token, StringComparer.OrdinalIgnoreCase));
    }
}

// Column-level boolean/equals filter (for tri-state and primitives)
public sealed class EqualsColumnFilter<TData> : IColumnFilter<TData>
{
    public EqualsColumnFilter(string columnId, object? value)
    {
        ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
        Value = value;
    }

    public string ColumnId { get; }
    public object? Value { get; }

    public bool Evaluate(Table<TData> table, Row<TData> row)
    {
        var cell = row.GetCell(ColumnId).Value;

        if (Value is bool b)
        {
            return cell is bool cb && cb == b;
        }
        if (Value is int i)
        {
            return cell is int ci && ci == i;
        }
        if (Value is double d)
        {
            return cell is double cd && Math.Abs(cd - d) < 0.001;
        }

        return string.Equals(cell?.ToString(), Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

