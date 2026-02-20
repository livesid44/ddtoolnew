using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Analysis.Commands;

// ── AI Analysis result DTO ────────────────────────────────────────────────────

public record AiAnalysisResultDto(
    double AutomationPotentialScore,
    double ComplianceScore,
    IReadOnlyList<string> KeyInsights,
    IReadOnlyList<string> Recommendations,
    double ConfidenceScore);

// ── Analyse Process Command ───────────────────────────────────────────────────

/// <summary>
/// Runs AI analysis on a process (using its description and uploaded artifacts),
/// then updates the process's AutomationScore and ComplianceScore.
/// When Azure OpenAI is not configured, returns representative mock results (dev mode).
/// </summary>
public record AnalyseProcessCommand(Guid ProcessId) : IRequest<AiAnalysisResultDto>;

public class AnalyseProcessCommandHandler(
    IProcessRepository processRepo,
    IArtifactRepository artifactRepo,
    IAiAnalysisService aiService,
    IUnitOfWork uow)
    : IRequestHandler<AnalyseProcessCommand, AiAnalysisResultDto>
{
    public async Task<AiAnalysisResultDto> Handle(AnalyseProcessCommand request, CancellationToken ct)
    {
        var process = await processRepo.GetByIdAsync(request.ProcessId, ct)
                      ?? throw new KeyNotFoundException($"Process {request.ProcessId} not found.");

        var artifacts = await artifactRepo.GetByProcessIdAsync(request.ProcessId, ct);
        var artifactTexts = artifacts.Select(a => a.FileName).ToList();

        var result = await aiService.AnalyzeProcessAsync(process.Description, artifactTexts, ct);

        process.UpdateScores(result.AutomationPotentialScore, result.ComplianceScore);
        await uow.SaveChangesAsync(ct);

        return new AiAnalysisResultDto(
            result.AutomationPotentialScore,
            result.ComplianceScore,
            result.KeyInsights,
            result.Recommendations,
            result.ConfidenceScore);
    }
}
