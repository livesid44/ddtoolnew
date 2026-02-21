using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Development fallback for <see cref="IIntakeChatService"/>.
/// Simulates a guided conversation and AI analysis without requiring Azure OpenAI.
/// </summary>
internal sealed class MockIntakeChatService : IIntakeChatService
{
    // Ordered question sequence for the guided chat
    private static readonly (string Field, string Question)[] Questions =
    [
        ("title",        "Great! Let's start. What is the **name** of the business process you'd like to submit?"),
        ("department",   "Which **department** or team owns this process? (e.g. Finance, Operations, HR)"),
        ("description",  "Can you give a brief **description** of the process — what it does and any pain points?"),
        ("location",     "Where is this process primarily carried out? (city, office, or 'Global')"),
        ("businessUnit", "Which **business unit** is responsible? (optional – press Enter to skip)"),
        ("contactEmail", "What is the best **email address** to reach the process owner? (optional – press Enter to skip)"),
        ("queuePriority","How urgent is this? Please choose a **priority**: Low, Medium, High, or Critical."),
    ];

    public Task<IntakeChatServiceResponse> SendMessageAsync(
        IReadOnlyList<IntakeChatMessage> history,
        string userMessage,
        IntakeMetaFields currentFields,
        CancellationToken ct)
    {
        // First message – return opening greeting
        if (history.Count == 0)
        {
            return Task.FromResult(new IntakeChatServiceResponse(
                "Hello! I'm here to help you submit a new business process for review. " + Questions[0].Question,
                currentFields,
                false));
        }

        // Determine which field to collect next based on what's already populated
        var updated = currentFields;
        var lastUserMsg = userMessage.Trim();

        if (string.IsNullOrWhiteSpace(currentFields.Title))
            updated = updated with { Title = lastUserMsg };
        else if (string.IsNullOrWhiteSpace(currentFields.Department))
            updated = updated with { Department = lastUserMsg };
        else if (string.IsNullOrWhiteSpace(currentFields.Description))
            updated = updated with { Description = lastUserMsg };
        else if (string.IsNullOrWhiteSpace(currentFields.Location))
            updated = updated with { Location = lastUserMsg == string.Empty ? null : lastUserMsg };
        else if (string.IsNullOrWhiteSpace(currentFields.BusinessUnit))
            updated = updated with { BusinessUnit = lastUserMsg == string.Empty ? null : lastUserMsg };
        else if (string.IsNullOrWhiteSpace(currentFields.ContactEmail))
            updated = updated with { ContactEmail = lastUserMsg == string.Empty ? null : lastUserMsg };
        else if (string.IsNullOrWhiteSpace(currentFields.QueuePriority) || currentFields.QueuePriority == "Medium")
        {
            var priority = lastUserMsg.ToLowerInvariant() switch
            {
                "low"      => "Low",
                "high"     => "High",
                "critical" => "Critical",
                _          => "Medium"
            };
            updated = updated with { QueuePriority = priority };
        }

        // Determine next question
        var isComplete = !string.IsNullOrWhiteSpace(updated.Title) &&
                         !string.IsNullOrWhiteSpace(updated.Department) &&
                         !string.IsNullOrWhiteSpace(updated.Description);

        string message;
        if (isComplete && !string.IsNullOrWhiteSpace(updated.QueuePriority))
        {
            message = $"Thank you! I have all the information I need. Here's a summary:\n\n" +
                      $"- **Process Name**: {updated.Title}\n" +
                      $"- **Department**: {updated.Department}\n" +
                      $"- **Description**: {updated.Description}\n" +
                      $"- **Location**: {updated.Location ?? "Not specified"}\n" +
                      $"- **Priority**: {updated.QueuePriority}\n\n" +
                      "Click **Submit** to confirm, then upload supporting documents for AI analysis.";
            isComplete = true;
        }
        else
        {
            // Find first unanswered question
            message = "Thank you! ";
            if (string.IsNullOrWhiteSpace(updated.Department))
                message += Questions[1].Question;
            else if (string.IsNullOrWhiteSpace(updated.Description))
                message += Questions[2].Question;
            else if (string.IsNullOrWhiteSpace(updated.Location))
                message += Questions[3].Question;
            else if (string.IsNullOrWhiteSpace(updated.BusinessUnit))
                message += Questions[4].Question;
            else if (string.IsNullOrWhiteSpace(updated.ContactEmail))
                message += Questions[5].Question;
            else
                message += Questions[6].Question;
        }

        return Task.FromResult(new IntakeChatServiceResponse(message, updated, isComplete));
    }

    public Task<IntakeAnalysisResult> AnalyseIntakeAsync(
        string title,
        string? description,
        IEnumerable<string> artifactTexts,
        CancellationToken ct)
    {
        var brief = $"The '{title}' process has been reviewed. " +
                    $"{(string.IsNullOrWhiteSpace(description) ? "No description was provided." : description)} " +
                    "Based on the uploaded artifacts, this process shows moderate automation potential with clear " +
                    "opportunities for workflow standardisation and digital transformation.";

        var checkpoints = new List<string>
        {
            "Verify all input data sources are documented and accessible",
            "Confirm regulatory compliance requirements (GDPR, SOX, ISO 27001 as applicable)",
            "Map current process actors, roles, and handover points",
            "Identify and quantify manual effort (FTE hours per month)",
            "Document all exception-handling paths and edge cases",
            "Validate SLA requirements and acceptable automation downtime",
            "Confirm integration points with downstream/upstream systems"
        };

        var actionables = new List<string>
        {
            "Create detailed process flow diagram (BPMN 2.0 recommended)",
            "Prioritise quick-win automation candidates (high-volume, rule-based steps)",
            "Schedule stakeholder workshop to validate current-state documentation",
            "Define KPIs and success metrics for the automation programme",
            "Conduct vendor assessment for RPA / AI tooling if required",
            "Develop a phased implementation roadmap with milestone dates",
            "Establish a change management and training plan for impacted teams"
        };

        return Task.FromResult(new IntakeAnalysisResult(brief, checkpoints, actionables));
    }
}
