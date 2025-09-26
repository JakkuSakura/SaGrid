using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Export;

internal sealed class ExportModule : IAdvancedModule
{
    public string Name => "Export";

    public IReadOnlyList<string> Dependencies { get; } = new[] { "EventService" };

    public void Initialize(AdvancedModuleContext context)
    {
        if (!context.TryResolve<ExportService>(out var exportService))
        {
            exportService = new ExportService();
            context.RegisterService<ExportService>(exportService);
        }

        if (!context.TryResolve<IExportCoordinator>(out _))
        {
            var eventService = context.Resolve<IEventService>();
            context.RegisterService<IExportCoordinator>(new ExportCoordinator(exportService, eventService));
        }
    }
}
