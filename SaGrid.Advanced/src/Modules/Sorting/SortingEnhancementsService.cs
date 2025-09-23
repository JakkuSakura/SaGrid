using System.Runtime.CompilerServices;

namespace SaGrid.Advanced.Modules.Sorting;

/// <summary>
/// Provides sorting-related helpers that extend the base table behaviour.
/// Mirrors AG Grid's modular sorting enhancements.
/// </summary>
public class SortingEnhancementsService
{
    private sealed class GridState
    {
        public bool? MultiSortOverride;
    }

    private readonly ConditionalWeakTable<object, GridState> _state = new();

    public bool IsMultiSortEnabled<TData>(SaGrid<TData> grid)
    {
        var state = _state.GetOrCreateValue(grid);
        return state.MultiSortOverride ?? grid.Options.EnableMultiSort;
    }

    public void ToggleMultiSortOverride<TData>(SaGrid<TData> grid)
    {
        var state = _state.GetOrCreateValue(grid);
        var current = IsMultiSortEnabled(grid);
        state.MultiSortOverride = !current;
        grid.ScheduleUIUpdate();
    }

    public void ResetOverride<TData>(SaGrid<TData> grid)
    {
        var state = _state.GetOrCreateValue(grid);
        state.MultiSortOverride = null;
        grid.ScheduleUIUpdate();
    }
}
