using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Utils;
using SaGrid.Core;

namespace SaGrid.Advanced.DragDrop;

/// <summary>
/// Comprehensive drag and drop manager with visual feedback following AG Grid patterns
/// </summary>
public class DragDropManager<TData>
{
    private readonly IEventService _eventService;
    private readonly Visual _rootVisual;
    private readonly DragValidationService<TData>? _validationService;
    
    // Drag state
    private bool _isDragging;
    private IDragSource? _currentDragSource;
    private Control? _dragGhost;
    private Point _dragStartPoint;
    private Point _currentPointerPosition;
    
    // Drop zones
    private readonly List<IDropZone> _dropZones = new();
    private IDropZone? _currentDropZone;
    private Control? _dropIndicator;
    
    // Animation and timing
    private readonly DispatcherTimer _dragUpdateTimer;
    private const double DragThreshold = 5.0;
    private const int AnimationDuration = 200;
    
    // Touch support
    private bool _isTouchDrag;
    private DispatcherTimer? _holdTimer;

    public bool IsDragging => _isDragging;
    public event EventHandler<DragEventArgs>? DragStarted;
    public event EventHandler<DragEventArgs>? DragEnded;
    public event EventHandler<DragEventArgs>? DragCancelled;

    public Visual RootVisual => _rootVisual;

