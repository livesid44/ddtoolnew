using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Interfaces;

/// <summary>
/// Generic repository abstraction. Infrastructure layer provides EF Core implementation.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Process-specific repository with domain queries.
/// </summary>
public interface IProcessRepository : IRepository<Process>
{
    Task<IReadOnlyList<Process>> GetByDepartmentAsync(string department, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Process>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, filtered and sorted slice of processes.
    /// </summary>
    Task<(IReadOnlyList<Process> Items, int TotalCount)> GetPagedAsync(
        string? department = null,
        ProcessStatus? status = null,
        string? ownerId = null,
        string? sortBy = null,
        bool descending = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Artifact-specific repository.
/// </summary>
public interface IArtifactRepository : IRepository<ProcessArtifact>
{
    Task<IReadOnlyList<ProcessArtifact>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
}

/// <summary>
/// WorkflowStep-specific repository.
/// </summary>
public interface IWorkflowStepRepository : IRepository<WorkflowStep>
{
    Task<IReadOnlyList<WorkflowStep>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
}

/// <summary>
/// KanbanCard-specific repository.
/// </summary>
public interface IKanbanCardRepository : IRepository<KanbanCard>
{
    Task<IReadOnlyList<KanbanCard>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of Work: saves all pending changes atomically.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
