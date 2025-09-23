namespace SaGrid.Advanced.Modules.SideBar;

/// <summary>
/// Placeholder service representing side bar state and actions. Future work will flesh this out with real UI integration.
/// </summary>
public class SideBarService
{
    private bool _visible;
    private string? _activePanelId;

    public bool IsVisible => _visible;

    public string? ActivePanelId => _activePanelId;

    public void SetVisible(bool visible)
    {
        _visible = visible;
    }

    public void SetActivePanel(string? panelId)
    {
        _activePanelId = panelId;
    }
}
