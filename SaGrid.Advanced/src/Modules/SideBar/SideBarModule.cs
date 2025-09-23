using System;
using System.Linq;
using SaGrid.Advanced.Modules;
using SaGrid.Advanced.Modules.SideBar;

namespace SaGrid.Modules.SideBar;

internal sealed class SideBarModule : IAdvancedModule
{
    public string Name => "SideBar";

    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    public void Initialize(AdvancedModuleContext context)
    {
        // Register the side bar service so UI layers can retrieve it later.
        if (!context.TryResolve<SideBarService>(out var service))
        {
            service = new SideBarService();
            context.RegisterService(service);
        }

        if (!service.GetPanels().Any())
        {
            service.SetPanels(SideBarDefaultPanels.CreateDefaultPanels());
        }
    }
}
