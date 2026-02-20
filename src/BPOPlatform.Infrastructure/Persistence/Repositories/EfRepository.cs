using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPOPlatform.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic EF Core repository implementation.
/// </summary>
internal class EfRepository<T>(BPODbContext db) : IRepository<T> where T : BaseEntity
{
    protected readonly BPODbContext Db = db;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Db.Set<T>().FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Db.Set<T>().AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Db.Set<T>().AddAsync(entity, ct);

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        Db.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is not null)
            Db.Set<T>().Remove(entity);
    }
}
