using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Events;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// Represents a discovered business process in the BPO platform.
/// </summary>
public class Process : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Department { get; private set; } = string.Empty;
    public ProcessStatus Status { get; private set; } = ProcessStatus.Draft;
    public double AutomationScore { get; private set; }
    public double ComplianceScore { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;

    private readonly List<ProcessArtifact> _artifacts = [];
    public IReadOnlyCollection<ProcessArtifact> Artifacts => _artifacts.AsReadOnly();

    private readonly List<WorkflowStep> _workflowSteps = [];
    public IReadOnlyCollection<WorkflowStep> WorkflowSteps => _workflowSteps.AsReadOnly();

    // EF Core constructor
    private Process() { }

    public static Process Create(string name, string description, string department, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var process = new Process
        {
            Name = name,
            Description = description,
            Department = department,
            OwnerId = ownerId,
            Status = ProcessStatus.Draft
        };

        // Seed the standard 5-step discovery workflow
        process._workflowSteps.AddRange(DefaultWorkflowSteps(process.Id));

        process.AddDomainEvent(new ProcessCreatedEvent(process.Id, name));
        return process;
    }

    /// <summary>Returns the five standard BPO discovery workflow steps for a new process.</summary>
    private static IEnumerable<WorkflowStep> DefaultWorkflowSteps(Guid processId) =>
    [
        WorkflowStep.Create(processId, 1, "Meta Information",  ProcessStatus.Draft),
        WorkflowStep.Create(processId, 2, "Artifact Upload",   ProcessStatus.InProgress),
        WorkflowStep.Create(processId, 3, "AI Validation",     ProcessStatus.UnderReview),
        WorkflowStep.Create(processId, 4, "Review & Approval", ProcessStatus.Approved),
        WorkflowStep.Create(processId, 5, "Deployment",        ProcessStatus.Deployed),
    ];

    public void UpdateDetails(string name, string description, string department)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description;
        Department = department;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdvanceStatus(ProcessStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ProcessStatusChangedEvent(Id, newStatus));
    }

    public void UpdateScores(double automationScore, double complianceScore)
    {
        AutomationScore = Math.Clamp(automationScore, 0, 100);
        ComplianceScore = Math.Clamp(complianceScore, 0, 100);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddArtifact(ProcessArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        _artifacts.Add(artifact);
        UpdatedAt = DateTime.UtcNow;
    }
}
