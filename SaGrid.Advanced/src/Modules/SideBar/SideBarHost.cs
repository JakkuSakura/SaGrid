using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace SaGrid.Advanced.Modules.SideBar;

/// <summary>
/// Visual host for the side bar, with button strip and tool panel area. Supports left and right placement.
/// </summary>
public class SideBarHost : UserControl
{
    private SideBarService? _service;
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

    public void Initialize(SideBarService service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        if (_service != null)
        {
            _service.StateChanged -= OnServiceStateChanged;
        }

        _service = service;
        _service.StateChanged += OnServiceStateChanged;

        SyncFromService(_service.GetState());
    }

    private void OnServiceStateChanged(object? sender, SideBarChangedEventArgs e)
    {
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
        if (_service == null)
        {
            return;
        }

        IsVisible = state.IsVisible;

        UpdateLayoutForPosition(state.Position);
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
        if (_service == null)
        {
            return;
        }

        if (sender is ToggleButton button && button.Tag is string panelId)
        {
            var isActive = string.Equals(_service.ActivePanelId, panelId, StringComparison.OrdinalIgnoreCase);
            if (isActive)
            {
                _service.ClosePanel();
            }
            else
            {
                _service.OpenPanel(panelId);
            }
        }
    }

    private void UpdateActivePanel()
    {
        if (_service == null)
        {
            _panelHost.Content = null;
            return;
        }

        var control = _service.GetActivePanelControl();
        _panelHost.Content = control;

        // Update button check states
        foreach (var child in _buttonPanel.Children.OfType<ToggleButton>())
        {
            if (child.Tag is string panelId)
            {
                child.IsChecked = string.Equals(_service.ActivePanelId, panelId, StringComparison.OrdinalIgnoreCase);
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
}
