using Azure.AI.DocumentIntelligence;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Infrastructure.Services;

public class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// Azure AI Document Intelligence implementation of <see cref="IDocumentIntelligenceService"/>.
/// Uses the prebuilt read model to extract text from PDFs and scanned images.
/// Managed Identity is used in production; a mock is used when the endpoint is not configured.
/// </summary>
public class AzureDocumentIntelligenceService(
    DocumentIntelligenceClient client,
    ILogger<AzureDocumentIntelligenceService> logger) : IDocumentIntelligenceService
{
    public async Task<string> ExtractTextAsync(Stream documentStream, string fileName, CancellationToken ct = default)
    {
        logger.LogInformation("Extracting text from document: {FileName}", fileName);

        var content = await BinaryData.FromStreamAsync(documentStream, ct);
        var operation = await client.AnalyzeDocumentAsync(
            Azure.WaitUntil.Completed,
            "prebuilt-read",
            content,
            cancellationToken: ct);

        var result = operation.Value;
        var text = string.Join("\n", result.Pages.SelectMany(p => p.Lines).Select(l => l.Content));

        logger.LogInformation("Extracted {CharCount} characters from {FileName}", text.Length, fileName);
        return text;
    }
}
