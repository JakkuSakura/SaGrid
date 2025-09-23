using System.Collections.Generic;

namespace SaGrid.Advanced.Modules;

/// <summary>
/// Describes an advanced module that can extend SaGrid.Advanced capabilities.
/// Inspired by AG Grid's module architecture.
/// </summary>
public interface IAdvancedModule
{
    string Name { get; }

    IReadOnlyList<string> Dependencies { get; }

    void Initialize(AdvancedModuleContext context);
}
