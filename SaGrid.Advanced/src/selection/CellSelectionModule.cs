using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;

namespace SaGrid.Advanced.Selection;

internal sealed class CellSelectionModule : IAdvancedModule
{
    public string Name => "CellSelection";

    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    public void Initialize(AdvancedModuleContext context)
    {
        if (!context.TryResolve<CellSelectionService>(out _))
        {
            context.RegisterService(new CellSelectionService());
        }
    }
}
