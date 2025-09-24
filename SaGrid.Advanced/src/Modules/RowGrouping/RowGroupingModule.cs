using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.RowGrouping;

/// <summary>
/// Registers the grouping service within the module registry. Mirrors the pattern used by
/// the other advanced modules so any consumer resolving <see cref="IGroupingService"/>
/// receives the shared instance.
/// </summary>
public sealed class RowGroupingModule : IAdvancedModule
{
    public string Name => "RowGrouping";

    public IReadOnlyList<string> Dependencies { get; } = new[] { "EventService" };

    public void Initialize(AdvancedModuleContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!context.TryResolve<IGroupingService>(out _))
        {
            var eventService = context.Resolve<IEventService>();
            context.RegisterService<IGroupingService>(new GroupingService(eventService));
        }
    }
}
