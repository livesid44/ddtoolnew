using BPOPlatform.Application.Intake.DTOs;
using BPOPlatform.Application.Intake.Mappings;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Application.Artifacts.Commands;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;
using System.Text.Json;

namespace BPOPlatform.Application.Intake.Commands;

// ── Start Intake ──────────────────────────────────────────────────────────────

/// <summary>Creates a new IntakeRequest in Draft status and returns the initial AI greeting.</summary>
public record StartIntakeCommand(string OwnerId, string QueuePriority = "Medium") : IRequest<IntakeDto>;

public class StartIntakeCommandHandler(IIntakeRepository repo, IIntakeChatService chatService, IUnitOfWork uow)
    : IRequestHandler<StartIntakeCommand, IntakeDto>
{
    public async Task<IntakeDto> Handle(StartIntakeCommand request, CancellationToken ct)
    {
        // Get the opening greeting from the chat service
        var initial = await chatService.SendMessageAsync(
            [],
            "Hello, I'd like to submit a new process for review.",
            new IntakeMetaFields(),
            ct);

        var intake = IntakeRequest.Create("New Intake", request.OwnerId, request.QueuePriority);
        var history = new List<IntakeChatMessage>
        {
            new("user",      "Hello, I'd like to submit a new process for review.", DateTime.UtcNow),
            new("assistant", initial.AssistantMessage,                               DateTime.UtcNow)
        };
        intake.UpdateChatHistory(JsonSerializer.Serialize(history));

        await repo.AddAsync(intake, ct);
        await uow.SaveChangesAsync(ct);
        return intake.ToDto();
    }
}

// ── Send Chat Message ─────────────────────────────────────────────────────────

/// <summary>Sends a user message and returns the AI assistant response + updated meta fields.</summary>
public record SendIntakeChatCommand(
    Guid IntakeRequestId,
    string UserMessage
) : IRequest<IntakeChatResponseDto>;

public class SendIntakeChatCommandValidator : AbstractValidator<SendIntakeChatCommand>
{
    public SendIntakeChatCommandValidator()
    {
        RuleFor(x => x.IntakeRequestId).NotEmpty();
        RuleFor(x => x.UserMessage).NotEmpty().MaximumLength(4000);
    }
}

public class SendIntakeChatCommandHandler(
    IIntakeRepository repo,
    IIntakeChatService chatService,
    IUnitOfWork uow)
    : IRequestHandler<SendIntakeChatCommand, IntakeChatResponseDto>
{
    public async Task<IntakeChatResponseDto> Handle(SendIntakeChatCommand request, CancellationToken ct)
    {
        var intake = await repo.GetByIdAsync(request.IntakeRequestId, ct)
                     ?? throw new KeyNotFoundException($"Intake {request.IntakeRequestId} not found.");

        if (intake.Status != Domain.Enums.IntakeStatus.Draft)
            throw new InvalidOperationException("Chat is only available for Draft intakes.");

        // Deserialise chat history
        var history = JsonSerializer.Deserialize<List<IntakeChatMessage>>(intake.ChatHistoryJson)
                      ?? [];

        // Reconstruct current fields from intake entity
        var currentFields = new IntakeMetaFields(
            intake.Title == "New Intake" ? null : intake.Title,
            intake.Department,
            intake.Location,
            intake.Description,
            intake.QueuePriority,
            intake.ContactEmail,
            intake.BusinessUnit);

        var response = await chatService.SendMessageAsync(history, request.UserMessage, currentFields, ct);

        // Append both turns to history
        history.Add(new IntakeChatMessage("user",      request.UserMessage,        DateTime.UtcNow));
        history.Add(new IntakeChatMessage("assistant", response.AssistantMessage, DateTime.UtcNow));
        intake.UpdateChatHistory(JsonSerializer.Serialize(history));

        // Apply any extracted fields back onto the intake entity
        var f = response.UpdatedFields;
        intake.UpdateMeta(
            f.Title       ?? intake.Title,
            f.Description ?? intake.Description,
            f.Department  ?? intake.Department,
            f.Location    ?? intake.Location,
            f.BusinessUnit ?? intake.BusinessUnit,
            f.ContactEmail ?? intake.ContactEmail,
            f.QueuePriority ?? intake.QueuePriority);

        await uow.SaveChangesAsync(ct);

        return new IntakeChatResponseDto(
            response.AssistantMessage,
            new IntakeMetaFieldsDto(
                intake.Title == "New Intake" ? f.Title : intake.Title,
                intake.Department,
                intake.Location,
                intake.Description,
                intake.QueuePriority,
                intake.ContactEmail,
                intake.BusinessUnit),
            response.IsComplete);
    }
}

