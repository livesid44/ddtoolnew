using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Functions;

/// <summary>
/// Durable orchestrator for automated process analysis.
/// Runs four activities in sequence:
///   1. ExtractDocumentsActivity  – Document Intelligence on all PDF artifacts
///   2. TranscribeAudioActivity   – Speech Services on all audio artifacts
///   3. AnalyseProcessActivity    – AI analysis using extracted content
///   4. AdvanceWorkflowActivity   – Advance process status to UnderReview
/// </summary>
public class ProcessOrchestrator
{
    [Function(nameof(ProcessOrchestrator))]
    public async Task<OrchestrationResult> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<ProcessOrchestrator>();
        var processId = context.GetInput<Guid>();

        logger.LogInformation("Orchestration started for process {ProcessId}", processId);

        try
        {
            // Step 1: Extract text from PDF artifacts
            var extractedCount = await context.CallActivityAsync<int>(
                nameof(ProcessActivities.ExtractDocumentsActivity),
                processId);
            logger.LogInformation("Extracted text from {Count} PDF artifact(s)", extractedCount);

            // Step 2: Transcribe audio artifacts
            var transcribedCount = await context.CallActivityAsync<int>(
                nameof(ProcessActivities.TranscribeAudioActivity),
                processId);
            logger.LogInformation("Transcribed {Count} audio artifact(s)", transcribedCount);

            // Step 3: Run AI analysis
            var analysisScore = await context.CallActivityAsync<double>(
                nameof(ProcessActivities.AnalyseProcessActivity),
                processId);
            logger.LogInformation("AI analysis complete. Automation score: {Score}", analysisScore);

            // Step 4: Advance workflow to UnderReview
            await context.CallActivityAsync(
                nameof(ProcessActivities.AdvanceWorkflowActivity),
                processId);
            logger.LogInformation("Process {ProcessId} advanced to UnderReview", processId);

            return new OrchestrationResult(
                ProcessId: processId,
                ExtractedArtifacts: extractedCount,
                TranscribedArtifacts: transcribedCount,
                AutomationScore: analysisScore,
                Status: "Completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Orchestration failed for process {ProcessId}", processId);
            return new OrchestrationResult(
                ProcessId: processId,
                ExtractedArtifacts: 0,
                TranscribedArtifacts: 0,
                AutomationScore: 0,
                Status: $"Failed: {ex.Message}");
        }
    }
}

/// <summary>Result emitted by the orchestrator and returned to the HTTP caller.</summary>
public record OrchestrationResult(
    Guid ProcessId,
    int ExtractedArtifacts,
    int TranscribedArtifacts,
    double AutomationScore,
    string Status);
