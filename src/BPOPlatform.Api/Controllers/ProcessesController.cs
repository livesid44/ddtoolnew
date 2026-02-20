using BPOPlatform.Application.Processes.Commands;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Application.Processes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ProcessesController(IMediator mediator) : ControllerBase
{
    /// <summary>List all processes, optionally filtered by department.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? department, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllProcessesQuery(department), ct);
        return Ok(result);
    }

    /// <summary>Get a process by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new GetProcessByIdQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Create a new process.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProcessCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Advance the status of a process through the workflow.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdvanceStatus(Guid id, [FromBody] AdvanceStatusRequest body, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new AdvanceProcessStatusCommand(id, body.NewStatus), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record AdvanceStatusRequest(string NewStatus);
