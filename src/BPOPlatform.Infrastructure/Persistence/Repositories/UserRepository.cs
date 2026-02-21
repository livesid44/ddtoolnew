using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;
using BPOPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BPOPlatform.Infrastructure.Persistence.Repositories;

internal class UserRepository(BPODbContext context) : EfRepository<ApplicationUser>(context), IUserRepository
{
    public Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken ct) =>
        context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

    public Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct) =>
        context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

    public async Task<IReadOnlyList<ApplicationUser>> GetAllActiveAsync(CancellationToken ct) =>
        await context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(string username, string email, CancellationToken ct) =>
        context.Users
            .AnyAsync(u => u.Username == username || u.Email == email, ct);
}
