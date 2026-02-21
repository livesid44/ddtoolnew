using BPOPlatform.Application.Documents.DTOs;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace BPOPlatform.Application.Tickets.Commands;

// ── Create External Ticket ────────────────────────────────────────────────────

/// <summary>
/// Creates a ticket in an external ticketing system (ServiceNow, Jira, Azure DevOps)
/// via a Power Automate HTTP-triggered flow.
/// When Power Automate is not configured, a no-op service returns a placeholder ticket.
/// </summary>
public record CreateTicketCommand(
    Guid ProcessId,
    string Title,
    string Description,
    string Priority = "Medium"
) : IRequest<TicketDto>;

public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    private static readonly HashSet<string> ValidPriorities = ["Low", "Medium", "High", "Critical"];

    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.");
    }
}

public class CreateTicketCommandHandler(
    IProcessRepository processRepo,
    IExternalTicketingService ticketingService)
    : IRequestHandler<CreateTicketCommand, TicketDto>
{
    public async Task<TicketDto> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        // Verify the process exists
        _ = await processRepo.GetByIdAsync(request.ProcessId, ct)
            ?? throw new KeyNotFoundException($"Process {request.ProcessId} not found.");

        var ticket = await ticketingService.CreateTicketAsync(
            request.Title,
            request.Description,
            request.ProcessId.ToString(),
            request.Priority,
            ct);

        return new TicketDto(
            ticket.TicketId,
            ticket.Url,
            ticket.Status,
            request.ProcessId.ToString());
    }
}
