using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced.Interactive;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;
using SaGrid.Core.Models;

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
    private string? _targetPinnedArea;
    private bool _isDropValid;
    private IDragSource? _currentDragSource;
    
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

        _currentDragSource = dragSource;
        CalculateDropPosition(localPoint.Value);

        if (_dropIndex < 0 || !_isDropValid)
        {
            return false;
        }

        var moved = _columnService.MoveColumn(draggedColumn.Id, _dropIndex, _targetPinnedArea);
        if (moved)
        {
            _currentDragSource = null;
        }

        return moved;
    }

    public void OnDragEnter(IDragSource dragSource)
    {
        if (CanAccept(dragSource))
        {
            // Add visual feedback for valid drop zone
            _currentDragSource = dragSource;
            AddDropZoneHighlight();
        }
    }

    public void OnDragLeave(IDragSource dragSource)
    {
        // Remove visual feedback
        RemoveDropZoneHighlight();
        _currentDragSource = null;
        _isDropValid = false;
    }

    public void OnDragOver(IDragSource dragSource, Point position)
    {
        if (CanAccept(dragSource))
        {
            _currentDragSource = dragSource;
            var localPoint = _rootVisual.TranslatePoint(position, _headerContainer);
            if (localPoint != null)
            {
                CalculateDropPosition(localPoint.Value);
                UpdateDropZoneHighlight();
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
            _targetPinnedArea = null;
            _isDropValid = true;
            return;
        }

        var headerChildren = _headerContainer.Children.Cast<Control>().ToList();
        var relativeX = localPosition.X;
        _dropIndex = headerChildren.Count;
        _dropBefore = false;

        for (int i = 0; i < headerChildren.Count; i++)
        {
            var header = headerChildren[i];
            var origin = header.TranslatePoint(new Point(0, 0), _headerContainer) ?? new Point(0, 0);
            var width = header.Bounds.Width;
            var left = origin.X;
            var right = left + width;

            if (relativeX <= left + width / 2)
            {
                _dropIndex = i;
                _dropBefore = true;
                break;
            }
            else if (relativeX <= right)
            {
                _dropIndex = i + 1;
                _dropBefore = false;
                break;
            }
        }

        _targetPinnedArea = ResolveTargetPinnedArea(headerChildren, _dropIndex, _dropBefore);
        _isDropValid = EvaluateDropValidity(headerChildren, _dropIndex, _targetPinnedArea);
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
        _isDropValid = false;
        _targetPinnedArea = null;
    }

    private void UpdateDropZoneHighlight()
    {
        if (_isDropValid)
        {
            _headerContainer.Background = new SolidColorBrush(Colors.LightGreen, 0.12);
        }
        else
        {
            _headerContainer.Background = new SolidColorBrush(Colors.IndianRed, 0.15);
        }
    }

    private string? ResolveTargetPinnedArea(List<Control> headerChildren, int dropIndex, bool dropBefore)
    {
        string? pinned = null;

        string? ResolveFromControl(Control control)
        {
            if (control.Tag is string id)
            {
                return _columnService.GetColumnPinnedArea(id);
            }
            return null;
        }

        if (dropIndex > 0 && dropBefore && dropIndex - 1 < headerChildren.Count)
        {
            pinned = ResolveFromControl(headerChildren[dropIndex - 1]);
        }
        else if (dropIndex < headerChildren.Count)
        {
            pinned = ResolveFromControl(headerChildren[dropIndex]);
        }

        pinned ??= ResolveFromControl(headerChildren.Last());
        return pinned;
    }

    private bool EvaluateDropValidity(List<Control> headerChildren, int dropIndex, string? pinnedArea)
    {
        if (_currentDragSource?.GetDragData() is not IColumn<TData> column)
        {
            return false;
        }

        var displayIndex = Math.Clamp(dropIndex, 0, headerChildren.Count);
        return _columnService.CanMoveColumn(column.Id, displayIndex, pinnedArea);
    }
}

/// <summary>
/// Row grouping drop zone for advanced grouping features
/// </summary>
public class GroupingDropZone<TData> : IDropZone
{
    private readonly Panel _groupingPanel;
    private readonly IGroupingService _groupingService;
    private readonly SaGrid<TData> _grid;
    private readonly Visual _rootVisual;

    private int _dropIndex = -1;
    private bool _dropBefore = true;
    
    public GroupingDropZone(Panel groupingPanel,
        IGroupingService groupingService,
        SaGrid<TData> grid,
        Visual rootVisual)
    {
        _groupingPanel = groupingPanel;
        _groupingService = groupingService;
        _grid = grid;
        _rootVisual = rootVisual;
    }

    public bool ContainsPoint(Point position)
    {
        if (!_groupingPanel.IsVisible)
        {
            return false;
        }

        var local = _rootVisual.TranslatePoint(position, _groupingPanel);
        if (local == null)
        {
            return false;
        }

        var bounds = new Rect(_groupingPanel.Bounds.Size);
        return bounds.Contains(local.Value);
    }

