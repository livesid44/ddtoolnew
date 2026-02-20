using System.Net.Http.Json;
using System.Text.Json;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BPOPlatform.Infrastructure.Services;

public class PowerAutomateOptions
{
    public const string SectionName = "PowerAutomate";
    /// <summary>HTTP trigger URL of the Power Automate flow (from flow > trigger > HTTP POST URL).</summary>
    public string FlowUrl { get; set; } = string.Empty;
}

/// <summary>
/// Power Automate implementation of <see cref="IExternalTicketingService"/>.
/// Sends an HTTP POST to a Power Automate HTTP-triggered flow which creates a ticket
/// in the configured system (ServiceNow, Jira, Azure DevOps, etc.).
/// </summary>
public class PowerAutomateTicketingService(
    IHttpClientFactory httpClientFactory,
    IOptions<PowerAutomateOptions> options,
    ILogger<PowerAutomateTicketingService> logger) : IExternalTicketingService
{
    private readonly PowerAutomateOptions _opts = options.Value;

    public async Task<ExternalTicket> CreateTicketAsync(
        string title, string description, string processId, string priority, CancellationToken ct = default)
    {
        logger.LogInformation("Creating external ticket for process {ProcessId} via Power Automate", processId);

        var payload = new
        {
            title,
            description,
            processId,
            priority,
            source = "BPO Platform",
            createdAt = DateTime.UtcNow
        };

        var client = httpClientFactory.CreateClient("PowerAutomate");
        var response = await client.PostAsJsonAsync(_opts.FlowUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        // Power Automate HTTP flow responses vary; extract common fields defensively
        try
        {
            using var doc = JsonDocument.Parse(json);
            var ticketId = doc.RootElement.TryGetProperty("ticketId", out var tid)
                ? tid.GetString() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();
            var url = doc.RootElement.TryGetProperty("ticketUrl", out var turl)
                ? turl.GetString() ?? string.Empty
                : string.Empty;
            return new ExternalTicket(ticketId, url, "Created");
        }
        catch (JsonException ex)
        {
            // Flow returned a non-JSON response (e.g. plain text "202 Accepted").
            // Synthesise a ticket reference rather than failing the request.
            logger.LogWarning(ex, "Power Automate response was not valid JSON; synthesising ticket reference");
            return new ExternalTicket(Guid.NewGuid().ToString(), string.Empty, "Created");
        }
    }
}
