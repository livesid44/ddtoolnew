using BPOPlatform.Application.Artifacts.Commands;
using BPOPlatform.Application.Artifacts.Queries;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/processes/{processId:guid}/artifacts")]
[Produces("application/json")]
public class ArtifactsController(IMediator mediator) : ControllerBase
{
    /// <summary>List all artifacts for a process.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ArtifactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid processId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetArtifactsByProcessQuery(processId), ct);
        return Ok(result);
    }

    /// <summary>Upload an artifact file to a process. Max 100 MB.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ArtifactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(104_857_600)] // 100 MB
    public async Task<IActionResult> Upload(
        Guid processId,
        IFormFile file,
        [FromQuery] ArtifactType artifactType = ArtifactType.Other,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        await using var stream = file.OpenReadStream();
        var bytes = new byte[file.Length];
        _ = await stream.ReadAsync(bytes, ct);

        var command = new UploadArtifactCommand(
            processId,
            file.FileName,
            artifactType,
            bytes,
            file.ContentType,
            file.Length);

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { processId }, result);
    }

    /// <summary>Get a short-lived download URL for an artifact (1 hour by default).</summary>
    [HttpGet("{artifactId:guid}/download-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl(
        Guid processId,
        Guid artifactId,
        [FromQuery] int expiryMinutes = 60,
        CancellationToken ct = default)
    {
        var uri = await mediator.Send(new GetArtifactDownloadUrlCommand(processId, artifactId, expiryMinutes), ct);
        return Ok(new { downloadUrl = uri.ToString(), expiresInMinutes = expiryMinutes });
    }

    /// <summary>Delete an artifact from a process.</summary>
    [HttpDelete("{artifactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid processId, Guid artifactId, CancellationToken ct)
    {
        await mediator.Send(new DeleteArtifactCommand(processId, artifactId), ct);
        return NoContent();
    }
}
