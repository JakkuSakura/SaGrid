using System;
using System.Collections.Concurrent;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Editing;

public sealed class CellEditorRegistry : ICellEditorRegistry
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    public ICellEditorService<TData> GetOrCreate<TData>()
    {
        var service = (CellEditorService<TData>)_services.GetOrAdd(typeof(TData), _ => new CellEditorService<TData>());
        return service;
    }
}
