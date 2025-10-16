using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using SaGrid.Core;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public readonly record struct TableColumnMeasurement(
    string ColumnId,
    double Width,
    double MinWidth,
    double MaxWidth,
    bool CanResize);

public sealed class TableColumnLayoutSnapshot
{
    private readonly Dictionary<string, int> _indexById;
    private readonly double[] _offsets;
    private readonly string[] _orderedIds;

    public TableColumnLayoutSnapshot(IReadOnlyList<TableColumnMeasurement> measurements)
    {
        Columns = measurements;
        _orderedIds = measurements.Select(m => m.ColumnId).ToArray();
        _indexById = new Dictionary<string, int>(_orderedIds.Length);

        _offsets = new double[_orderedIds.Length];
        double offset = 0;
        for (var i = 0; i < _orderedIds.Length; i++)
        {
            _indexById[_orderedIds[i]] = i;
            _offsets[i] = offset;
            offset += measurements[i].Width;
        }

        TotalWidth = offset;
    }

    public IReadOnlyList<TableColumnMeasurement> Columns { get; }
    public double TotalWidth { get; }

    public IEnumerable<string> ColumnIds => _orderedIds;

    public double GetWidth(string columnId)
    {
        return Columns[GetIndex(columnId)].Width;
    }

    public double GetMinWidth(string columnId)
    {
        return Columns[GetIndex(columnId)].MinWidth;
    }

    public double GetMaxWidth(string columnId)
    {
        return Columns[GetIndex(columnId)].MaxWidth;
    }

    public double GetOffset(string columnId)
    {
        return _offsets[GetIndex(columnId)];
    }

    public double GetSpanWidth(IReadOnlyList<string> columnIds)
    {
        if (columnIds == null || columnIds.Count == 0)
        {
            return 0;
        }

        var firstIndex = GetIndex(columnIds[0]);
        var lastIndex = GetIndex(columnIds[^1]);

        var start = _offsets[firstIndex];
        var end = _offsets[lastIndex] + Columns[lastIndex].Width;
        return Math.Max(0, end - start);
    }

    private int GetIndex(string columnId)
    {
        if (_indexById.TryGetValue(columnId, out var index))
        {
            return index;
        }

        throw new ArgumentException($"Unknown column id '{columnId}'", nameof(columnId));
    }

    public static TableColumnLayoutSnapshot From<TData>(Table<TData> table)
    {
        return new TableColumnLayoutSnapshot(TableColumnLayout.MeasureColumns(table));
    }
}

public static class TableColumnLayout
{
    internal const double DefaultMinWidth = 40d;

    public static IReadOnlyList<TableColumnMeasurement> MeasureColumns<TData>(Table<TData> table)
    {
        var measurements = new List<TableColumnMeasurement>(table.VisibleLeafColumns.Count);

        foreach (var column in table.VisibleLeafColumns.Cast<Column<TData>>())
        {
            measurements.Add(CreateMeasurement(column));
        }

        return measurements;
    }

    private static TableColumnMeasurement CreateMeasurement<TData>(Column<TData> column)
    {
        var min = column.ColumnDef.MinSize.HasValue
            ? Math.Max(column.ColumnDef.MinSize.Value, 1)
            : 40d;

        var max = column.ColumnDef.MaxSize.HasValue
            ? Math.Max(column.ColumnDef.MaxSize.Value, (int)Math.Ceiling(min))
            : (int?)null;

        var maxWidth = max.HasValue ? (double)max.Value : double.PositiveInfinity;

        var width = column.Size;
        if (double.IsNaN(width) || width <= 0)
        {
            width = column.ColumnDef.Size ?? min;
        }

        width = Math.Max(width, min);
        if (!double.IsPositiveInfinity(maxWidth))
        {
            width = Math.Min(width, maxWidth);
        }

        return new TableColumnMeasurement(column.Id, width, min, maxWidth, column.CanResize);
    }
}

public sealed class TableColumnLayoutManager<TData>
{
    private readonly Table<TData> _table;
    private TableColumnLayoutSnapshot _snapshot;
    private readonly List<ColumnLayoutPanel> _panels = new();
    private readonly Dictionary<string, ColumnWidthDefinition> _widthDefinitions = new();
    private readonly HashSet<string> _manualStarOverrides = new();
    private double _lastAvailableWidth = double.NaN;
    private bool _isApplyingSizing;
    private int _autoSizingSuspendCount;
    private bool _pendingStarSizing;

    public TableColumnLayoutManager(Table<TData> table)
    {
        _table = table;
        UpdateWidthDefinitions();
        _snapshot = TableColumnLayoutSnapshot.From(table);
    }

