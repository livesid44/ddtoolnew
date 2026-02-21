using System.Net.Http.Headers;
using System.Text.Json;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BPOPlatform.Infrastructure.Services;

public class SpeechServicesOptions
{
    public const string SectionName = "SpeechServices";
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
}

/// <summary>
/// Azure AI Speech Services implementation of <see cref="ISpeechTranscriptionService"/>.
/// Calls the Azure Cognitive Services Speech-to-Text REST API v3.
/// Uses the subscription key from Key Vault in production; mock is used when not configured.
/// </summary>
public class AzureSpeechTranscriptionService(
    IHttpClientFactory httpClientFactory,
    IOptions<SpeechServicesOptions> options,
    ILogger<AzureSpeechTranscriptionService> logger) : ISpeechTranscriptionService
{
    private readonly SpeechServicesOptions _opts = options.Value;

    public async Task<string> TranscribeAsync(Stream audioStream, string contentType, CancellationToken ct = default)
    {
        logger.LogInformation("Transcribing audio via Azure Speech Services (region: {Region})", _opts.Region);

        var endpoint = !string.IsNullOrWhiteSpace(_opts.Endpoint)
            ? _opts.Endpoint
            : $"https://{_opts.Region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=en-US";

        var client = httpClientFactory.CreateClient("SpeechServices");
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _opts.SubscriptionKey);

        using var content = new StreamContent(audioStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await client.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        // Azure Speech REST response: { "RecognitionStatus": "Success", "DisplayText": "..." }
        if (doc.RootElement.TryGetProperty("DisplayText", out var textEl))
            return textEl.GetString() ?? string.Empty;

        logger.LogWarning("Speech transcription returned unexpected shape: {Json}", json);
        return string.Empty;
    }
}
