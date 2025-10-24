using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

/// <summary>
/// Display-only virtualized body panel. It materializes only the visible rows
/// and arranges cells using the provided TableColumnLayoutSnapshot. No interactions.
/// </summary>
internal sealed class VirtualizedBodyPanel<TData> : Panel
{
    private readonly Table<TData> _table;
    private readonly TableColumnLayoutManager<TData> _layoutManager;
    private readonly TableCellRenderer<TData> _cellRenderer = new();

    private TableColumnLayoutSnapshot _snapshot;
    private double _verticalOffset;
    private double _viewportHeight;
    private int _firstRowIndex;
    private IReadOnlyList<Row<TData>> _visibleRows = Array.Empty<Row<TData>>();

    public VirtualizedBodyPanel(Table<TData> table, TableColumnLayoutManager<TData> layoutManager)
    {
        _table = table;
        _layoutManager = layoutManager;
        _snapshot = layoutManager.Snapshot;

        _layoutManager.RegisterPanel(new ColumnLayoutPanel()); // ensure width hints flow
        _layoutManager.Refresh();

        EffectiveViewportChanged += (_, e) =>
        {
            _viewportHeight = Math.Max(0, e.EffectiveViewport.Height);
            _verticalOffset = Math.Max(0, e.EffectiveViewport.Y);
            RefreshVisible();
        };
    }

    private void RefreshVisible()
    {
        _snapshot = _layoutManager.Snapshot;

        var totalRows = _table.RowModel.Rows.Count;
        if (totalRows == 0)
        {
            _visibleRows = Array.Empty<Row<TData>>();
            _firstRowIndex = 0;
            Children.Clear();
            InvalidateMeasure();
            InvalidateArrange();
            return;
        }

        // Walk heights to find the first row in view
        var idx = 0;
        var acc = 0.0;
        while (idx < totalRows)
        {
            var h = _table.GetRowHeight(idx);
            if (acc + h > _verticalOffset) break;
            acc += h;
            idx++;
        }
        _firstRowIndex = Math.Clamp(idx, 0, Math.Max(0, totalRows - 1));

        var rows = new List<Row<TData>>();
        var covered = acc;
        var i = _firstRowIndex;
        while (i < totalRows && (covered < _verticalOffset + _viewportHeight || rows.Count == 0))
        {
            rows.Add(_table.RowModel.Rows[i]);
            covered += _table.GetRowHeight(i);
            i++;
        }
        if (i < totalRows)
        {
            rows.Add(_table.RowModel.Rows[i]); // small buffer
        }

        _visibleRows = rows;

        MaterializeCells();
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void MaterializeCells()
    {
        Children.Clear();
        foreach (var row in _visibleRows)
        {
            var displayIndex = row.DisplayIndex >= 0 ? row.DisplayIndex : row.Index;
            foreach (var column in _table.VisibleLeafColumns)
            {
                var cell = _cellRenderer.CreateCell(_table, row, (Column<TData>)column, displayIndex);
                Children.Add(cell);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        double totalHeight = 0;
        for (var i = 0; i < _table.RowModel.Rows.Count; i++)
        {
            totalHeight += _table.GetRowHeight(i);
        }

        var width = _snapshot.TotalWidth;
        if (!double.IsNaN(availableSize.Width) && !double.IsInfinity(availableSize.Width))
        {
            width = Math.Max(width, availableSize.Width);
        }

        return new Size(width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _snapshot = _layoutManager.Snapshot;
        if (!double.IsNaN(finalSize.Width) && finalSize.Width > 0)
        {
            _layoutManager.ReportViewportWidth(finalSize.Width);
        }
        var y = _verticalOffset - GetAccumulatedHeight(0, _firstRowIndex);
        var rowIndex = _firstRowIndex;
        var childIndex = 0;

        foreach (var row in _visibleRows)
        {
            var rowHeight = _table.GetRowHeight(rowIndex);
            foreach (var col in _table.VisibleLeafColumns)
            {
                if (childIndex >= Children.Count)
                    break;

                var child = Children[childIndex++];
                var left = _snapshot.GetOffset(col.Id);
                var width = _snapshot.GetWidth(col.Id);
                var rect = new Rect(left, y, width, rowHeight);
                child.Arrange(rect);
            }
            y += rowHeight;
            rowIndex++;
        }

        // Zero any excess children
        for (; childIndex < Children.Count; childIndex++)
        {
            Children[childIndex].Arrange(new Rect(0, 0, 0, 0));
        }

        return finalSize;
    }

    private double GetAccumulatedHeight(int start, int count)
    {
        double acc = 0;
        var end = Math.Min(_table.RowModel.Rows.Count, start + count);
        for (var i = start; i < end; i++)
        {
            acc += _table.GetRowHeight(i);
        }
        return acc;
    }
}
