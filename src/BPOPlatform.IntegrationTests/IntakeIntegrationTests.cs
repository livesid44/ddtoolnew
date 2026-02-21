using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BPOPlatform.Application.Intake.DTOs;
using BPOPlatform.Application.Processes.DTOs;
using FluentAssertions;
using Xunit;

namespace BPOPlatform.IntegrationTests;

public class IntakeIntegrationTests(BpoApiFactory factory)
    : IClassFixture<BpoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── POST /api/v1/intake/start ─────────────────────────────────────────────

    [Fact]
    public async Task Start_Returns201WithIntakeDto()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/intake/start", new { QueuePriority = "High" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<IntakeDto>(JsonOpts);
        body.Should().NotBeNull();
        body!.Status.Should().Be("Draft");
        body.OwnerId.Should().NotBeNullOrWhiteSpace();
    }

    // ── POST /api/v1/intake/{id}/chat ─────────────────────────────────────────

    [Fact]
    public async Task Chat_Returns200WithAssistantMessage()
    {
        var intake = await StartIntake();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/intake/{intake.Id}/chat",
            new { Message = "Invoice Automation Process" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IntakeChatResponseDto>(JsonOpts);
        body.Should().NotBeNull();
        body!.AssistantMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ── POST /api/v1/intake/{id}/submit ──────────────────────────────────────

    [Fact]
    public async Task Submit_Returns200WithSubmittedStatus()
    {
        var intake = await StartIntake();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/intake/{intake.Id}/submit",
            new
            {
                Title = "Invoice Automation",
                Description = "Automate accounts payable",
                Department = "Finance",
                Location = "London",
                QueuePriority = "High"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IntakeDto>(JsonOpts);
        body.Should().NotBeNull();
        body!.Status.Should().Be("Submitted");
        body.Title.Should().Be("Invoice Automation");
        body.Department.Should().Be("Finance");
    }

    // ── POST /api/v1/intake/{id}/analyse ─────────────────────────────────────

    [Fact]
    public async Task Analyse_Returns200WithBriefAndCheckpoints()
    {
        var intake = await SubmitIntake();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/intake/{intake.Id}/analyse", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IntakeAnalysisDto>(JsonOpts);
        body.Should().NotBeNull();
        body!.IntakeRequestId.Should().Be(intake.Id);
        body.Brief.Should().NotBeNullOrWhiteSpace();
        body.Checkpoints.Should().NotBeEmpty();
        body.Actionables.Should().NotBeEmpty();
    }

    // ── POST /api/v1/intake/{id}/promote ─────────────────────────────────────

    [Fact]
    public async Task Promote_Returns201WithProcessDto()
    {
        var intake = await AnalyseIntake();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/intake/{intake.Id}/promote", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ProcessDto>(JsonOpts);
        body.Should().NotBeNull();
        body!.Name.Should().Be("Invoice Automation");
        body.Department.Should().Be("Finance");
    }

    // ── GET /api/v1/intake/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_Returns200()
    {
        var intake = await StartIntake();

        var response = await _client.GetAsync($"/api/v1/intake/{intake.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IntakeDto>(JsonOpts);
        body.Should().NotBeNull();
        body!.Id.Should().Be(intake.Id);
    }

    // ── GET /api/v1/intake ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyIntakes_Returns200WithList()
    {
        // Create two intakes
        await StartIntake();
        await StartIntake();

        var response = await _client.GetAsync("/api/v1/intake");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<IntakeDto>>(JsonOpts);
        body.Should().NotBeNull();
        body!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── 404 paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Returns404ForUnknownId()
    {
        var response = await _client.GetAsync($"/api/v1/intake/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Chat_Returns404ForUnknownIntakeId()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/intake/{Guid.NewGuid()}/chat",
            new { Message = "hello" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Promote_Returns400ForSubmittedIntake()
    {
        var intake = await SubmitIntake();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/intake/{intake.Id}/promote", new { });

        // Not analysed yet → InvalidOperationException → 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<IntakeDto> StartIntake()
    {
        var r = await _client.PostAsJsonAsync("/api/v1/intake/start", new { QueuePriority = "Medium" });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IntakeDto>(JsonOpts))!;
    }

    private async Task<IntakeDto> SubmitIntake()
    {
        var intake = await StartIntake();
        var r = await _client.PostAsJsonAsync($"/api/v1/intake/{intake.Id}/submit", new
        {
            Title = "Invoice Automation",
            Description = "Automate accounts payable",
            Department = "Finance",
            Location = "London",
            QueuePriority = "High"
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IntakeDto>(JsonOpts))!;
    }

    private async Task<IntakeDto> AnalyseIntake()
    {
        var submitted = await SubmitIntake();
        var r = await _client.PostAsJsonAsync($"/api/v1/intake/{submitted.Id}/analyse", new { });
        r.EnsureSuccessStatusCode();
        // The analyse endpoint returns IntakeAnalysisDto; return the intake by its known ID
        return (await _client.GetFromJsonAsync<IntakeDto>($"/api/v1/intake/{submitted.Id}", JsonOpts))!;
    }
}