// ── Submit Meta ───────────────────────────────────────────────────────────────

/// <summary>Finalises the meta-collection step and advances status to Submitted.</summary>
public record SubmitIntakeMetaCommand(
    Guid IntakeRequestId,
    string Title,
    string? Description,
    string Department,
    string? Location,
    string? BusinessUnit,
    string? ContactEmail,
    string QueuePriority = "Medium"
) : IRequest<IntakeDto>;

public class SubmitIntakeMetaCommandValidator : AbstractValidator<SubmitIntakeMetaCommand>
{
    public SubmitIntakeMetaCommandValidator()
    {
        RuleFor(x => x.IntakeRequestId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.QueuePriority)
            .Must(p => new[] { "Low", "Medium", "High", "Critical" }.Contains(p))
            .WithMessage("QueuePriority must be Low, Medium, High, or Critical.");
    }
}

public class SubmitIntakeMetaCommandHandler(IIntakeRepository repo, IUnitOfWork uow)
    : IRequestHandler<SubmitIntakeMetaCommand, IntakeDto>
{
    public async Task<IntakeDto> Handle(SubmitIntakeMetaCommand request, CancellationToken ct)
    {
        var intake = await repo.GetByIdAsync(request.IntakeRequestId, ct)
                     ?? throw new KeyNotFoundException($"Intake {request.IntakeRequestId} not found.");

        intake.UpdateMeta(request.Title, request.Description, request.Department,
            request.Location, request.BusinessUnit, request.ContactEmail, request.QueuePriority);
        intake.Submit();

        await uow.SaveChangesAsync(ct);
        return intake.ToDto();
    }
}

// ── Upload Intake Artifact ────────────────────────────────────────────────────

public record UploadIntakeArtifactCommand(
    Guid IntakeRequestId,
    string FileName,
    ArtifactType ArtifactType,
    byte[] Content,
    string ContentType,
    long FileSizeBytes
) : IRequest<IntakeArtifactDto>;

public class UploadIntakeArtifactCommandValidator : AbstractValidator<UploadIntakeArtifactCommand>
{
    private const long MaxFileSizeBytes = 200 * 1024 * 1024; // 200 MB

