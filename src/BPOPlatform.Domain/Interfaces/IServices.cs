namespace BPOPlatform.Domain.Interfaces;

/// <summary>
/// Abstraction for Azure Blob Storage. Infrastructure layer provides the real implementation;
/// a local file-system implementation can be swapped in for development/testing.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Uploads a file stream and returns the blob path (relative URL).</summary>
    Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Returns a short-lived SAS URL for secure download.</summary>
    Task<Uri> GetDownloadUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken cancellationToken = default);

    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstraction for Azure OpenAI / LLM analysis.
/// </summary>
public interface IAiAnalysisService
{
    /// <summary>Analyze process description and artifacts, returning structured insights.</summary>
    Task<AiAnalysisResult> AnalyzeProcessAsync(string processDescription, IEnumerable<string> artifactTexts, CancellationToken cancellationToken = default);
}

/// <summary>Result returned by the AI analysis service.</summary>
public record AiAnalysisResult(
    double AutomationPotentialScore,
    double ComplianceScore,
    IReadOnlyList<string> KeyInsights,
    IReadOnlyList<string> Recommendations,
    double ConfidenceScore
);
