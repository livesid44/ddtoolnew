using BPOPlatform.Domain.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Functions;

/// <summary>
/// Azure Function triggered when a new file is uploaded to Blob Storage.
/// Invokes the AI analysis service and updates the process artifact record.
/// </summary>
public class ArtifactAnalysisTrigger(IAiAnalysisService aiService, IArtifactRepository artifacts, IUnitOfWork uow, ILogger<ArtifactAnalysisTrigger> logger)
{
    [Function(nameof(ArtifactAnalysisTrigger))]
    public async Task Run(
        [BlobTrigger("process-artifacts/{name}", Connection = "AzureWebJobsStorage")] Stream blobStream,
        string name,
        FunctionContext context,
        CancellationToken ct)
    {
        logger.LogInformation("Processing artifact: {Name}", name);

        // Derive processId from blob name convention: "{processId}/{fileName}"
        var segments = name.Split('/');
        if (segments.Length < 2 || !Guid.TryParse(segments[0], out var processId))
        {
            logger.LogWarning("Unexpected blob name format: {Name}", name);
            return;
        }

        var artifactList = await artifacts.GetByProcessIdAsync(processId, ct);
        var artifact = artifactList.FirstOrDefault(a => a.BlobPath.EndsWith(name));

        if (artifact is null)
        {
            logger.LogWarning("No artifact record found for blob: {Name}", name);
            return;
        }

        // For non-text artifacts we use an empty context; a real implementation would extract text first.
        var analysis = await aiService.AnalyzeProcessAsync(
            processDescription: $"Artifact: {artifact.FileName}",
            artifactTexts: [],
            ct);

        artifact.MarkAnalyzed(analysis.ConfidenceScore);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Artifact {Name} analyzed. Confidence: {Score:P0}", name, analysis.ConfidenceScore);
    }
}
