using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace BPOPlatform.Application.Kanban.Commands;

// ── Kanban Card DTO ───────────────────────────────────────────────────────────

public record KanbanCardDto(
    Guid Id,
    Guid ProcessId,
    string Title,
    string Description,
    string Column,
    TaskPriority Priority,
    string AssignedTo,
    int Position,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Create Kanban Card ────────────────────────────────────────────────────────

public record CreateKanbanCardCommand(
    Guid ProcessId,
    string Title,
    string Description,
    string Column,
    TaskPriority Priority,
    string AssignedTo
) : IRequest<KanbanCardDto>;

public class CreateKanbanCardCommandValidator : AbstractValidator<CreateKanbanCardCommand>
{
    public CreateKanbanCardCommandValidator()
    {
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Column).NotEmpty().MaximumLength(50);
    }
}

public class CreateKanbanCardCommandHandler(
    IKanbanCardRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<CreateKanbanCardCommand, KanbanCardDto>
{
    public async Task<KanbanCardDto> Handle(CreateKanbanCardCommand request, CancellationToken ct)
    {
        // Position = count of existing cards in this column + 1
        var existing = await repo.GetByProcessIdAsync(request.ProcessId, ct);
        var position = existing.Count(c => c.Column == request.Column);

        var card = KanbanCard.Create(
            request.ProcessId, request.Title, request.Description,
            request.Column, request.Priority, request.AssignedTo, position);

        await repo.AddAsync(card, ct);
        await uow.SaveChangesAsync(ct);
        return card.ToDto();
    }
}

// ── Update Kanban Card ────────────────────────────────────────────────────────

public record UpdateKanbanCardCommand(
    Guid CardId,
    string Title,
    string Description,
    TaskPriority Priority,
    string AssignedTo
) : IRequest<KanbanCardDto>;

public class UpdateKanbanCardCommandValidator : AbstractValidator<UpdateKanbanCardCommand>
{
    public UpdateKanbanCardCommandValidator()
    {
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateKanbanCardCommandHandler(
    IKanbanCardRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<UpdateKanbanCardCommand, KanbanCardDto>
{
    public async Task<KanbanCardDto> Handle(UpdateKanbanCardCommand request, CancellationToken ct)
    {
        var card = await repo.GetByIdAsync(request.CardId, ct)
                   ?? throw new KeyNotFoundException($"KanbanCard {request.CardId} not found.");

        card.Update(request.Title, request.Description, request.Priority, request.AssignedTo);
        await uow.SaveChangesAsync(ct);
        return card.ToDto();
    }
}

// ── Move Kanban Card ─────────────────────────────────────────────────────────

public record MoveKanbanCardCommand(
    Guid CardId,
    string NewColumn,
    int NewPosition
) : IRequest<KanbanCardDto>;

public class MoveKanbanCardCommandHandler(
    IKanbanCardRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<MoveKanbanCardCommand, KanbanCardDto>
{
    public async Task<KanbanCardDto> Handle(MoveKanbanCardCommand request, CancellationToken ct)
    {
        var card = await repo.GetByIdAsync(request.CardId, ct)
                   ?? throw new KeyNotFoundException($"KanbanCard {request.CardId} not found.");

        card.Move(request.NewColumn, request.NewPosition);
        await uow.SaveChangesAsync(ct);
        return card.ToDto();
    }
}

// ── Delete Kanban Card ────────────────────────────────────────────────────────

public record DeleteKanbanCardCommand(Guid CardId) : IRequest;

public class DeleteKanbanCardCommandHandler(
    IKanbanCardRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<DeleteKanbanCardCommand>
{
    public async Task Handle(DeleteKanbanCardCommand request, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(request.CardId, ct)
            ?? throw new KeyNotFoundException($"KanbanCard {request.CardId} not found.");

        await repo.DeleteAsync(request.CardId, ct);
        await uow.SaveChangesAsync(ct);
    }
}

// ── Mapping helper ────────────────────────────────────────────────────────────

internal static class KanbanMappings
{
    internal static KanbanCardDto ToDto(this KanbanCard c) => new(
        c.Id, c.ProcessId, c.Title, c.Description,
        c.Column, c.Priority, c.AssignedTo, c.Position,
        c.CreatedAt, c.UpdatedAt);
}
