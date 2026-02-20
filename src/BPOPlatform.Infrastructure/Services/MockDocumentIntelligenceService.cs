using BPOPlatform.Domain.Interfaces;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Development/test fallback implementation of <see cref="IDocumentIntelligenceService"/>.
/// Returns representative placeholder text without calling Azure AI Document Intelligence.
/// Registered automatically when no Document Intelligence endpoint is configured.
/// </summary>
public class MockDocumentIntelligenceService : IDocumentIntelligenceService
{
    public Task<string> ExtractTextAsync(Stream documentStream, string fileName, CancellationToken ct = default)
    {
        var mockText =
            $"[Mock Document Intelligence] Extracted text from: {fileName}\n\n" +
            "PROCESS OVERVIEW\n" +
            "This document describes a multi-step business process involving data validation, " +
            "approval workflows, and exception handling. The process includes the following stages:\n\n" +
            "1. Data Collection: Gather input data from source systems via automated connectors.\n" +
            "2. Validation Rules: Apply 12 business rules to verify data completeness and accuracy.\n" +
            "3. Approval Routing: Route exceptions to department managers for manual review.\n" +
            "4. Output Generation: Produce structured output reports for downstream consumption.\n\n" +
            "Key metrics: average cycle time 4.2 hours, exception rate 3.8%, throughput 250 cases/day.";

        return Task.FromResult(mockText);
    }
}
