using BPOPlatform.Domain.Interfaces;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// No-op fallback implementation of <see cref="IExternalTicketingService"/>.
/// Returns a synthesised ticket reference without calling any external system.
/// Registered automatically when no Power Automate flow URL is configured.
/// </summary>
public class NoOpTicketingService : IExternalTicketingService
{
    public Task<ExternalTicket> CreateTicketAsync(
        string title, string description, string processId, string priority, CancellationToken ct = default)
    {
        var ticketId = $"LOCAL-{Guid.NewGuid():N}";
        var result = new ExternalTicket(
            TicketId: ticketId,
            Url: $"https://placeholder.ticketing.example/tickets/{ticketId}",
            Status: "Created (No-Op – Power Automate not configured)");
        return Task.FromResult(result);
    }
}
