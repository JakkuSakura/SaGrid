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
        InitializeSizingState();
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
        if (column == null || !column.CanResize || IsStarColumn(column))
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

            var targetWidth = ComputeAutoSizeTarget(column);
            if (!targetWidth.HasValue)
            {
                return false;
            }

            return SetColumnWidth(columnId, targetWidth.Value);
        }
        catch
        {
            return false;
        }
    }

    public bool AutoSizeColumnPair(string primaryColumnId, string? secondaryColumnId)
    {
        try
        {
            if (string.IsNullOrEmpty(secondaryColumnId))
            {
                return AutoSizeColumn(primaryColumnId);
            }

            if (_table.GetColumn(primaryColumnId) is not Column<TData> primary ||
                _table.GetColumn(secondaryColumnId) is not Column<TData> secondary)
            {
                return false;
            }

            if (!primary.CanResize || !secondary.CanResize)
            {
                return false;
            }

            var targetWidth = ComputeAutoSizeTarget(primary);
            if (!targetWidth.HasValue)
            {
                return false;
            }

            var delta = targetWidth.Value - primary.Size;
            if (Math.Abs(delta) < 0.01)
            {
                return false;
            }

            return ResizeColumnPair(primaryColumnId, secondaryColumnId, delta);
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
        if (string.IsNullOrEmpty(secondaryColumnId))
        {
            return false;
        }

        if (_table.GetColumn(primaryColumnId) is not Column<TData> primary ||
            _table.GetColumn(secondaryColumnId) is not Column<TData> secondary)
        {
            return false;
        }

        if (!primary.CanResize || !secondary.CanResize)
        {
            return false;
        }

        var sizing = _table.State.ColumnSizing ?? new ColumnSizingState();
        var widths = new Dictionary<string, double>(sizing.Items);
        var starWeights = new Dictionary<string, double>(sizing.StarWeights);
        var totalWidth = sizing.TotalWidth
            ?? _table.VisibleLeafColumns.Cast<Column<TData>>().Sum(c => c.Size);

    var visibleColumns = _table.VisibleLeafColumns.Cast<Column<TData>>().ToList();
    EnsureWidthEntries(widths, visibleColumns);

    var starColumns = visibleColumns.Where(IsStarColumn).ToList();
    BackfillMissingStarWidths(visibleColumns, starColumns, widths, starWeights, ref totalWidth);

        var primarySnapshot = CreateSnapshot(primary);
        var secondarySnapshot = CreateSnapshot(secondary);

        var changed = false;
        var scenario = ResizeScenario.FixedFixed;

        if (!primarySnapshot.IsStar && !secondarySnapshot.IsStar)
        {
            changed = ApplyFixedFixedResize(primarySnapshot, secondarySnapshot, delta, widths);
            scenario = ResizeScenario.FixedFixed;
        }
        else if (primarySnapshot.IsStar && secondarySnapshot.IsStar)
        {
            changed = ApplyStarStarResize(primarySnapshot, secondarySnapshot, delta, widths);
            scenario = ResizeScenario.StarStar;
        }
        else if (!primarySnapshot.IsStar && secondarySnapshot.IsStar)
        {
            changed = ApplyFixedStarResize(primarySnapshot, secondarySnapshot, delta, widths);
            scenario = ResizeScenario.FixedStar;
        }
        else
        {
            changed = ApplyStarFixedResize(primarySnapshot, secondarySnapshot, delta, widths);
            scenario = ResizeScenario.StarFixed;
        }

        if (!changed)
        {
            return false;
        }

        NormalizeStarWeights(widths, starWeights, starColumns, scenario, primarySnapshot, secondarySnapshot);

        var capturedTotalWidth = widths.Values.Sum();
        if (capturedTotalWidth > 0)
        {
            totalWidth = capturedTotalWidth;
        }

        var newState = new ColumnSizingState(widths, starWeights, totalWidth);
        _table.SetState(state => state with { ColumnSizing = newState });

        if (widths.TryGetValue(primary.Id, out var primaryWidth))
        {
            _eventService.DispatchEvent("columnResized", new ColumnResizedEventArgs<TData>(primary.Id, primaryWidth));
        }

        if (widths.TryGetValue(secondary.Id, out var secondaryWidth))
        {
            _eventService.DispatchEvent("columnResized", new ColumnResizedEventArgs<TData>(secondary.Id, secondaryWidth));
        }

        return true;
    }

    private readonly record struct ColumnSnapshot(Column<TData> Column, double Width, double Min, double Max, bool IsStar)
    {
        public string Id => Column.Id;
    }

    private ColumnSnapshot CreateSnapshot(Column<TData> column)
    {
        var (min, max) = GetColumnMinMax(column);
        return new ColumnSnapshot(column, column.Size, min, max, IsStarColumn(column));
    }

    private static (double Primary, double Secondary) SolvePairedWidths(ColumnSnapshot primary, ColumnSnapshot secondary, double delta)
    {
        var pairSum = primary.Width + secondary.Width;
        var desiredPrimary = Math.Clamp(primary.Width + delta, primary.Min, primary.Max);
        desiredPrimary = Math.Clamp(desiredPrimary, pairSum - secondary.Max, pairSum - secondary.Min);
        var desiredSecondary = pairSum - desiredPrimary;
        desiredSecondary = Math.Clamp(desiredSecondary, secondary.Min, secondary.Max);
        desiredPrimary = pairSum - desiredSecondary;
        return (desiredPrimary, desiredSecondary);
    }

    private bool ApplyFixedFixedResize(ColumnSnapshot primary, ColumnSnapshot secondary, double delta, Dictionary<string, double> widths)
    {
        var (desiredPrimary, desiredSecondary) = SolvePairedWidths(primary, secondary, delta);

        if (Math.Abs(desiredPrimary - primary.Width) < 0.01 && Math.Abs(desiredSecondary - secondary.Width) < 0.01)
        {
            return false;
        }

        widths[primary.Id] = desiredPrimary;
        widths[secondary.Id] = desiredSecondary;
        return true;
    }

    private bool ApplyStarStarResize(
        ColumnSnapshot primary,
        ColumnSnapshot secondary,
        double delta,
        Dictionary<string, double> widths)
    {
        var (desiredPrimary, desiredSecondary) = SolvePairedWidths(primary, secondary, delta);

        if (Math.Abs(desiredPrimary - primary.Width) < 0.01 && Math.Abs(desiredSecondary - secondary.Width) < 0.01)
        {
            return false;
        }

        widths[primary.Id] = desiredPrimary;
        widths[secondary.Id] = desiredSecondary;
        return true;
    }

    private bool ApplyFixedStarResize(
        ColumnSnapshot fixedColumn,
        ColumnSnapshot starColumn,
        double delta,
        Dictionary<string, double> widths)
    {
        var (desiredFixed, desiredStar) = SolvePairedWidths(fixedColumn, starColumn, delta);

        if (Math.Abs(desiredFixed - fixedColumn.Width) < 0.01 && Math.Abs(desiredStar - starColumn.Width) < 0.01)
        {
            return false;
        }

        widths[fixedColumn.Id] = desiredFixed;

        widths[starColumn.Id] = desiredStar;
        return true;
    }

    private bool ApplyStarFixedResize(
        ColumnSnapshot starColumn,
        ColumnSnapshot fixedColumn,
        double delta,
        Dictionary<string, double> widths)
    {
        var (desiredStar, desiredFixed) = SolvePairedWidths(starColumn, fixedColumn, delta);

        if (Math.Abs(desiredStar - starColumn.Width) < 0.01 && Math.Abs(desiredFixed - fixedColumn.Width) < 0.01)
        {
            return false;
        }

        widths[fixedColumn.Id] = desiredFixed;

        widths[starColumn.Id] = desiredStar;
        return true;
    }

    private void InitializeSizingState()
    {
        var sizing = _table.State.ColumnSizing;
        if (sizing == null)
        {
            return;
        }

        var visibleColumns = _table.VisibleLeafColumns.Cast<Column<TData>>().ToList();
        if (visibleColumns.Count == 0 || !visibleColumns.Any(IsStarColumn))
        {
            return;
        }

        var widths = new Dictionary<string, double>(sizing.Items);
        var starWeights = new Dictionary<string, double>(sizing.StarWeights);
        var totalWidth = sizing.TotalWidth ?? 0;

        var previousWidths = new Dictionary<string, double>(widths);
        var previousWeights = new Dictionary<string, double>(starWeights);
        var previousTotal = sizing.TotalWidth;

        EnsureWidthEntries(widths, visibleColumns);

        var starColumns = visibleColumns.Where(IsStarColumn).ToList();
        BackfillMissingStarWidths(visibleColumns, starColumns, widths, starWeights, ref totalWidth);

        var normalizedTotal = totalWidth > 0 ? totalWidth : sizing.TotalWidth;

        if (!AreDictionariesEqual(previousWidths, widths) ||
            !AreDictionariesEqual(previousWeights, starWeights) ||
            !TotalsEqual(previousTotal, normalizedTotal))
        {
            var updatedState = new ColumnSizingState(widths, starWeights, normalizedTotal);
            _table.SetState(state => state with { ColumnSizing = updatedState }, updateRowModel: false);
        }
    }

    private static bool TotalsEqual(double? original, double? current)
    {
        if (!original.HasValue && !current.HasValue)
        {
            return true;
        }

        if (!original.HasValue || !current.HasValue)
        {
            return false;
        }

        return Math.Abs(original.Value - current.Value) <= 0.5;
    }

    private static bool AreDictionariesEqual(IReadOnlyDictionary<string, double> left, IReadOnlyDictionary<string, double> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var candidate))
            {
                return false;
            }

            if (Math.Abs(candidate - value) > 0.5)
            {
                return false;
            }
        }

        foreach (var key in right.Keys)
        {
            if (!left.ContainsKey(key))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureWidthEntries(Dictionary<string, double> widths, IReadOnlyList<Column<TData>> columns)
    {
        foreach (var column in columns)
        {
            if (!widths.ContainsKey(column.Id))
            {
                widths[column.Id] = column.Size;
            }
        }
    }

    private void NormalizeStarWeights(
        Dictionary<string, double> widths,
        Dictionary<string, double> starWeights,
        IReadOnlyList<Column<TData>> starColumns,
        ResizeScenario scenario,
        ColumnSnapshot primary,
        ColumnSnapshot secondary)
    {
        var starIds = new HashSet<string>(starColumns.Select(c => c.Id));

        foreach (var key in starWeights.Keys.ToList())
        {
            if (!starIds.Contains(key))
            {
                starWeights.Remove(key);
            }
        }

        if (starColumns.Count == 0)
        {
            return;
        }

        foreach (var column in starColumns)
        {
            var (min, _) = GetColumnMinMax(column);
            var width = widths.TryGetValue(column.Id, out var stored)
                ? stored
                : column.Size;

            var clamped = Math.Max(width, min);
            widths[column.Id] = clamped;

            var extra = Math.Max(clamped - min, 0);
            var definition = column.ColumnDef.Width;
            var fallback = definition.HasValue && definition.Value.Mode == ColumnWidthMode.Star
                ? Math.Max(definition.Value.Value, 0.0001)
                : 1.0;

            starWeights[column.Id] = extra > 0.0001 ? extra : fallback;
        }
    }

    private enum ResizeScenario
    {
        FixedFixed,
        StarStar,
        FixedStar,
        StarFixed
    }

    private void BackfillMissingStarWidths(
        IReadOnlyList<Column<TData>> visibleColumns,
        IReadOnlyList<Column<TData>> starColumns,
        Dictionary<string, double> widths,
        Dictionary<string, double> starWeights,
        ref double totalWidth)
    {
        if (starColumns.Count == 0)
        {
            totalWidth = totalWidth > 0 ? totalWidth : widths.Values.Sum();
            return;
        }

        var missingStars = starColumns.Where(c =>
            !widths.ContainsKey(c.Id) ||
            !starWeights.TryGetValue(c.Id, out var storedWeight) || storedWeight <= 0.0001).ToList();
        if (missingStars.Count == 0)
        {
            totalWidth = totalWidth > 0 ? totalWidth : widths.Values.Sum();
            return;
        }

        double fixedTotal = 0;
        double knownStarTotal = 0;

        foreach (var column in visibleColumns)
        {
            if (IsStarColumn(column))
            {
                if (starWeights.TryGetValue(column.Id, out var storedWeight) && storedWeight > 0.0001 &&
                    widths.TryGetValue(column.Id, out var starWidth))
                {
                    knownStarTotal += starWidth;
                }
            }
            else if (widths.TryGetValue(column.Id, out var fixedWidth))
            {
                fixedTotal += fixedWidth;
            }
        }

        double configuredTotal = totalWidth;
        if (configuredTotal <= 0)
        {
            configuredTotal = fixedTotal + knownStarTotal;
        }

        var minSum = missingStars.Sum(c => GetColumnMinMax(c).Min);
        var availableForStars = Math.Max(configuredTotal - fixedTotal, 0);
        var remainingForMissing = Math.Max(availableForStars - knownStarTotal, 0);
        var extraSpace = Math.Max(remainingForMissing - minSum, 0);

        var totalWeight = missingStars.Sum(c =>
        {
            if (starWeights.TryGetValue(c.Id, out var stored) && stored > 0.0001)
            {
                return stored;
            }

            var definition = c.ColumnDef.Width;
            if (definition.HasValue && definition.Value.Mode == ColumnWidthMode.Star && definition.Value.Value > 0.0001)
            {
                return definition.Value.Value;
            }

            return 1.0;
        });

        if (totalWeight <= 0.0001)
        {
            totalWeight = missingStars.Count;
        }

        foreach (var column in missingStars)
        {
            var (min, max) = GetColumnMinMax(column);

            double weight = 1.0;
            if (starWeights.TryGetValue(column.Id, out var storedWeight) && storedWeight > 0.0001)
            {
                weight = storedWeight;
            }
            else if (column.ColumnDef.Width.HasValue && column.ColumnDef.Width.Value.Mode == ColumnWidthMode.Star)
            {
                weight = Math.Max(column.ColumnDef.Width.Value.Value, 0.0001);
            }

            var share = extraSpace > 0 ? (weight / totalWeight) * extraSpace : 0;
            var width = Math.Clamp(min + share, min, max);

            widths[column.Id] = width;
            starWeights[column.Id] = Math.Max(width - min, 0.0001);
            knownStarTotal += width;
        }

        if (configuredTotal <= 0)
        {
            totalWidth = fixedTotal + knownStarTotal;
        }
    }

    private double? ComputeAutoSizeTarget(Column<TData> column)
    {
        if (column == null || IsStarColumn(column))
        {
            return null;
        }

        var columnId = column.Id;

        var headerWidth = Math.Max(100, columnId.Length * 8 + 40);

        var contentWidth = headerWidth;
        var sampleRows = _table.RowModel.Rows.Take(10);

        foreach (var row in sampleRows)
        {
            var cellValue = row.GetValue<object>(columnId)?.ToString() ?? string.Empty;
            var estimatedWidth = cellValue.Length * 8 + 20;
            contentWidth = Math.Max(contentWidth, estimatedWidth);
        }

        var capped = Math.Min(contentWidth, 400);
        var (minWidth, maxWidth) = GetColumnMinMax(column);
        return Math.Clamp(capped, minWidth, maxWidth);
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

    private static bool IsStarColumn(Column<TData> column)
    {
        return column.ColumnDef.Width?.Mode == ColumnWidthMode.Star;
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