    public UploadIntakeArtifactCommandValidator()
    {
        RuleFor(x => x.IntakeRequestId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotNull().NotEmpty();
        RuleFor(x => x.FileSizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxFileSizeBytes);
    }
}

public class UploadIntakeArtifactCommandHandler(
    IIntakeRepository intakeRepo,
    IBlobStorageService blobService,
    IUnitOfWork uow)
    : IRequestHandler<UploadIntakeArtifactCommand, IntakeArtifactDto>
{
    private const string ContainerName = "intake-artifacts";

    public async Task<IntakeArtifactDto> Handle(UploadIntakeArtifactCommand request, CancellationToken ct)
    {
        var intake = await intakeRepo.GetByIdAsync(request.IntakeRequestId, ct)
                     ?? throw new KeyNotFoundException($"Intake {request.IntakeRequestId} not found.");

        if (intake.Status == Domain.Enums.IntakeStatus.Promoted)
            throw new InvalidOperationException("Cannot upload artifacts to a promoted intake.");

        var blobName = $"intake/{request.IntakeRequestId}/{Guid.NewGuid()}_{request.FileName}";
        using var stream = new MemoryStream(request.Content);
        var blobPath = await blobService.UploadAsync(ContainerName, blobName, stream, request.ContentType, ct);

        var artifact = IntakeArtifact.Create(request.IntakeRequestId, request.FileName, request.ArtifactType, blobPath, request.FileSizeBytes);
        intake.AddArtifact(artifact);

        await uow.SaveChangesAsync(ct);
        return artifact.ToDto();
    }
}

// ── Analyse Intake ────────────────────────────────────────────────────────────

/// <summary>Runs AI analysis on the intake and returns brief + checkpoints + actionables.</summary>
public record AnalyseIntakeCommand(Guid IntakeRequestId) : IRequest<IntakeAnalysisDto>;

public class AnalyseIntakeCommandHandler(
    IIntakeRepository repo,
    IIntakeChatService chatService,
    IUnitOfWork uow)
    : IRequestHandler<AnalyseIntakeCommand, IntakeAnalysisDto>
{
    public async Task<IntakeAnalysisDto> Handle(AnalyseIntakeCommand request, CancellationToken ct)
    {
        var intake = await repo.GetByIdAsync(request.IntakeRequestId, ct)
                     ?? throw new KeyNotFoundException($"Intake {request.IntakeRequestId} not found.");

        var artifactTexts = intake.Artifacts
            .Select(a => a.ExtractedText ?? a.FileName)
            .ToList();

        var result = await chatService.AnalyseIntakeAsync(intake.Title, intake.Description, artifactTexts, ct);

        intake.SetAnalysisResults(
            result.Brief,
            JsonSerializer.Serialize(result.Checkpoints),
            JsonSerializer.Serialize(result.Actionables));

        await uow.SaveChangesAsync(ct);

        return new IntakeAnalysisDto(intake.Id, result.Brief, result.Checkpoints, result.Actionables);
    }
}

// ── Promote Intake to Project ─────────────────────────────────────────────────

/// <summary>
/// Promotes an analysed IntakeRequest to a full Process project:
/// creates the Process, copies intake artifacts as ProcessArtifacts, marks intake as Promoted.
/// </summary>
public record PromoteIntakeCommand(Guid IntakeRequestId) : IRequest<ProcessDto>;

public class PromoteIntakeCommandHandler(
    IIntakeRepository intakeRepo,
    IProcessRepository processRepo,
    IArtifactRepository artifactRepo,
    IUnitOfWork uow)
    : IRequestHandler<PromoteIntakeCommand, ProcessDto>
{
    public async Task<ProcessDto> Handle(PromoteIntakeCommand request, CancellationToken ct)
    {
        var intake = await intakeRepo.GetByIdAsync(request.IntakeRequestId, ct)
                     ?? throw new KeyNotFoundException($"Intake {request.IntakeRequestId} not found.");

        if (intake.Status != Domain.Enums.IntakeStatus.Analysed)
            throw new InvalidOperationException("Only an Analysed intake can be promoted to a project.");

        // Create the process
        var process = Domain.Entities.Process.Create(
            intake.Title,
            intake.Description ?? string.Empty,
            intake.Department ?? "General",
            intake.OwnerId);

        await processRepo.AddAsync(process, ct);

        // Copy intake artifacts to process artifacts
        foreach (var ia in intake.Artifacts)
        {
            var pa = ProcessArtifact.Create(process.Id, ia.FileName, ia.ArtifactType, ia.BlobPath, ia.FileSizeBytes);
            if (ia.ExtractedText is not null)
                pa.SetExtractedText(ia.ExtractedText);
            process.AddArtifact(pa);
            await artifactRepo.AddAsync(pa, ct);
        }

        intake.MarkPromoted(process.Id);

        await uow.SaveChangesAsync(ct);
        return process.ToDto();
    }
}
