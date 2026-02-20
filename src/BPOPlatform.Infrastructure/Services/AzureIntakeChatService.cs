using Azure.AI.OpenAI;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.Text.Json;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Azure OpenAI-backed intake chat service.
/// Uses GPT-4o to guide users through process meta-data collection in a conversational way,
/// then analyses submitted artifacts to produce a brief + checkpoints + actionables.
/// </summary>
internal sealed class AzureIntakeChatService(IConfiguration configuration) : IIntakeChatService
{
    private const string SystemPromptChat = """
        You are an AI assistant helping to collect information about a business process for the BPO AI Platform.
        Your goal is to extract the following fields through friendly conversation:
        - Title (required): a concise name for the process
        - Department (required): which business department this belongs to
        - Description (required): a clear description of the process, its purpose and pain points
        - Location: geographic location or site where the process runs
        - BusinessUnit: the business unit or team responsible
        - ContactEmail: an email address for the process owner
        - QueuePriority: urgency level (Low / Medium / High / Critical)

        Ask naturally and one topic at a time. When you have collected all required fields (Title, Department, Description),
        respond with a JSON block at the end of your message in this exact format:
        ```json
        {"title":"...", "department":"...", "description":"...", "location":"...", "businessUnit":"...",
         "contactEmail":"...", "queuePriority":"...", "isComplete": true}
        ```
        If some fields are still missing, set "isComplete": false. For any optional fields not yet collected, use null.
        """;

    private const string SystemPromptAnalysis = """
        You are an AI process analyst. Given information about a business process, produce:
        1. A one-paragraph executive brief (3-5 sentences).
        2. A list of 5-8 key checkpoints (validation steps for the process).
        3. A list of 5-8 actionables (specific improvements or next steps).

        Respond ONLY with valid JSON in this format:
        {"brief":"...","checkpoints":["...","..."],"actionables":["...","..."]}
        """;

    public async Task<IntakeChatServiceResponse> SendMessageAsync(
        IReadOnlyList<IntakeChatMessage> history,
        string userMessage,
        IntakeMetaFields currentFields,
        CancellationToken ct)
    {
        var client = CreateClient(out var modelName);
        if (client is null)
            throw new InvalidOperationException("Azure OpenAI is not configured.");

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPromptChat)
        };

        foreach (var h in history)
        {
            if (h.Role == "user")
                messages.Add(new UserChatMessage(h.Content));
            else
                messages.Add(new AssistantChatMessage(h.Content));
        }
        messages.Add(new UserChatMessage(userMessage));

        var completion = await client.GetChatClient(modelName)
            .CompleteChatAsync(messages, cancellationToken: ct);

        var text = completion.Value.Content[0].Text;
        return ParseChatResponse(text, currentFields);
    }

    public async Task<IntakeAnalysisResult> AnalyseIntakeAsync(
        string title,
        string? description,
        IEnumerable<string> artifactTexts,
        CancellationToken ct)
    {
        var client = CreateClient(out var modelName);
        if (client is null)
            throw new InvalidOperationException("Azure OpenAI is not configured.");

        var context = $"Process: {title}\nDescription: {description ?? "N/A"}\n" +
                      string.Join("\n---\n", artifactTexts.Take(5));

        var completion = await client.GetChatClient(modelName)
            .CompleteChatAsync(
            [
                new SystemChatMessage(SystemPromptAnalysis),
                new UserChatMessage(context)
            ], cancellationToken: ct);

        return ParseAnalysisResponse(completion.Value.Content[0].Text);
    }

    private AzureOpenAIClient? CreateClient(out string modelName)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
        modelName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.StartsWith("__"))
            return null;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return null;

        return new AzureOpenAIClient(uri, new Azure.Identity.DefaultAzureCredential());
    }

    private static IntakeChatServiceResponse ParseChatResponse(string text, IntakeMetaFields currentFields)
    {
        // Try to extract JSON block from the assistant message
        var jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart < 0) jsonStart = text.LastIndexOf('{');
        var jsonEnd = text.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            try
            {
                var jsonText = text[jsonStart..].TrimStart('`', 'j', 's', 'o', 'n', '\n', ' ');
                if (jsonText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    jsonText = jsonText[7..];
                jsonText = jsonText.TrimEnd('`', ' ', '\n');

                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                string? GetStr(string k) =>
                    root.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString() : null;

                bool isComplete = root.TryGetProperty("isComplete", out var ic) && ic.GetBoolean();

                var updated = new IntakeMetaFields(
                    GetStr("title")        ?? currentFields.Title,
                    GetStr("department")   ?? currentFields.Department,
                    GetStr("location")     ?? currentFields.Location,
                    GetStr("description")  ?? currentFields.Description,
                    GetStr("queuePriority") ?? currentFields.QueuePriority,
                    GetStr("contactEmail") ?? currentFields.ContactEmail,
                    GetStr("businessUnit") ?? currentFields.BusinessUnit);

                // Strip the JSON block from the visible message
                var visibleMessage = jsonStart > 0 ? text[..jsonStart].Trim() : text;

                return new IntakeChatServiceResponse(visibleMessage, updated, isComplete);
            }
            catch { /* Fall through to plain response */ }
        }

        return new IntakeChatServiceResponse(text, currentFields, false);
    }

    private static IntakeAnalysisResult ParseAnalysisResponse(string text)
    {
        try
        {
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = text[jsonStart..(jsonEnd + 1)];
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var brief = root.TryGetProperty("brief", out var b) ? b.GetString() ?? string.Empty : string.Empty;

                IReadOnlyList<string> GetList(string key)
                {
                    if (root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                        return arr.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();
                    return [];
                }

                return new IntakeAnalysisResult(brief, GetList("checkpoints"), GetList("actionables"));
            }
        }
        catch { /* Fall through */ }

        return new IntakeAnalysisResult(text, [], []);
    }
}
