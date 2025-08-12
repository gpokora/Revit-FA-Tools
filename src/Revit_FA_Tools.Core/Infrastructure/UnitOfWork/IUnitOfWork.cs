using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Revit_FA_Tools.Core.Infrastructure.UnitOfWork
{
    /// <summary>
    /// Unit of Work pattern for managing transactions
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Begins a new transaction
        /// </summary>
        void BeginTransaction();

        /// <summary>
        /// Commits the current transaction
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rolls back the current transaction
        /// </summary>
        void Rollback();

        /// <summary>
        /// Saves changes without committing the transaction
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Gets whether a transaction is active
        /// </summary>
        bool HasActiveTransaction { get; }

        /// <summary>
        /// Registers an entity for insertion
        /// </summary>
        void RegisterNew<T>(T entity) where T : class;

        /// <summary>
        /// Registers an entity for update
        /// </summary>
        void RegisterModified<T>(T entity) where T : class;

        /// <summary>
        /// Registers an entity for deletion
        /// </summary>
        void RegisterDeleted<T>(T entity) where T : class;

        /// <summary>
        /// Gets the repository for a specific entity type
        /// </summary>
        IRepository<T> GetRepository<T>() where T : class;
    }

    /// <summary>
    /// Repository interface for data access
    /// </summary>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Gets an entity by ID
        /// </summary>
        Task<T> GetByIdAsync(object id);

        /// <summary>
        /// Gets all entities
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Adds a new entity
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>
        /// Updates an existing entity
        /// </summary>
        Task UpdateAsync(T entity);

        /// <summary>
        /// Deletes an entity
        /// </summary>
        Task DeleteAsync(T entity);

        /// <summary>
        /// Checks if an entity exists
        /// </summary>
        Task<bool> ExistsAsync(object id);
    }
}