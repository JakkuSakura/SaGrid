using System.Collections.Generic;
using SaGrid.Core;

namespace SaGrid.Advanced.Interfaces;

public interface IFilterService
{
    SetFilterState GetSetFilterState<TData>(SaGrid<TData> grid, string columnId);

    IReadOnlyList<SetFilterValue> GetDistinctValues<TData>(SaGrid<TData> grid, string columnId);

    void ApplySetFilter<TData>(SaGrid<TData> grid, string columnId, SetFilterState state);

    void ClearFilter<TData>(SaGrid<TData> grid, string columnId);

    // Quick filter removed; use global filter via grid.SetGlobalFilter
}

public sealed record SetFilterValue(string Value, int Occurrences);
