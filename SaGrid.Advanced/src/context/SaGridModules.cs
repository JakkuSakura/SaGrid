using SaGrid.Advanced.Context;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Advanced.Modules.StatusBar;
using SaGrid.Advanced.Modules.Export;
using SaGrid.Advanced.Selection;
using SaGrid.Advanced.Modules.Sorting;

namespace SaGrid.Advanced.Context;

internal static class SaGridModules
{
    private static bool _initialized;
    private static readonly object SyncRoot = new();

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            ModuleRegistry.RegisterModules(new IAdvancedModule[]
            {
                new SideBarModule(),
                new StatusBarModule(),
                new SortingEnhancementsModule(),
                new CellSelectionModule(),
                new ExportModule()
            });

            _initialized = true;
        }
    }
}
