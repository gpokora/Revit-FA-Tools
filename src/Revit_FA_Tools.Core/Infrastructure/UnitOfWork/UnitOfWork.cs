using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Revit_FA_Tools.Core.Infrastructure.UnitOfWork
{
    /// <summary>
    /// Implementation of Unit of Work pattern
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();
        private readonly List<EntityEntry> _newEntities = new List<EntityEntry>();
        private readonly List<EntityEntry> _modifiedEntities = new List<EntityEntry>();
        private readonly List<EntityEntry> _deletedEntities = new List<EntityEntry>();
        private bool _hasActiveTransaction = false;
        private bool _disposed = false;

        public bool HasActiveTransaction => _hasActiveTransaction;

        public void BeginTransaction()
        {
            if (_hasActiveTransaction)
            {
                throw new InvalidOperationException("A transaction is already active.");
            }

            _hasActiveTransaction = true;
            _newEntities.Clear();
            _modifiedEntities.Clear();
            _deletedEntities.Clear();
        }

        public async Task CommitAsync()
        {
            if (!_hasActiveTransaction)
            {
                throw new InvalidOperationException("No active transaction to commit.");
            }

            try
            {
                // Process all pending changes
                await ProcessPendingChanges();
                
                _hasActiveTransaction = false;
                _newEntities.Clear();
                _modifiedEntities.Clear();
                _deletedEntities.Clear();
            }
            catch
            {
                Rollback();
                throw;
            }
        }

        public void Rollback()
        {
            if (!_hasActiveTransaction)
            {
                return;
            }

            _hasActiveTransaction = false;
            _newEntities.Clear();
            _modifiedEntities.Clear();
            _deletedEntities.Clear();
        }

        public async Task<int> SaveChangesAsync()
        {
            if (!_hasActiveTransaction)
            {
                throw new InvalidOperationException("No active transaction. Call BeginTransaction first.");
            }

            return await ProcessPendingChanges();
        }

        public void RegisterNew<T>(T entity) where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!_hasActiveTransaction)
                BeginTransaction();

            _newEntities.Add(new EntityEntry { Entity = entity, EntityType = typeof(T), State = EntityState.Added });
        }

        public void RegisterModified<T>(T entity) where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!_hasActiveTransaction)
                BeginTransaction();

            // Remove from new entities if it exists there
            var existingNew = _newEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (existingNew != null)
            {
                return; // Already new, no need to mark as modified
            }

            // Remove any existing modified entry and add the new one
            _modifiedEntities.RemoveAll(e => ReferenceEquals(e.Entity, entity));
            _modifiedEntities.Add(new EntityEntry { Entity = entity, EntityType = typeof(T), State = EntityState.Modified });
        }

        public void RegisterDeleted<T>(T entity) where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!_hasActiveTransaction)
                BeginTransaction();

            // Remove from new entities if it exists there
            var existingNew = _newEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (existingNew != null)
            {
                _newEntities.Remove(existingNew);
                return; // Was new, just remove it
            }

            // Remove from modified entities
            _modifiedEntities.RemoveAll(e => ReferenceEquals(e.Entity, entity));

            // Add to deleted
            _deletedEntities.Add(new EntityEntry { Entity = entity, EntityType = typeof(T), State = EntityState.Deleted });
        }

        public IRepository<T> GetRepository<T>() where T : class
        {
            if (_repositories.TryGetValue(typeof(T), out var repository))
            {
                return (IRepository<T>)repository;
            }

            var newRepository = new Repository<T>(this);
            _repositories[typeof(T)] = newRepository;
            return newRepository;
        }

        private async Task<int> ProcessPendingChanges()
        {
            int changesProcessed = 0;

            // Process deletions first
            foreach (var entry in _deletedEntities)
            {
                await ProcessDeletedEntity(entry);
                changesProcessed++;
            }

            // Process modifications
            foreach (var entry in _modifiedEntities)
            {
                await ProcessModifiedEntity(entry);
                changesProcessed++;
            }

            // Process new entities last
            foreach (var entry in _newEntities)
            {
                await ProcessNewEntity(entry);
                changesProcessed++;
            }

            return changesProcessed;
        }

        private async Task ProcessNewEntity(EntityEntry entry)
        {
            // Implementation would depend on the persistence mechanism
            // For now, we'll just mark it as processed
            await Task.FromResult(true);
        }

        private async Task ProcessModifiedEntity(EntityEntry entry)
        {
            // Implementation would depend on the persistence mechanism
            // For now, we'll just mark it as processed
            await Task.FromResult(true);
        }

        private async Task ProcessDeletedEntity(EntityEntry entry)
        {
            // Implementation would depend on the persistence mechanism
            // For now, we'll just mark it as processed
            await Task.FromResult(true);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_hasActiveTransaction)
                {
                    Rollback();
                }

                _repositories.Clear();
                _disposed = true;
            }
        }

        private class EntityEntry
        {
            public object Entity { get; set; }
            public Type EntityType { get; set; }
            public EntityState State { get; set; }
        }

        private enum EntityState
        {
            Added,
            Modified,
            Deleted
        }
    }

    /// <summary>
    /// Generic repository implementation
    /// </summary>
    internal class Repository<T> : IRepository<T> where T : class
    {
        private readonly UnitOfWork _unitOfWork;

        public Repository(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<T> GetByIdAsync(object id)
        {
            // Implementation would depend on the data source
            // For now, return null
            return await Task.FromResult<T>(null);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            // Implementation would depend on the data source
            // For now, return empty list
            return await Task.FromResult(new List<T>());
        }

        public async Task AddAsync(T entity)
        {
            _unitOfWork.RegisterNew(entity);
            await Task.CompletedTask;
        }

        public async Task UpdateAsync(T entity)
        {
            _unitOfWork.RegisterModified(entity);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(T entity)
        {
            _unitOfWork.RegisterDeleted(entity);
            await Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(object id)
        {
            var entity = await GetByIdAsync(id);
            return entity != null;
        }
    }
}