using System;
using SaGrid.Advanced.Context;

namespace SaGrid.Advanced.Modules.StatusBar;

internal sealed class StatusBarModule : IAdvancedModule
{
    public string Name => "StatusBar";

    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    public void Initialize(AdvancedModuleContext context)
    {
        // Register the status bar service so UI layers can retrieve it later.
        if (!context.TryResolve<StatusBarService>(out _))
        {
            context.RegisterService(new StatusBarService());
        }
    }
}