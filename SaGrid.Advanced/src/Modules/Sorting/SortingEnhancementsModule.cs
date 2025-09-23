using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;

namespace SaGrid.Advanced.Modules.Sorting;

internal sealed class SortingEnhancementsModule : IAdvancedModule
{
    public string Name => "SortingEnhancements";

    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    public void Initialize(AdvancedModuleContext context)
    {
        if (!context.TryResolve<SortingEnhancementsService>(out _))
        {
            context.RegisterService(new SortingEnhancementsService());
        }
    }
}
