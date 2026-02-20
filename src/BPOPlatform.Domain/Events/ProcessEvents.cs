using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Events;

public record ProcessCreatedEvent(Guid ProcessId, string ProcessName) : IDomainEvent;

public record ProcessStatusChangedEvent(Guid ProcessId, ProcessStatus NewStatus) : IDomainEvent;

public record ArtifactUploadedEvent(Guid ProcessId, Guid ArtifactId, string FileName) : IDomainEvent;

public record ArtifactAnalyzedEvent(Guid ArtifactId, double ConfidenceScore) : IDomainEvent;
