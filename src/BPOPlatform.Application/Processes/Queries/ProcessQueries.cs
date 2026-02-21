using BPOPlatform.Application.Common.DTOs;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Processes.Queries;

// ── Get All Processes (paginated, filtered, sorted) ───────────────────────────

public record GetAllProcessesQuery(
    string? Department = null,
    ProcessStatus? Status = null,
    string? OwnerId = null,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<ProcessSummaryDto>>;

public class GetAllProcessesQueryHandler(IProcessRepository repo)
    : IRequestHandler<GetAllProcessesQuery, PagedResult<ProcessSummaryDto>>
{
    public async Task<PagedResult<ProcessSummaryDto>> Handle(GetAllProcessesQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await repo.GetPagedAsync(
            request.Department,
            request.Status,
            request.OwnerId,
            request.SortBy,
            request.Descending,
            request.Page,
            request.PageSize,
            ct);

        var dtos = items
            .Select(p => new ProcessSummaryDto(p.Id, p.Name, p.Department, p.Status, p.AutomationScore))
            .ToList();

        return new PagedResult<ProcessSummaryDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ── Get Process By Id ─────────────────────────────────────────────────────────

public record GetProcessByIdQuery(Guid Id) : IRequest<ProcessDto>;

public class GetProcessByIdQueryHandler(IProcessRepository repo)
    : IRequestHandler<GetProcessByIdQuery, ProcessDto>
{
    public async Task<ProcessDto> Handle(GetProcessByIdQuery request, CancellationToken ct)
    {
        var process = await repo.GetByIdAsync(request.Id, ct)
                      ?? throw new KeyNotFoundException($"Process {request.Id} not found.");
        return process.ToDto();
    }
}

// ── Get Workflow Steps for a Process ─────────────────────────────────────────

public record GetWorkflowStepsQuery(Guid ProcessId) : IRequest<IReadOnlyList<WorkflowStepDto>>;

public class GetWorkflowStepsQueryHandler(IWorkflowStepRepository repo)
    : IRequestHandler<GetWorkflowStepsQuery, IReadOnlyList<WorkflowStepDto>>
{
    public async Task<IReadOnlyList<WorkflowStepDto>> Handle(GetWorkflowStepsQuery request, CancellationToken ct)
    {
        var steps = await repo.GetByProcessIdAsync(request.ProcessId, ct);
        return steps.OrderBy(s => s.StepOrder).Select(s => s.ToDto()).ToList();
    }
}
