using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using SaGrid.Advanced;

namespace SaGrid.Advanced.Modules.SideBar;

/// <summary>
/// Manages side bar state, registered tool panels, and placement. Mirrors AG Grid's side bar service pattern.
/// Stores independent state per SaGrid instance.
/// </summary>
public class SideBarService
{
    private sealed class SideBarInstance
    {
        public bool Visible = true;
        public string? ActivePanelId;
        public SideBarPosition Position = SideBarPosition.Left;
        public List<SideBarPanelDefinition> PanelList = new();
        public Dictionary<string, SideBarPanelDefinition> PanelDefinitions = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Control> PanelCache = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConditionalWeakTable<object, SideBarInstance> _instances = new();

    public event EventHandler<SideBarChangedEventArgs>? StateChanged;

    public IReadOnlyList<SideBarPanelDefinition> GetPanels<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return new ReadOnlyCollection<SideBarPanelDefinition>(instance.PanelList.ToList());
    }

    public void SetPanels<TData>(SaGrid<TData> grid, IEnumerable<SideBarPanelDefinition> definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        var instance = GetOrCreateInstance(grid);
        var definitionList = definitions.ToList();
        instance.PanelList = definitionList;
        instance.PanelDefinitions = definitionList.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        instance.PanelCache.Clear();

        if (instance.ActivePanelId != null && !instance.PanelDefinitions.ContainsKey(instance.ActivePanelId))
        {
            instance.ActivePanelId = null;
        }

        NotifyStateChanged(grid, SideBarChangeKind.PanelsChanged);
    }

    public void RegisterPanel<TData>(SaGrid<TData> grid, SideBarPanelDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidOperationException("Side bar panels require a valid Id.");
        }

        var instance = GetOrCreateInstance(grid);
        instance.PanelDefinitions[definition.Id] = definition;
        instance.PanelList.RemoveAll(p => string.Equals(p.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
        instance.PanelList.Add(definition);
        instance.PanelCache.Remove(definition.Id);
        NotifyStateChanged(grid, SideBarChangeKind.PanelsChanged);
    }

    public bool IsPanelRegistered<TData>(SaGrid<TData> grid, string panelId)
    {
        var instance = GetOrCreateInstance(grid);
        return instance.PanelDefinitions.ContainsKey(panelId);
    }

    public void OpenPanel<TData>(SaGrid<TData> grid, string panelId)
    {
        var instance = GetOrCreateInstance(grid);
        if (!instance.PanelDefinitions.ContainsKey(panelId))
        {
            throw new InvalidOperationException($"Side bar panel '{panelId}' is not registered.");
        }

        var changed = instance.ActivePanelId != panelId;
        instance.ActivePanelId = panelId;
        if (!instance.Visible)
        {
            instance.Visible = true;
            changed = true;
        }

        if (changed)
        {
            NotifyStateChanged(grid, SideBarChangeKind.ActivePanelChanged);
        }
    }

    public void ClosePanel<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.ActivePanelId != null)
        {
            instance.ActivePanelId = null;
            NotifyStateChanged(grid, SideBarChangeKind.ActivePanelChanged);
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
        if (!visible)
        {
            instance.ActivePanelId = null;
        }

        NotifyStateChanged(grid, SideBarChangeKind.VisibilityChanged);
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

    public void SetPosition<TData>(SaGrid<TData> grid, SideBarPosition position)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.Position == position)
        {
            return;
        }

        instance.Position = position;
        NotifyStateChanged(grid, SideBarChangeKind.PositionChanged);
    }

    public SideBarPosition GetPosition<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return instance.Position;
    }

    public Control? GetActivePanelControl<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.ActivePanelId == null)
        {
            return null;
        }

        if (!instance.PanelCache.TryGetValue(instance.ActivePanelId, out var control))
        {
            var definition = instance.PanelDefinitions[instance.ActivePanelId];
            control = definition.ContentFactory();
            instance.PanelCache[instance.ActivePanelId] = control;
        }

        return control;
    }

    public SideBarState GetState<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return new SideBarState(instance.Visible, instance.Position, GetPanels(grid), instance.ActivePanelId);
    }

    public string? GetActivePanelId<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        return instance.ActivePanelId;
    }

    public void EnsureDefaultPanels<TData>(SaGrid<TData> grid)
    {
        var instance = GetOrCreateInstance(grid);
        if (instance.PanelDefinitions.Count == 0)
        {
        SetPanels(grid, SideBarDefaultPanels.CreateDefaultPanels(grid));
        }
    }

    private SideBarInstance GetOrCreateInstance(object grid)
    {
        return _instances.GetValue(grid, _ => new SideBarInstance());
    }

    private void NotifyStateChanged<TData>(SaGrid<TData> grid, SideBarChangeKind changeKind)
    {
        var state = GetState(grid);
        StateChanged?.Invoke(this, new SideBarChangedEventArgs(grid, state, changeKind));
    }
}

public enum SideBarPosition
{
    Left,
    Right
}

public record SideBarPanelDefinition(string Id, string Title, Func<Control> ContentFactory, string? Icon = null);

public record SideBarState(bool IsVisible, SideBarPosition Position, IReadOnlyList<SideBarPanelDefinition> Panels, string? ActivePanelId);

public enum SideBarChangeKind
{
    VisibilityChanged,
    ActivePanelChanged,
    PositionChanged,
    PanelsChanged
}

public sealed class SideBarChangedEventArgs : EventArgs
{
    public SideBarChangedEventArgs(object grid, SideBarState state, SideBarChangeKind change)
    {
        Grid = grid;
        State = state;
        Change = change;
    }

    public object Grid { get; }

    public SideBarState State { get; }

    public SideBarChangeKind Change { get; }
}
