using System;

namespace Revit_FA_Tools.Core.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Service provider interface for dependency injection
    /// </summary>
    public interface IServiceProvider
    {
        /// <summary>
        /// Gets a service of the specified type
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// Gets a service of the specified type
        /// </summary>
        object GetService(Type serviceType);

        /// <summary>
        /// Gets a required service of the specified type
        /// </summary>
        T GetRequiredService<T>() where T : class;

        /// <summary>
        /// Creates a new scope for scoped services
        /// </summary>
        IServiceScope CreateScope();
    }

    /// <summary>
    /// Represents a scope for scoped services
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        /// <summary>
        /// Gets the service provider for this scope
        /// </summary>
        IServiceProvider ServiceProvider { get; }
    }
}