using BPOPlatform.Application.DocumentIntelligence.Commands;
using BPOPlatform.Application.Documents.DTOs;
using BPOPlatform.Application.SpeechTranscription.Commands;
using BPOPlatform.Application.Tickets.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/processes/{processId:guid}")]
[Produces("application/json")]
public class AutomationController(IMediator mediator) : ControllerBase
{
    // ── Document Intelligence ─────────────────────────────────────────────────

    /// <summary>
    /// Extracts text from a PDF or image artifact using Azure AI Document Intelligence.
    /// The extracted text is stored on the artifact record for use in subsequent AI analysis.
    /// </summary>
    [HttpPost("artifacts/{artifactId:guid}/extract-text")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExtractText(
        Guid processId,
        Guid artifactId,
        CancellationToken ct)
    {
        var text = await mediator.Send(new ExtractDocumentTextCommand(processId, artifactId), ct);
        return Ok(new { extractedText = text, charCount = text.Length });
    }

    // ── Speech Transcription ──────────────────────────────────────────────────

    /// <summary>
    /// Transcribes an audio artifact (MP3, WAV, M4A) using Azure AI Speech Services.
    /// The transcript is stored on the artifact record for use in subsequent AI analysis.
    /// </summary>
    [HttpPost("artifacts/{artifactId:guid}/transcribe")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transcribe(
        Guid processId,
        Guid artifactId,
        CancellationToken ct)
    {
        var transcript = await mediator.Send(new TranscribeAudioCommand(processId, artifactId), ct);
        return Ok(new { transcript, wordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length });
    }

    // ── External Ticketing ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a ticket in an external ticketing system (ServiceNow, Jira, Azure DevOps)
    /// via a Power Automate HTTP-triggered flow.
    /// </summary>
    [HttpPost("tickets")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTicket(
        Guid processId,
        [FromBody] CreateTicketRequest body,
        CancellationToken ct)
    {
        var ticket = await mediator.Send(
            new CreateTicketCommand(processId, body.Title, body.Description, body.Priority), ct);
        return CreatedAtAction(nameof(CreateTicket), new { processId }, ticket);
    }
}

/// <summary>Request body for ticket creation.</summary>
public record CreateTicketRequest(string Title, string Description, string Priority = "Medium");
