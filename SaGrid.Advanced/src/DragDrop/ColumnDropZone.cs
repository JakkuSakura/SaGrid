using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced.Interactive;
using SaGrid.Core;

namespace SaGrid.Advanced.DragDrop;

/// <summary>
/// Drop zone for column reordering following AG Grid's column drop patterns
/// </summary>
public class ColumnDropZone<TData> : IDropZone
{
    private readonly Panel _headerContainer;
    private readonly Visual _rootVisual;
    private readonly ColumnInteractiveService<TData> _columnService;
    private readonly Table<TData> _table;
    
    // Drop position calculation
    private int _dropIndex = -1;
    private bool _dropBefore = true;
    
    public ColumnDropZone(Panel headerContainer, ColumnInteractiveService<TData> columnService, Table<TData> table, Visual rootVisual)
    {
        _headerContainer = headerContainer;
        _columnService = columnService;
        _table = table;
        _rootVisual = rootVisual;
    }

    public bool ContainsPoint(Point position)
    {
        if (!_headerContainer.IsVisible || _headerContainer.Bounds.Width <= 0)
        {
            return false;
        }

        var localPoint = _rootVisual.TranslatePoint(position, _headerContainer);
        if (localPoint == null)
        {
            return false;
        }

        var localRect = new Rect(_headerContainer.Bounds.Size);
        return localRect.Contains(localPoint.Value);
    }

    public bool CanAccept(IDragSource dragSource)
    {
        // Accept column drag sources
        return dragSource.GetDragData() is IColumn<TData>;
    }

    public bool TryDrop(IDragSource dragSource, Point position)
    {
        if (!CanAccept(dragSource)) return false;
        
        var draggedColumn = dragSource.GetDragData() as IColumn<TData>;
        if (draggedColumn == null) return false;

        // Calculate drop position
        var localPoint = _rootVisual.TranslatePoint(position, _headerContainer);
        if (localPoint == null)
        {
            return false;
        }

        CalculateDropPosition(localPoint.Value);
        
        if (_dropIndex >= 0)
        {
            // Perform the column move
            var success = _columnService.MoveColumn(draggedColumn.Id, _dropIndex);
            return success;
        }

        return false;
    }

    public void OnDragEnter(IDragSource dragSource)
    {
        if (CanAccept(dragSource))
        {
            // Add visual feedback for valid drop zone
            AddDropZoneHighlight();
        }
    }

    public void OnDragLeave(IDragSource dragSource)
    {
        // Remove visual feedback
        RemoveDropZoneHighlight();
    }

    public void OnDragOver(IDragSource dragSource, Point position)
    {
        if (CanAccept(dragSource))
        {
            var localPoint = _rootVisual.TranslatePoint(position, _headerContainer);
            if (localPoint != null)
            {
                CalculateDropPosition(localPoint.Value);
            }
        }
    }

    public Point GetDropIndicatorPosition(Point position)
    {
        if (_headerContainer.Children.Count == 0)
        {
            return position;
        }

        var localPoint = _rootVisual.TranslatePoint(position, _headerContainer);
        if (localPoint == null)
        {
            return position;
        }

        CalculateDropPosition(localPoint.Value);

        if (_dropIndex >= 0 && _headerContainer.Children.Count > 0)
        {
            var headerChildren = _headerContainer.Children.Cast<Control>().ToList();
            
            if (_dropIndex < headerChildren.Count)
            {
                var targetHeader = headerChildren[_dropIndex];
                var headerOrigin = targetHeader.TranslatePoint(new Point(0, 0), _headerContainer) ?? new Point(0, 0);
                var headerSize = targetHeader.Bounds.Size;

                var x = _dropBefore ? headerOrigin.X : headerOrigin.X + headerSize.Width;
                var y = headerOrigin.Y;

                return _headerContainer.TranslatePoint(new Point(x, y), _rootVisual) ?? position;
            }
            else if (headerChildren.Any())
            {
                // Drop at the end
                var lastHeader = headerChildren.Last();
                var headerOrigin = lastHeader.TranslatePoint(new Point(0, 0), _headerContainer) ?? new Point(0, 0);
                var headerSize = lastHeader.Bounds.Size;
                var point = new Point(headerOrigin.X + headerSize.Width, headerOrigin.Y);
                return _headerContainer.TranslatePoint(point, _rootVisual) ?? position;
            }
        }
        
        return position;
    }

