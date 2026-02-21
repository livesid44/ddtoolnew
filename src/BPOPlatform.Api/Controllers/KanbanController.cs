using BPOPlatform.Application.Kanban.Commands;
using BPOPlatform.Application.Kanban.Queries;
using BPOPlatform.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Produces("application/json")]
public class KanbanController(IMediator mediator) : ControllerBase
{
    // ── Board ────────────────────────────────────────────────────────────────

    /// <summary>Get the full Kanban board for a process, grouped by column.</summary>
    [HttpGet("api/v1/processes/{processId:guid}/kanban")]
    [ProducesResponseType(typeof(KanbanBoardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoard(Guid processId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetKanbanBoardQuery(processId), ct);
        return Ok(result);
    }

    // ── Cards ────────────────────────────────────────────────────────────────

    /// <summary>Create a new Kanban card for a process.</summary>
    [HttpPost("api/v1/processes/{processId:guid}/kanban/cards")]
    [ProducesResponseType(typeof(KanbanCardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCard(
        Guid processId,
        [FromBody] CreateKanbanCardRequest body,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateKanbanCardCommand(processId, body.Title, body.Description, body.Column, body.Priority, body.AssignedTo), ct);
        return CreatedAtAction(nameof(GetBoard), new { processId }, result);
    }

    /// <summary>Update a Kanban card's title, description, priority, or assignee.</summary>
    [HttpPut("api/v1/kanban/cards/{cardId:guid}")]
    [ProducesResponseType(typeof(KanbanCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCard(
        Guid cardId,
        [FromBody] UpdateKanbanCardRequest body,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateKanbanCardCommand(cardId, body.Title, body.Description, body.Priority, body.AssignedTo), ct);
        return Ok(result);
    }

    /// <summary>Move a Kanban card to a different column / position.</summary>
    [HttpPatch("api/v1/kanban/cards/{cardId:guid}/move")]
    [ProducesResponseType(typeof(KanbanCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveCard(
        Guid cardId,
        [FromBody] MoveKanbanCardRequest body,
        CancellationToken ct)
    {
        var result = await mediator.Send(new MoveKanbanCardCommand(cardId, body.NewColumn, body.NewPosition), ct);
        return Ok(result);
    }

    /// <summary>Delete a Kanban card.</summary>
    [HttpDelete("api/v1/kanban/cards/{cardId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCard(Guid cardId, CancellationToken ct)
    {
        await mediator.Send(new DeleteKanbanCardCommand(cardId), ct);
        return NoContent();
    }
}

public record CreateKanbanCardRequest(
    string Title,
    string Description,
    string Column,
    TaskPriority Priority,
    string AssignedTo);

public record UpdateKanbanCardRequest(
    string Title,
    string Description,
    TaskPriority Priority,
    string AssignedTo);

public record MoveKanbanCardRequest(string NewColumn, int NewPosition);
