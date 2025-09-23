using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;

namespace SaGrid.Advanced.Modules.Export;

internal sealed class ExportModule : IAdvancedModule
{
    public string Name => "Export";

    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    public void Initialize(AdvancedModuleContext context)
    {
        if (!context.TryResolve<ExportService>(out _))
        {
            context.RegisterService(new ExportService());
        }
    }
}
