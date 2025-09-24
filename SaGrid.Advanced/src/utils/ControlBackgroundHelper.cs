using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace SaGrid.Advanced.Utils;

internal static class ControlBackgroundHelper
{
    public static bool TryGetAccessors(Control control, out Func<IBrush?> getter, out Action<IBrush?> setter)
    {
        switch (control)
        {
            case Border border:
                getter = () => border.Background;
                setter = brush => border.Background = brush;
                return true;
            case Panel panel:
                getter = () => panel.Background;
                setter = brush => panel.Background = brush;
                return true;
            case TemplatedControl templated:
                getter = () => templated.Background;
                setter = brush => templated.Background = brush;
                return true;
            default:
                getter = null!;
                setter = null!;
                return false;
        }
    }
}
