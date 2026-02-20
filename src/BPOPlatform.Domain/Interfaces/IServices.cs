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

    /// <summary>Downloads a blob and returns a readable stream.</summary>
    Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

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

// ── Phase 4 Interfaces ────────────────────────────────────────────────────────

/// <summary>
/// Abstraction for Azure AI Document Intelligence (Form Recognizer).
/// Extracts structured text from PDFs and scanned documents.
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>Extracts text content from a document stream (PDF, image, etc.).</summary>
    Task<string> ExtractTextAsync(Stream documentStream, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstraction for Azure AI Speech Services.
/// Transcribes audio files (MP3, WAV, M4A) to text.
/// </summary>
public interface ISpeechTranscriptionService
{
    /// <summary>Transcribes an audio stream to text.</summary>
    Task<string> TranscribeAsync(Stream audioStream, string contentType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstraction for generating structured process documentation (Word / Markdown).
/// The LLM-backed implementation calls Azure OpenAI; the fallback uses a template.
/// </summary>
public interface IDocumentGenerationService
{
    /// <summary>Generates a formatted document for a process and returns the file bytes + metadata.</summary>
    Task<DocumentGenerationResult> GenerateAsync(DocumentGenerationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Input model for document generation.</summary>
public record DocumentGenerationRequest(
    string ProcessName,
    string Department,
    string Description,
    double AutomationScore,
    double ComplianceScore,
    IReadOnlyList<string> KeyInsights,
    IReadOnlyList<string> Recommendations,
    string Format = "markdown"
);

/// <summary>Result of document generation: file bytes ready to stream to the client.</summary>
public record DocumentGenerationResult(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Abstraction for creating tickets in external ticketing systems
/// (ServiceNow, Jira, Azure DevOps) via Power Automate HTTP flows.
/// </summary>
public interface IExternalTicketingService
{
    /// <summary>Creates a ticket and returns the ticket reference.</summary>
    Task<ExternalTicket> CreateTicketAsync(string title, string description, string processId, string priority, CancellationToken cancellationToken = default);
}

/// <summary>Reference to a ticket created in an external system.</summary>
public record ExternalTicket(string TicketId, string Url, string Status);
