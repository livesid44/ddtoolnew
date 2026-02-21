using System.Net;
using System.Net.Http.Json;
using BPOPlatform.Application.Kanban.Commands;
using BPOPlatform.Application.Kanban.Queries;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Enums;
using FluentAssertions;

namespace BPOPlatform.IntegrationTests;

/// <summary>Integration tests for the Kanban board endpoints.</summary>
public class KanbanIntegrationTests(BpoApiFactory factory)
    : IClassFixture<BpoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetBoard_NewProcess_ReturnsEmptyColumns()
    {
        var process = await CreateProcess("Kanban Board Test");
        var response = await _client.GetAsync($"/api/v1/processes/{process!.Id}/kanban");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await response.Content.ReadFromJsonAsync<KanbanBoardDto>();
        board.Should().NotBeNull();
        board!.ProcessId.Should().Be(process.Id);
        board.Columns.Should().ContainKey("Todo");
        board.Columns.Should().ContainKey("InProgress");
        board.Columns.Should().ContainKey("Done");
        board.Columns["Todo"].Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCard_ValidBody_Returns201AndAppearsOnBoard()
    {
        var process = await CreateProcess("Card Creation Test");
        var cardPayload = new
        {
            title = "Fix authentication bug",
            description = "JWT token validation failing",
            column = "Todo",
            priority = (int)TaskPriority.High,
            assignedTo = "dev@example.com"
        };

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v1/processes/{process!.Id}/kanban/cards", cardPayload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var card = await createResponse.Content.ReadFromJsonAsync<KanbanCardDto>();
        card.Should().NotBeNull();
        card!.Title.Should().Be("Fix authentication bug");
        card.Column.Should().Be("Todo");
        card.Priority.Should().Be(TaskPriority.High);

        // Verify it appears on the board
        var boardResp = await _client.GetAsync($"/api/v1/processes/{process.Id}/kanban");
        var board = await boardResp.Content.ReadFromJsonAsync<KanbanBoardDto>();
        board!.Columns["Todo"].Should().HaveCount(1);
        board.Columns["Todo"][0].Title.Should().Be("Fix authentication bug");
    }

    [Fact]
    public async Task MoveCard_TodoToInProgress_UpdatesColumn()
    {
        var process = await CreateProcess("Move Card Test");
        var card = await CreateCard(process!.Id, "Move Me", "Todo");

        var movePayload = new { newColumn = "InProgress", newPosition = 0 };
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/kanban/cards/{card!.Id}/move", movePayload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var moved = await response.Content.ReadFromJsonAsync<KanbanCardDto>();
        moved!.Column.Should().Be("InProgress");
    }

    [Fact]
    public async Task UpdateCard_ValidBody_ChangesTitle()
    {
        var process = await CreateProcess("Update Card Test");
        var card = await CreateCard(process!.Id, "Old Title", "Todo");

        var update = new { title = "New Title", description = "updated desc", priority = 1, assignedTo = "" };
        var response = await _client.PutAsJsonAsync($"/api/v1/kanban/cards/{card!.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<KanbanCardDto>();
        updated!.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task DeleteCard_Existing_Returns204AndRemovedFromBoard()
    {
        var process = await CreateProcess("Delete Card Test");
        var card = await CreateCard(process!.Id, "To Delete", "Todo");

        var deleteResp = await _client.DeleteAsync($"/api/v1/kanban/cards/{card!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var boardResp = await _client.GetAsync($"/api/v1/processes/{process.Id}/kanban");
        var board = await boardResp.Content.ReadFromJsonAsync<KanbanBoardDto>();
        board!.Columns["Todo"].Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ProcessDto?> CreateProcess(string name)
    {
        var payload = new { name, description = "d", department = "IT", ownerId = "owner" };
        var r = await _client.PostAsJsonAsync("/api/v1/processes", payload);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<ProcessDto>();
    }

    private async Task<KanbanCardDto?> CreateCard(Guid processId, string title, string column)
    {
        var payload = new { title, description = "", column, priority = 1, assignedTo = "" };
        var r = await _client.PostAsJsonAsync($"/api/v1/processes/{processId}/kanban/cards", payload);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<KanbanCardDto>();
    }
}
