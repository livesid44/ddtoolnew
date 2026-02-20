using BPOPlatform.Application.Documents.DTOs;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace BPOPlatform.Application.Documents.Commands;

// ── Generate Process Document ─────────────────────────────────────────────────

/// <summary>
/// Generates structured process documentation for a given process.
/// Supported formats: "markdown" (default), "html", "docx".
/// When Azure OpenAI is configured, the LLM produces the content; otherwise a template is used.
/// </summary>
public record GenerateProcessDocumentCommand(Guid ProcessId, string Format = "markdown")
    : IRequest<DocumentOutputDto>;

public class GenerateProcessDocumentCommandValidator : AbstractValidator<GenerateProcessDocumentCommand>
{
    private static readonly HashSet<string> ValidFormats = ["markdown", "html", "docx"];

    public GenerateProcessDocumentCommandValidator()
    {
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Format)
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("Format must be one of: markdown, html, docx.");
    }
}

public class GenerateProcessDocumentCommandHandler(
    IProcessRepository processRepo,
    IArtifactRepository artifactRepo,
    IAiAnalysisService aiService,
    IDocumentGenerationService docGenService)
    : IRequestHandler<GenerateProcessDocumentCommand, DocumentOutputDto>
{
    public async Task<DocumentOutputDto> Handle(GenerateProcessDocumentCommand request, CancellationToken ct)
    {
        var process = await processRepo.GetByIdAsync(request.ProcessId, ct)
                      ?? throw new KeyNotFoundException($"Process {request.ProcessId} not found.");

        var artifacts = await artifactRepo.GetByProcessIdAsync(request.ProcessId, ct);
        var artifactTexts = artifacts
            .Select(a => a.ExtractedText ?? a.FileName)
            .ToList();

        // Run AI analysis to get fresh insights for the document
        var analysis = await aiService.AnalyzeProcessAsync(process.Description, artifactTexts, ct);

        var genRequest = new DocumentGenerationRequest(
            ProcessName: process.Name,
            Department: process.Department,
            Description: process.Description,
            AutomationScore: process.AutomationScore,
            ComplianceScore: process.ComplianceScore,
            KeyInsights: analysis.KeyInsights,
            Recommendations: analysis.Recommendations,
            Format: request.Format.ToLowerInvariant()
        );

        var result = await docGenService.GenerateAsync(genRequest, ct);
        return new DocumentOutputDto(result.FileName, result.ContentType, result.Content);
    }
}