    public double GetDropIndicatorHeight()
    {
        if (_headerContainer.Children.Count > 0)
        {
            var firstHeader = _headerContainer.Children.Cast<Control>().FirstOrDefault();
            return firstHeader?.Bounds.Height ?? 30;
        }
        return 30;
    }

    private void CalculateDropPosition(Point localPosition)
    {
        if (_headerContainer.Children.Count == 0)
        {
            _dropIndex = 0;
            _dropBefore = true;
            return;
        }

        var headerChildren = _headerContainer.Children.Cast<Control>().ToList();
        var relativeX = localPosition.X;
        
        for (int i = 0; i < headerChildren.Count; i++)
        {
            var header = headerChildren[i];
            var origin = header.TranslatePoint(new Point(0, 0), _headerContainer) ?? new Point(0, 0);
            var width = header.Bounds.Width;
            var left = origin.X;
            var right = left + width;

            if (relativeX <= left + width / 2)
            {
                // Drop before this header
                _dropIndex = i;
                _dropBefore = true;
                return;
            }
            else if (relativeX <= right)
            {
                // Drop after this header
                _dropIndex = i + 1;
                _dropBefore = false;
                return;
            }
        }
        
        // Drop at the end
        _dropIndex = headerChildren.Count;
        _dropBefore = false;
    }

    private void AddDropZoneHighlight()
    {
        // Add visual feedback that this is a valid drop zone
        _headerContainer.Background = new SolidColorBrush(Colors.LightBlue, 0.1);
    }

    private void RemoveDropZoneHighlight()
    {
        // Remove visual feedback
        _headerContainer.Background = null;
    }
}

/// <summary>
/// Row grouping drop zone for advanced grouping features
/// </summary>
public class GroupingDropZone<TData> : IDropZone
{
    private readonly Control _groupingArea;
    private readonly ColumnInteractiveService<TData> _columnService;
    
    public GroupingDropZone(Control groupingArea, ColumnInteractiveService<TData> columnService)
    {
        _groupingArea = groupingArea;
        _columnService = columnService;
    }

    public bool ContainsPoint(Point position)
    {
        return _groupingArea.IsVisible && _groupingArea.Bounds.Contains(position);
    }

    public bool CanAccept(IDragSource dragSource)
    {
        return dragSource.GetDragData() is IColumn<TData>;
    }

    public bool TryDrop(IDragSource dragSource, Point position)
    {
        if (!CanAccept(dragSource)) return false;
        
        var draggedColumn = dragSource.GetDragData() as IColumn<TData>;
        if (draggedColumn == null) return false;

        // For now, just show that we would group by this column
        // In a full implementation, this would add the column to grouping
        ShowGroupingFeedback(draggedColumn.Id);
        
        return true;
    }

    public void OnDragEnter(IDragSource dragSource)
    {
        AddGroupingZoneHighlight();
    }

    public void OnDragLeave(IDragSource dragSource)
    {
        RemoveGroupingZoneHighlight();
    }

    public void OnDragOver(IDragSource dragSource, Point position)
    {
        // Could show preview of where the grouping chip would appear
    }

    public Point GetDropIndicatorPosition(Point position)
    {
        // For grouping zone, indicator appears in the center
        return new Point(
            _groupingArea.Bounds.Center.X, 
            _groupingArea.Bounds.Top
        );
    }

    public double GetDropIndicatorHeight()
    {
        return _groupingArea.Bounds.Height;
    }

    private void AddGroupingZoneHighlight()
    {
        if (_groupingArea is Border border)
        {
            border.Background = new SolidColorBrush(Colors.Orange, 0.2);
            border.BorderBrush = new SolidColorBrush(Colors.Orange);
            border.BorderThickness = new Thickness(2);
        }
    }

    private void RemoveGroupingZoneHighlight()
    {
        if (_groupingArea is Border border)
        {
            border.Background = null;
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);
        }
    }

    private void ShowGroupingFeedback(string columnId)
    {
        // Show temporary feedback that grouping would be applied
        var feedbackText = new TextBlock
        {
            Text = $"Group by {columnId}",
            Foreground = new SolidColorBrush(Colors.Green)
        };
        
        if (_groupingArea is Panel panel)
        {
            panel.Children.Add(feedbackText);
            
            // Remove after 2 seconds
            DispatcherTimer.RunOnce(() => panel.Children.Remove(feedbackText), TimeSpan.FromSeconds(2));
        }
    }
}
