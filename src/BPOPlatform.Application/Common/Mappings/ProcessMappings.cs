using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Entities;

namespace BPOPlatform.Application.Processes.DTOs;

internal static class ProcessMappingExtensions
{
    internal static ProcessDto ToDto(this Process p) => new(
        p.Id,
        p.Name,
        p.Description,
        p.Department,
        p.Status,
        p.AutomationScore,
        p.ComplianceScore,
        p.OwnerId,
        p.CreatedAt,
        p.Artifacts.Count
    );

    internal static WorkflowStepDto ToDto(this WorkflowStep ws) => new(
        ws.Id,
        ws.ProcessId,
        ws.StepOrder,
        ws.StepName,
        ws.RequiredStatus,
        ws.IsCompleted,
        ws.CompletedAt,
        ws.CompletedBy,
        ws.Notes
    );

    internal static ArtifactDto ToDto(this ProcessArtifact a) => new(
        a.Id,
        a.ProcessId,
        a.FileName,
        a.ArtifactType,
        a.FileSizeBytes,
        a.IsAnalyzed,
        a.ConfidenceScore,
        a.CreatedAt
    );
}
