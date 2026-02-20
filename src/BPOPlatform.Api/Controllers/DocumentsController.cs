using BPOPlatform.Application.Documents.Commands;
using BPOPlatform.Application.Documents.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/processes/{processId:guid}/documents")]
[Produces("application/json")]
public class DocumentsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Generates structured process documentation using AI and returns it as a downloadable file.
    /// Supported formats: markdown (default), html, docx.
    /// When Azure OpenAI is configured, the LLM produces the content;
    /// otherwise a template-based fallback is used.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate(
        Guid processId,
        [FromQuery] string format = "markdown",
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GenerateProcessDocumentCommand(processId, format), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
