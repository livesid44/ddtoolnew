using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Application.Intake.DTOs;

public record IntakeDto(
    Guid Id,
    string Title,
    string? Description,
    string? Department,
    string? Location,
    string? BusinessUnit,
    string? ContactEmail,
    string QueuePriority,
    string OwnerId,
    string Status,
    string ChatHistoryJson,
    Guid? PromotedProcessId,
    IReadOnlyList<IntakeArtifactDto> Artifacts,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record IntakeArtifactDto(
    Guid Id,
    Guid IntakeRequestId,
    string FileName,
    string ArtifactType,
    long FileSizeBytes,
    bool HasExtractedText);

public record IntakeChatResponseDto(
    string AssistantMessage,
    IntakeMetaFieldsDto CurrentFields,
    bool IsComplete);

public record IntakeMetaFieldsDto(
    string? Title,
    string? Department,
    string? Location,
    string? Description,
    string? QueuePriority,
    string? ContactEmail,
    string? BusinessUnit);

public record IntakeAnalysisDto(
    Guid IntakeRequestId,
    string Brief,
    IReadOnlyList<string> Checkpoints,
    IReadOnlyList<string> Actionables);