    public DragDropManager(IEventService eventService, Visual rootVisual, DragValidationService<TData>? validationService = null)
    {
        _eventService = eventService;
        _rootVisual = rootVisual;
        _validationService = validationService;
        
        _dragUpdateTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnDragUpdate);
    }

    public void RegisterDragSource(IDragSource dragSource)
    {
        if (dragSource.Element is Control control)
        {
            _dragSources[control] = dragSource;
            control.PointerPressed += OnPointerPressed;
            control.PointerMoved += OnPointerMoved;
            control.PointerReleased += OnPointerReleased;
            control.PointerCaptureLost += OnPointerCaptureLost;
            
            // Add touch support
            control.Tapped += OnTapped;
        }
    }

    public void UnregisterDragSource(IDragSource dragSource)
    {
        if (dragSource.Element is Control control)
        {
            _dragSources.Remove(control);
            control.PointerPressed -= OnPointerPressed;
            control.PointerMoved -= OnPointerMoved;
            control.PointerReleased -= OnPointerReleased;
            control.PointerCaptureLost -= OnPointerCaptureLost;
            
            // Remove touch support
            control.Tapped -= OnTapped;
        }
    }

    public void RegisterDropZone(IDropZone dropZone)
    {
        _dropZones.Add(dropZone);
    }

    public void UnregisterDropZone(IDropZone dropZone)
    {
        _dropZones.Remove(dropZone);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            var point = e.GetCurrentPoint(control);
            
            // Handle touch input
            if (point.Pointer.Type == PointerType.Touch)
            {
                OnTouchDragStart(control, point.Position);
                return;
            }
            
            // Handle mouse input
            if (point.Properties.IsLeftButtonPressed)
            {
                _dragStartPoint = point.Position;
                _currentPointerPosition = e.GetCurrentPoint(_rootVisual).Position;
                
                // Find the drag source
                _currentDragSource = FindDragSource(control);
                if (_currentDragSource != null)
                {
                    e.Pointer.Capture(control);
                    e.Handled = true;
                }
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_currentDragSource != null && sender is Control control)
        {
            var currentPos = e.GetCurrentPoint(control).Position;
            var distance = Point.Distance(_dragStartPoint, currentPos);
            
            _currentPointerPosition = e.GetCurrentPoint(_rootVisual).Position;

            if (!_isDragging && distance > DragThreshold)
            {
                StartDrag(control, e);
            }
            else if (_isDragging)
            {
                UpdateDragPosition();
                UpdateDropZone();
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Handle touch release
        if (_isTouchDrag)
        {
            _holdTimer?.Stop();
            _holdTimer = null;
            
            if (_isDragging)
            {
                CompleteDrag();
            }
            else
            {
                CancelDrag();
            }
            
            _isTouchDrag = false;
        }
        else if (_isDragging)
        {
            CompleteDrag();
        }
        else
        {
            CancelDrag();
        }
        
        if (sender is Control)
        {
            e.Pointer.Capture(null);
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDragging)
        {
            CancelDrag();
        }
    }

    private void StartDrag(Control sourceControl, PointerEventArgs e)
    {
        if (_currentDragSource == null) return;

        // Validate if drag can start
        if (_validationService != null && _currentDragSource.GetDragData() is IColumn<TData> column)
        {
            if (!_validationService.CanDragColumn(column.Id))
            {
                // Show validation feedback
                ShowValidationFeedback(column.Id, DragOperation.Move);
                CancelDrag();
                return;
            }
        }

        _isDragging = true;
        
        // Create drag ghost
        CreateDragGhost(sourceControl);
        
        // Show drag ghost
        if (_dragGhost != null)
        {
            AddToOverlay(_dragGhost);
            UpdateDragPosition();
            
            // Animate drag ghost appearance
            DragAnimationService.AnimateDragStart(_dragGhost);
        }

        // Notify drag source
        _currentDragSource.OnDragStarted();
        
        // Start update timer
        _dragUpdateTimer.Start();
        
        // Fire events
        var dragArgs = new DragEventArgs(_currentDragSource, _currentPointerPosition);
        DragStarted?.Invoke(this, dragArgs);
        _eventService.DispatchEvent("dragStarted", dragArgs);
    }

    private void CompleteDrag()
    {
        if (!_isDragging || _currentDragSource == null) return;

        var success = false;
        
        // Attempt drop if we have a valid drop zone
        if (_currentDropZone != null)
        {
            success = _currentDropZone.TryDrop(_currentDragSource, _currentPointerPosition);
        }

        // Clean up
        FinalizeDrag(success);
    }

    private void CancelDrag()
    {
        if (_isDragging)
        {
            FinalizeDrag(false);
        }
        else
        {
            // Just clean up state if we never started dragging
            _currentDragSource = null;
        }
    }

    private void FinalizeDrag(bool success)
    {
        if (!_isDragging) return;

        _isDragging = false;
        _dragUpdateTimer.Stop();

        // Animate ghost removal
        if (_dragGhost != null)
        {
            AnimateGhostRemoval(_dragGhost, success);
        }

        // Clean up drop indicator
        RemoveDropIndicator();

        // Notify current drop zone
        if (_currentDropZone != null)
        {
            _currentDropZone.OnDragLeave(_currentDragSource!);
            _currentDropZone = null;
        }

        // Notify drag source
        _currentDragSource?.OnDragEnded(success);

        // Fire events
        var dragArgs = new DragEventArgs(_currentDragSource!, _currentPointerPosition);
        if (success)
        {
            DragEnded?.Invoke(this, dragArgs);
            _eventService.DispatchEvent("dragEnded", dragArgs);
        }
        else
        {
            DragCancelled?.Invoke(this, dragArgs);
            _eventService.DispatchEvent("dragCancelled", dragArgs);
        }

        _currentDragSource = null;
    }

    private void CreateDragGhost(Control sourceControl)
    {
        // Create a visual copy of the dragged element
        _dragGhost = new Border
        {
            Background = new SolidColorBrush(Colors.White, 0.9),
            BorderBrush = new SolidColorBrush(Colors.Blue, 0.8),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Colors.Black,
                OffsetX = 2,
                OffsetY = 2,
                Blur = 8,
                Spread = 0
            }),
            Child = new TextBlock
            {
                Text = GetDragText(sourceControl),
                Padding = new Thickness(8, 4),
                FontWeight = FontWeight.SemiBold
            }
        };

        // Set initial position
        Canvas.SetLeft(_dragGhost, _currentPointerPosition.X - 50);
        Canvas.SetTop(_dragGhost, _currentPointerPosition.Y - 15);
    }

    private string GetDragText(Control sourceControl)
    {
        if (_currentDragSource?.GetDragData() is IColumn<TData> column)
        {
            return $"📋 {column.Id}";
        }
        
        return "📋 Dragging...";
    }

    private void UpdateDragPosition()
    {
        if (_dragGhost != null)
        {
            // Use smooth animation for drag ghost movement
            DragAnimationService.AnimateDragMove(_dragGhost, _currentPointerPosition, smooth: true);
        }
    }

    private void UpdateDropZone()
    {
        var newDropZone = FindDropZoneAt(_currentPointerPosition);
        
        if (newDropZone != _currentDropZone)
        {
            // Leave current drop zone
            if (_currentDropZone != null)
            {
                _currentDropZone.OnDragLeave(_currentDragSource!);
                RemoveDropIndicator();
            }
            
            // Enter new drop zone
            _currentDropZone = newDropZone;
            if (_currentDropZone != null)
            {
                _currentDropZone.OnDragEnter(_currentDragSource!);
                CreateDropIndicator();
            }
        }
        else if (_currentDropZone != null)
        {
            // Update existing drop zone
            _currentDropZone.OnDragOver(_currentDragSource!, _currentPointerPosition);
            UpdateDropIndicator();
        }
    }

    private void CreateDropIndicator()
    {
        if (_currentDropZone == null) return;

        var position = _currentDropZone.GetDropIndicatorPosition(_currentPointerPosition);
        
        _dropIndicator = new Rectangle
        {
            Width = 3,
            Height = _currentDropZone.GetDropIndicatorHeight(),
            Fill = new SolidColorBrush(Colors.Blue),
            Opacity = 0
        };

        AddToOverlay(_dropIndicator);
        Canvas.SetLeft(_dropIndicator, position.X);
        Canvas.SetTop(_dropIndicator, position.Y);
        
        // Animate drop indicator appearance
        DragAnimationService.AnimateDropIndicator(_dropIndicator, position);
    }

    private void UpdateDropIndicator()
    {
        if (_dropIndicator == null || _currentDropZone == null) return;

        var position = _currentDropZone.GetDropIndicatorPosition(_currentPointerPosition);
        Canvas.SetLeft(_dropIndicator, position.X);
        Canvas.SetTop(_dropIndicator, position.Y);
    }

    private void RemoveDropIndicator()
    {
        if (_dropIndicator != null)
        {
            RemoveFromOverlay(_dropIndicator);
            _dropIndicator = null;
        }
    }

    private void OnDragUpdate(object? sender, EventArgs e)
    {
        if (_isDragging)
        {
            // Smooth drag updates
            UpdateDragPosition();
        }
    }

    private void AnimateGhostRemoval(Control ghost, bool success)
    {
        if (success)
        {
            DragAnimationService.AnimateSuccessfulDrop(ghost, () => RemoveFromOverlay(ghost));
        }
        else
        {
            DragAnimationService.AnimateFailedDrop(ghost, () => RemoveFromOverlay(ghost));
        }
    }

    // Registry of drag sources
    private readonly Dictionary<Control, IDragSource> _dragSources = new();

    private IDragSource? FindDragSource(Control control)
    {
        // Look for the drag source in our registry
        if (_dragSources.TryGetValue(control, out var dragSource))
        {
            return dragSource;
        }

        // Search parent hierarchy for registered drag sources
        var parent = control.Parent as Control;
        while (parent != null)
        {
            if (_dragSources.TryGetValue(parent, out dragSource))
            {
                return dragSource;
            }
            parent = parent.Parent as Control;
        }

        return null;
    }

    private IDropZone? FindDropZoneAt(Point position)
    {
        foreach (var dropZone in _dropZones)
        {
            if (dropZone.ContainsPoint(position))
            {
                return dropZone;
            }
        }
        return null;
    }

    private void AddToOverlay(Control element)
    {
        // Add to the visual overlay - this needs to be implemented based on the host
        if (_rootVisual is Panel panel)
        {
            panel.Children.Add(element);
        }
    }

    private void RemoveFromOverlay(Control element)
    {
        // Remove from the visual overlay
        if (_rootVisual is Panel panel)
        {
            panel.Children.Remove(element);
        }
    }

    private void ShowValidationFeedback(string columnId, DragOperation operation)
    {
        if (_validationService == null) return;

        var message = _validationService.GetValidationMessage(columnId, operation);
        
        // Create temporary feedback
        var feedbackText = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Colors.Red),
            Background = new SolidColorBrush(Colors.White, 0.9),
            Padding = new Thickness(8, 4),
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var feedbackBorder = new Border
        {
            Child = feedbackText,
            Background = new SolidColorBrush(Colors.White, 0.9),
            BorderBrush = new SolidColorBrush(Colors.Red),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Colors.Black,
                OffsetX = 2,
                OffsetY = 2,
                Blur = 6
            })
        };

        AddToOverlay(feedbackBorder);
        Canvas.SetLeft(feedbackBorder, _currentPointerPosition.X - 100);
        Canvas.SetTop(feedbackBorder, _currentPointerPosition.Y - 30);

        // Remove after 2 seconds
        DispatcherTimer.RunOnce(() => RemoveFromOverlay(feedbackBorder), TimeSpan.FromSeconds(2));
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        // Handle single tap for touch devices - could trigger selection or other actions
        if (!_isDragging && sender is Control control)
        {
            var dragSource = FindDragSource(control);
            if (dragSource != null && _validationService != null)
            {
                if (dragSource.GetDragData() is IColumn<TData> column)
                {
                    // Provide visual feedback for tappable elements
                    ShowTouchFeedback(control);
                }
            }
        }
    }

    private void OnTouchDragStart(Control control, Point position)
    {
        // Enhanced touch drag start with long press detection
        _isTouchDrag = true;
        _dragStartPoint = position;
        _currentPointerPosition = position;
        
        _currentDragSource = FindDragSource(control);
        if (_currentDragSource != null)
        {
            // Provide haptic feedback for touch devices
            ShowTouchDragFeedback(control);

            // Start drag after short delay for touch
            _holdTimer?.Stop();

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };

            void OnTick(object? sender, EventArgs args)
            {
                if (sender is DispatcherTimer dt)
                {
                    dt.Tick -= OnTick;
                    dt.Stop();
                    _holdTimer = null;
                }

                if (_currentDragSource != null && _isTouchDrag)
                {
                    StartTouchDrag(control);
                }
            }

            timer.Tick += OnTick;
            _holdTimer = timer;
            timer.Start();
        }
    }

    private void StartTouchDrag(Control sourceControl)
    {
        if (_currentDragSource == null) return;

        // Validate if drag can start
        if (_validationService != null && _currentDragSource.GetDragData() is IColumn<TData> column)
        {
            if (!_validationService.CanDragColumn(column.Id))
            {
                ShowValidationFeedback(column.Id, DragOperation.Move);
                CancelDrag();
                return;
            }
        }

        _isDragging = true;
        
        // Create larger drag ghost for touch
        CreateTouchDragGhost(sourceControl);
        
        if (_dragGhost != null)
        {
            AddToOverlay(_dragGhost);
            UpdateDragPosition();
            DragAnimationService.AnimateDragStart(_dragGhost);
        }

        _currentDragSource.OnDragStarted();
        _dragUpdateTimer.Start();
        
        var dragArgs = new DragEventArgs(_currentDragSource, _currentPointerPosition);
        DragStarted?.Invoke(this, dragArgs);
        _eventService.DispatchEvent("dragStarted", dragArgs);
    }

    private void CreateTouchDragGhost(Control sourceControl)
    {
        // Create a larger, more visible drag ghost for touch devices
        _dragGhost = new Border
        {
            Background = new SolidColorBrush(Colors.White, 0.95),
            BorderBrush = new SolidColorBrush(Colors.Blue, 0.8),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Colors.Black,
                OffsetX = 4,
                OffsetY = 4,
                Blur = 12,
                Spread = 0
            }),
            Child = new TextBlock
            {
                Text = GetDragText(sourceControl),
                Padding = new Thickness(16, 8),
                FontWeight = FontWeight.Bold,
                FontSize = 16, // Larger for touch
                Foreground = new SolidColorBrush(Colors.DarkBlue)
            }
        };

        Canvas.SetLeft(_dragGhost, _currentPointerPosition.X - 75); // Larger offset
        Canvas.SetTop(_dragGhost, _currentPointerPosition.Y - 25);
    }

    private void ShowTouchFeedback(Control control)
    {
        // Brief visual feedback for touch interaction
        var originalOpacity = control.Opacity;
        control.Opacity = 0.7;
        
        DispatcherTimer.RunOnce(() => control.Opacity = originalOpacity, TimeSpan.FromMilliseconds(150));
    }

    private void ShowTouchDragFeedback(Control control)
    {
        if (!ControlBackgroundHelper.TryGetAccessors(control, out var getter, out var setter))
        {
            return;
        }

        var original = getter();
        setter(new SolidColorBrush(Colors.LightBlue, 0.5));

        DispatcherTimer.RunOnce(() => setter(original), TimeSpan.FromMilliseconds(300));
    }
}

/// <summary>
/// Drag source interface
/// </summary>
public interface IDragSource
{
    Control Element { get; }
    object GetDragData();
    void OnDragStarted();
    void OnDragEnded(bool success);
}

/// <summary>
/// Drop zone interface
/// </summary>
public interface IDropZone
{
    bool ContainsPoint(Point position);
    bool CanAccept(IDragSource dragSource);
    bool TryDrop(IDragSource dragSource, Point position);
    void OnDragEnter(IDragSource dragSource);
    void OnDragLeave(IDragSource dragSource);
    void OnDragOver(IDragSource dragSource, Point position);
    Point GetDropIndicatorPosition(Point position);
    double GetDropIndicatorHeight();
}

/// <summary>
/// Drag event arguments
/// </summary>
public class DragEventArgs : EventArgs
{
    public IDragSource DragSource { get; }
    public Point Position { get; }

    public DragEventArgs(IDragSource dragSource, Point position)
    {
        DragSource = dragSource;
        Position = position;
    }
}
