using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Functions;

/// <summary>
/// HTTP-triggered Azure Function that starts a Durable process orchestration.
/// Call: POST /api/processes/{processId}/orchestrate
/// Response: 202 Accepted with management URLs for the orchestration instance.
/// </summary>
public class ProcessOrchestrationTrigger(ILogger<ProcessOrchestrationTrigger> logger)
{
    [Function(nameof(ProcessOrchestrationTrigger))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "processes/{processId}/orchestrate")]
        HttpRequestData req,
        string processId,
        [DurableClient] DurableTaskClient client,
        FunctionContext context,
        CancellationToken ct)
    {
        if (!Guid.TryParse(processId, out var processGuid))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("processId must be a valid GUID.");
            return badRequest;
        }

        logger.LogInformation("Starting process orchestration for {ProcessId}", processId);

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ProcessOrchestrator),
            processGuid,
            null,
            ct);

        logger.LogInformation("Orchestration started: {InstanceId}", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId, ct);
    }
}
