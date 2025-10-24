using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Filters;

public sealed class FilterService : IFilterService
{
    private sealed class FilterState
    {
        public Dictionary<string, SetFilterState> ColumnFilters { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConditionalWeakTable<object, FilterState> _state = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<SetFilterValue>> _distinctCache = new();
    private readonly IEventService _eventService;

    public FilterService(IEventService eventService)
    {
        _eventService = eventService;
    }

    public SetFilterState GetSetFilterState<TData>(SaGrid<TData> grid, string columnId)
    {
        var state = _state.GetValue(grid, _ => new FilterState());
        if (state.ColumnFilters.TryGetValue(columnId, out var existing))
        {
            return existing;
        }

        return new SetFilterState(Array.Empty<string>());
    }

    public IReadOnlyList<SetFilterValue> GetDistinctValues<TData>(SaGrid<TData> grid, string columnId)
    {
        var cacheKey = CreateCacheKey(grid, columnId);
        if (_distinctCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var rows = grid.PreFilteredRowModel.Rows;
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var value = row.GetCell(columnId).Value;
            var key = value?.ToString() ?? string.Empty;
            lookup[key] = lookup.GetValueOrDefault(key, 0) + 1;
        }

        var result = lookup
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new SetFilterValue(kvp.Key, kvp.Value))
            .ToList()
            .AsReadOnly();

        _distinctCache[cacheKey] = result;
        return result;
    }

    public void ApplySetFilter<TData>(SaGrid<TData> grid, string columnId, SetFilterState state)
    {
        var filterState = _state.GetValue(grid, _ => new FilterState());

        if (state.SelectedValues.Count == 0 && !state.IncludeBlanks)
        {
            filterState.ColumnFilters.Remove(columnId);
            grid.ClearColumnFilter(columnId);
        }
        else
        {
            filterState.ColumnFilters[columnId] = state;
            grid.SetColumnFilter(columnId, state);
        }

        _distinctCache.TryRemove(CreateCacheKey(grid, columnId), out _);
        RaiseFilterChanged(grid, columnId, state);
    }

    public void ClearFilter<TData>(SaGrid<TData> grid, string columnId)
    {
        var filterState = _state.GetValue(grid, _ => new FilterState());
        if (filterState.ColumnFilters.Remove(columnId))
        {
            grid.ClearColumnFilter(columnId);
            RaiseFilterChanged(grid, columnId, null);
        }
    }

    // Quick filter removed; rely on global filter only

    public void NotifyManualFilterChange<TData>(SaGrid<TData> grid, string columnId)
    {
        var filterState = _state.GetValue(grid, _ => new FilterState());
        if (filterState.ColumnFilters.Remove(columnId))
        {
            _distinctCache.TryRemove(CreateCacheKey(grid, columnId), out _);
        }
    }

    internal void EnsureFilterPanel<TData>(SaGrid<TData> grid, SideBarService sideBarService)
    {
        if (!sideBarService.IsPanelRegistered(grid, FilterPanelDefinition.PanelId))
        {
            sideBarService.RegisterPanel(grid, FilterPanelDefinition.CreatePanel(grid, this));
        }
    }

    internal void InvalidateDistinctCache<TData>(SaGrid<TData> grid, string columnId)
    {
        _distinctCache.TryRemove(CreateCacheKey(grid, columnId), out _);
    }

    // No quick filter state to retrieve

    private void RaiseFilterChanged<TData>(SaGrid<TData> grid, string columnId, SetFilterState? state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var args = new FilterChangedEventArgs(grid, columnId, state);
            _eventService.DispatchEvent(GridEventTypes.FilterChanged, args);
        });

        grid.RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Filter));
        grid.ScheduleUIUpdate();
    }

    private static string CreateCacheKey<TData>(SaGrid<TData> grid, string columnId)
    {
        return $"{grid.GetHashCode()}::{columnId}";
    }
}
