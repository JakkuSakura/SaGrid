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
    private const double DefaultMinWidth = 40d;

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
            : DefaultMinWidth;

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

    public TableColumnLayoutManager(Table<TData> table)
    {
        _table = table;
        _snapshot = TableColumnLayoutSnapshot.From(table);
    }

    public TableColumnLayoutSnapshot Snapshot => _snapshot;

    public ColumnLayoutPanel CreatePanel()
    {
        var panel = new ColumnLayoutPanel();
        RegisterPanel(panel);
        return panel;
    }

    public void RegisterPanel(ColumnLayoutPanel panel)
    {
        if (!_panels.Contains(panel))
        {
            _panels.Add(panel);
            panel.Layout = _snapshot;
            panel.DetachedFromVisualTree += PanelOnDetachedFromVisualTree;
        }
    }

    private void PanelOnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is ColumnLayoutPanel panel)
        {
            panel.DetachedFromVisualTree -= PanelOnDetachedFromVisualTree;
            _panels.Remove(panel);
        }
    }

    public void Refresh()
    {
        _snapshot = TableColumnLayoutSnapshot.From(_table);
        foreach (var panel in _panels.ToArray())
        {
            panel.Layout = _snapshot;
        }
    }
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
