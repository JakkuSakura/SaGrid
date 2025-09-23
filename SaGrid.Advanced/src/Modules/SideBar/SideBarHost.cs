using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
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

        IsEnabled = state.IsVisible;
        IsHitTestVisible = state.IsVisible;
        Opacity = state.IsVisible ? 1 : 0;

        UpdateLayoutForPosition(state.Position);
        ApplyVisibility(state);
        BuildButtons(state);
        UpdateActivePanel();
    }

    private void BuildButtons(SideBarState state)
    {
        _buttonPanel.Children.Clear();

        foreach (var panel in state.Panels)
        {
            var button = new ToggleButton
            {
                Content = panel.Title,
                Tag = panel.Id,
                Margin = new Thickness(0, 0, 0, 6),
                IsChecked = string.Equals(state.ActivePanelId, panel.Id, StringComparison.OrdinalIgnoreCase)
            };

            button.Click += OnPanelButtonClicked;
            _buttonPanel.Children.Add(button);
        }
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
