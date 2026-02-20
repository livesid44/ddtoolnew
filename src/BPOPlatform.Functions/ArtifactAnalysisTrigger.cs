using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Functions;

/// <summary>
/// Azure Function triggered when a new file is uploaded to Blob Storage.
/// Phase 4: extracts text from PDFs via Document Intelligence and transcribes audio via Speech Services
/// before invoking AI analysis and updating the process artifact record.
/// </summary>
public class ArtifactAnalysisTrigger(
    IAiAnalysisService aiService,
    IDocumentIntelligenceService docIntelligence,
    ISpeechTranscriptionService speechService,
    IArtifactRepository artifacts,
    IUnitOfWork uow,
    ILogger<ArtifactAnalysisTrigger> logger)
{
    private static readonly HashSet<string> PdfExtensions   = [".pdf", ".tiff", ".tif", ".jpg", ".jpeg", ".png"];
    private static readonly HashSet<string> AudioExtensions = [".mp3", ".wav", ".m4a", ".ogg"];

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

        // ── Phase 4: extract rich text before AI analysis ────────────────────
        var ext = Path.GetExtension(artifact.FileName).ToLowerInvariant();
        string extractedContext = artifact.FileName;

        if (PdfExtensions.Contains(ext))
        {
            logger.LogInformation("Running Document Intelligence on PDF artifact: {Name}", artifact.FileName);
            extractedContext = await docIntelligence.ExtractTextAsync(blobStream, artifact.FileName, ct);
            artifact.SetExtractedText(extractedContext);
        }
        else if (AudioExtensions.Contains(ext))
        {
            logger.LogInformation("Running Speech transcription on audio artifact: {Name}", artifact.FileName);
            var contentType = ext switch
            {
                ".mp3" => "audio/mpeg",
                ".m4a" => "audio/mp4",
                ".ogg" => "audio/ogg",
                _      => "audio/wav"
            };
            extractedContext = await speechService.TranscribeAsync(blobStream, contentType, ct);
            artifact.SetExtractedText(extractedContext);
        }

        // ── AI analysis using extracted context ──────────────────────────────
        var analysis = await aiService.AnalyzeProcessAsync(
            processDescription: $"Artifact: {artifact.FileName}",
            artifactTexts: [extractedContext],
            ct);

        artifact.MarkAnalyzed(analysis.ConfidenceScore);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation(
            "Artifact {Name} analyzed. Confidence: {Score:P0}, Automation: {Auto:F1}, Compliance: {Comp:F1}",
            name, analysis.ConfidenceScore, analysis.AutomationPotentialScore, analysis.ComplianceScore);
    }
}
