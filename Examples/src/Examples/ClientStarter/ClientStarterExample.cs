using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Examples.Models;
using SaGrid;
using SaGrid.Advanced;
using SaGrid.Core;

namespace Examples.Examples.ClientStarter;

internal sealed class ClientStarterExample : IExample
{
    public string Name => "Client-Side Starter";
    public string Description => "Lightweight client row model showcasing basic sorting, filtering, and selection.";

    public ExampleHost Create()
    {
        var data = ExampleData.GenerateSmallDataset(200);
        var columns = ExampleData.CreateDefaultColumns();

        var options = new TableOptions<Person>
        {
            Data = data,
            Columns = columns,
            EnableSorting = true,
            EnableColumnFilters = true,
            EnableRowSelection = true,
            EnableCellSelection = true,
            EnableGlobalFilter = true
        };

        var grid = new SaGrid<Person>(options);
        grid.SetHeaderRenderer("department", _ => "Department");

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 8
        };

        var title = new TextBlock
        {
            Text = "Client-side row model with 200 generated rows.",
            FontWeight = FontWeight.Bold,
            FontSize = 16
        };
        layout.Children.Add(title);

        var instructions = new TextBlock
        {
            Text = "Try sorting, filtering, or selecting rows to explore SaGrid.Advanced basics.",
            Foreground = Brushes.Gray,
            FontSize = 13
        };
        Grid.SetRow(instructions, 1);
        layout.Children.Add(instructions);

        var capabilityPanel = new WrapPanel();
        capabilityPanel.Children.Add(CreateCapabilityChip("Sorting"));
        capabilityPanel.Children.Add(CreateCapabilityChip("Column Filters"));
        capabilityPanel.Children.Add(CreateCapabilityChip("Row Selection"));
        capabilityPanel.Children.Add(CreateCapabilityChip("Cell Selection"));
        capabilityPanel.Children.Add(CreateCapabilityChip("Quick Search"));
        Grid.SetRow(capabilityPanel, 2);
        layout.Children.Add(capabilityPanel);

        var gridComponent = grid.Component;
        gridComponent.HorizontalAlignment = HorizontalAlignment.Stretch;
        gridComponent.VerticalAlignment = VerticalAlignment.Stretch;
        gridComponent.MinHeight = 360;

        var gridHost = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Background = Brushes.White,
            Child = gridComponent
        };
        Grid.SetRow(gridHost, 3);
        layout.Children.Add(gridHost);

        return new ExampleHost(layout);
    }

    private static Control CreateCapabilityChip(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFE8F1")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FFB4D6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#8A2C7C"))
            }
        };
    }
}
