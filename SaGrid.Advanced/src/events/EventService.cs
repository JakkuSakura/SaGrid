using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaGrid.Advanced.Events;

/// <summary>
/// Event service implementation following AG Grid's EventService pattern.
/// Provides typed event handling with support for both async and sync global listeners.
/// </summary>
public class EventService : IEventService
{
    private readonly ConcurrentDictionary<string, List<object>> _eventListeners = new();
    private readonly List<(Action<string, object> Listener, bool Async)> _globalListeners = new();
    private readonly object _lock = new();

    public void AddEventListener<T>(string eventType, Action<T> listener) where T : class
    {
        if (listener == null) throw new ArgumentNullException(nameof(listener));

        _eventListeners.AddOrUpdate(
            eventType,
            new List<object> { listener },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(listener);
                    return existing;
                }
            });
    }

    public void RemoveEventListener<T>(string eventType, Action<T> listener) where T : class
    {
        if (listener == null) throw new ArgumentNullException(nameof(listener));

        if (_eventListeners.TryGetValue(eventType, out var listeners))
        {
            lock (listeners)
            {
                listeners.Remove(listener);
                if (listeners.Count == 0)
                {
                    _eventListeners.TryRemove(eventType, out _);
                }
            }
        }
    }

    public void AddGlobalListener(Action<string, object> listener, bool async = true)
    {
        if (listener == null) throw new ArgumentNullException(nameof(listener));

        lock (_lock)
        {
            _globalListeners.Add((listener, async));
        }
    }

    public void RemoveGlobalListener(Action<string, object> listener)
    {
        if (listener == null) throw new ArgumentNullException(nameof(listener));

        lock (_lock)
        {
            for (int i = _globalListeners.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_globalListeners[i].Listener, listener))
                {
                    _globalListeners.RemoveAt(i);
                }
            }
        }
    }

    public void DispatchEvent<T>(string eventType, T eventData) where T : class
    {
        if (eventData == null) throw new ArgumentNullException(nameof(eventData));

        // Dispatch to specific event listeners
        if (_eventListeners.TryGetValue(eventType, out var listeners))
        {
            List<object> listenersCopy;
            lock (listeners)
            {
                listenersCopy = new List<object>(listeners);
            }

            foreach (var listener in listenersCopy)
            {
                if (listener is Action<T> typedListener)
                {
                    try
                    {
                        typedListener(eventData);
                    }
                    catch (Exception ex)
                    {
                        // Log the exception but don't let it break other listeners
                        Console.WriteLine($"Error in event listener for {eventType}: {ex.Message}");
                    }
                }
            }
        }

        // Dispatch to global listeners
        List<(Action<string, object> Listener, bool Async)> globalListenersCopy;
        lock (_lock)
        {
            globalListenersCopy = new List<(Action<string, object>, bool)>(_globalListeners);
        }

        foreach (var (globalListener, async) in globalListenersCopy)
        {
            try
            {
                if (async)
                {
                    Task.Run(() => globalListener(eventType, eventData));
                }
                else
                {
                    globalListener(eventType, eventData);
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't let it break other listeners
                Console.WriteLine($"Error in global event listener for {eventType}: {ex.Message}");
            }
        }
    }

    public void RemoveAllEventListeners()
    {
        _eventListeners.Clear();
        lock (_lock)
        {
            _globalListeners.Clear();
        }
    }
}

/// <summary>
/// Common grid event types following AG Grid's pattern
/// </summary>
public static class GridEventTypes
{
    // Grid lifecycle events
    public const string GridReady = "gridReady";
    public const string GridPreDestroyed = "gridPreDestroyed";
    public const string GridDestroyed = "gridDestroyed";

    // Row events
    public const string RowDataChanged = "rowDataChanged";
    public const string RowDataUpdated = "rowDataUpdated";
    public const string RowSelected = "rowSelected";
    public const string RowClicked = "rowClicked";
    public const string RowDoubleClicked = "rowDoubleClicked";

    // Cell events
    public const string CellClicked = "cellClicked";
    public const string CellDoubleClicked = "cellDoubleClicked";
    public const string CellFocused = "cellFocused";
    public const string CellValueChanged = "cellValueChanged";

    // Column events
    public const string ColumnResized = "columnResized";
    public const string ColumnMoved = "columnMoved";
    public const string ColumnVisible = "columnVisible";
    public const string ColumnPinned = "columnPinned";

    // Filter events
    public const string FilterChanged = "filterChanged";
    public const string FilterModified = "filterModified";

    // Editing events
    public const string CellEditStarted = "cellEditStarted";
    public const string CellEditCommitted = "cellEditCommitted";
    public const string CellEditCancelled = "cellEditCancelled";
    public const string BatchEditStarted = "batchEditStarted";
    public const string BatchEditCommitted = "batchEditCommitted";
    public const string BatchEditCancelled = "batchEditCancelled";
    public const string BatchEditUndone = "batchEditUndone";
    public const string BatchEditRedone = "batchEditRedone";

    // Sort events
    public const string SortChanged = "sortChanged";

    // Selection events
    public const string SelectionChanged = "selectionChanged";

    // Model events
    public const string ModelUpdated = "modelUpdated";

    // Grouping and aggregation events
    public const string GroupingChanged = "groupingChanged";
    public const string AggregationChanged = "aggregationChanged";
}
