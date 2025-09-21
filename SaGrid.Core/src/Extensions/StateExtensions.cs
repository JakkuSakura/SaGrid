using System.Collections;

namespace SaGrid.Core;

// Extension methods to make state types work like collections
public static class StateExtensions
{
    // SortingState extensions
    public static int Count(this SortingState state) => state.Columns.Count;

    public static SortingState Add(this SortingState state, ColumnSort sort) =>
        state with { Columns = state.Columns.Append(sort).ToList() };

    public static SortingState Clear(this SortingState state) =>
        state with { Columns = new List<ColumnSort>() };

    public static SortingState Where(this SortingState state, Func<ColumnSort, bool> predicate) =>
        state with { Columns = state.Columns.Where(predicate).ToList() };

    public static ColumnSort? FirstOrDefault(this SortingState state, Func<ColumnSort, bool> predicate) =>
        state.Columns.FirstOrDefault(predicate);

    public static int FindIndex(this SortingState state, Func<ColumnSort, bool> predicate) =>
        state.Columns.FindIndex(c => predicate(c));

    public static SortingState RemoveAt(this SortingState state, int index)
    {
        var newList = state.Columns.ToList();
        newList.RemoveAt(index);
        return state with { Columns = newList };
    }

    public static SortingState Concat(this SortingState state, IEnumerable<ColumnSort>? other)
    {
        if (other == null) return state;
        return state with { Columns = state.Columns.Concat(other).ToList() };
    }

    public static bool Contains(this SortingState state, Func<ColumnSort, bool> predicate) =>
        state.Columns.Any(predicate);

    // ColumnFiltersState extensions
    public static int Count(this ColumnFiltersState state) => state.Filters.Count;

    public static ColumnFiltersState Add(this ColumnFiltersState state, ColumnFilter filter) =>
        state with { Filters = state.Filters.Append(filter).ToList() };

    public static ColumnFiltersState Clear(this ColumnFiltersState state) =>
        state with { Filters = new List<ColumnFilter>() };

    public static ColumnFiltersState Where(this ColumnFiltersState state, Func<ColumnFilter, bool> predicate) =>
        state with { Filters = state.Filters.Where(predicate).ToList() };

    public static ColumnFilter? FirstOrDefault(this ColumnFiltersState state, Func<ColumnFilter, bool> predicate) =>
        state.Filters.FirstOrDefault(predicate);

    public static bool Any(this ColumnFiltersState state, Func<ColumnFilter, bool> predicate) =>
        state.Filters.Any(predicate);

    public static bool Contains(this ColumnFiltersState state, Func<ColumnFilter, bool> predicate) =>
        state.Filters.Any(predicate);

    // GroupingState extensions
    public static int Count(this GroupingState state) => state.Groups.Count;

    public static GroupingState Add(this GroupingState state, string group) =>
        state with { Groups = state.Groups.Append(group).ToList() };

    public static GroupingState Remove(this GroupingState state, string group)
    {
        var newList = state.Groups.Where(g => g != group).ToList();
        return state with { Groups = newList };
    }

    public static bool Contains(this GroupingState state, string group) =>
        state.Groups.Contains(group);

    public static bool Any(this GroupingState state) => state.Groups.Any();

    public static string First(this GroupingState state) => state.Groups.First();

    public static int FindIndex(this GroupingState state, Func<string, bool> predicate) =>
        state.Groups.FindIndex(u => predicate(u));

    // Dictionary-based state extensions
    public static bool GetValueOrDefault(this RowSelectionState state, string key, bool defaultValue = false) =>
        state.Items.GetValueOrDefault(key, defaultValue);

    public static bool GetValueOrDefault(this ExpandedState state, string key, bool defaultValue = false) =>
        state.Items.GetValueOrDefault(key, defaultValue);

    public static bool GetValueOrDefault(this ColumnVisibilityState state, string key, bool defaultValue = false) =>
        state.Items.GetValueOrDefault(key, defaultValue);

    public static double GetValueOrDefault(this ColumnSizingState state, string key, double defaultValue = 150) =>
        state.Items.GetValueOrDefault(key, defaultValue);

    // Update methods for dictionaries
    public static RowSelectionState With(this RowSelectionState state, string key, bool value)
    {
        var newDict = new Dictionary<string, bool>(state.Items) { [key] = value };
        return state with { Items = newDict };
    }

    public static ExpandedState With(this ExpandedState state, string key, bool value)
    {
        var newDict = new Dictionary<string, bool>(state.Items) { [key] = value };
        return state with { Items = newDict };
    }

    public static ColumnVisibilityState With(this ColumnVisibilityState state, string key, bool value)
    {
        var newDict = new Dictionary<string, bool>(state.Items) { [key] = value };
        return state with { Items = newDict };
    }

    public static ColumnSizingState With(this ColumnSizingState state, string key, double value)
    {
        var newDict = new Dictionary<string, double>(state.Items) { [key] = value };
        return state with { Items = newDict };
    }

    public static ColumnSizingState Remove(this ColumnSizingState state, string key)
    {
        var newDict = new Dictionary<string, double>(state.Items);
        newDict.Remove(key);
        return state with { Items = newDict };
    }

    public static ColumnVisibilityState Remove(this ColumnVisibilityState state, string key)
    {
        var newDict = new Dictionary<string, bool>(state.Items);
        newDict.Remove(key);
        return state with { Items = newDict };
    }
}