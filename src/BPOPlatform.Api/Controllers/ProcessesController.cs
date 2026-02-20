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
        var result = await mediator.Send(new GetProcessByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Get the workflow steps for a process.</summary>
    [HttpGet("{id:guid}/workflow-steps")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowStepDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkflowSteps(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetWorkflowStepsQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Create a new process.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProcessCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update process details.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProcessRequest body, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateProcessCommand(id, body.Name, body.Description, body.Department), ct);
        return Ok(result);
    }

    /// <summary>Delete a process.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteProcessCommand(id), ct);
        return NoContent();
    }

    /// <summary>Advance the status of a process through the workflow.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdvanceStatus(Guid id, [FromBody] AdvanceStatusRequest body, CancellationToken ct)
    {
        var result = await mediator.Send(new AdvanceProcessStatusCommand(id, body.NewStatus), ct);
        return Ok(result);
    }
}

public record UpdateProcessRequest(string Name, string Description, string Department);
public record AdvanceStatusRequest(string NewStatus);

