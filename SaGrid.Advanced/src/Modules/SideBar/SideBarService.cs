using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;

namespace SaGrid.Advanced.Modules.SideBar;

/// <summary>
/// Manages side bar state, registered tool panels, and placement. Mirrors AG Grid's side bar service pattern.
/// </summary>
public class SideBarService
{
    private readonly Dictionary<string, SideBarPanelDefinition> _panelDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Control> _panelCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _visible = true;
    private string? _activePanelId;
    private SideBarPosition _position = SideBarPosition.Left;

    public event EventHandler<SideBarChangedEventArgs>? StateChanged;

    public bool IsVisible => _visible;

    public string? ActivePanelId => _activePanelId;

    public SideBarPosition Position => _position;

    public IReadOnlyList<SideBarPanelDefinition> GetPanels()
    {
        return new ReadOnlyCollection<SideBarPanelDefinition>(_panelDefinitions.Values.ToList());
    }

    public void SetPanels(IEnumerable<SideBarPanelDefinition> definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        _panelDefinitions.Clear();
        _panelCache.Clear();

        foreach (var definition in definitions)
        {
            RegisterPanel(definition);
        }

        if (_activePanelId != null && !_panelDefinitions.ContainsKey(_activePanelId))
        {
            _activePanelId = null;
        }

        NotifyStateChanged(SideBarChangeKind.PanelsChanged);
    }

    public void RegisterPanel(SideBarPanelDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidOperationException("Side bar panels require a valid Id.");
        }

        _panelDefinitions[definition.Id] = definition;
        NotifyStateChanged(SideBarChangeKind.PanelsChanged);
    }

    public bool IsPanelRegistered(string panelId) => _panelDefinitions.ContainsKey(panelId);

    public void OpenPanel(string panelId)
    {
        if (!_panelDefinitions.ContainsKey(panelId))
        {
            throw new InvalidOperationException($"Side bar panel '{panelId}' is not registered.");
        }

        var changed = _activePanelId != panelId;
        _activePanelId = panelId;
        if (!_visible)
        {
            _visible = true;
            changed = true;
        }

        if (changed)
        {
            NotifyStateChanged(SideBarChangeKind.ActivePanelChanged);
        }
    }

    public void ClosePanel()
    {
        if (_activePanelId != null)
        {
            _activePanelId = null;
            NotifyStateChanged(SideBarChangeKind.ActivePanelChanged);
        }
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }

        _visible = visible;
        if (!visible)
        {
            _activePanelId = null;
        }

        NotifyStateChanged(SideBarChangeKind.VisibilityChanged);
    }

    public void ToggleVisible()
    {
        SetVisible(!_visible);
    }

    public void SetPosition(SideBarPosition position)
    {
        if (_position == position)
        {
            return;
        }

        _position = position;
        NotifyStateChanged(SideBarChangeKind.PositionChanged);
    }

    public Control? GetActivePanelControl()
    {
        if (_activePanelId == null)
        {
            return null;
        }

        if (!_panelCache.TryGetValue(_activePanelId, out var control))
        {
            var definition = _panelDefinitions[_activePanelId];
            control = definition.ContentFactory();
            _panelCache[_activePanelId] = control;
        }

        return control;
    }

    public SideBarState GetState()
    {
        return new SideBarState(_visible, _position, GetPanels(), _activePanelId);
    }

    private void NotifyStateChanged(SideBarChangeKind changeKind)
    {
        StateChanged?.Invoke(this, new SideBarChangedEventArgs(GetState(), changeKind));
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
    public SideBarChangedEventArgs(SideBarState state, SideBarChangeKind change)
    {
        State = state;
        Change = change;
    }

    public SideBarState State { get; }

    public SideBarChangeKind Change { get; }
}
