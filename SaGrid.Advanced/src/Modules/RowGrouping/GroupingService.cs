using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.RowModel;
using SaGrid.Core;
using SaGrid.Advanced;

namespace SaGrid.Advanced.Modules.RowGrouping;

/// <summary>
/// Primary service responsible for grouping operations. The implementation mirrors the
/// public surface exposed by AG Grid's column group service while integrating with the
/// safer, immutable table state mutations used throughout SaGrid.Core.
/// </summary>
public class GroupingService : IGroupingService
{
    private sealed class GroupingMetadata
    {
        public List<GroupingColumnState> Columns = new();
        public bool PivotMode { get; set; }
    }

    private readonly IEventService _eventService;
    private readonly ConditionalWeakTable<object, GroupingMetadata> _metadata = new();

    public GroupingService(IEventService eventService)
    {
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    public IReadOnlyList<string> GetGroupedColumnIds<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        IReadOnlyList<string>? groups = grid.State.Grouping?.Groups;
        return groups ?? Array.Empty<string>();
    }

    public void SetGrouping<TData>(SaGrid<TData> grid, IEnumerable<string> columnIds)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (columnIds == null) throw new ArgumentNullException(nameof(columnIds));

        var ordered = NormalizeGroupingList(grid, columnIds);
        var current = GetGroupedColumnIds(grid);

        if (current.SequenceEqual(ordered))
        {
            return;
        }

        GroupingExtensions.SetGrouping(grid.InnerTable, ordered);
        SynchroniseMetadata(grid, ordered);
        NotifyGroupingChanged(grid, ordered, ClientSideRowModelStage.Group);
    }

    public void AddGroupingColumn<TData>(SaGrid<TData> grid, string columnId, int? insertAtIndex = null)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (string.IsNullOrWhiteSpace(columnId)) throw new ArgumentException("Column id is required", nameof(columnId));

        var column = grid.GetColumn(columnId);
        if (column == null || !column.CanGroup)
        {
            return;
        }

        var current = GetGroupedColumnIds(grid).Where(id => id != columnId).ToList();
        var index = insertAtIndex ?? current.Count;
        index = Math.Clamp(index, 0, current.Count);
        current.Insert(index, columnId);

        GroupingExtensions.SetGrouping(grid.InnerTable, current);
        SynchroniseMetadata(grid, current);
        NotifyGroupingChanged(grid, current, ClientSideRowModelStage.Group);
    }

    public void RemoveGroupingColumn<TData>(SaGrid<TData> grid, string columnId)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (string.IsNullOrWhiteSpace(columnId)) throw new ArgumentException("Column id is required", nameof(columnId));

        var current = GetGroupedColumnIds(grid).ToList();
        if (!current.Remove(columnId))
        {
            return;
        }

        GroupingExtensions.SetGrouping(grid.InnerTable, current);
        SynchroniseMetadata(grid, current);
        NotifyGroupingChanged(grid, current, ClientSideRowModelStage.Group);
    }

    public void MoveGroupingColumn<TData>(SaGrid<TData> grid, string columnId, int targetIndex)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (string.IsNullOrWhiteSpace(columnId)) throw new ArgumentException("Column id is required", nameof(columnId));

        var current = GetGroupedColumnIds(grid).ToList();
        if (!current.Remove(columnId))
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, current.Count);
        current.Insert(targetIndex, columnId);

        GroupingExtensions.SetGrouping(grid.InnerTable, current);
        SynchroniseMetadata(grid, current);
        NotifyGroupingChanged(grid, current, ClientSideRowModelStage.Group);
    }

    public void ClearGrouping<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        if (GetGroupedColumnIds(grid).Count == 0)
        {
            return;
        }

        GroupingExtensions.SetGrouping(grid.InnerTable, Array.Empty<string>());
        SynchroniseMetadata(grid, Array.Empty<string>());
        NotifyGroupingChanged(grid, Array.Empty<string>(), ClientSideRowModelStage.Map);
    }

    public GroupingConfiguration GetConfiguration<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        var metadata = _metadata.GetOrCreateValue(grid);
        var ordered = OrderMetadataToMatch(grid, metadata);
        return new GroupingConfiguration(ordered, metadata.PivotMode);
    }

    private IReadOnlyList<GroupingColumnState> OrderMetadataToMatch<TData>(SaGrid<TData> grid, GroupingMetadata metadata)
    {
        var groupedIds = GetGroupedColumnIds(grid);
        if (groupedIds.Count == 0)
        {
            metadata.Columns.Clear();
            return Array.Empty<GroupingColumnState>();
        }

        var lookup = metadata.Columns.ToDictionary(c => c.ColumnId, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<GroupingColumnState>(groupedIds.Count);
        foreach (var id in groupedIds)
        {
            if (lookup.TryGetValue(id, out var state))
            {
                ordered.Add(state);
            }
            else
            {
                ordered.Add(new GroupingColumnState(id));
            }
        }

        metadata.Columns = ordered;
        return ordered;
    }

    private List<string> NormalizeGroupingList<TData>(SaGrid<TData> grid, IEnumerable<string> columnIds)
    {
        var leafLookup = grid.AllLeafColumns.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var id in columnIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!leafLookup.TryGetValue(id, out var column) || !column.CanGroup)
            {
                continue;
            }

            if (!ordered.Contains(column.Id))
            {
                ordered.Add(column.Id);
            }
        }

        return ordered;
    }

    private void SynchroniseMetadata<TData>(SaGrid<TData> grid, IReadOnlyList<string> groupedIds)
    {
        var metadata = _metadata.GetOrCreateValue(grid);
        var existing = metadata.Columns.ToDictionary(c => c.ColumnId, StringComparer.OrdinalIgnoreCase);
        metadata.Columns = groupedIds
            .Select(id => existing.TryGetValue(id, out var state) ? state : new GroupingColumnState(id))
            .ToList();
    }

    private void NotifyGroupingChanged<TData>(SaGrid<TData> grid, IReadOnlyList<string> groupedIds, ClientSideRowModelStage stage)
    {
        _eventService.DispatchEvent(GridEventTypes.GroupingChanged, new GroupingChangedEventArgs(grid, groupedIds));
        grid.RefreshModel(new RefreshModelParams(stage));
        grid.ScheduleUIUpdate();
    }
}
