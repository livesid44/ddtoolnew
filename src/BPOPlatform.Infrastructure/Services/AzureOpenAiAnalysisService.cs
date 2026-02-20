using Azure.AI.OpenAI;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BPOPlatform.Infrastructure.Services;

public class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
}

/// <summary>
/// Azure OpenAI implementation of <see cref="IAiAnalysisService"/>.
/// Uses Managed Identity in production; API key in development via appsettings.
/// </summary>
public class AzureOpenAiAnalysisService(
    AzureOpenAIClient openAiClient,
    IOptions<AzureOpenAiOptions> options,
    ILogger<AzureOpenAiAnalysisService> logger) : IAiAnalysisService
{
    private readonly AzureOpenAiOptions _opts = options.Value;

    public async Task<AiAnalysisResult> AnalyzeProcessAsync(
        string processDescription,
        IEnumerable<string> artifactTexts,
        CancellationToken ct = default)
    {
        var combinedContext = string.Join("\n\n", artifactTexts);
        var systemPrompt =
            "You are an expert BPO process analyst. Analyse the provided process description and artifacts. " +
            "Return a JSON object with fields: automationPotentialScore (0-100), complianceScore (0-100), " +
            "keyInsights (array of strings), recommendations (array of strings), confidenceScore (0-1).";

        var chatClient = openAiClient.GetChatClient(_opts.DeploymentName);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage($"Process: {processDescription}\n\nArtifacts:\n{combinedContext}")
        };

        logger.LogInformation("Calling Azure OpenAI for process analysis...");

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        var content = response.Value.Content[0].Text;

        // Parse the JSON response
        return ParseAnalysisResult(content);
    }

    private static AiAnalysisResult ParseAnalysisResult(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var insights = root.TryGetProperty("keyInsights", out var insightsEl)
                ? insightsEl.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                : new List<string>();

            var recommendations = root.TryGetProperty("recommendations", out var recEl)
                ? recEl.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                : new List<string>();

            return new AiAnalysisResult(
                root.TryGetProperty("automationPotentialScore", out var a) ? a.GetDouble() : 0,
                root.TryGetProperty("complianceScore", out var c) ? c.GetDouble() : 0,
                insights,
                recommendations,
                root.TryGetProperty("confidenceScore", out var conf) ? conf.GetDouble() : 0
            );
        }
        catch
        {
            // Return a safe default if parsing fails
            return new AiAnalysisResult(0, 0, [], [], 0);
        }
    }
}
