using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPOPlatform.Infrastructure.Persistence.Repositories;

internal class IntakeRepository(BPODbContext db)
    : EfRepository<IntakeRequest>(db), IIntakeRepository
{
    public async Task<IReadOnlyList<IntakeRequest>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => await Db.IntakeRequests
            .Include(r => r.Artifacts)
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    // Override to eager-load artifacts
    public override async Task<IntakeRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Db.IntakeRequests
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
