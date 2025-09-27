using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Examples.Examples;
using Examples.Examples.BatchEditing;
using Examples.Examples.ClientStarter;
using Examples.Examples.ServerSide;

namespace Examples;

public class MainWindow : Window
{
    private readonly IReadOnlyList<IExample> _examples;
    private ComboBox? _exampleSelector;
    private TextBlock? _exampleDescription;
    private ContentControl? _exampleHost;
    private ExampleHost? _currentHost;

    public MainWindow()
    {
        Title = "SaGrid.Advanced Example Gallery";
        Width = 1200;
        Height = 850;

        _examples = new List<IExample>
        {
            new ClientStarterExample(),
            new BatchEditingExample(),
            new ServerSideAnalyticsExample()
        };

        Content = BuildLayout();

        if (_exampleSelector != null && _exampleSelector.Items != null)
        {
            _exampleSelector.SelectedIndex = 0;
        }
    }

    private Control BuildLayout()
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Margin = new Thickness(20)
        };

        container.Children.Add(new TextBlock
        {
            Text = "SaGrid.Advanced Example Gallery",
            FontSize = 22,
            FontWeight = FontWeight.Bold
        });

        container.Children.Add(new TextBlock
        {
            Text = "Select an example scenario to load the corresponding grid configuration.",
            FontSize = 14,
            Foreground = Brushes.Gray
        });

        _exampleSelector = new ComboBox
        {
            ItemsSource = _examples,
            SelectedIndex = -1,
            MinWidth = 260
        };
        _exampleSelector.SelectionChanged += OnExampleSelected;
        container.Children.Add(_exampleSelector);

        _exampleDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        container.Children.Add(_exampleDescription);

        _exampleHost = new ContentControl
        {
            Content = new TextBlock
            {
                Text = "Choose an example above to see it rendered here.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }
        };

        container.Children.Add(new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = _exampleHost
        });

        return new ScrollViewer { Content = container };
    }

    private void OnExampleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_exampleSelector?.SelectedItem is IExample example)
        {
            LoadExample(example);
        }
    }

    private void LoadExample(IExample example)
    {
        _currentHost?.Cleanup?.Invoke();
        _currentHost = null;

        var host = example.Create();
        if (_exampleHost != null)
        {
            _exampleHost.Content = host.Content;
        }

        if (_exampleDescription != null)
        {
            _exampleDescription.Text = example.Description;
        }

        _currentHost = host;
    }

    protected override void OnClosed(EventArgs e)
    {
        _currentHost?.Cleanup?.Invoke();
        _currentHost = null;
        base.OnClosed(e);
    }
}
