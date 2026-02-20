using System.Text;
using BPOPlatform.Domain.Interfaces;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Template-based fallback implementation of <see cref="IDocumentGenerationService"/>.
/// Generates a well-structured Markdown (or HTML/Word) report without calling Azure OpenAI.
/// Registered automatically when no Azure OpenAI endpoint is configured.
/// </summary>
public class MarkdownDocumentGenerationService : IDocumentGenerationService
{
    public Task<DocumentGenerationResult> GenerateAsync(DocumentGenerationRequest request, CancellationToken ct = default)
    {
        var markdown = BuildMarkdown(request);
        var result = OpenAiDocumentGenerationService.ConvertToFormat(markdown, request.ProcessName, request.Format);
        return Task.FromResult(result);
    }

    private static string BuildMarkdown(DocumentGenerationRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {r.ProcessName} – Process Discovery Report");
        sb.AppendLine($"**Department:** {r.Department}  ");
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine(r.Description);
        sb.AppendLine();
        sb.AppendLine("## Process Metrics");
        sb.AppendLine("| Metric | Score |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Automation Potential | {r.AutomationScore:F1} / 100 |");
        sb.AppendLine($"| Compliance | {r.ComplianceScore:F1} / 100 |");
        sb.AppendLine();
        sb.AppendLine("## Key Findings");
        foreach (var insight in r.KeyInsights)
            sb.AppendLine($"- {insight}");
        sb.AppendLine();
        sb.AppendLine("## Recommendations");
        foreach (var rec in r.Recommendations)
            sb.AppendLine($"- {rec}");
        sb.AppendLine();
        sb.AppendLine("## Next Steps");
        sb.AppendLine("1. Review this report with relevant stakeholders.");
        sb.AppendLine("2. Prioritise automation opportunities based on ROI.");
        sb.AppendLine("3. Schedule a follow-up discovery workshop.");
        sb.AppendLine("4. Assign owners to each recommendation.");
        return sb.ToString();
    }
}
