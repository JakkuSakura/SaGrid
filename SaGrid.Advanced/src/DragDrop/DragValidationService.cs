using SaGrid.Core;

namespace SaGrid.Advanced.DragDrop;

/// <summary>
/// Service for validating drag and drop operations following AG Grid patterns
/// </summary>
public class DragValidationService<TData>
{
    private readonly Table<TData> _table;
    
    public DragValidationService(Table<TData> table)
    {
        _table = table;
    }

    /// <summary>
    /// Validates if a column can be dragged
    /// </summary>
    public bool CanDragColumn(string columnId)
    {
        var column = _table.AllLeafColumns.FirstOrDefault(c => c.Id == columnId);
        if (column == null) return false;

        // Check if column is draggable
        // In AG Grid, pinned columns have restrictions
        if (IsPinnedColumn(column))
        {
            return CanDragPinnedColumn(column);
        }

        // Check if column is locked
        if (IsColumnLocked(column))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates if a column can be dropped at a specific position
    /// </summary>
    public bool CanDropColumn(string columnId, int targetIndex, string? targetColumnId = null)
    {
        var draggedColumn = _table.AllLeafColumns.FirstOrDefault(c => c.Id == columnId);
        if (draggedColumn == null) return false;

        // Validate target index bounds
        if (targetIndex < 0 || targetIndex > _table.AllLeafColumns.Count()) return false;

        // Check if target position is valid for pinned columns
        if (IsPinnedColumn(draggedColumn))
        {
            return CanDropPinnedColumn(draggedColumn, targetIndex);
        }

        // Check if dropping into a pinned area
        if (targetColumnId != null)
        {
            var targetColumn = _table.AllLeafColumns.FirstOrDefault(c => c.Id == targetColumnId);
            if (targetColumn != null && IsPinnedColumn(targetColumn))
            {
                return CanDropIntoPinnedArea(draggedColumn, targetColumn);
            }
        }

        return true;
    }

    /// <summary>
    /// Validates if columns can be grouped
    /// </summary>
    public bool CanGroupByColumn(string columnId)
    {
        var column = _table.AllLeafColumns.FirstOrDefault(c => c.Id == columnId);
        if (column == null) return false;

        // Check if column supports grouping
        return IsColumnGroupable(column);
    }

    /// <summary>
    /// Gets validation message for failed operations
    /// </summary>
    public string GetValidationMessage(string columnId, DragOperation operation, string? details = null)
    {
        var column = _table.AllLeafColumns.FirstOrDefault(c => c.Id == columnId);
        if (column == null) return $"Column '{columnId}' not found";

        return operation switch
        {
            DragOperation.Move when IsColumnLocked(column) => $"Column '{columnId}' is locked and cannot be moved",
            DragOperation.Move when IsPinnedColumn(column) => $"Pinned column '{columnId}' can only be moved within pinned area",
            DragOperation.Group when !IsColumnGroupable(column) => $"Column '{columnId}' cannot be used for grouping",
            DragOperation.Resize when IsColumnResizeLocked(column) => $"Column '{columnId}' width is locked",
            _ => $"Operation '{operation}' not allowed for column '{columnId}'"
        };
    }

    /// <summary>
    /// Gets visual feedback class for drag operations
    /// </summary>
    public string GetDragFeedbackClass(string columnId, DragOperation operation)
    {
        var column = _table.AllLeafColumns.FirstOrDefault(c => c.Id == columnId);
        if (column == null) return "drag-invalid";

        return operation switch
        {
            DragOperation.Move when CanDragColumn(columnId) => "drag-move-valid",
            DragOperation.Group when CanGroupByColumn(columnId) => "drag-group-valid",
            DragOperation.Resize when !IsColumnResizeLocked(column) => "drag-resize-valid",
            _ => "drag-invalid"
        };
    }

    private bool IsPinnedColumn(IColumn<TData> column)
    {
        // Check if column is pinned left or right
        // This would integrate with AG Grid's pinning system
        return false; // Placeholder implementation
    }

    private bool CanDragPinnedColumn(IColumn<TData> column)
    {
        // Pinned columns can usually only be moved within their pinned area
        return true; // Placeholder implementation
    }

    private bool CanDropPinnedColumn(IColumn<TData> column, int targetIndex)
    {
        // Validate that pinned column drop position is within pinned area
        return true; // Placeholder implementation
    }

    private bool CanDropIntoPinnedArea(IColumn<TData> draggedColumn, IColumn<TData> targetColumn)
    {
        // Check if non-pinned column can be dropped into pinned area
        return !IsPinnedColumn(draggedColumn); // Non-pinned can become pinned
    }

    private bool IsColumnLocked(IColumn<TData> column)
    {
        // Check if column has edit/move restrictions
        // This would check column definition properties
        return false; // Placeholder implementation
    }

    private bool IsColumnGroupable(IColumn<TData> column)
    {
        // Check if column supports grouping
        // This would check column definition and data type
        return true; // Placeholder implementation
    }

    private bool IsColumnResizeLocked(IColumn<TData> column)
    {
        // Check if column width can be changed
        return false; // Placeholder implementation
    }
}

/// <summary>
/// Types of drag operations
/// </summary>
public enum DragOperation
{
    Move,
    Group,
    Resize,
    Pin,
    Hide
}

/// <summary>
/// Drag constraints configuration
/// </summary>
public class DragConstraints
{
    public bool AllowColumnReordering { get; set; } = true;
    public bool AllowColumnGrouping { get; set; } = true;
    public bool AllowColumnResizing { get; set; } = true;
    public bool AllowColumnPinning { get; set; } = true;
    public bool PreservePinnedColumnOrder { get; set; } = true;
    public bool RequireModifierForMultiSort { get; set; } = false;
    public double MinColumnWidth { get; set; } = 50;
    public double MaxColumnWidth { get; set; } = 1000;
    public int MaxGroupingLevels { get; set; } = 5;
}