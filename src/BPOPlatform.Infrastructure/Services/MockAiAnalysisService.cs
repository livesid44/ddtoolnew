using BPOPlatform.Domain.Interfaces;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Development/test fallback implementation of <see cref="IAiAnalysisService"/>.
/// Returns representative mock scores and insights without calling Azure OpenAI.
/// Registered automatically when no Azure OpenAI endpoint is configured.
/// </summary>
public class MockAiAnalysisService : IAiAnalysisService
{
    public Task<AiAnalysisResult> AnalyzeProcessAsync(
        string processDescription,
        IEnumerable<string> artifactTexts,
        CancellationToken cancellationToken = default)
    {
        var result = new AiAnalysisResult(
            AutomationPotentialScore: 72.5,
            ComplianceScore: 88.0,
            KeyInsights:
            [
                "High degree of repetition detected across workflow steps",
                "Manual data entry identified in three key stages",
                "Low exception rate (< 5%) indicates process stability"
            ],
            Recommendations:
            [
                "Automate data extraction with an RPA tool (e.g. Power Automate Desktop)",
                "Implement a digital approval workflow to eliminate paper-based steps",
                "Add monitoring dashboards for real-time SLA tracking"
            ],
            ConfidenceScore: 0.75
        );

        return Task.FromResult(result);
    }
}
