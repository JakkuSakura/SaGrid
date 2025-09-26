using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SaGrid;

namespace SaGrid.Advanced.Modules.SideBar;

/// <summary>
/// Visual host for the side bar, with button strip and tool panel area. Supports left and right placement.
/// </summary>
public class SideBarHost : UserControl
{
    private SideBarService? _service;
    private object? _grid;
    private Func<SideBarState>? _getState;
    private Action<string>? _openPanel;
    private Action? _closePanel;
    private Func<string?>? _getActivePanelId;
    private Func<Control?>? _getActivePanelControl;
    private readonly Grid _rootGrid;
    private readonly StackPanel _buttonPanel;
    private readonly ContentControl _panelHost;
    private ToggleButton? _lastFocusedButton;
    private bool _isVisible;
    private string? _activePanelId;

    public SideBarHost()
    {
        _rootGrid = new Grid();
        _buttonPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _panelHost = new ContentControl();

        _rootGrid.Children.Add(_buttonPanel);
        _rootGrid.Children.Add(_panelHost);

        Content = _rootGrid;

        UpdateLayoutForPosition(SideBarPosition.Left);
    }

    public void Initialize<TData>(SideBarService service, SaGrid<TData> grid)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        if (_service != null)
        {
            _service.StateChanged -= OnServiceStateChanged;
        }

        _service = service;
        _grid = grid;
        _getState = () => service.GetState(grid);
        _openPanel = panelId => service.OpenPanel(grid, panelId);
        _closePanel = () => service.ClosePanel(grid);
        _getActivePanelId = () => service.GetActivePanelId(grid);
        _getActivePanelControl = () => service.GetActivePanelControl(grid);
        _service.StateChanged += OnServiceStateChanged;

