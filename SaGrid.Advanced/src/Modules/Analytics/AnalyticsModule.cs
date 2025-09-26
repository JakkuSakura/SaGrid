using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Analytics;

internal sealed class AnalyticsModule : IAdvancedModule
{
    public string Name => "Analytics";

    public IReadOnlyList<string> Dependencies { get; } = new[] { "EventService", "Aggregation" };

    public void Initialize(AdvancedModuleContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!context.TryResolve<IChartIntegrationService>(out _))
        {
            var eventService = context.Resolve<IEventService>();
            context.RegisterService<IChartIntegrationService>(new ChartIntegrationService(eventService));
        }
    }
}
