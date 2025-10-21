using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SaGrid.Advanced.Behaviors;

// Code-based equivalent to XAML Interaction behavior:
// <ic:ButtonClickEventTriggerBehavior KeyModifiers="Control,Shift,Command,Option" />
public class ButtonClickEventTriggerBehavior
{
    public KeyModifiers RequiredModifiers { get; set; } = KeyModifiers.None;
    public Action? Action { get; set; }

    private bool _attached;

    public void Attach(Button button)
    {
        if (_attached) return;
        _attached = true;

        // Use tunneling to see the pointer event before Button handles it
        button.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Match exact modifier combination to avoid overlapping triggers
        if (e.KeyModifiers == RequiredModifiers)
        {
            Action?.Invoke();
            e.Handled = true;
        }
    }
}