    public TableColumnLayoutSnapshot Snapshot => _snapshot;

    public ColumnLayoutPanel CreatePanel()
    {
        var panel = new ColumnLayoutPanel
        {
            WidthObserver = OnPanelWidthAvailable
        };
        RegisterPanel(panel);
        return panel;
    }

    public void RegisterPanel(ColumnLayoutPanel panel)
    {
        if (!_panels.Contains(panel))
        {
            _panels.Add(panel);
            panel.Layout = _snapshot;
            panel.WidthObserver = OnPanelWidthAvailable;
            panel.DetachedFromVisualTree += PanelOnDetachedFromVisualTree;
        }
    }

    private void PanelOnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is ColumnLayoutPanel panel)
        {
            panel.DetachedFromVisualTree -= PanelOnDetachedFromVisualTree;
            panel.WidthObserver = null;
            _panels.Remove(panel);
        }
    }

    public void Refresh()
    {
        UpdateWidthDefinitions();

        _snapshot = TableColumnLayoutSnapshot.From(_table);
        foreach (var panel in _panels.ToArray())
        {
            panel.Layout = _snapshot;
        }

        if (_autoSizingSuspendCount == 0 && !double.IsNaN(_lastAvailableWidth))
        {
            EnsureStarSizing(_lastAvailableWidth);
        }
        else if (_autoSizingSuspendCount > 0)
        {
            _pendingStarSizing = true;
        }
    }

    private void UpdateWidthDefinitions()
    {
        _widthDefinitions.Clear();

        foreach (var column in _table.AllLeafColumns)
        {
            if (column.ColumnDef.Width.HasValue)
            {
                _widthDefinitions[column.Id] = column.ColumnDef.Width.Value;
            }
        }

        foreach (var columnId in _manualStarOverrides.ToList())
        {
            if (!_widthDefinitions.TryGetValue(columnId, out var definition) || definition.Mode != ColumnWidthMode.Star)
            {
                _manualStarOverrides.Remove(columnId);
            }
        }
    }

    private void OnPanelWidthAvailable(double width)
    {
        if (double.IsNaN(width) || width <= 0)
        {
            return;
        }

        _lastAvailableWidth = width;

        if (_autoSizingSuspendCount == 0)
        {
            EnsureStarSizing(width);
        }
        else
        {
            _pendingStarSizing = true;
        }
    }

    public IDisposable? BeginUserResize(string columnId)
    {
        _autoSizingSuspendCount++;
        _pendingStarSizing = true;
        return new AutoSizingScope(this);
    }

    private void EndUserResize()
    {
        if (_autoSizingSuspendCount == 0)
        {
            return;
        }

        _autoSizingSuspendCount--;

        if (_autoSizingSuspendCount == 0 && _pendingStarSizing && !double.IsNaN(_lastAvailableWidth))
        {
            EnsureStarSizing(_lastAvailableWidth);
        }
    }

    public void RegisterManualWidth(string columnId)
    {
        _pendingStarSizing = true;

        if (_widthDefinitions.TryGetValue(columnId, out var definition) && definition.Mode == ColumnWidthMode.Star)
        {
            _manualStarOverrides.Add(columnId);
        }
    }

    private void EnsureStarSizing(double availableWidth)
    {
        if (_isApplyingSizing)
        {
            return;
        }

        var visibleColumns = _table.VisibleLeafColumns.Cast<Column<TData>>().ToList();
        if (visibleColumns.Count == 0)
        {
            _pendingStarSizing = false;
            return;
        }

        if (!_widthDefinitions.Values.Any(def => def.Mode == ColumnWidthMode.Star))
        {
            _pendingStarSizing = false;
            return;
        }

        var sizingState = _table.State.ColumnSizing ?? new ColumnSizingState();
        var updatedItems = new Dictionary<string, double>(sizingState.Items);
        var starColumns = new List<StarColumnInfo>();
        double fixedTotal = 0;
        var changed = false;

        foreach (var columnId in _manualStarOverrides.ToList())
        {
            if (!updatedItems.ContainsKey(columnId))
            {
                _manualStarOverrides.Remove(columnId);
            }
        }

        foreach (var column in visibleColumns)
        {
            var (min, max) = GetBounds(column);
            var hasManualSizing = sizingState.Items.ContainsKey(column.Id);

            if (_manualStarOverrides.Contains(column.Id))
            {
                if (updatedItems.TryGetValue(column.Id, out var manualWidth))
                {
                    var clampedManual = ClampToBounds(manualWidth, min, max);
                    if (Math.Abs(clampedManual - manualWidth) > 0.5)
                    {
                        updatedItems[column.Id] = clampedManual;
                        changed = true;
                    }

                    fixedTotal += clampedManual;
                }

                continue;
            }

            if (_widthDefinitions.TryGetValue(column.Id, out var definition))
            {
                if (definition.Mode == ColumnWidthMode.Fixed)
                {
                    if (hasManualSizing && updatedItems.TryGetValue(column.Id, out var manualWidth))
                    {
                        var clampedManual = ClampToBounds(manualWidth, min, max);
                        if (Math.Abs(clampedManual - manualWidth) > 0.5)
                        {
                            updatedItems[column.Id] = clampedManual;
                            changed = true;
                        }

                        fixedTotal += clampedManual;
                    }
                    else
                    {
                        var target = ClampToBounds(definition.Value, min, max);
                        if (!updatedItems.TryGetValue(column.Id, out var current) || Math.Abs(current - target) > 0.5)
                        {
                            updatedItems[column.Id] = target;
                            changed = true;
                        }

                        fixedTotal += target;
                    }

                    continue;
                }

                if (definition.Mode == ColumnWidthMode.Star)
                {
                    updatedItems.Remove(column.Id);

                    var currentWidth = sizingState.Items.TryGetValue(column.Id, out var stored)
                        ? ClampToBounds(stored, min, max)
                        : ClampToBounds(column.Size, min, max);

                    starColumns.Add(new StarColumnInfo(column.Id, definition.Value <= 0 ? 1 : definition.Value, min, max, currentWidth));
                    continue;
                }
            }

            var fallbackWidth = updatedItems.TryGetValue(column.Id, out var existingWidth)
                ? existingWidth
                : ClampToBounds(column.Size, min, max);

            fallbackWidth = ClampToBounds(fallbackWidth, min, max);

            if (!updatedItems.TryGetValue(column.Id, out var existing) || Math.Abs(existing - fallbackWidth) > 0.5)
            {
                updatedItems[column.Id] = fallbackWidth;
                changed = true;
            }

            fixedTotal += fallbackWidth;
        }

        if (starColumns.Count == 0)
        {
            if (changed)
            {
                ApplySizing(updatedItems);
            }

            _pendingStarSizing = false;
            return;
        }

        var starWidths = ComputeStarWidths(availableWidth, fixedTotal, starColumns);

        foreach (var star in starColumns)
        {
            var target = ClampToBounds(starWidths.GetValueOrDefault(star.Id, star.Current), star.Min, star.Max);
            if (!updatedItems.TryGetValue(star.Id, out var existing) || Math.Abs(existing - target) > 0.5)
            {
                updatedItems[star.Id] = target;
                changed = true;
            }
        }

        if (changed)
        {
            ApplySizing(updatedItems);
        }

        _pendingStarSizing = false;
    }

    private void ApplySizing(Dictionary<string, double> items)
    {
        _isApplyingSizing = true;
        try
        {
            var newSizing = new ColumnSizingState(new Dictionary<string, double>(items));
            _table.SetState(state => state with { ColumnSizing = newSizing }, updateRowModel: false);
        }
        finally
        {
            _isApplyingSizing = false;
        }
    }

    private static Dictionary<string, double> ComputeStarWidths(double availableWidth, double fixedTotal, List<StarColumnInfo> stars)
    {
        var result = new Dictionary<string, double>(stars.Count);
        if (stars.Count == 0)
        {
            return result;
        }

        foreach (var star in stars)
        {
            result[star.Id] = star.Min;
        }

        var available = Math.Max(availableWidth - fixedTotal, 0);
        var totalMin = stars.Sum(s => s.Min);
        var remainingExtra = Math.Max(available - totalMin, 0);

        if (remainingExtra <= 0)
        {
            return result;
        }

        var active = new List<StarColumnInfo>(stars);

        while (remainingExtra > 0 && active.Count > 0)
        {
            var weightSum = active.Sum(s => s.Weight);
            if (weightSum <= 0)
            {
                break;
            }

            var toRemove = new List<StarColumnInfo>();

            foreach (var star in active)
            {
                var baseWidth = result[star.Id];
                var maxGrowth = double.IsPositiveInfinity(star.Max) ? double.PositiveInfinity : star.Max - baseWidth;
                if (maxGrowth <= 0)
                {
                    toRemove.Add(star);
                    continue;
                }

                var share = remainingExtra * (star.Weight / weightSum);
                var applied = Math.Min(share, maxGrowth);
                result[star.Id] = baseWidth + applied;
                remainingExtra -= applied;

                if (maxGrowth - applied <= 0.01)
                {
                    toRemove.Add(star);
                }

                if (remainingExtra <= 0)
                {
                    break;
                }
            }

            if (toRemove.Count == 0)
            {
                var evenShare = remainingExtra / active.Count;
                foreach (var star in active)
                {
                    var baseWidth = result[star.Id];
                    var maxGrowth = double.IsPositiveInfinity(star.Max) ? double.PositiveInfinity : star.Max - baseWidth;
                    if (maxGrowth <= 0)
                    {
                        continue;
                    }

                    var applied = Math.Min(evenShare, maxGrowth);
                    result[star.Id] = baseWidth + applied;
                    remainingExtra -= applied;

                    if (remainingExtra <= 0)
                    {
                        break;
                    }
                }

                break;
            }

            foreach (var star in toRemove)
            {
                active.Remove(star);
            }
        }

        return result;
    }

    private static (double Min, double Max) GetBounds(Column<TData> column)
    {
        var min = column.ColumnDef.MinSize.HasValue
            ? Math.Max(column.ColumnDef.MinSize.Value, 1)
            : TableColumnLayout.DefaultMinWidth;

        var max = column.ColumnDef.MaxSize.HasValue
            ? Math.Max(column.ColumnDef.MaxSize.Value, min)
            : double.PositiveInfinity;

        return (min, max);
    }

    private static double ClampToBounds(double width, double min, double max)
    {
        var clamped = double.IsNaN(width) ? min : Math.Max(width, min);
        if (!double.IsPositiveInfinity(max))
        {
            clamped = Math.Min(clamped, max);
        }

        return clamped;
    }

    private sealed class AutoSizingScope : IDisposable
    {
        private readonly TableColumnLayoutManager<TData> _manager;
        private bool _disposed;

        public AutoSizingScope(TableColumnLayoutManager<TData> manager)
        {
            _manager = manager;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _manager.EndUserResize();
        }
    }

    private readonly record struct StarColumnInfo(string Id, double Weight, double Min, double Max, double Current);
}