    public bool CanAccept(IDragSource dragSource)
    {
        return dragSource.GetDragData() is IColumn<TData> column && column.CanGroup;
    }

    public bool TryDrop(IDragSource dragSource, Point position)
    {
        if (!CanAccept(dragSource))
        {
            return false;
        }

        if (dragSource.GetDragData() is not IColumn<TData> column)
        {
            return false;
        }

        var local = _rootVisual.TranslatePoint(position, _groupingPanel);
        if (local == null)
        {
            return false;
        }

        CalculateDropPosition(local.Value);

        var groupedIds = _groupingService.GetGroupedColumnIds(_grid);
        var insertIndex = _dropIndex >= 0 ? _dropIndex : groupedIds.Count;
        insertIndex = Math.Clamp(insertIndex, 0, groupedIds.Count);

        var existingIndex = -1;
        for (var i = 0; i < groupedIds.Count; i++)
        {
            if (string.Equals(groupedIds[i], column.Id, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            if (existingIndex < insertIndex)
            {
                insertIndex -= 1;
            }

            if (existingIndex == insertIndex)
            {
                return false;
            }

            _groupingService.MoveGroupingColumn(_grid, column.Id, insertIndex);
        }
        else
        {
            _groupingService.AddGroupingColumn(_grid, column.Id, insertIndex);
        }

        ShowGroupingFeedback(column.Id);
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
        var local = _rootVisual.TranslatePoint(position, _groupingPanel);
        if (local != null)
        {
            CalculateDropPosition(local.Value);
        }
    }

    public Point GetDropIndicatorPosition(Point position)
    {
        var chips = GetChipControls();
        if (chips.Count == 0)
        {
            var center = _groupingPanel.Bounds.Center;
            return _groupingPanel.TranslatePoint(new Point(center.X, _groupingPanel.Bounds.Top), _rootVisual) ?? position;
        }

        var index = Math.Clamp(_dropIndex, 0, chips.Count);
        if (index < chips.Count)
        {
            var target = chips[index];
            var origin = target.TranslatePoint(new Point(0, 0), _groupingPanel) ?? new Point(0, 0);
            var x = _dropBefore ? origin.X : origin.X + target.Bounds.Width;
            return _groupingPanel.TranslatePoint(new Point(x, origin.Y), _rootVisual) ?? position;
        }

        var last = chips[^1];
        var lastOrigin = last.TranslatePoint(new Point(0, 0), _groupingPanel) ?? new Point(0, 0);
        var dropPoint = new Point(lastOrigin.X + last.Bounds.Width, lastOrigin.Y);
        return _groupingPanel.TranslatePoint(dropPoint, _rootVisual) ?? position;
    }

    public double GetDropIndicatorHeight()
    {
        var chips = GetChipControls();
        if (chips.Count > 0)
        {
            return chips[0].Bounds.Height;
        }
        return _groupingPanel.Bounds.Height;
    }

    private List<Control> GetChipControls()
    {
        return _groupingPanel.Children
            .OfType<Control>()
            .Where(c => c.Tag is string)
            .ToList();
    }

    private void CalculateDropPosition(Point localPosition)
    {
        var chips = GetChipControls();
        if (chips.Count == 0)
        {
            _dropIndex = 0;
            _dropBefore = true;
            return;
        }

        var relativeX = localPosition.X;

        for (int i = 0; i < chips.Count; i++)
        {
            var chip = chips[i];
            var origin = chip.TranslatePoint(new Point(0, 0), _groupingPanel) ?? new Point(0, 0);
            var left = origin.X;
            var right = left + chip.Bounds.Width;

            if (relativeX <= left + chip.Bounds.Width / 2)
            {
                _dropIndex = i;
                _dropBefore = true;
                return;
            }

            if (relativeX <= right)
            {
                _dropIndex = i + 1;
                _dropBefore = false;
                return;
            }
        }

        _dropIndex = chips.Count;
        _dropBefore = false;
    }

    private void AddGroupingZoneHighlight()
    {
        if (_groupingPanel.Parent is Border border)
        {
            border.Background = new SolidColorBrush(Colors.Moccasin, 0.3);
            border.BorderBrush = new SolidColorBrush(Colors.OrangeRed);
            border.BorderThickness = new Thickness(2);
        }
    }

    private void RemoveGroupingZoneHighlight()
    {
        if (_groupingPanel.Parent is Border border)
        {
            border.Background = new SolidColorBrush(Colors.WhiteSmoke);
            border.BorderBrush = Brushes.LightGray;
            border.BorderThickness = new Thickness(1);
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
        
        _groupingPanel.Children.Add(feedbackText);

        // Remove after 1.5 seconds if still present
        DispatcherTimer.RunOnce(() =>
        {
            if (_groupingPanel.Children.Contains(feedbackText))
            {
                _groupingPanel.Children.Remove(feedbackText);
            }
        }, TimeSpan.FromMilliseconds(1500));
    }
}
