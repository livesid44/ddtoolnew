using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// A workflow step in the process discovery pipeline.
/// Maps to the 5-step workflow: Meta Info → Upload → AI Validate → Review → Deploy.
/// </summary>
public class WorkflowStep : BaseEntity
{
    public Guid ProcessId { get; private set; }
    public int StepOrder { get; private set; }
    public string StepName { get; private set; } = string.Empty;
    public ProcessStatus RequiredStatus { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public string? Notes { get; private set; }

    private WorkflowStep() { }

    public static WorkflowStep Create(Guid processId, int stepOrder, string stepName, ProcessStatus requiredStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        return new WorkflowStep
        {
            ProcessId = processId,
            StepOrder = stepOrder,
            StepName = stepName,
            RequiredStatus = requiredStatus
        };
    }

    public void Complete(string completedBy, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completedBy);
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        CompletedBy = completedBy;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}
