using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Filters;

public sealed class FilterModule : IAdvancedModule
{
    public string Name => "Filters";

    public IReadOnlyList<string> Dependencies { get; } = new[]
    {
        "EventService",
        "SideBar"
    };

    public void Initialize(AdvancedModuleContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!context.TryResolve<IFilterService>(out _))
        {
            var eventService = context.Resolve<IEventService>();
            context.RegisterService<IFilterService>(new FilterService(eventService));
        }
    }
}
