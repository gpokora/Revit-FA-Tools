using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_FA_Tools.Core.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Service lifetime options
    /// </summary>
    public enum ServiceLifetime
    {
        Transient,
        Scoped,
        Singleton
    }

    /// <summary>
    /// Service descriptor for registration
    /// </summary>
    public class ServiceDescriptor
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public Func<IServiceProvider, object> ImplementationFactory { get; set; }
        public object ImplementationInstance { get; set; }
        public ServiceLifetime Lifetime { get; set; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        public ServiceDescriptor(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationFactory = factory;
            Lifetime = lifetime;
        }

        public ServiceDescriptor(Type serviceType, object instance)
        {
            ServiceType = serviceType;
            ImplementationInstance = instance;
            Lifetime = ServiceLifetime.Singleton;
        }
    }

    /// <summary>
    /// Service collection for registering services
    /// </summary>
    public class ServiceCollection : IServiceCollection
    {
        private readonly List<ServiceDescriptor> _descriptors = new List<ServiceDescriptor>();

        public IServiceCollection AddTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Transient));
            return this;
        }

        public IServiceCollection AddTransient<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), provider => factory(provider), ServiceLifetime.Transient));
            return this;
        }

        public IServiceCollection AddScoped<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Scoped));
            return this;
        }

        public IServiceCollection AddScoped<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), provider => factory(provider), ServiceLifetime.Scoped));
            return this;
        }

        public IServiceCollection AddSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
            return this;
        }

        public IServiceCollection AddSingleton<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), provider => factory(provider), ServiceLifetime.Singleton));
            return this;
        }

        public IServiceCollection AddSingleton<TService>(TService instance)
            where TService : class
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), instance));
            return this;
        }

        public IServiceProvider BuildServiceProvider()
        {
            return new ServiceProvider(_descriptors);
        }
    }

    /// <summary>
    /// Service collection interface
    /// </summary>
    public interface IServiceCollection
    {
        IServiceCollection AddTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        IServiceCollection AddTransient<TService>(Func<IServiceProvider, TService> factory)
            where TService : class;

        IServiceCollection AddScoped<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        IServiceCollection AddScoped<TService>(Func<IServiceProvider, TService> factory)
            where TService : class;

        IServiceCollection AddSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        IServiceCollection AddSingleton<TService>(Func<IServiceProvider, TService> factory)
            where TService : class;

        IServiceCollection AddSingleton<TService>(TService instance)
            where TService : class;

        IServiceProvider BuildServiceProvider();
    }
}