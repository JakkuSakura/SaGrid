using System;
using Avalonia;
using Avalonia.Controls;

namespace SaGrid.Advanced.DragDrop;

/// <summary>
/// Minimal animation helpers for drag & drop interactions. These are intentionally light-weight
/// to avoid depending on optional Avalonia animation packages while still providing immediate
/// visual feedback hooks.
/// </summary>
public static class DragAnimationService
{
    public static void AnimateDragStart(Control dragGhost)
    {
        dragGhost.Opacity = 0.9;
    }

    public static void AnimateDragMove(Control dragGhost, Point newPosition, bool smooth = true)
    {
        _ = smooth; // Smoothness hint currently unused but kept for future enhancement.
        Canvas.SetLeft(dragGhost, newPosition.X - 50);
        Canvas.SetTop(dragGhost, newPosition.Y - 15);
    }

    public static void AnimateSuccessfulDrop(Control dragGhost, Action onComplete)
    {
        dragGhost.Opacity = 0;
        onComplete();
    }

    public static void AnimateFailedDrop(Control dragGhost, Action onComplete)
    {
        dragGhost.Opacity = 0;
        onComplete();
    }

    public static void AnimateDropZoneEnter(Control dropZone)
    {
        dropZone.Opacity = 0.8;
    }

    public static void AnimateDropZoneExit(Control dropZone)
    {
        dropZone.Opacity = 0;
    }

    public static void AnimateDropIndicator(Control indicator, Point position)
    {
        Canvas.SetLeft(indicator, position.X);
        Canvas.SetTop(indicator, position.Y);
        indicator.Opacity = 1;
    }
}
