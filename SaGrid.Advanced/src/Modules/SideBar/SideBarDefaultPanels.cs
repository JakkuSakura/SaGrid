using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SaGrid.Advanced.Modules.SideBar;

namespace SaGrid.Modules.SideBar;

public static class SideBarDefaultPanels
{
    public const string ColumnManagerId = "columnManager";
    public const string FilterPanelId = "filterPanel";
    public const string InfoPanelId = "infoPanel";

    public static IReadOnlyList<SideBarPanelDefinition> CreateDefaultPanels()
    {
        return new List<SideBarPanelDefinition>
        {
            new SideBarPanelDefinition(ColumnManagerId, "Columns", CreateColumnManager),
            new SideBarPanelDefinition(FilterPanelId, "Filters", CreateFilterPanel),
            new SideBarPanelDefinition(InfoPanelId, "Info", CreateInfoPanel)
        };
    }

    private static Control CreateColumnManager()
    {
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(12),
            Children =
            {
                new TextBlock
                {
                    Text = "Column manager placeholder",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                new TextBlock
                {
                    Text = "Future enhancement: expose column visibility, pinning, and order controls.",
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }

    private static Control CreateFilterPanel()
    {
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(12),
            Children =
            {
                new TextBlock
                {
                    Text = "Filter tools placeholder",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                new TextBlock
                {
                    Text = "Future enhancement: list active filters and provide quick add/remove controls.",
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }

    private static Control CreateInfoPanel()
    {
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(12),
            Children =
            {
                new TextBlock
                {
                    Text = "Information",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                new TextBlock
                {
                    Text = "Use this area to display custom metrics, documentation links, or status widgets.",
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }
}
