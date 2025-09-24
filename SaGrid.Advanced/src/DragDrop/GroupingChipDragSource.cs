using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SaGrid.Core;

namespace SaGrid.Advanced.DragDrop;

/// <summary>
/// Drag source representing a grouping chip. It reuses the same drag/drop manager infrastructure
/// as column headers but keeps the visual feedback chip-specific so we avoid leaking header
/// styling assumptions.
/// </summary>
public class GroupingChipDragSource<TData> : IDragSource
{
    private readonly IColumn<TData> _column;
    private readonly Control _chipControl;

    public GroupingChipDragSource(IColumn<TData> column, Control chipControl)
    {
        _column = column;
        _chipControl = chipControl;
    }

    public Control Element => _chipControl;

    public object GetDragData()
    {
        return _column;
    }

    public void OnDragStarted()
    {
        _chipControl.Opacity = 0.6;
        _chipControl.Cursor = new Cursor(StandardCursorType.SizeAll);
        if (_chipControl is Border border)
        {
            border.BorderBrush = Brushes.SteelBlue;
            border.BorderThickness = new Thickness(2);
        }
    }

    public void OnDragEnded(bool success)
    {
        _chipControl.Opacity = 1.0;
        _chipControl.Cursor = new Cursor(StandardCursorType.Hand);

        if (_chipControl is Border border)
        {
            border.BorderBrush = Brushes.SteelBlue;
            border.BorderThickness = new Thickness(1);
            border.Background = new SolidColorBrush(success ? Colors.LightSkyBlue : Colors.LightSteelBlue, 0.9);
        }
    }
}
