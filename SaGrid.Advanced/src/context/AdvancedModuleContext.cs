using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SaGrid.Advanced.Context;

/// <summary>
/// Service registry handed to advanced modules. Provides a small dependency injection
/// container inspired by AG Grid's BeanCollection so modules can register and resolve
/// shared services with singleton or transient lifetimes.
/// </summary>
public sealed class AdvancedModuleContext : IBeanContext
{
    private readonly BeanCollection _beans = new();

    public void RegisterService<TService>(TService instance) where TService : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        _beans.RegisterInstance(typeof(TService), instance, replaceExisting: true);
    }

    public void RegisterSingleton<TService>(Func<IBeanContext, TService> factory, bool replaceExisting = false)
        where TService : class
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _beans.RegisterSingleton(typeof(TService), ctx => factory(ctx), replaceExisting);
    }

    public void RegisterSingleton<TService>(Func<TService> factory, bool replaceExisting = false)
        where TService : class
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _beans.RegisterSingleton(typeof(TService), _ => factory(), replaceExisting);
    }

    public void RegisterTransient<TService>(Func<IBeanContext, TService> factory) where TService : class
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _beans.RegisterTransient(typeof(TService), ctx => factory(ctx));
    }

    public void RegisterTransient<TService>(Func<TService> factory) where TService : class
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _beans.RegisterTransient(typeof(TService), _ => factory());
    }

    public bool TryResolve<TService>([NotNullWhen(true)] out TService? service) where TService : class
    {
        return _beans.TryResolve(this, out service);
    }

    public TService Resolve<TService>() where TService : class
    {
        return _beans.Resolve<TService>(this);
    }

    public IReadOnlyList<TService> ResolveAll<TService>() where TService : class
    {
        return _beans.ResolveAll<TService>(this);
    }
}
