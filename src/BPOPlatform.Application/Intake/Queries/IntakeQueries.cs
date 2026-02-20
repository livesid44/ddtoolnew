using BPOPlatform.Application.Intake.DTOs;
using BPOPlatform.Application.Intake.Mappings;
using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Intake.Queries;

// ── Get single intake by ID ───────────────────────────────────────────────────

public record GetIntakeByIdQuery(Guid IntakeRequestId) : IRequest<IntakeDto>;

public class GetIntakeByIdQueryHandler(IIntakeRepository repo)
    : IRequestHandler<GetIntakeByIdQuery, IntakeDto>
{
    public async Task<IntakeDto> Handle(GetIntakeByIdQuery request, CancellationToken ct)
    {
        var intake = await repo.GetByIdAsync(request.IntakeRequestId, ct)
                     ?? throw new KeyNotFoundException($"Intake {request.IntakeRequestId} not found.");
        return intake.ToDto();
    }
}

// ── Get my intakes ────────────────────────────────────────────────────────────

public record GetMyIntakesQuery(string OwnerId) : IRequest<IReadOnlyList<IntakeDto>>;

public class GetMyIntakesQueryHandler(IIntakeRepository repo)
    : IRequestHandler<GetMyIntakesQuery, IReadOnlyList<IntakeDto>>
{
    public async Task<IReadOnlyList<IntakeDto>> Handle(GetMyIntakesQuery request, CancellationToken ct)
    {
        var intakes = await repo.GetByOwnerAsync(request.OwnerId, ct);
        return intakes.Select(i => i.ToDto()).ToList();
    }
}
