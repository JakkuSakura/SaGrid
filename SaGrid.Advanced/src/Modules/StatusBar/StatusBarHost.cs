using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid;

namespace SaGrid.Advanced.Modules.StatusBar;

/// <summary>
/// Avalonia control that hosts status bar widgets. Mirrors AG Grid's status bar component pattern.
/// Automatically arranges widgets horizontally and responds to service state changes.
/// </summary>
public class StatusBarHost : UserControl
{
    private StatusBarService? _service;
    private object? _grid;
    private StackPanel? _container;
    private StackPanel? _rightContainer;

    public void Initialize(StatusBarService service, object grid)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));

        _service.StateChanged += OnServiceStateChanged;

        BuildUI();
        UpdateContent();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_service != null)
        {
            _service.StateChanged -= OnServiceStateChanged;
        }
    }

    private void BuildUI()
    {
        // Use a Grid with two columns for better layout control
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Left side for status widgets
        _container = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_container, 0);
        grid.Children.Add(_container);

        // Right side for pagination widgets
        _rightContainer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_rightContainer, 1);
        grid.Children.Add(_rightContainer);

        var border = new Border
        {
            Child = grid,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
            Padding = new Thickness(8, 4),
            MinHeight = 32
        };

        Content = border;
    }

    private void OnServiceStateChanged(object? sender, StatusBarChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Grid, _grid))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            UpdateContent();
        });
    }

    private void UpdateContent()
    {
        if (_service == null || _grid == null || _container == null || _rightContainer == null)
        {
            return;
        }

        _container.Children.Clear();
        _rightContainer.Children.Clear();

        // Use reflection to call generic methods since we can't use dynamic in this context
        var isVisibleMethod = typeof(StatusBarService).GetMethod(nameof(StatusBarService.IsVisible));
        var getWidgetControlsMethod = typeof(StatusBarService).GetMethod(nameof(StatusBarService.GetWidgetControls));
        var getWidgetsMethod = typeof(StatusBarService).GetMethod(nameof(StatusBarService.GetWidgets));
        
        var gridType = _grid.GetType();
        var dataType = gridType.GetGenericArguments()[0];
        
        var isVisible = (bool)isVisibleMethod!.MakeGenericMethod(dataType).Invoke(_service, new[] { _grid })!;
        
        if (isVisible)
        {
            var controls = (IReadOnlyList<Control>)getWidgetControlsMethod!.MakeGenericMethod(dataType).Invoke(_service, new[] { _grid })!;
            var widgets = (IReadOnlyList<StatusBarWidgetDefinition>)getWidgetsMethod!.MakeGenericMethod(dataType).Invoke(_service, new[] { _grid })!;
            var widgetPairs = controls
                .Zip(widgets, (control, def) => new { Control = control, Definition = def })
                .OrderBy(x => x.Definition.Order)
                .ToList();

            // Separate pagination from other widgets for better layout
            var paginationWidgets = widgetPairs.Where(x => x.Definition.Id == "pagination").ToList();
            var otherWidgets = widgetPairs.Where(x => x.Definition.Id != "pagination").ToList();

            // Add status widgets (left side)
            foreach (var widget in otherWidgets)
            {
                _container.Children.Add(widget.Control);

                // Add separator between widgets (except for the last one)
                if (widget != otherWidgets.Last())
                {
                    _container.Children.Add(new Border
                    {
                        Width = 1,
                        Height = 20,
                        Background = Brushes.LightGray,
                        Margin = new Thickness(8, 0)
                    });
                }
            }

            // Add pagination widgets to right container
            foreach (var widget in paginationWidgets)
            {
                _rightContainer.Children.Add(widget.Control);
            }

            IsVisible = _container.Children.Count > 0 || _rightContainer.Children.Count > 0;
        }
        else
        {
            IsVisible = false;
        }
    }

    public void RefreshWidgets()
    {
        UpdateContent();
    }
}