using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        var selectorPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6
        };

        _exampleSelector = new ComboBox
        {
            ItemsSource = _examples,
            SelectedIndex = -1,
            MinWidth = 260
        };
        _exampleSelector.SelectionChanged += OnExampleSelected;
        selectorPanel.Children.Add(_exampleSelector);

        _exampleDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        selectorPanel.Children.Add(_exampleDescription);

        Grid.SetRow(selectorPanel, 2);
        layout.Children.Add(selectorPanel);

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

        var hostScrollViewer = new ScrollViewer
        {
            Content = _exampleHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var hostBorder = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = hostScrollViewer
        };

        Grid.SetRow(hostBorder, 3);
        layout.Children.Add(hostBorder);

        return layout;
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
