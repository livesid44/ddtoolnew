using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// Represents an in-flight intake request: a guided conversation that collects meta information
/// about a business process, then accepts artifacts and AI analysis before being promoted into
/// a full <see cref="Process"/> project.
/// </summary>
public class IntakeRequest : BaseEntity
{
    // ── Meta fields (populated progressively through the chat) ───────────────
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Department { get; private set; }
    public string? Location { get; private set; }
    public string? BusinessUnit { get; private set; }
    public string? ContactEmail { get; private set; }

    /// <summary>Queue priority: Low / Medium / High / Critical.</summary>
    public string QueuePriority { get; private set; } = "Medium";

    public string OwnerId { get; private set; } = string.Empty;
    public IntakeStatus Status { get; private set; } = IntakeStatus.Draft;

    /// <summary>JSON-serialised list of <see cref="IntakeChatMessage"/> objects.</summary>
    public string ChatHistoryJson { get; private set; } = "[]";

    // ── AI analysis results (populated after AnalyseIntake) ──────────────────
    /// <summary>One-paragraph AI-generated brief of the process.</summary>
    public string? AiBrief { get; private set; }

    /// <summary>JSON array of check-point strings.</summary>
    public string? AiCheckpointsJson { get; private set; }

    /// <summary>JSON array of actionable strings.</summary>
    public string? AiActionablesJson { get; private set; }

    /// <summary>Set when the intake is promoted to a full Process project.</summary>
    public Guid? PromotedProcessId { get; private set; }

    private readonly List<IntakeArtifact> _artifacts = [];
    public IReadOnlyCollection<IntakeArtifact> Artifacts => _artifacts.AsReadOnly();

    // EF Core constructor
    private IntakeRequest() { }

    public static IntakeRequest Create(string title, string ownerId, string queuePriority = "Medium")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        return new IntakeRequest
        {
            Title = title,
            OwnerId = ownerId,
            QueuePriority = queuePriority,
            Status = IntakeStatus.Draft
        };
    }

    public void UpdateMeta(
        string title,
        string? description,
        string? department,
        string? location,
        string? businessUnit,
        string? contactEmail,
        string? queuePriority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
        Description = description;
        Department = department;
        Location = location;
        BusinessUnit = businessUnit;
        ContactEmail = contactEmail;
        if (!string.IsNullOrWhiteSpace(queuePriority))
            QueuePriority = queuePriority;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateChatHistory(string chatHistoryJson)
    {
        ChatHistoryJson = chatHistoryJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Submit()
    {
        if (Status != IntakeStatus.Draft)
            throw new InvalidOperationException("Only a Draft intake can be submitted.");
        Status = IntakeStatus.Submitted;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddArtifact(IntakeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        _artifacts.Add(artifact);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAnalysisResults(string brief, string checkpointsJson, string actionablesJson)
    {
        AiBrief = brief;
        AiCheckpointsJson = checkpointsJson;
        AiActionablesJson = actionablesJson;
        Status = IntakeStatus.Analysed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPromoted(Guid processId)
    {
        if (Status != IntakeStatus.Analysed)
            throw new InvalidOperationException("Only an Analysed intake can be promoted.");
        PromotedProcessId = processId;
        Status = IntakeStatus.Promoted;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>A single message in the intake guided-chat conversation.</summary>
public record IntakeChatMessage(
    /// <summary>"user" or "assistant"</summary>
    string Role,
    string Content,
    DateTime Timestamp);
