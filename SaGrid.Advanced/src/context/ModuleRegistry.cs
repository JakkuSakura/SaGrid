using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SaGrid.Advanced.Context;

/// <summary>
/// Minimal module registry mirroring AG Grid's module system. Allows modules to register services and expose APIs.
/// </summary>
public static class ModuleRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<string, IAdvancedModule> RegisteredModules = new(StringComparer.OrdinalIgnoreCase);
    private static readonly AdvancedModuleContext SharedContext = new();

    public static AdvancedModuleContext Context => SharedContext;

    public static void RegisterModules(IEnumerable<IAdvancedModule> modules)
    {
        if (modules == null) throw new ArgumentNullException(nameof(modules));

        lock (SyncRoot)
        {
            foreach (var module in modules)
            {
                RegisterModuleInternal(module);
            }
        }
    }

    public static bool IsModuleRegistered(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) throw new ArgumentException("Module name is required", nameof(moduleName));
        return RegisteredModules.ContainsKey(moduleName);
    }

    private static void RegisterModuleInternal(IAdvancedModule module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));
        if (string.IsNullOrWhiteSpace(module.Name))
        {
            throw new InvalidOperationException("Modules must provide a non-empty Name.");
        }

        if (RegisteredModules.ContainsKey(module.Name))
        {
            return; // already initialized
        }

        foreach (var dependency in module.Dependencies)
        {
            if (!RegisteredModules.ContainsKey(dependency))
            {
                throw new InvalidOperationException($"Module '{module.Name}' depends on '{dependency}' which has not been registered.");
            }
        }

        module.Initialize(SharedContext);
        RegisteredModules[module.Name] = module;
    }
}
