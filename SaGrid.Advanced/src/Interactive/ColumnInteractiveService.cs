using System;
using System.Collections.Generic;
using System.Linq;
using SaGrid.Core;
using SaGrid.Advanced.Events;

namespace SaGrid.Advanced.Interactive;

/// <summary>
/// Simple interactive column service for basic column operations
/// This is a simplified version that will be expanded with AG Grid features
/// </summary>
public class ColumnInteractiveService<TData>
{
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
    public bool MoveColumn(string columnId, int toIndex)
    {
        try
        {
            var currentOrder = _table.State.ColumnOrder?.Order ?? _table.AllLeafColumns.Select(c => c.Id).ToList();
            var newOrder = currentOrder.ToList();

            // Remove the column from its current position
            if (!newOrder.Remove(columnId))
            {
                return false; // Column not found
            }

            // Insert at the new position
            var clampedIndex = Math.Max(0, Math.Min(toIndex, newOrder.Count));
            newOrder.Insert(clampedIndex, columnId);

            // Update table state
            _table.SetState(state => state with 
            { 
                ColumnOrder = new ColumnOrderState(newOrder)
            });

            // Fire event
            _eventService.DispatchEvent("columnMoved", new ColumnMovedEventArgs<TData>(columnId, toIndex));
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Set column width with basic validation
    /// </summary>
    public bool SetColumnWidth(string columnId, double width)
    {
        try
        {
            const double minWidth = 20;
            const double maxWidth = 2000;

            // Basic validation
            if (width < minWidth || width > maxWidth)
            {
                return false;
            }

            var column = _table.GetColumn(columnId);
            if (column == null)
            {
                return false;
            }

            // Update column sizing
            var columnSizing = _table.State.ColumnSizing ?? new ColumnSizingState();
            var newSizing = columnSizing.With(columnId, width);
            
            _table.SetState(state => state with { ColumnSizing = newSizing });

            // Fire event
            _eventService.DispatchEvent("columnResized", new ColumnResizedEventArgs<TData>(columnId, width));
            
            return true;
        }
        catch
        {
            return false;
        }
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
        try
        {
            // This would be implemented based on SaGrid's pinning capabilities
            // For now, just fire the event
            _eventService.DispatchEvent("columnPinned", new ColumnPinnedEventArgs<TData>(columnId, pinned));
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Event arguments for column operations
/// </summary>
public class ColumnMovedEventArgs<TData> : AgEventArgs
{
    public string ColumnId { get; }
    public int NewIndex { get; }

    public ColumnMovedEventArgs(string columnId, int newIndex) : base("columnMoved", columnId)
    {
        ColumnId = columnId;
        NewIndex = newIndex;
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

    public ColumnPinnedEventArgs(string columnId, bool isPinned) : base("columnPinned", columnId)
    {
        ColumnId = columnId;
        IsPinned = isPinned;
    }
}