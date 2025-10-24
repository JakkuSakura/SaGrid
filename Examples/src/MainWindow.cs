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
    private TabControl? _exampleTabs;
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

        if (_exampleTabs?.Items is IList<object> items && items.Count > 0)
        {
            _exampleTabs.SelectedIndex = 0;
        }
    }

    private Control BuildLayout()
    {
        var layout = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 12
        };

        var title = new TextBlock
        {
            Text = "SaGrid.Advanced Example Gallery",
            FontSize = 22,
            FontWeight = FontWeight.Bold
        };
        layout.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = "Select an example scenario to load the corresponding grid configuration.",
            FontSize = 14,
            Foreground = Brushes.Gray
        };
        Grid.SetRow(subtitle, 1);
        layout.Children.Add(subtitle);

        _exampleTabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _exampleTabs.SelectionChanged += OnTabSelected;

        var tabs = new List<TabItem>();
        foreach (var ex in _examples)
        {
            tabs.Add(new TabItem { Header = ex.Name, Tag = ex });
        }
        _exampleTabs.ItemsSource = tabs;

        _exampleDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var tabsAndDesc = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        tabsAndDesc.Children.Add(_exampleTabs);
        tabsAndDesc.Children.Add(_exampleDescription);

        Grid.SetRow(tabsAndDesc, 2);
        layout.Children.Add(tabsAndDesc);

        _exampleHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = new TextBlock
            {
                Text = "Choose an example above to see it rendered here.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }
        };

        var hostBorder = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = _exampleHost
        };

        Grid.SetRow(hostBorder, 3);
        layout.Children.Add(hostBorder);

        return layout;
    }

    private void OnTabSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_exampleTabs?.SelectedItem is TabItem tab && tab.Tag is IExample example)
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