        SyncFromService(_getState());
    }

    private void OnServiceStateChanged(object? sender, SideBarChangedEventArgs e)
    {
        if (_grid == null || !ReferenceEquals(e.Grid, _grid))
        {
            return;
        }

        void Apply() => SyncFromService(e.State);

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Apply);
        }
        else
        {
            Apply();
        }
    }

    private void SyncFromService(SideBarState state)
    {
        if (_service == null || _grid == null || _getState == null)
        {
            return;
        }

        var wasVisible = _isVisible;
        var previousActivePanelId = _activePanelId;

        _isVisible = state.IsVisible;
        _activePanelId = state.ActivePanelId;

        IsEnabled = state.IsVisible;
        IsHitTestVisible = state.IsVisible;
        Opacity = state.IsVisible ? 1 : 0;

        UpdateLayoutForPosition(state.Position);
        ApplyVisibility(state);
        BuildButtons(state);
        UpdateActivePanel();

        if (_isVisible)
        {
            if (!wasVisible)
            {
                EnsureButtonFocus(force: true, preferredPanelId: _activePanelId);
            }
            else if (!string.Equals(previousActivePanelId, _activePanelId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureButtonFocus(force: false, preferredPanelId: _activePanelId);
            }
        }
    }

    private void BuildButtons(SideBarState state)
    {
        foreach (var existing in _buttonPanel.Children.OfType<ToggleButton>().ToList())
        {
            existing.Click -= OnPanelButtonClicked;
            existing.KeyDown -= OnPanelButtonKeyDown;
            existing.GotFocus -= OnPanelButtonGotFocus;
        }

        _buttonPanel.Children.Clear();

        foreach (var panel in state.Panels)
        {
            var button = CreatePanelButton(panel, state.ActivePanelId);
            _buttonPanel.Children.Add(button);
        }
    }

    private ToggleButton CreatePanelButton(SideBarPanelDefinition panel, string? activePanelId)
    {
        var isActive = string.Equals(activePanelId, panel.Id, StringComparison.OrdinalIgnoreCase);

        var button = new ToggleButton
        {
            Tag = panel.Id,
            Margin = new Thickness(0, 0, 0, 6),
            IsChecked = isActive,
            Focusable = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ToolTip = panel.Title,
            Content = CreateButtonContent(panel)
        };

        AutomationProperties.SetName(button, panel.Title);

        button.Click += OnPanelButtonClicked;
        button.KeyDown += OnPanelButtonKeyDown;
        button.GotFocus += OnPanelButtonGotFocus;

        return button;
    }

    private Control CreateButtonContent(SideBarPanelDefinition panel)
    {
        if (!string.IsNullOrWhiteSpace(panel.Icon) && SideBarIconLibrary.TryCreate(panel.Icon, out var iconControl))
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4
            };

            stack.Children.Add(iconControl);
            stack.Children.Add(new TextBlock
            {
                Text = panel.Title,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            return stack;
        }

        return new TextBlock
        {
            Text = panel.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
    }

    private void OnPanelButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_service == null || _grid == null)
        {
            return;
        }

        if (sender is ToggleButton button && button.Tag is string panelId)
        {
            var activePanelId = _getActivePanelId?.Invoke();
            var isActive = string.Equals(activePanelId, panelId, StringComparison.OrdinalIgnoreCase);
            if (isActive)
            {
                _closePanel?.Invoke();
            }
            else
            {
                _openPanel?.Invoke(panelId);
            }
        }
    }

    private void UpdateActivePanel()
    {
        if (_service == null || _grid == null)
        {
            _panelHost.Content = null;
            return;
        }

        var control = _getActivePanelControl?.Invoke();
        _panelHost.Content = control;

        // Update button check states
        var activePanelIdCurrent = _getActivePanelId?.Invoke();
        foreach (var child in _buttonPanel.Children.OfType<ToggleButton>())
        {
            if (child.Tag is string panelId)
            {
                child.IsChecked = string.Equals(activePanelIdCurrent, panelId, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void OnPanelButtonKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ToggleButton button)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
            case Key.Right:
                MoveFocusRelative(button, +1);
                e.Handled = true;
                break;
            case Key.Up:
            case Key.Left:
                MoveFocusRelative(button, -1);
                e.Handled = true;
                break;
            case Key.Home:
                FocusButton(0);
                e.Handled = true;
                break;
            case Key.End:
                var buttons = GetButtonList();
                if (buttons.Count > 0)
                {
                    FocusButton(buttons.Count - 1);
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                _closePanel?.Invoke();
                EnsureButtonFocus(force: true);
                e.Handled = true;
                break;
        }
    }

    private void OnPanelButtonGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is ToggleButton button)
        {
            _lastFocusedButton = button;
        }
    }

    private void MoveFocusRelative(ToggleButton current, int delta)
    {
        var buttons = GetButtonList();
        if (buttons.Count == 0)
        {
            return;
        }

        var index = buttons.IndexOf(current);
        if (index < 0 && _lastFocusedButton != null)
        {
            index = buttons.IndexOf(_lastFocusedButton);
        }

        if (index < 0)
        {
            index = 0;
        }

        var nextIndex = (index + delta) % buttons.Count;
        if (nextIndex < 0)
        {
            nextIndex += buttons.Count;
        }

        FocusButton(nextIndex);
    }

    private void FocusButton(int index)
    {
        var buttons = GetButtonList();
        if (index < 0 || index >= buttons.Count)
        {
            return;
        }

        buttons[index].Focus();
    }

    private List<ToggleButton> GetButtonList()
    {
        return _buttonPanel.Children.OfType<ToggleButton>().ToList();
    }

    // Mirrors AG Grid's ManagedFocusFeature by keeping navigation inside the strip and restoring focus when reopened.
    private void EnsureButtonFocus(bool force, string? preferredPanelId = null)
    {
        if (!_isVisible)
        {
            return;
        }

        var buttons = GetButtonList();
        if (buttons.Count == 0)
        {
            return;
        }

        if (!force)
        {
            var current = FocusManager.Instance?.Current as IVisual;
            if (current != null && _buttonPanel.IsVisualAncestorOf(current))
            {
                return;
            }
        }

        ToggleButton? target = null;

        if (!string.IsNullOrEmpty(preferredPanelId))
        {
            target = buttons.FirstOrDefault(b => string.Equals(b.Tag as string, preferredPanelId, StringComparison.OrdinalIgnoreCase));
        }

        target ??= buttons.FirstOrDefault(b => b.IsChecked == true);
        target ??= buttons[0];

        var buttonToFocus = target;
        Dispatcher.UIThread.Post(() => buttonToFocus.Focus());
    }

    private void UpdateLayoutForPosition(SideBarPosition position)
    {
        _rootGrid.ColumnDefinitions.Clear();

        if (position == SideBarPosition.Left)
        {
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            Grid.SetColumn(_buttonPanel, 0);
            Grid.SetColumn(_panelHost, 1);

            _buttonPanel.Margin = new Thickness(0, 0, 8, 0);
        }
        else
        {
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid.SetColumn(_panelHost, 0);
            Grid.SetColumn(_buttonPanel, 1);

            _buttonPanel.Margin = new Thickness(8, 0, 0, 0);
        }
    }

    private void ApplyVisibility(SideBarState state)
    {
        if (_rootGrid?.ColumnDefinitions.Count != 2)
        {
            return;
        }

        if (state.Position == SideBarPosition.Left)
        {
            _rootGrid.ColumnDefinitions[0].Width = state.IsVisible ? GridLength.Auto : new GridLength(0);
            _rootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            _rootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _rootGrid.ColumnDefinitions[1].Width = state.IsVisible ? GridLength.Auto : new GridLength(0);
        }
    }
}
