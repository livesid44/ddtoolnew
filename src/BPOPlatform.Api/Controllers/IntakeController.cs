using BPOPlatform.Application.Intake.Commands;
using BPOPlatform.Application.Intake.DTOs;
using BPOPlatform.Application.Intake.Queries;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BPOPlatform.Api.Controllers;

/// <summary>
/// Intake Module – guided AI-assisted onboarding of new business processes.
/// Flow: Start → Chat (collect meta) → Submit → Upload artifacts → Analyse → Promote to project.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class IntakeController(IMediator mediator, ICurrentUserService currentUser) : ControllerBase
{
    // ── Start a new intake ────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new intake request and returns the initial AI greeting with an opening question.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(IntakeDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Start(
        [FromBody] StartIntakeRequest body,
        CancellationToken ct)
    {
        var ownerId = currentUser.UserId?.ToString() ?? body.OwnerId ?? "anonymous";
        var result = await mediator.Send(
            new StartIntakeCommand(ownerId, body.QueuePriority ?? "Medium"), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // ── Send a chat message ───────────────────────────────────────────────────

    /// <summary>
    /// Sends a user message in the intake chat; returns the AI response and extracted meta fields.
    /// Repeat until <c>isComplete</c> is true, then call <c>POST /intake/{id}/submit</c>.
    /// </summary>
    [HttpPost("{id:guid}/chat")]
    [ProducesResponseType(typeof(IntakeChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Chat(
        Guid id,
        [FromBody] ChatMessageRequest body,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SendIntakeChatCommand(id, body.Message), ct);
        return Ok(result);
    }

    // ── Submit meta information ───────────────────────────────────────────────

    /// <summary>
    /// Finalises meta collection and advances the intake to Submitted status.
    /// After this the user can upload documents via <c>POST /intake/{id}/artifacts</c>.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(IntakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromBody] SubmitIntakeMetaRequest body,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SubmitIntakeMetaCommand(
            id, body.Title, body.Description, body.Department,
            body.Location, body.BusinessUnit, body.ContactEmail,
            body.QueuePriority ?? "Medium"), ct);
        return Ok(result);
    }

    // ── Upload artifact ───────────────────────────────────────────────────────

    /// <summary>
    /// Uploads a document, video, or audio file to the intake for AI analysis.
    /// Accepts multipart/form-data with a <c>file</c> field.
    /// </summary>
    [HttpPost("{id:guid}/artifacts")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IntakeArtifactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadArtifact(
        Guid id,
        IFormFile file,
        [FromForm] string? artifactType,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        if (!Enum.TryParse<ArtifactType>(artifactType, true, out var type))
            type = InferArtifactType(file.FileName);

        var result = await mediator.Send(new UploadIntakeArtifactCommand(
            id, file.FileName, type, ms.ToArray(),
            file.ContentType, file.Length), ct);

        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    // ── Run AI analysis ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs AI analysis on the intake and returns a brief, checkpoints, and actionables.
    /// Call after uploading at least one artifact for best results.
    /// </summary>
    [HttpPost("{id:guid}/analyse")]
    [ProducesResponseType(typeof(IntakeAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Analyse(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new AnalyseIntakeCommand(id), ct);
        return Ok(result);
    }

    // ── Promote to project ────────────────────────────────────────────────────

    /// <summary>
    /// Promotes the analysed intake into a full Process project.
    /// Creates the Process, copies artifacts, and returns the new ProcessDto.
    /// </summary>
    [HttpPost("{id:guid}/promote")]
    [ProducesResponseType(typeof(ProcessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Promote(Guid id, CancellationToken ct)
    {
        var dto = await mediator.Send(new PromoteIntakeCommand(id), ct);
        return CreatedAtAction(
            nameof(ProcessesController.GetById),
            "Processes",
            new { id = dto.Id },
            dto);
    }

    // ── Get intake by ID ──────────────────────────────────────────────────────

    /// <summary>Get an intake request by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IntakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetIntakeByIdQuery(id), ct);
        return Ok(result);
    }

    // ── List my intakes ───────────────────────────────────────────────────────

    /// <summary>Lists all intake requests for the current user (SuperAdmin sees own; others see own).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IntakeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyIntakes(CancellationToken ct)
    {
        var ownerId = currentUser.UserId?.ToString() ?? string.Empty;
        var result = await mediator.Send(new GetMyIntakesQuery(ownerId), ct);
        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ArtifactType InferArtifactType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"                   => ArtifactType.Pdf,
            ".mp4" or ".avi" or ".mov" or ".mkv" or ".webm" => ArtifactType.Video,
            ".mp3" or ".wav" or ".m4a" or ".ogg" or ".flac" => ArtifactType.Audio,
            ".txt" or ".vtt" or ".srt"                       => ArtifactType.Transcription,
            ".xlsx" or ".xls" or ".csv"                      => ArtifactType.Spreadsheet,
            _                        => ArtifactType.Other
        };
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record StartIntakeRequest(string? OwnerId, string? QueuePriority);
public record ChatMessageRequest(string Message);
public record SubmitIntakeMetaRequest(
    string Title,
    string? Description,
    string Department,
    string? Location,
    string? BusinessUnit,
    string? ContactEmail,
    string? QueuePriority);
