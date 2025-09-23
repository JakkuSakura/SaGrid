using SaGrid.Advanced.Modules;
using SaGrid.Advanced.Modules.SideBar;
using SaGrid.Modules.Export;
using SaGrid.Modules.Selection;
using SaGrid.Modules.SideBar;
using SaGrid.Modules.Sorting;

namespace SaGrid;

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
                new SortingEnhancementsModule(),
                new CellSelectionModule(),
                new ExportModule()
            });

            _initialized = true;
        }
    }
}
