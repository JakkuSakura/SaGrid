using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using SaGrid;

namespace SaGrid.Advanced.Modules.StatusBar;

/// <summary>
/// Manages status bar widgets and state per grid instance. Mirrors AG Grid's StatusBarService pattern.
/// Allows modules to register custom status widgets that display grid statistics and information.
/// </summary>
public class StatusBarService
{
    private sealed class StatusBarInstance
    {
        public bool Visible = true;
        public StatusBarPosition Position = StatusBarPosition.Bottom;
        public List<StatusBarWidgetDefinition> WidgetList = new();
        public Dictionary<string, StatusBarWidgetDefinition> WidgetDefinitions = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Control> WidgetCache = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConditionalWeakTable<object, StatusBarInstance> _instances = new();

    public event EventHandler<StatusBarChangedEventArgs>? StateChanged;

    public IReadOnlyList<StatusBarWidgetDefinition> GetWidgets<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return new ReadOnlyCollection<StatusBarWidgetDefinition>(instance.WidgetList.ToList());
    }

    public void SetWidgets<TData>(SaGrid<TData> grid, IEnumerable<StatusBarWidgetDefinition> definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        var instance = GetOrCreateInstance(grid);
        var definitionList = definitions.ToList();
        instance.WidgetList = definitionList;
        instance.WidgetDefinitions = definitionList.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        instance.WidgetCache.Clear();

        NotifyStateChanged(grid, StatusBarChangeKind.WidgetsChanged);
    }

    public void RegisterWidget<TData>(SaGrid<TData> grid, StatusBarWidgetDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidOperationException("Status bar widgets require a valid Id.");
        }

        var instance = GetOrCreateInstance(grid);
        instance.WidgetDefinitions[definition.Id] = definition;
        instance.WidgetList.RemoveAll(w => string.Equals(w.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
        instance.WidgetList.Add(definition);
        instance.WidgetCache.Remove(definition.Id);
        NotifyStateChanged(grid, StatusBarChangeKind.WidgetsChanged);
    }

    public bool IsWidgetRegistered<TData>(SaGrid<TData> grid, string widgetId)
    {
        var instance = GetOrCreateInstance(grid);
        return instance.WidgetDefinitions.ContainsKey(widgetId);
    }

    public void UnregisterWidget<TData>(SaGrid<TData> grid, string widgetId)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.WidgetDefinitions.Remove(widgetId))
        {
            instance.WidgetList.RemoveAll(w => string.Equals(w.Id, widgetId, StringComparison.OrdinalIgnoreCase));
            instance.WidgetCache.Remove(widgetId);
            NotifyStateChanged(grid, StatusBarChangeKind.WidgetsChanged);
        }
    }

    public void SetVisible<TData>(SaGrid<TData> grid, bool visible)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.Visible == visible)
        {
            return;
        }

        instance.Visible = visible;
        NotifyStateChanged(grid, StatusBarChangeKind.VisibilityChanged);
    }

    public void ToggleVisible<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        SetVisible(grid, !instance.Visible);
    }

    public bool IsVisible<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return instance.Visible;
    }

    public void SetPosition<TData>(SaGrid<TData> grid, StatusBarPosition position)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.Position == position)
        {
            return;
        }

        instance.Position = position;
        NotifyStateChanged(grid, StatusBarChangeKind.PositionChanged);
    }

    public StatusBarPosition GetPosition<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return instance.Position;
    }

    public IReadOnlyList<Control> GetWidgetControls<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        var controls = new List<Control>();

        foreach (var definition in instance.WidgetList)
        {
            if (!instance.WidgetCache.TryGetValue(definition.Id, out var control))
            {
                control = definition.ContentFactory(grid);
                instance.WidgetCache[definition.Id] = control;
            }
            controls.Add(control);
        }

        return controls.AsReadOnly();
    }

    public StatusBarState GetState<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return new StatusBarState(instance.Visible, instance.Position, GetWidgets(grid));
    }

    public void EnsureDefaultWidgets<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.WidgetDefinitions.Count == 0)
        {
            SetWidgets(grid, StatusBarDefaultWidgets.CreateDefaultWidgets<TData>());
        }
    }

    private StatusBarInstance GetOrCreateInstance(object grid)
    {
        return _instances.GetValue(grid, _ => new StatusBarInstance());
    }

    private void NotifyStateChanged<TData>(SaGrid<TData> grid, StatusBarChangeKind changeKind)
    {
        var state = GetState(grid);
        StateChanged?.Invoke(this, new StatusBarChangedEventArgs(grid, state, changeKind));
    }
}

public enum StatusBarPosition
{
    Top,
    Bottom
}

public record StatusBarWidgetDefinition(string Id, string? Title, Func<object, Control> ContentFactory, int Order = 0);

public record StatusBarState(bool IsVisible, StatusBarPosition Position, IReadOnlyList<StatusBarWidgetDefinition> Widgets);

public enum StatusBarChangeKind
{
    VisibilityChanged,
    PositionChanged,
    WidgetsChanged
}

public sealed class StatusBarChangedEventArgs : EventArgs
{
    public StatusBarChangedEventArgs(object grid, StatusBarState state, StatusBarChangeKind change)
    {
        Grid = grid;
        State = state;
        Change = change;
    }

    public object Grid { get; }

    public StatusBarState State { get; }

    public StatusBarChangeKind Change { get; }
}