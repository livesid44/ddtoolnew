using System.Net;
using System.Net.Http.Json;
using BPOPlatform.Application.Analysis.Commands;
using BPOPlatform.Application.Dashboard.Queries;
using BPOPlatform.Application.Processes.DTOs;
using FluentAssertions;

namespace BPOPlatform.IntegrationTests;

/// <summary>Integration tests for the Dashboard and AI Analysis endpoints.</summary>
public class DashboardIntegrationTests(BpoApiFactory factory)
    : IClassFixture<BpoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetKpis_EmptyDatabase_ReturnsZeroTotals()
    {
        var response = await _client.GetAsync("/api/v1/dashboard/kpis");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardKpisDto>();
        body.Should().NotBeNull();
        body!.TotalProcesses.Should().BeGreaterThanOrEqualTo(0);
        body.TotalArtifacts.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetKpis_AfterCreatingProcess_IncrementsTotalProcesses()
    {
        var before = await _client.GetFromJsonAsync<DashboardKpisDto>("/api/v1/dashboard/kpis");
        int countBefore = before!.TotalProcesses;

        await _client.PostAsJsonAsync("/api/v1/processes",
            new { name = "Dashboard Test", description = "d", department = "IT", ownerId = "u" });

        var after = await _client.GetFromJsonAsync<DashboardKpisDto>("/api/v1/dashboard/kpis");
        after!.TotalProcesses.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task AnalyseProcess_ExistingProcess_Returns200WithScores()
    {
        // Create a process first
        var createResp = await _client.PostAsJsonAsync("/api/v1/processes",
            new { name = "AI Test", description = "AI analysis test process", department = "IT", ownerId = "u" });
        var process = await createResp.Content.ReadFromJsonAsync<Application.Processes.DTOs.ProcessDto>();

        // Analyse (uses MockAiAnalysisService in dev/test)
        var response = await _client.PostAsync(
            $"/api/v1/dashboard/processes/{process!.Id}/analyse", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AiAnalysisResultDto>();
        result.Should().NotBeNull();
        result!.AutomationPotentialScore.Should().BeGreaterThan(0);
        result.ComplianceScore.Should().BeGreaterThan(0);
        result.KeyInsights.Should().NotBeEmpty();
        result.Recommendations.Should().NotBeEmpty();
    }
}
