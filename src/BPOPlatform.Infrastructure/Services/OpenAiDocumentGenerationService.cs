using System.Text;
using Azure.AI.OpenAI;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Azure OpenAI-backed implementation of <see cref="IDocumentGenerationService"/>.
/// Uses GPT-4o to generate structured process documentation in Markdown, HTML, or Word.
/// Falls back to <see cref="MarkdownDocumentGenerationService"/> when OpenAI is not configured.
/// </summary>
public class OpenAiDocumentGenerationService(
    AzureOpenAIClient openAiClient,
    IOptions<AzureOpenAiOptions> options,
    ILogger<OpenAiDocumentGenerationService> logger) : IDocumentGenerationService
{
    private readonly AzureOpenAiOptions _opts = options.Value;

    public async Task<DocumentGenerationResult> GenerateAsync(DocumentGenerationRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Generating process documentation for {ProcessName} via Azure OpenAI", request.ProcessName);

        var insightsBullets = string.Join("\n", request.KeyInsights.Select(i => $"- {i}"));
        var recBullets = string.Join("\n", request.Recommendations.Select(r => $"- {r}"));

        var systemPrompt =
            "You are a senior BPO process documentation specialist. " +
            "Generate a comprehensive, professional process discovery report in Markdown. " +
            "Include: executive summary, process overview, metrics & KPIs, key findings, " +
            "automation opportunities, compliance notes, and recommended next steps. " +
            "Be concise but thorough. Use ## headings, bullet lists, and a metrics table.";

        var userPrompt =
            $"Process Name: {request.ProcessName}\n" +
            $"Department: {request.Department}\n" +
            $"Description: {request.Description}\n" +
            $"Automation Potential Score: {request.AutomationScore:F1}/100\n" +
            $"Compliance Score: {request.ComplianceScore:F1}/100\n\n" +
            $"Key Insights:\n{insightsBullets}\n\n" +
            $"Recommendations:\n{recBullets}";

        var chatClient = openAiClient.GetChatClient(_opts.DeploymentName);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        var markdownContent = response.Value.Content[0].Text;

        return ConvertToFormat(markdownContent, request.ProcessName, request.Format);
    }

    internal static DocumentGenerationResult ConvertToFormat(string markdownContent, string processName, string format)
    {
        var safeName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));

        return format switch
        {
            "html" => new DocumentGenerationResult(
                $"{safeName}_Report.html",
                "text/html",
                Encoding.UTF8.GetBytes(WrapInHtml(markdownContent, processName))),
            "docx" => DocumentWordBuilder.Build(markdownContent, processName),
            _ => new DocumentGenerationResult(
                $"{safeName}_Report.md",
                "text/markdown",
                Encoding.UTF8.GetBytes(markdownContent))
        };
    }

    private static string WrapInHtml(string markdown, string title)
    {
        var body = SimpleMarkdownToHtml(markdown);
        return $"<!DOCTYPE html>\n<html lang=\"en\">\n<head><meta charset=\"UTF-8\"><title>{title} – Process Report</title>\n" +
               "<style>body{font-family:Segoe UI,sans-serif;max-width:900px;margin:2rem auto;line-height:1.6}\n" +
               "h1,h2,h3{color:#1a237e}table{border-collapse:collapse;width:100%}\n" +
               "th,td{border:1px solid #ddd;padding:8px}th{background:#e8eaf6}</style></head>\n" +
               $"<body>{body}</body></html>";
    }

    private static string SimpleMarkdownToHtml(string md)
    {
        // Basic Markdown → HTML transformation sufficient for document export
        var sb = new StringBuilder();
        foreach (var line in md.Split('\n'))
        {
            if (line.StartsWith("## "))      sb.AppendLine($"<h2>{line[3..]}</h2>");
            else if (line.StartsWith("# "))  sb.AppendLine($"<h1>{line[2..]}</h1>");
            else if (line.StartsWith("- "))  sb.AppendLine($"<li>{line[2..]}</li>");
            else if (string.IsNullOrWhiteSpace(line)) sb.AppendLine("<br>");
            else sb.AppendLine($"<p>{line}</p>");
        }
        return sb.ToString();
    }
}
