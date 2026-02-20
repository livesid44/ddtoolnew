using BPOPlatform.Application.Kanban.Commands;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Kanban.Queries;

// ── Kanban Board DTO ─────────────────────────────────────────────────────────

/// <summary>Represents the full Kanban board for a process, grouped by column.</summary>
public record KanbanBoardDto(
    Guid ProcessId,
    IReadOnlyDictionary<string, IReadOnlyList<KanbanCardDto>> Columns);

// ── Get Kanban Board Query ────────────────────────────────────────────────────

public record GetKanbanBoardQuery(Guid ProcessId) : IRequest<KanbanBoardDto>;

public class GetKanbanBoardQueryHandler(IKanbanCardRepository repo)
    : IRequestHandler<GetKanbanBoardQuery, KanbanBoardDto>
{
    public async Task<KanbanBoardDto> Handle(GetKanbanBoardQuery request, CancellationToken ct)
    {
        var cards = await repo.GetByProcessIdAsync(request.ProcessId, ct);

        // Group cards by column, ordered by Position within each column
        var grouped = cards
            .GroupBy(c => c.Column)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<KanbanCardDto>)g.OrderBy(c => c.Position).Select(c => c.ToDto()).ToList());

        // Ensure all default columns are present, even if empty
        foreach (var col in KanbanColumns.All)
        {
            if (!grouped.ContainsKey(col))
                grouped[col] = [];
        }

        return new KanbanBoardDto(request.ProcessId, grouped);
    }
}
