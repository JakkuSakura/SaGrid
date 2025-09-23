using System;

namespace SaGrid.Advanced.Events;

/// <summary>
/// Event service interface following AG Grid's IEventService pattern.
/// Provides typed event handling with global and local listeners.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Add a typed event listener
    /// </summary>
    void AddEventListener<T>(string eventType, Action<T> listener) where T : class;

    /// <summary>
    /// Remove a typed event listener
    /// </summary>
    void RemoveEventListener<T>(string eventType, Action<T> listener) where T : class;

    /// <summary>
    /// Add a global event listener that receives all events
    /// </summary>
    void AddGlobalListener(Action<string, object> listener, bool async = true);

    /// <summary>
    /// Remove a global event listener
    /// </summary>
    void RemoveGlobalListener(Action<string, object> listener);

    /// <summary>
    /// Dispatch an event to all registered listeners
    /// </summary>
    void DispatchEvent<T>(string eventType, T eventData) where T : class;

    /// <summary>
    /// Remove all event listeners
    /// </summary>
    void RemoveAllEventListeners();
}

/// <summary>
/// Base event arguments following AG Grid's event pattern
/// </summary>
public abstract class AgEventArgs
{
    public string Type { get; }
    public object Source { get; }

    protected AgEventArgs(string type, object source)
    {
        Type = type;
        Source = source;
    }
}