using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace BPOPlatform.Application.Artifacts.Commands;

// ── Upload Artifact ────────────────────────────────────────────────────────────

/// <summary>Uploads a file artifact to a process. File bytes are passed in-memory.</summary>
public record UploadArtifactCommand(
    Guid ProcessId,
    string FileName,
    ArtifactType ArtifactType,
    byte[] Content,
    string ContentType,
    long FileSizeBytes
) : IRequest<ArtifactDto>;

public class UploadArtifactCommandValidator : AbstractValidator<UploadArtifactCommand>
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    public UploadArtifactCommandValidator()
    {
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotNull().NotEmpty().WithMessage("File content is required.");
        RuleFor(x => x.FileSizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File must be between 1 byte and {MaxFileSizeBytes / 1024 / 1024} MB.");
    }
}

public class UploadArtifactCommandHandler(
    IProcessRepository processRepo,
    IArtifactRepository artifactRepo,
    IBlobStorageService blobService,
    IUnitOfWork uow)
    : IRequestHandler<UploadArtifactCommand, ArtifactDto>
{
    private const string ContainerName = "process-artifacts";

    public async Task<ArtifactDto> Handle(UploadArtifactCommand request, CancellationToken ct)
    {
        var process = await processRepo.GetByIdAsync(request.ProcessId, ct)
                      ?? throw new KeyNotFoundException($"Process {request.ProcessId} not found.");

        var blobName = $"{request.ProcessId}/{Guid.NewGuid()}_{request.FileName}";
        using var stream = new MemoryStream(request.Content);
        var blobPath = await blobService.UploadAsync(ContainerName, blobName, stream, request.ContentType, ct);

        var artifact = ProcessArtifact.Create(
            request.ProcessId, request.FileName, request.ArtifactType, blobPath, request.FileSizeBytes);

        process.AddArtifact(artifact);
        await artifactRepo.AddAsync(artifact, ct);
        await uow.SaveChangesAsync(ct);

        return artifact.ToDto();
    }
}

// ── Get Artifact Download URL ──────────────────────────────────────────────────

public record GetArtifactDownloadUrlCommand(
    Guid ProcessId,
    Guid ArtifactId,
    int ExpiryMinutes = 60
) : IRequest<Uri>;

public class GetArtifactDownloadUrlCommandHandler(
    IArtifactRepository artifactRepo,
    IBlobStorageService blobService)
    : IRequestHandler<GetArtifactDownloadUrlCommand, Uri>
{
    private const string ContainerName = "process-artifacts";

    public async Task<Uri> Handle(GetArtifactDownloadUrlCommand request, CancellationToken ct)
    {
        var artifact = await artifactRepo.GetByIdAsync(request.ArtifactId, ct)
                       ?? throw new KeyNotFoundException($"Artifact {request.ArtifactId} not found.");

        if (artifact.ProcessId != request.ProcessId)
            throw new KeyNotFoundException($"Artifact {request.ArtifactId} does not belong to process {request.ProcessId}.");

        return await blobService.GetDownloadUrlAsync(
            ContainerName, artifact.BlobPath, TimeSpan.FromMinutes(request.ExpiryMinutes), ct);
    }
}

// ── Delete Artifact ────────────────────────────────────────────────────────────

public record DeleteArtifactCommand(Guid ProcessId, Guid ArtifactId) : IRequest;

public class DeleteArtifactCommandHandler(
    IArtifactRepository artifactRepo,
    IBlobStorageService blobService,
    IUnitOfWork uow)
    : IRequestHandler<DeleteArtifactCommand>
{
    private const string ContainerName = "process-artifacts";

    public async Task Handle(DeleteArtifactCommand request, CancellationToken ct)
    {
        var artifact = await artifactRepo.GetByIdAsync(request.ArtifactId, ct)
                       ?? throw new KeyNotFoundException($"Artifact {request.ArtifactId} not found.");

        if (artifact.ProcessId != request.ProcessId)
            throw new KeyNotFoundException($"Artifact {request.ArtifactId} does not belong to process {request.ProcessId}.");

        await blobService.DeleteAsync(ContainerName, artifact.BlobPath, ct);
        await artifactRepo.DeleteAsync(artifact.Id, ct);
        await uow.SaveChangesAsync(ct);
    }
}