public class ColumnLayoutPanel : Panel
{
    public static readonly StyledProperty<TableColumnLayoutSnapshot?> LayoutProperty =
        AvaloniaProperty.Register<ColumnLayoutPanel, TableColumnLayoutSnapshot?>(nameof(Layout));

    public static readonly AttachedProperty<string?> ColumnIdProperty =
        AvaloniaProperty.RegisterAttached<ColumnLayoutPanel, Control, string?>("ColumnId");

    public static readonly AttachedProperty<IReadOnlyList<string>?> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<ColumnLayoutPanel, Control, IReadOnlyList<string>?>("ColumnSpan");

    static ColumnLayoutPanel()
    {
        LayoutProperty.Changed.AddClassHandler<ColumnLayoutPanel>((panel, _) =>
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        });

        ColumnIdProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (control.Parent is ColumnLayoutPanel parent)
            {
                parent.InvalidateArrange();
            }
        });

        ColumnSpanProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (control.Parent is ColumnLayoutPanel parent)
            {
                parent.InvalidateMeasure();
                parent.InvalidateArrange();
            }
        });
    }

    public TableColumnLayoutSnapshot? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public Action<double>? WidthObserver { get; set; }

    public static void SetColumnId(Control control, string? columnId) => control.SetValue(ColumnIdProperty, columnId);
    public static string? GetColumnId(Control control) => control.GetValue(ColumnIdProperty);

    public static void SetColumnSpan(Control control, IReadOnlyList<string>? columnIds) => control.SetValue(ColumnSpanProperty, columnIds);
    public static IReadOnlyList<string>? GetColumnSpan(Control control) => control.GetValue(ColumnSpanProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        double desiredHeight = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height));
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
        }

        var width = Layout?.TotalWidth ?? 0;
        if (!double.IsInfinity(availableSize.Width) && availableSize.Width > width)
        {
            width = availableSize.Width;
        }

        return new Size(width, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = Layout;

        WidthObserver?.Invoke(finalSize.Width);

        foreach (var child in Children)
        {
            double left = 0;
            double width = finalSize.Width;

            if (layout != null)
            {
                var span = GetColumnSpan(child);
                if (span != null && span.Count > 0)
                {
                    left = layout.GetOffset(span[0]);
                    width = layout.GetSpanWidth(span);
                }
                else
                {
                    var columnId = GetColumnId(child);
                    if (!string.IsNullOrEmpty(columnId))
                    {
                        left = layout.GetOffset(columnId!);
                        width = layout.GetWidth(columnId!);
                    }
                }
            }

            var rect = new Rect(left, 0, width, finalSize.Height);
            child.Arrange(rect);
        }

        return finalSize;
    }
}
