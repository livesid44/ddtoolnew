using BPOPlatform.Application.Intake.DTOs;
using BPOPlatform.Domain.Entities;

namespace BPOPlatform.Application.Intake.Mappings;

internal static class IntakeMappings
{
    public static IntakeDto ToDto(this IntakeRequest r) => new(
        r.Id,
        r.Title,
        r.Description,
        r.Department,
        r.Location,
        r.BusinessUnit,
        r.ContactEmail,
        r.QueuePriority,
        r.OwnerId,
        r.Status.ToString(),
        r.ChatHistoryJson,
        r.PromotedProcessId,
        r.Artifacts.Select(a => a.ToDto()).ToList(),
        r.CreatedAt,
        r.UpdatedAt);

    public static IntakeArtifactDto ToDto(this IntakeArtifact a) => new(
        a.Id,
        a.IntakeRequestId,
        a.FileName,
        a.ArtifactType.ToString(),
        a.FileSizeBytes,
        a.ExtractedText is not null);
}
