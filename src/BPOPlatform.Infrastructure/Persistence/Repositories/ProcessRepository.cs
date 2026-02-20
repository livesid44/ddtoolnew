using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPOPlatform.Infrastructure.Persistence.Repositories;

internal class ProcessRepository(BPODbContext db) : EfRepository<Process>(db), IProcessRepository
{
    public async Task<IReadOnlyList<Process>> GetByDepartmentAsync(string department, CancellationToken ct = default)
        => await Db.Processes
            .Include(p => p.Artifacts)
            .Include(p => p.WorkflowSteps)
            .Where(p => p.Department == department)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Process>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => await Db.Processes
            .Where(p => p.OwnerId == ownerId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await Db.Processes.CountAsync(ct);
}

internal class ArtifactRepository(BPODbContext db) : EfRepository<ProcessArtifact>(db), IArtifactRepository
{
    public async Task<IReadOnlyList<ProcessArtifact>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
        => await Db.Artifacts
            .Where(a => a.ProcessId == processId)
            .AsNoTracking()
            .ToListAsync(ct);
}

internal class WorkflowStepRepository(BPODbContext db) : EfRepository<WorkflowStep>(db), IWorkflowStepRepository
{
    public async Task<IReadOnlyList<WorkflowStep>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
        => await Db.WorkflowSteps
            .Where(ws => ws.ProcessId == processId)
            .OrderBy(ws => ws.StepOrder)
            .AsNoTracking()
            .ToListAsync(ct);
}

/// <summary>Unit of Work delegates to EF Core's SaveChangesAsync.</summary>
internal class UnitOfWork(BPODbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
