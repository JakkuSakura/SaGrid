using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced.Interactive;
using SaGrid.Core;
using SaGrid.Advanced.Utils;

namespace SaGrid.Advanced.DragDrop;

/// <summary>
/// Drag source for column headers following AG Grid's column drag patterns
/// </summary>
public class ColumnDragSource<TData> : IDragSource
{
    private readonly IColumn<TData> _column;
    private readonly Control _headerControl;
    private readonly ColumnInteractiveService<TData> _columnService;
    
    public Control Element => _headerControl;
    
    public ColumnDragSource(IColumn<TData> column, Control headerControl, ColumnInteractiveService<TData> columnService)
    {
        _column = column;
        _headerControl = headerControl;
        _columnService = columnService;
    }

    public object GetDragData()
    {
        return _column;
    }

    public void OnDragStarted()
    {
        // Visual feedback - make original header semi-transparent
        _headerControl.Opacity = 0.5;
        
        // Add drag cursor
        _headerControl.Cursor = new Cursor(StandardCursorType.Hand);
        
        // Could also add a visual drag indicator
        if (_headerControl is Border border)
        {
            border.BorderBrush = new SolidColorBrush(Colors.Blue);
            border.BorderThickness = new Thickness(2);
        }
    }

    public void OnDragEnded(bool success)
    {
        // Restore original appearance
        _headerControl.Opacity = 1.0;
        _headerControl.Cursor = null;
        
        if (_headerControl is Border border)
        {
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);
        }

        if (success)
        {
            // Successful drop - could add success animation
            AnimateSuccess();
        }
        else
        {
            // Failed drop - could add failure animation
            AnimateFailure();
        }
    }

    private void AnimateSuccess()
    {
        FlashBackground(new SolidColorBrush(Colors.LightGreen, 0.3));
    }

    private void AnimateFailure()
    {
        FlashBackground(new SolidColorBrush(Colors.LightCoral, 0.3));
    }

    private void FlashBackground(IBrush highlight)
    {
        if (!ControlBackgroundHelper.TryGetAccessors(_headerControl, out var getter, out var setter))
        {
            return;
        }

        var original = getter();
        setter(highlight);

        DispatcherTimer.RunOnce(() => setter(original), TimeSpan.FromMilliseconds(200));
    }
}
