using SaGrid.Advanced.Modules.SideBar;

namespace SaGrid.Advanced.Modules.Filters;

internal static class FilterPanelDefinition
{
    public const string PanelId = "filterPanel";

    public static SideBarPanelDefinition CreatePanel<TData>(SaGrid<TData> grid, FilterService filterService)
    {
        return new SideBarPanelDefinition(
            PanelId,
            "Filters",
            () => new FilterPanelView<TData>(grid, filterService),
            SideBarIcons.Filters
        );
    }
}
