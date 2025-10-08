using System;
using System.Collections.Generic;
using System.Linq;
using SaGrid.Core;
using SaGrid.Advanced.Events;
using SaGrid.Core.Models;

namespace SaGrid.Advanced.Interactive;

/// <summary>
/// Simple interactive column service for basic column operations
/// This is a simplified version that will be expanded with AG Grid features
/// </summary>
public class ColumnInteractiveService<TData>
{
        private static class ColumnMetaKeys
        {
            public const string SuppressMovable = "sagrid.suppressMove";
            public const string LockPosition = "sagrid.lockPosition";
            public const string LockPinned = "sagrid.lockPinned";
            public const string Pinned = "sagrid.pinned";
        }

    private readonly Table<TData> _table;
    private readonly IEventService _eventService;

    public ColumnInteractiveService(Table<TData> table, IEventService eventService)
    {
        _table = table;
        _eventService = eventService;
    }

    /// <summary>
    /// Move a column to a new position
    /// </summary>
    public bool MoveColumn(string columnId, int toDisplayIndex, string? targetPinnedArea = null)
    {
        var column = _table.GetColumn(columnId);
        if (column == null || !IsColumnMovable(column))
        {
            return false;
        }

        var displayOrder = GetVisibleLeafOrder().ToList();
        var currentIndex = displayOrder.IndexOf(columnId);
        if (currentIndex == -1)
        {
            return false;
        }

        var desiredIndex = Math.Clamp(toDisplayIndex, 0, displayOrder.Count);

        // Validate destination pinned area
        var destinationPinnedArea = ResolveDestinationPinnedArea(targetPinnedArea, displayOrder, desiredIndex);
        if (!CanMoveToPinnedArea(column, destinationPinnedArea))
        {
            return false;
        }

        if (!CanMoveRelativeToLockedColumns(columnId, desiredIndex, displayOrder))
        {
            return false;
        }

        if (currentIndex == desiredIndex)
        {
            if (GetColumnPinnedArea(columnId) == destinationPinnedArea)
            {
                return false; // nothing to do
            }
        }

        displayOrder.Remove(columnId);
        if (desiredIndex > displayOrder.Count)
        {
            desiredIndex = displayOrder.Count;
        }
        displayOrder.Insert(desiredIndex, columnId);

        var newOrder = BuildNewColumnOrder(displayOrder);
        var newPinning = BuildNewPinning(columnId, destinationPinnedArea, displayOrder);

        _table.SetState(state => state with
        {
            ColumnOrder = new ColumnOrderState(newOrder),
            ColumnPinning = newPinning
        });

        _eventService.DispatchEvent("columnMoved", new ColumnMovedEventArgs<TData>(columnId, desiredIndex, destinationPinnedArea));
        return true;
    }

    /// <summary>
    /// Set column width with basic validation
    /// </summary>
    public bool SetColumnWidth(string columnId, double width)
    {
        var column = _table.GetColumn(columnId);
        if (column == null || !column.CanResize)
        {
            return false;
        }

        var (minWidth, maxWidth) = GetColumnMinMax(column);
        var clamped = Math.Clamp(width, minWidth, maxWidth);

        var columnSizing = _table.State.ColumnSizing ?? new ColumnSizingState();
        var currentWidth = column.Size;
        if (Math.Abs(currentWidth - clamped) < 0.01)
        {
            return false;
        }

        var newSizing = columnSizing.With(columnId, clamped);
        _table.SetState(state => state with { ColumnSizing = newSizing });
        _eventService.DispatchEvent("columnResized", new ColumnResizedEventArgs<TData>(columnId, clamped));
        return true;
    }

