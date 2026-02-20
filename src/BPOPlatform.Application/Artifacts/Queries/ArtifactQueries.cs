using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Artifacts.Queries;

// ── Get Artifacts for a Process ────────────────────────────────────────────────

public record GetArtifactsByProcessQuery(Guid ProcessId) : IRequest<IReadOnlyList<ArtifactDto>>;

public class GetArtifactsByProcessQueryHandler(IArtifactRepository repo)
    : IRequestHandler<GetArtifactsByProcessQuery, IReadOnlyList<ArtifactDto>>
{
    public async Task<IReadOnlyList<ArtifactDto>> Handle(GetArtifactsByProcessQuery request, CancellationToken ct)
    {
        var artifacts = await repo.GetByProcessIdAsync(request.ProcessId, ct);
        return artifacts.Select(a => a.ToDto()).ToList();
    }
}
