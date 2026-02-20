using System.Net;
using System.Net.Http.Json;
using BPOPlatform.Application.Common.DTOs;
using BPOPlatform.Application.Processes.DTOs;
using FluentAssertions;

namespace BPOPlatform.IntegrationTests;

/// <summary>Integration tests for the Processes API endpoints.</summary>
public class ProcessesIntegrationTests(BpoApiFactory factory)
    : IClassFixture<BpoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── GET /api/v1/processes ─────────────────────────────────────────────────

    [Fact]
    public async Task GetProcesses_ReturnsOkWithPagedResultShape()
    {
        var response = await _client.GetAsync("/api/v1/processes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ProcessSummaryDto>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProcesses_WithPaginationParams_ReturnsCorrectPage()
    {
        // Seed 3 processes
        for (int i = 1; i <= 3; i++)
            await _client.PostAsJsonAsync("/api/v1/processes",
                new { name = $"Process {i}", description = "d", department = "IT", ownerId = "owner" });

        var response = await _client.GetAsync("/api/v1/processes?page=1&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<ProcessSummaryDto>>();
        body!.Items.Count.Should().BeLessOrEqualTo(2);
        body.PageSize.Should().Be(2);
    }

    // ── POST /api/v1/processes ────────────────────────────────────────────────

    [Fact]
    public async Task CreateProcess_ValidBody_Returns201WithDto()
    {
        var payload = new
        {
            name = "AP Invoice Processing",
            description = "Accounts Payable workflow",
            department = "Finance",
            ownerId = "user-test"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/processes", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProcessDto>();
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("AP Invoice Processing");
        dto.Department.Should().Be("Finance");
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateProcess_MissingName_Returns422()
    {
        var payload = new { name = "", description = "d", department = "IT", ownerId = "u" };
        var response = await _client.PostAsJsonAsync("/api/v1/processes", payload);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── GET /api/v1/processes/{id} ────────────────────────────────────────────

    [Fact]
    public async Task GetProcessById_Existing_Returns200()
    {
        var created = await CreateProcess("Get By Id Test");
        var response = await _client.GetAsync($"/api/v1/processes/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProcessDto>();
        dto!.Id.Should().Be(created.Id);
        dto.Name.Should().Be("Get By Id Test");
    }

    [Fact]
    public async Task GetProcessById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/processes/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/v1/processes/{id}/workflow-steps ─────────────────────────────

    [Fact]
    public async Task GetWorkflowSteps_NewProcess_Returns5DefaultSteps()
    {
        var created = await CreateProcess("Workflow Steps Test");
        var response = await _client.GetAsync($"/api/v1/processes/{created!.Id}/workflow-steps");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var steps = await response.Content.ReadFromJsonAsync<List<WorkflowStepDto>>();
        steps.Should().HaveCount(5);
        steps![0].StepOrder.Should().Be(1);
        steps[0].StepName.Should().Be("Meta Information");
        steps[4].StepName.Should().Be("Deployment");
    }

    // ── PUT /api/v1/processes/{id} ────────────────────────────────────────────

    [Fact]
    public async Task UpdateProcess_ValidBody_Returns200WithUpdatedName()
    {
        var created = await CreateProcess("Original Name");
        var update = new { name = "Updated Name", description = "new desc", department = "Finance" };
        var response = await _client.PutAsJsonAsync($"/api/v1/processes/{created!.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProcessDto>();
        dto!.Name.Should().Be("Updated Name");
    }

    // ── PATCH /api/v1/processes/{id}/status ──────────────────────────────────

    [Fact]
    public async Task AdvanceStatus_ValidStatus_Returns200WithNewStatus()
    {
        var created = await CreateProcess("Status Test");
        var patch = new { newStatus = "InProgress" };
        var response = await _client.PatchAsJsonAsync($"/api/v1/processes/{created!.Id}/status", patch);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProcessDto>();
        dto!.Status.Should().Be(Domain.Enums.ProcessStatus.InProgress);
    }

    // ── DELETE /api/v1/processes/{id} ─────────────────────────────────────────

    [Fact]
    public async Task DeleteProcess_Existing_Returns204ThenGetReturns404()
    {
        var created = await CreateProcess("To Delete");
        var deleteResponse = await _client.DeleteAsync($"/api/v1/processes/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/processes/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /healthz ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    // ── Filtering & sorting ───────────────────────────────────────────────────

    [Fact]
    public async Task GetProcesses_FilterByDepartment_ReturnsOnlyMatchingProcesses()
    {
        await CreateProcess("Finance Process", "Finance");
        await CreateProcess("HR Process", "HR");

        var response = await _client.GetAsync("/api/v1/processes?department=Finance");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ProcessSummaryDto>>();
        body!.Items.Should().OnlyContain(p => p.Department == "Finance");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ProcessDto?> CreateProcess(string name, string department = "IT")
    {
        var payload = new { name, description = "test desc", department, ownerId = "owner" };
        var response = await _client.PostAsJsonAsync("/api/v1/processes", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProcessDto>();
    }
}
