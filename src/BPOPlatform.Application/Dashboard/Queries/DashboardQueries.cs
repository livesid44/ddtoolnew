using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Dashboard.Queries;

// ── Dashboard KPI DTO ─────────────────────────────────────────────────────────

public record DashboardKpisDto(
    int TotalProcesses,
    IReadOnlyDictionary<string, int> ProcessesByStatus,
    double AvgAutomationScore,
    double AvgComplianceScore,
    int TotalArtifacts,
    int AnalyzedArtifacts);

// ── Get Dashboard KPIs Query ─────────────────────────────────────────────────

public record GetDashboardKpisQuery : IRequest<DashboardKpisDto>;

public class GetDashboardKpisQueryHandler(IProcessRepository processRepo, IArtifactRepository artifactRepo)
    : IRequestHandler<GetDashboardKpisQuery, DashboardKpisDto>
{
    public async Task<DashboardKpisDto> Handle(GetDashboardKpisQuery request, CancellationToken ct)
    {
        var processes = await processRepo.GetAllAsync(ct);
        var artifacts = await artifactRepo.GetAllAsync(ct);

        var byStatus = processes
            .GroupBy(p => p.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var avgAutomation = processes.Count > 0
            ? Math.Round(processes.Average(p => p.AutomationScore), 1)
            : 0;

        var avgCompliance = processes.Count > 0
            ? Math.Round(processes.Average(p => p.ComplianceScore), 1)
            : 0;

        return new DashboardKpisDto(
            TotalProcesses: processes.Count,
            ProcessesByStatus: byStatus,
            AvgAutomationScore: avgAutomation,
            AvgComplianceScore: avgCompliance,
            TotalArtifacts: artifacts.Count,
            AnalyzedArtifacts: artifacts.Count(a => a.IsAnalyzed));
    }
}
