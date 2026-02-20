using System.Net;
using System.Net.Http.Json;
using BPOPlatform.Application.Processes.DTOs;
using FluentAssertions;

namespace BPOPlatform.IntegrationTests;

/// <summary>
/// Integration tests for the Phase 4 Automation endpoints:
/// document text extraction, speech transcription, document generation, and external tickets.
/// </summary>
public class Phase4AutomationIntegrationTests(BpoApiFactory factory)
    : IClassFixture<BpoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── POST .../artifacts/{id}/extract-text ──────────────────────────────────

    [Fact]
    public async Task ExtractText_ArtifactNotFound_Returns404()
    {
        var process = await CreateProcess("Extract Text Process");
        var response = await _client.PostAsync(
            $"/api/v1/processes/{process!.Id}/artifacts/{Guid.NewGuid()}/extract-text", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST .../artifacts/{id}/transcribe ───────────────────────────────────

    [Fact]
    public async Task Transcribe_ArtifactNotFound_Returns404()
    {
        var process = await CreateProcess("Transcribe Process");
        var response = await _client.PostAsync(
            $"/api/v1/processes/{process!.Id}/artifacts/{Guid.NewGuid()}/transcribe", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST .../tickets ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTicket_ValidBody_Returns201WithTicketDto()
    {
        var process = await CreateProcess("Ticket Process");

        var payload = new
        {
            title       = "AP Invoice automation issue",
            description = "Manual intervention required at step 3",
            priority    = "High"
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/processes/{process!.Id}/tickets", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ticketId");
    }

    [Fact]
    public async Task CreateTicket_InvalidPriority_Returns422()
    {
        var process = await CreateProcess("Ticket Priority Test");

        var payload = new
        {
            title       = "Test ticket",
            description = "A description",
            priority    = "UltraUrgent"   // invalid
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/processes/{process!.Id}/tickets", payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateTicket_ProcessNotFound_Returns404()
    {
        var payload = new { title = "T", description = "D", priority = "Low" };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/processes/{Guid.NewGuid()}/tickets", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST .../documents/generate ──────────────────────────────────────────

    [Fact]
    public async Task GenerateDocument_Markdown_Returns200WithFileDownload()
    {
        var process = await CreateProcess("Document Generation Process");

        var response = await _client.PostAsync(
            $"/api/v1/processes/{process!.Id}/documents/generate?format=markdown", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/markdown");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Document Generation Process");
    }

    [Fact]
    public async Task GenerateDocument_Html_Returns200WithHtmlContent()
    {
        var process = await CreateProcess("HTML Export Process");

        var response = await _client.PostAsync(
            $"/api/v1/processes/{process!.Id}/documents/generate?format=html", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public async Task GenerateDocument_Docx_Returns200WithBinaryContent()
    {
        var process = await CreateProcess("Word Export Process");

        var response = await _client.PostAsync(
            $"/api/v1/processes/{process!.Id}/documents/generate?format=docx", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        // Word documents start with the PK (ZIP) magic bytes
        bytes[0].Should().Be(0x50); // 'P'
        bytes[1].Should().Be(0x4B); // 'K'
    }

    [Fact]
    public async Task GenerateDocument_InvalidFormat_Returns422()
    {
        var process = await CreateProcess("Format Validation Process");

        var response = await _client.PostAsync(
            $"/api/v1/processes/{process!.Id}/documents/generate?format=exe", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GenerateDocument_ProcessNotFound_Returns404()
    {
        var response = await _client.PostAsync(
            $"/api/v1/processes/{Guid.NewGuid()}/documents/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ProcessDto?> CreateProcess(string name)
    {
        var payload = new { name, description = "Phase 4 test process", department = "IT", ownerId = "owner" };
        var response = await _client.PostAsJsonAsync("/api/v1/processes", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProcessDto>();
    }
}
