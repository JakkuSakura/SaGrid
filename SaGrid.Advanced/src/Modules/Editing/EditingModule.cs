using System;
using System.Collections.Generic;
using SaGrid.Advanced.Context;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Editing;

public sealed class EditingModule : IAdvancedModule
{
    public string Name => "Editing";

    public IReadOnlyList<string> Dependencies { get; } = new[]
    {
        "EventService"
    };

    public void Initialize(AdvancedModuleContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!context.TryResolve<ICellEditorRegistry>(out _))
        {
            context.RegisterService<ICellEditorRegistry>(new CellEditorRegistry());
        }
    }
}
