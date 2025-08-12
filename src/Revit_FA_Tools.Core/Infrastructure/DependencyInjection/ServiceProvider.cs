using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Revit_FA_Tools.Core.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Default implementation of service provider
    /// </summary>
    internal class ServiceProvider : IServiceProvider
    {
        private readonly List<ServiceDescriptor> _serviceDescriptors;
        private readonly Dictionary<Type, object> _singletonInstances = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _scopedInstances = new Dictionary<Type, object>();

        public ServiceProvider(List<ServiceDescriptor> serviceDescriptors)
        {
            _serviceDescriptors = serviceDescriptors;
        }

        public T GetService<T>() where T : class
        {
            return (T)GetService(typeof(T));
        }

        public object GetService(Type serviceType)
        {
            var descriptor = _serviceDescriptors.FirstOrDefault(x => x.ServiceType == serviceType);
            if (descriptor == null)
            {
                return null;
            }

            return CreateInstance(descriptor);
        }

        public T GetRequiredService<T>() where T : class
        {
            var service = GetService<T>();
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
            }
            return service;
        }

        public IServiceScope CreateScope()
        {
            return new ServiceScope(this);
        }

        private object CreateInstance(ServiceDescriptor descriptor)
        {
            if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                if (_singletonInstances.TryGetValue(descriptor.ServiceType, out var singletonInstance))
                {
                    return singletonInstance;
                }
            }

            if (descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                if (_scopedInstances.TryGetValue(descriptor.ServiceType, out var scopedInstance))
                {
                    return scopedInstance;
                }
            }

            object instance;
            if (descriptor.ImplementationFactory != null)
            {
                instance = descriptor.ImplementationFactory(this);
            }
            else
            {
                instance = CreateInstanceFromType(descriptor.ImplementationType);
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                _singletonInstances[descriptor.ServiceType] = instance;
            }
            else if (descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                _scopedInstances[descriptor.ServiceType] = instance;
            }

            return instance;
        }

        private object CreateInstanceFromType(Type implementationType)
        {
            var constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No constructors found for type {implementationType.Name}");
            }

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var service = GetService(parameterType);
                if (service == null)
                {
                    throw new InvalidOperationException($"Unable to resolve service for type {parameterType.Name}");
                }
                parameterInstances[i] = service;
            }

            return Activator.CreateInstance(implementationType, parameterInstances);
        }

        private class ServiceScope : IServiceScope
        {
            private readonly ServiceProvider _serviceProvider;

            public ServiceScope(ServiceProvider serviceProvider)
            {
                _serviceProvider = new ServiceProvider(serviceProvider._serviceDescriptors);
                foreach (var singleton in serviceProvider._singletonInstances)
                {
                    _serviceProvider._singletonInstances[singleton.Key] = singleton.Value;
                }
            }

            public IServiceProvider ServiceProvider => _serviceProvider;

            public void Dispose()
            {
                foreach (var scoped in _serviceProvider._scopedInstances.Values.OfType<IDisposable>())
                {
                    scoped.Dispose();
                }
                _serviceProvider._scopedInstances.Clear();
            }
        }
    }
}