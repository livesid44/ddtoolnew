using Microsoft.AspNetCore.SignalR;

namespace BPOPlatform.Api.Hubs;

/// <summary>
/// SignalR hub for real-time platform notifications pushed to connected browser clients.
/// Client groups:
///   "all"          – every authenticated user
///   "process-{id}" – users watching a specific process
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// Called automatically when a client connects.
    /// Adds the client to the "all" group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Subscribe the calling client to updates for a specific process.
    /// </summary>
    public async Task SubscribeToProcess(string processId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"process-{processId}");
    }

    /// <summary>
    /// Unsubscribe the calling client from a specific process group.
    /// </summary>
    public async Task UnsubscribeFromProcess(string processId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"process-{processId}");
    }
}
