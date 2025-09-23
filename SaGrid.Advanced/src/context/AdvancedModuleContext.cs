using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SaGrid.Advanced.Context;

/// <summary>
/// Simple service registry passed to advanced modules during initialization.
/// Mirrors the role of AG Grid's bean collection but simplified for SaGrid.
/// </summary>
public sealed class AdvancedModuleContext
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    public void RegisterService<TService>(TService instance) where TService : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        _services[typeof(TService)] = instance;
    }

    public bool TryResolve<TService>([NotNullWhen(true)] out TService? service) where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out var value) && value is TService typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    public TService Resolve<TService>() where TService : class
    {
        if (TryResolve<TService>(out var service))
        {
            return service;
        }

        throw new InvalidOperationException($"Service '{typeof(TService).Name}' is not registered.");
    }
}
