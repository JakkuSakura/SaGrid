using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Examples.Examples;
using Examples.Models;
using SaGrid;
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

        var layout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Client-side row model with 200 generated rows.",
                    FontWeight = FontWeight.Bold
                },
                new TextBlock
                {
                    Text = "Try sorting, filtering, or selecting rows to explore SaGrid.Advanced basics.",
                    Foreground = Brushes.Gray,
                    FontSize = 13
                },
                new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4),
                    Child = new SaGridComponent<Person>(grid)
                }
            }
        };

        return new ExampleHost(new ScrollViewer { Content = layout });
    }
}