    /// <summary>
    /// Auto-size a column based on content (simplified implementation)
    /// </summary>
    public bool AutoSizeColumn(string columnId)
    {
        try
        {
            var column = _table.GetColumn(columnId);
            if (column == null)
            {
                return false;
            }

            // Simplified auto-sizing: estimate based on column ID length and some content sampling
            var headerWidth = Math.Max(100, columnId.Length * 8 + 40); // Rough estimate
            
            // Sample a few rows to get content width
            var contentWidth = headerWidth;
            var sampleRows = _table.RowModel.Rows.Take(10);
            
            foreach (var row in sampleRows)
            {
                var cellValue = row.GetValue<object>(columnId)?.ToString() ?? "";
                var estimatedWidth = cellValue.Length * 8 + 20; // Rough text width estimate
                contentWidth = Math.Max(contentWidth, estimatedWidth);
            }

            // Cap the width to reasonable limits
            var finalWidth = Math.Min(contentWidth, 400);
            
            return SetColumnWidth(columnId, finalWidth);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Toggle column visibility
    /// </summary>
    public bool ToggleColumnVisibility(string columnId)
    {
        try
        {
            var column = _table.GetColumn(columnId);
            if (column == null)
            {
                return false;
            }

            _table.ToggleColumnVisibility(columnId);
            
            // Fire event
            _eventService.DispatchEvent("columnVisibilityChanged", new ColumnVisibilityChangedEventArgs<TData>(columnId, column.IsVisible));
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Pin/unpin a column
    /// </summary>
    public bool SetColumnPinned(string columnId, bool pinned)
    {
        var preferredArea = pinned ? ResolvePinnedPreference(columnId) : null;
        return SetColumnPinned(columnId, preferredArea);
    }

    public bool SetColumnPinned(string columnId, string? pinnedArea)
    {
        var column = _table.GetColumn(columnId);
        if (column == null)
        {
            return false;
        }

        if (!CanMoveToPinnedArea(column, pinnedArea))
        {
            return false;
        }

        var displayOrder = GetVisibleLeafOrder().ToList();
        if (!displayOrder.Contains(columnId))
        {
            return false;
        }

        var state = _table.State.ColumnPinning ?? new ColumnPinningState();
        var left = state.Left?.ToList() ?? new List<string>();
        var right = state.Right?.ToList() ?? new List<string>();

        left.Remove(columnId);
        right.Remove(columnId);

        if (!string.IsNullOrEmpty(pinnedArea))
        {
            if (!_table.Options.EnableColumnPinning)
            {
                return false;
            }

            if (string.Equals(pinnedArea, "left", StringComparison.OrdinalIgnoreCase))
            {
                InsertIntoPinnedList(left, columnId, displayOrder);
            }
            else if (string.Equals(pinnedArea, "right", StringComparison.OrdinalIgnoreCase))
            {
                InsertIntoPinnedList(right, columnId, displayOrder);
            }
            else
            {
                return false;
            }
        }

        var newState = new ColumnPinningState
        {
            Left = left.Count > 0 ? left : null,
            Right = right.Count > 0 ? right : null
        };

        _table.SetState(state => state with { ColumnPinning = newState });
        _eventService.DispatchEvent("columnPinned", new ColumnPinnedEventArgs<TData>(columnId, pinnedArea != null, pinnedArea));
        return true;
    }

    internal IReadOnlyList<string> GetVisibleLeafOrder()
    {
        return _table.VisibleLeafColumns.Select(c => c.Id).ToList();
    }

    internal string? GetColumnPinnedArea(string columnId)
    {
        var column = _table.GetColumn(columnId);
        return column?.PinnedPosition;
    }

    internal bool CanMoveColumn(string columnId, int targetIndex, string? targetPinnedArea)
    {
        var column = _table.GetColumn(columnId);
        if (column == null || !IsColumnMovable(column))
        {
            return false;
        }

        var displayOrder = GetVisibleLeafOrder().ToList();
        if (targetIndex < 0 || targetIndex > displayOrder.Count)
        {
            return false;
        }

        var destinationPinned = ResolveDestinationPinnedArea(targetPinnedArea, displayOrder, Math.Clamp(targetIndex, 0, Math.Max(displayOrder.Count - 1, 0)));
        if (!CanMoveToPinnedArea(column, destinationPinned))
        {
            return false;
        }

        return CanMoveRelativeToLockedColumns(columnId, Math.Clamp(targetIndex, 0, Math.Max(displayOrder.Count - 1, 0)), displayOrder);
    }

    private bool IsColumnMovable(Column<TData> column)
    {
        if (!column.CanResize && column.ColumnDef.Meta == null)
        {
            // respect table-level rule: if column moves disabled globally
            if (!_table.Options.EnableColumnReordering)
            {
                return false;
            }
        }

        if (column.ColumnDef.Meta != null)
        {
            if (TryGetMetaBool(column.ColumnDef.Meta, ColumnMetaKeys.SuppressMovable, out var suppress) && suppress)
            {
                return false;
            }

            if (TryGetMetaBool(column.ColumnDef.Meta, ColumnMetaKeys.LockPosition, out var locked) && locked)
            {
                return false;
            }
        }

        return true;
    }

    private bool CanMoveToPinnedArea(Column<TData> column, string? targetPinnedArea)
    {
        var currentPinned = column.PinnedPosition;

        if (column.ColumnDef.Meta != null &&
            TryGetMetaBool(column.ColumnDef.Meta, ColumnMetaKeys.LockPinned, out var lockPinned) && lockPinned)
        {
            return string.Equals(currentPinned, targetPinnedArea, StringComparison.OrdinalIgnoreCase);
        }

        if (!_table.Options.EnableColumnPinning && !string.IsNullOrEmpty(targetPinnedArea))
        {
            return false;
        }

        if (currentPinned == null && targetPinnedArea == null)
        {
            return true;
        }

        if (string.Equals(currentPinned, targetPinnedArea, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow unpinning or switching only if table option enables pinning
        return _table.Options.EnableColumnPinning;
    }

    private bool CanMoveRelativeToLockedColumns(string columnId, int targetIndex, List<string> displayOrder)
    {
        bool IsLocked(string id)
        {
            var column = _table.GetColumn(id);
            if (column?.ColumnDef.Meta == null)
            {
                return false;
            }

            return TryGetMetaBool(column.ColumnDef.Meta, ColumnMetaKeys.LockPosition, out var locked) && locked;
        }

        var lockedColumns = displayOrder.Where(IsLocked).ToList();
        if (!lockedColumns.Any())
        {
            return true;
        }

        // Ensure we are not moving before a locked column that precedes its locked range
        var lockedIndices = lockedColumns.Select(id => displayOrder.IndexOf(id)).Where(i => i >= 0).OrderBy(i => i).ToList();
        if (!lockedIndices.Any()) return true;

        var minLockedIndex = lockedIndices.First();
        var maxLockedIndex = lockedIndices.Last();

        if (IsLocked(columnId))
        {
            // Locked column can move only within locked range
            return targetIndex >= minLockedIndex && targetIndex <= maxLockedIndex;
        }

        // Unlocked columns cannot move into locked range
        return targetIndex < minLockedIndex || targetIndex > maxLockedIndex + 1;
    }

    private List<string> BuildNewColumnOrder(List<string> displayOrder)
    {
        var currentOrder = _table.State.ColumnOrder?.Order?.ToList() ?? _table.AllLeafColumns.Select(c => c.Id).ToList();
        var displaySet = new HashSet<string>(displayOrder);

        var newOrder = new List<string>(displayOrder);
        foreach (var id in currentOrder)
        {
            if (!displaySet.Contains(id))
            {
                newOrder.Add(id);
            }
        }

        return newOrder;
    }

    private ColumnPinningState BuildNewPinning(string columnId, string? targetPinnedArea, List<string> displayOrder)
    {
        var state = _table.State.ColumnPinning ?? new ColumnPinningState();
        var left = state.Left?.ToList() ?? new List<string>();
        var right = state.Right?.ToList() ?? new List<string>();

        left.Remove(columnId);
        right.Remove(columnId);

        if (string.Equals(targetPinnedArea, "left", StringComparison.OrdinalIgnoreCase))
        {
            InsertIntoPinnedList(left, columnId, displayOrder);
        }
        else if (string.Equals(targetPinnedArea, "right", StringComparison.OrdinalIgnoreCase))
        {
            InsertIntoPinnedList(right, columnId, displayOrder);
        }

        return new ColumnPinningState
        {
            Left = left.Count > 0 ? left : null,
            Right = right.Count > 0 ? right : null
        };
    }

    private static void InsertIntoPinnedList(List<string> list, string columnId, List<string> displayOrder)
    {
        var displayIndex = displayOrder.IndexOf(columnId);
        if (displayIndex < 0)
        {
            list.Add(columnId);
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var currentIndex = displayOrder.IndexOf(list[i]);
            if (currentIndex == -1 || currentIndex > displayIndex)
            {
                list.Insert(i, columnId);
                return;
            }
        }

        list.Add(columnId);
    }

    private static bool TryGetMetaBool(IReadOnlyDictionary<string, object> meta, string key, out bool value)
    {
        if (meta.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case bool b:
                    value = b;
                    return true;
                case string s when bool.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }
        }

        value = false;
        return false;
    }

    private string? ResolveDestinationPinnedArea(string? requestedArea, List<string> displayOrder, int targetIndex)
    {
        if (!string.IsNullOrEmpty(requestedArea))
        {
            return requestedArea;
        }

        if (targetIndex >= 0 && targetIndex < displayOrder.Count)
        {
            var targetColumn = _table.GetColumn(displayOrder[targetIndex]);
            return targetColumn?.PinnedPosition;
        }

        return null;
    }

    public bool ResizeColumnPair(string primaryColumnId, string? secondaryColumnId, double delta)
    {
        var primary = _table.GetColumn(primaryColumnId);
        if (primary == null || !primary.CanResize)
        {
            return false;
        }

        var (primaryMin, primaryMax) = GetColumnMinMax(primary);
        var sizing = _table.State.ColumnSizing ?? new ColumnSizingState();
        var primaryWidth = primary.Size;

        var desiredPrimary = Math.Clamp(primaryWidth + delta, primaryMin, primaryMax);
        var appliedDelta = desiredPrimary - primaryWidth;

        ColumnSizingState newSizing;

        if (!string.IsNullOrEmpty(secondaryColumnId))
        {
            var secondary = _table.GetColumn(secondaryColumnId);
            if (secondary == null || !secondary.CanResize)
            {
                return false;
            }

            var (secondaryMin, secondaryMax) = GetColumnMinMax(secondary);
            var secondaryWidth = secondary.Size;
            var desiredSecondary = Math.Clamp(secondaryWidth - appliedDelta, secondaryMin, secondaryMax);

            // adjust delta if neighbour cannot shrink fully
            var neighbourDelta = secondaryWidth - desiredSecondary;
            if (Math.Abs(neighbourDelta - appliedDelta) > 0.01)
            {
                appliedDelta = neighbourDelta;
                desiredPrimary = Math.Clamp(primaryWidth + appliedDelta, primaryMin, primaryMax);
            }

            newSizing = sizing
                .With(primaryColumnId, desiredPrimary)
                .With(secondaryColumnId!, desiredSecondary);

            _eventService.DispatchEvent("columnResized", new ColumnResizedEventArgs<TData>(secondaryColumnId!, desiredSecondary));
        }
        else
        {
            newSizing = sizing.With(primaryColumnId, desiredPrimary);
        }

        if (Math.Abs(appliedDelta) < 0.01)
        {
            return false;
        }

        _table.SetState(state => state with { ColumnSizing = newSizing });
        _eventService.DispatchEvent("columnResized", new ColumnResizedEventArgs<TData>(primaryColumnId, desiredPrimary));
        return true;
    }

    private (double Min, double Max) GetColumnMinMax(Column<TData> column)
    {
        var min = column.ColumnDef.MinSize.HasValue
            ? Math.Max(column.ColumnDef.MinSize.Value, 1)
            : 40;

        var max = column.ColumnDef.MaxSize.HasValue
            ? Math.Max(column.ColumnDef.MaxSize.Value, min)
            : double.PositiveInfinity;

        return (min, max);
    }

    private string? ResolvePinnedPreference(string columnId)
    {
        var column = _table.GetColumn(columnId);
        if (column?.ColumnDef.Meta != null &&
            column.ColumnDef.Meta.TryGetValue(ColumnMetaKeys.Pinned, out var value))
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return "left";
    }
}

/// <summary>
/// Event arguments for column operations
/// </summary>
public class ColumnMovedEventArgs<TData> : AgEventArgs
{
    public string ColumnId { get; }
    public int NewIndex { get; }
    public string? PinnedArea { get; }

    public ColumnMovedEventArgs(string columnId, int newIndex, string? pinnedArea) : base("columnMoved", columnId)
    {
        ColumnId = columnId;
        NewIndex = newIndex;
        PinnedArea = pinnedArea;
    }
}

public class ColumnResizedEventArgs<TData> : AgEventArgs
{
    public string ColumnId { get; }
    public double NewWidth { get; }

    public ColumnResizedEventArgs(string columnId, double newWidth) : base("columnResized", columnId)
    {
        ColumnId = columnId;
        NewWidth = newWidth;
    }
}

public class ColumnVisibilityChangedEventArgs<TData> : AgEventArgs
{
    public string ColumnId { get; }
    public bool IsVisible { get; }

    public ColumnVisibilityChangedEventArgs(string columnId, bool isVisible) : base("columnVisibilityChanged", columnId)
    {
        ColumnId = columnId;
        IsVisible = isVisible;
    }
}

public class ColumnPinnedEventArgs<TData> : AgEventArgs
{
    public string ColumnId { get; }
    public bool IsPinned { get; }
    public string? PinnedArea { get; }

    public ColumnPinnedEventArgs(string columnId, bool isPinned, string? pinnedArea) : base("columnPinned", columnId)
    {
        ColumnId = columnId;
        IsPinned = isPinned;
        PinnedArea = pinnedArea;
    }
}
