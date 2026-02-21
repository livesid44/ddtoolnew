using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
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

    public async Task<(IReadOnlyList<Process> Items, int TotalCount)> GetPagedAsync(
        string? department = null,
        ProcessStatus? status = null,
        string? ownerId = null,
        string? sortBy = null,
        bool descending = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = Db.Processes
            .Include(p => p.Artifacts)
            .Include(p => p.WorkflowSteps)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(p => p.Department == department);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(ownerId))
            query = query.Where(p => p.OwnerId == ownerId);

        var totalCount = await query.CountAsync(ct);

        query = sortBy?.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "department" => descending ? query.OrderByDescending(p => p.Department) : query.OrderBy(p => p.Department),
            "status" => descending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "automationscore" => descending ? query.OrderByDescending(p => p.AutomationScore) : query.OrderBy(p => p.AutomationScore),
            _ => descending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
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

internal class KanbanCardRepository(BPODbContext db) : EfRepository<KanbanCard>(db), IKanbanCardRepository
{
    public async Task<IReadOnlyList<KanbanCard>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
        => await Db.KanbanCards
            .Where(k => k.ProcessId == processId)
            .OrderBy(k => k.Column)
            .ThenBy(k => k.Position)
            .AsNoTracking()
            .ToListAsync(ct);
}

/// <summary>Unit of Work delegates to EF Core's SaveChangesAsync.</summary>
internal class UnitOfWork(BPODbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
