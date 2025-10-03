using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Abstractions.Errors;
using Tracker.Domain.Abstractions;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public abstract class EfRepositoryBase<TEntity>(TrackerDbContext db) : IRepository<TEntity> where TEntity : class
    {
        protected readonly TrackerDbContext _db = db;
        protected DbSet<TEntity> Set => _db.Set<TEntity>();
        public virtual Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Set.FindAsync([id], ct).AsTask();
        public virtual Task AddAsync(TEntity entity, CancellationToken ct = default)
        => Set.AddAsync(entity, ct).AsTask();
        public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        => Set.AddRangeAsync(entities, ct);
        public virtual Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            Set.Update(entity);
            return Task.CompletedTask;
        }
        public virtual Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            Set.Remove(entity);
            return Task.CompletedTask;
        }
        protected static Exception WrapConcurrency(DbUpdateConcurrencyException ex)
        => new ConcurrencyException("Conflicto de concurrencia (RowVersion). Vuelva a intentar con la última versión.", ex);
    }
}
