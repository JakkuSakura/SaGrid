using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Aggregation;

public sealed class AggregationModule : IAdvancedModule
{
    public string Name => "Aggregation";

    public IReadOnlyList<string> Dependencies { get; } = new[] { "EventService", "RowGrouping" };

    public void Initialize(AdvancedModuleContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!context.TryResolve<IAggregationService>(out _))
        {
            context.RegisterService<IAggregationService>(new AggregationService());
        }
    }
}
