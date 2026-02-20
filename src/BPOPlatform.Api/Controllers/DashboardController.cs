using BPOPlatform.Application.Analysis.Commands;
using BPOPlatform.Application.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
public class DashboardController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns platform-wide KPI metrics: process counts, avg scores, artifact totals.</summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(DashboardKpisDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpis(CancellationToken ct)
    {
        var result = await mediator.Send(new GetDashboardKpisQuery(), ct);
        return Ok(result);
    }

    /// <summary>Trigger AI analysis for a process and update its automation/compliance scores.</summary>
    [HttpPost("processes/{processId:guid}/analyse")]
    [ProducesResponseType(typeof(AiAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyseProcess(Guid processId, CancellationToken ct)
    {
        var result = await mediator.Send(new AnalyseProcessCommand(processId), ct);
        return Ok(result);
    }
}
