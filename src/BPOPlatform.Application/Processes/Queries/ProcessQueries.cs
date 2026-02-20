using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Processes.Queries;

// ── Get All Processes ─────────────────────────────────────────────────────────

public record GetAllProcessesQuery(string? Department = null) : IRequest<IReadOnlyList<ProcessSummaryDto>>;

public class GetAllProcessesQueryHandler(IProcessRepository repo)
    : IRequestHandler<GetAllProcessesQuery, IReadOnlyList<ProcessSummaryDto>>
{
    public async Task<IReadOnlyList<ProcessSummaryDto>> Handle(GetAllProcessesQuery request, CancellationToken ct)
    {
        var processes = string.IsNullOrWhiteSpace(request.Department)
            ? await repo.GetAllAsync(ct)
            : await repo.GetByDepartmentAsync(request.Department, ct);

        return processes
            .Select(p => new ProcessSummaryDto(p.Id, p.Name, p.Department, p.Status, p.AutomationScore))
            .ToList();
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
