using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SaGrid.Advanced.Context;

public interface IBeanContext
{
    TService Resolve<TService>() where TService : class;
    IReadOnlyList<TService> ResolveAll<TService>() where TService : class;
}

internal sealed class BeanCollection
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Type, List<BeanDefinition>> _definitions = new();

    public void RegisterInstance(Type serviceType, object instance, bool replaceExisting)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (instance == null) throw new ArgumentNullException(nameof(instance));

        var definition = BeanDefinition.CreateSingleton(serviceType, _ => instance, instance);
        AddDefinition(serviceType, definition, replaceExisting);
    }

    public void RegisterSingleton(Type serviceType, Func<IBeanContext, object> factory, bool replaceExisting)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var definition = BeanDefinition.CreateSingleton(serviceType, factory, null);
        AddDefinition(serviceType, definition, replaceExisting);
    }

    public void RegisterTransient(Type serviceType, Func<IBeanContext, object> factory)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var definition = BeanDefinition.CreateTransient(serviceType, factory);
        AddDefinition(serviceType, definition, replaceExisting: false);
    }

    public bool TryResolve<TService>(IBeanContext context, out TService? service) where TService : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!TryGetDefinitions(typeof(TService), out var definitions) || definitions.Count == 0)
        {
            service = null;
            return false;
        }

        service = (TService)definitions[0].GetInstance(context);
        return service != null;
    }

    public TService Resolve<TService>(IBeanContext context) where TService : class
    {
        if (!TryResolve(context, out TService? service))
        {
            throw new InvalidOperationException($"Service '{typeof(TService).Name}' is not registered.");
        }

        return service;
    }

    public IReadOnlyList<TService> ResolveAll<TService>(IBeanContext context) where TService : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (!TryGetDefinitions(typeof(TService), out var definitions) || definitions.Count == 0)
        {
            return Array.Empty<TService>();
        }

        var list = new List<TService>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (definition.GetInstance(context) is TService instance)
            {
                list.Add(instance);
            }
        }

        return new ReadOnlyCollection<TService>(list);
    }

    private void AddDefinition(Type serviceType, BeanDefinition definition, bool replaceExisting)
    {
        lock (_syncRoot)
        {
            if (replaceExisting || !_definitions.TryGetValue(serviceType, out var definitions))
            {
                _definitions[serviceType] = new List<BeanDefinition> { definition };
            }
            else
            {
                definitions.Add(definition);
            }
        }
    }

    private bool TryGetDefinitions(Type serviceType, out List<BeanDefinition> definitions)
    {
        lock (_syncRoot)
        {
            if (_definitions.TryGetValue(serviceType, out var existing))
            {
                definitions = new List<BeanDefinition>(existing);
                return true;
            }
        }

        definitions = new List<BeanDefinition>();
        return false;
    }

    private sealed class BeanDefinition
    {
        private readonly Func<IBeanContext, object> _factory;
        private readonly BeanLifetime _lifetime;
        private readonly object _instanceLock = new();
        private object? _singletonInstance;

        private BeanDefinition(Type serviceType, BeanLifetime lifetime, Func<IBeanContext, object> factory, object? instance)
        {
            ServiceType = serviceType;
            _lifetime = lifetime;
            _factory = factory;
            _singletonInstance = instance;
        }

        public Type ServiceType { get; }

        public static BeanDefinition CreateSingleton(Type serviceType, Func<IBeanContext, object> factory, object? instance)
        {
            return new BeanDefinition(serviceType, BeanLifetime.Singleton, factory, instance);
        }

        public static BeanDefinition CreateTransient(Type serviceType, Func<IBeanContext, object> factory)
        {
            return new BeanDefinition(serviceType, BeanLifetime.Transient, factory, null);
        }

        public object GetInstance(IBeanContext context)
        {
            if (_lifetime == BeanLifetime.Singleton)
            {
                var existing = Volatile.Read(ref _singletonInstance);
                if (existing != null)
                {
                    return existing;
                }

                lock (_instanceLock)
                {
                    existing = _singletonInstance;
                    if (existing == null)
                    {
                        existing = _factory(context) ?? throw new InvalidOperationException($"Factory for '{ServiceType.Name}' returned null.");
                        _singletonInstance = existing;
                    }
                }

                return existing;
            }

            return _factory(context) ?? throw new InvalidOperationException($"Factory for '{ServiceType.Name}' returned null.");
        }
    }

    private enum BeanLifetime
    {
        Singleton,
        Transient
    }
}
