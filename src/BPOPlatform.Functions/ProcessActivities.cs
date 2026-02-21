using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Functions;

/// <summary>
/// Durable activity functions used by <see cref="ProcessOrchestrator"/>.
/// Each activity is an independently retryable unit of work.
/// </summary>
public class ProcessActivities(
    IAiAnalysisService aiService,
    IDocumentIntelligenceService docIntelligence,
    ISpeechTranscriptionService speechService,
    IBlobStorageService blobService,
    IArtifactRepository artifactRepo,
    IProcessRepository processRepo,
    IUnitOfWork uow,
    ILogger<ProcessActivities> logger)
{
    private static readonly HashSet<string> PdfExtensions   = [".pdf", ".tiff", ".tif", ".jpg", ".jpeg", ".png"];
    private static readonly HashSet<string> AudioExtensions = [".mp3", ".wav", ".m4a", ".ogg"];
    private const string ContainerName = "process-artifacts";

    /// <summary>
    /// Activity 1: Runs Document Intelligence on all PDF/image artifacts that have not yet been extracted.
    /// Returns the count of artifacts processed.
    /// </summary>
    [Function(nameof(ExtractDocumentsActivity))]
    public async Task<int> ExtractDocumentsActivity([ActivityTrigger] Guid processId, FunctionContext context)
    {
        var artifacts = await artifactRepo.GetByProcessIdAsync(processId, default);
        var pdfArtifacts = artifacts
            .Where(a => PdfExtensions.Contains(Path.GetExtension(a.FileName).ToLowerInvariant())
                        && a.ExtractedText is null)
            .ToList();

        foreach (var artifact in pdfArtifacts)
        {
            try
            {
                await using var stream = await blobService.DownloadAsync(ContainerName, artifact.BlobPath);
                var text = await docIntelligence.ExtractTextAsync(stream, artifact.FileName);
                artifact.SetExtractedText(text);
                logger.LogInformation("[Activity] Extracted text from {FileName}", artifact.FileName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Activity] Failed to extract text from {FileName}", artifact.FileName);
            }
        }

        if (pdfArtifacts.Count > 0)
            await uow.SaveChangesAsync();

        return pdfArtifacts.Count;
    }

    /// <summary>
    /// Activity 2: Transcribes all audio artifacts that have not yet been transcribed.
    /// Returns the count of artifacts processed.
    /// </summary>
    [Function(nameof(TranscribeAudioActivity))]
    public async Task<int> TranscribeAudioActivity([ActivityTrigger] Guid processId, FunctionContext context)
    {
        var artifacts = await artifactRepo.GetByProcessIdAsync(processId, default);
        var audioArtifacts = artifacts
            .Where(a => AudioExtensions.Contains(Path.GetExtension(a.FileName).ToLowerInvariant())
                        && a.ExtractedText is null)
            .ToList();

        foreach (var artifact in audioArtifacts)
        {
            try
            {
                await using var stream = await blobService.DownloadAsync(ContainerName, artifact.BlobPath);
                var ext = Path.GetExtension(artifact.FileName).ToLowerInvariant();
                var contentType = ext switch { ".mp3" => "audio/mpeg", ".m4a" => "audio/mp4", ".ogg" => "audio/ogg", _ => "audio/wav" };
                var transcript = await speechService.TranscribeAsync(stream, contentType);
                artifact.SetExtractedText(transcript);
                logger.LogInformation("[Activity] Transcribed {FileName}", artifact.FileName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Activity] Failed to transcribe {FileName}", artifact.FileName);
            }
        }

        if (audioArtifacts.Count > 0)
            await uow.SaveChangesAsync();

        return audioArtifacts.Count;
    }

    /// <summary>
    /// Activity 3: Runs full AI analysis on the process using extracted artifact content.
    /// Updates the process automation and compliance scores.
    /// Returns the automation potential score.
    /// </summary>
    [Function(nameof(AnalyseProcessActivity))]
    public async Task<double> AnalyseProcessActivity([ActivityTrigger] Guid processId, FunctionContext context)
    {
        var process = await processRepo.GetByIdAsync(processId, default)
                      ?? throw new KeyNotFoundException($"Process {processId} not found.");

        var artifacts = await artifactRepo.GetByProcessIdAsync(processId, default);
        var artifactTexts = artifacts.Select(a => a.ExtractedText ?? a.FileName).ToList();

        var result = await aiService.AnalyzeProcessAsync(process.Description, artifactTexts);
        process.UpdateScores(result.AutomationPotentialScore, result.ComplianceScore);
        await uow.SaveChangesAsync();

        logger.LogInformation("[Activity] Analysis complete for {ProcessId}. Automation: {Score}", processId, result.AutomationPotentialScore);
        return result.AutomationPotentialScore;
    }

    /// <summary>
    /// Activity 4: Advances the process status to UnderReview to trigger the next workflow stage.
    /// </summary>
    [Function(nameof(AdvanceWorkflowActivity))]
    public async Task AdvanceWorkflowActivity([ActivityTrigger] Guid processId, FunctionContext context)
    {
        var process = await processRepo.GetByIdAsync(processId, default)
                      ?? throw new KeyNotFoundException($"Process {processId} not found.");

        if (process.Status < ProcessStatus.UnderReview)
        {
            process.AdvanceStatus(ProcessStatus.UnderReview);
            await uow.SaveChangesAsync();
            logger.LogInformation("[Activity] Process {ProcessId} advanced to UnderReview", processId);
        }
        else
        {
            logger.LogInformation("[Activity] Process {ProcessId} already at {Status}", processId, process.Status);
        }
    }
}
