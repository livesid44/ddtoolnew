using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Application.Processes.DTOs;

public record ProcessDto(
    Guid Id,
    string Name,
    string Description,
    string Department,
    ProcessStatus Status,
    double AutomationScore,
    double ComplianceScore,
    string OwnerId,
    DateTime CreatedAt,
    int ArtifactCount
);

public record ProcessSummaryDto(
    Guid Id,
    string Name,
    string Department,
    ProcessStatus Status,
    double AutomationScore
);

public record ArtifactDto(
    Guid Id,
    Guid ProcessId,
    string FileName,
    ArtifactType ArtifactType,
    long FileSizeBytes,
    bool IsAnalyzed,
    double? ConfidenceScore,
    DateTime CreatedAt
);
