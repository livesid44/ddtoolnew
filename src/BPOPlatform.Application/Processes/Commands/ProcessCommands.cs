using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace BPOPlatform.Application.Processes.Commands;

// ── Create Process ────────────────────────────────────────────────────────────

public record CreateProcessCommand(
    string Name,
    string Description,
    string Department,
    string OwnerId
) : IRequest<ProcessDto>;

public class CreateProcessCommandValidator : AbstractValidator<CreateProcessCommand>
{
    public CreateProcessCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}

public class CreateProcessCommandHandler(IProcessRepository repo, IUnitOfWork uow)
    : IRequestHandler<CreateProcessCommand, ProcessDto>
{
    public async Task<ProcessDto> Handle(CreateProcessCommand request, CancellationToken ct)
    {
        var process = Process.Create(request.Name, request.Description, request.Department, request.OwnerId);
        await repo.AddAsync(process, ct);
        await uow.SaveChangesAsync(ct);

        return process.ToDto();
    }
}

// ── Update Process Details ────────────────────────────────────────────────────

public record UpdateProcessCommand(
    Guid Id,
    string Name,
    string Description,
    string Department
) : IRequest<ProcessDto>;

public class UpdateProcessCommandValidator : AbstractValidator<UpdateProcessCommand>
{
    public UpdateProcessCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
    }
}

public class UpdateProcessCommandHandler(IProcessRepository repo, IUnitOfWork uow)
    : IRequestHandler<UpdateProcessCommand, ProcessDto>
{
    public async Task<ProcessDto> Handle(UpdateProcessCommand request, CancellationToken ct)
    {
        var process = await repo.GetByIdAsync(request.Id, ct)
                      ?? throw new KeyNotFoundException($"Process {request.Id} not found.");

        process.UpdateDetails(request.Name, request.Description, request.Department);
        await uow.SaveChangesAsync(ct);
        return process.ToDto();
    }
}

// ── Delete Process ────────────────────────────────────────────────────────────

public record DeleteProcessCommand(Guid Id) : IRequest;

public class DeleteProcessCommandHandler(IProcessRepository repo, IUnitOfWork uow)
    : IRequestHandler<DeleteProcessCommand>
{
    public async Task Handle(DeleteProcessCommand request, CancellationToken ct)
    {
        var exists = await repo.GetByIdAsync(request.Id, ct)
                     ?? throw new KeyNotFoundException($"Process {request.Id} not found.");

        await repo.DeleteAsync(exists.Id, ct);
        await uow.SaveChangesAsync(ct);
    }
}

// ── Advance Process Status ────────────────────────────────────────────────────

public record AdvanceProcessStatusCommand(Guid ProcessId, string NewStatus) : IRequest<ProcessDto>;

public class AdvanceProcessStatusCommandHandler(IProcessRepository repo, IUnitOfWork uow)
    : IRequestHandler<AdvanceProcessStatusCommand, ProcessDto>
{
    public async Task<ProcessDto> Handle(AdvanceProcessStatusCommand request, CancellationToken ct)
    {
        var process = await repo.GetByIdAsync(request.ProcessId, ct)
                      ?? throw new KeyNotFoundException($"Process {request.ProcessId} not found.");

        if (!Enum.TryParse<Domain.Enums.ProcessStatus>(request.NewStatus, true, out var status))
            throw new ArgumentException($"Invalid status: {request.NewStatus}");

        process.AdvanceStatus(status);
        await uow.SaveChangesAsync(ct);
        return process.ToDto();
    }
}
