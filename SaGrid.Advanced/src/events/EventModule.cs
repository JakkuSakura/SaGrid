using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;

namespace SaGrid.Advanced.Events;

internal sealed class EventModule : IAdvancedModule
{
    public string Name => "EventService";

    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    public void Initialize(AdvancedModuleContext context)
    {
        // Register the event service so other modules can use it
        if (!context.TryResolve<IEventService>(out _))
        {
            context.RegisterService<IEventService>(new EventService());
        }
    }
}