using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.DocumentIntelligence.Commands;

/// <summary>
/// Extracts text from a PDF (or image) artifact using Azure AI Document Intelligence.
/// The extracted text is stored on the <see cref="Domain.Entities.ProcessArtifact"/> record
/// so that subsequent AI analysis has richer context.
/// When Document Intelligence is not configured, a mock service is used instead.
/// </summary>
public record ExtractDocumentTextCommand(Guid ProcessId, Guid ArtifactId) : IRequest<string>;

public class ExtractDocumentTextCommandHandler(
    IArtifactRepository artifactRepo,
    IBlobStorageService blobService,
    IDocumentIntelligenceService docIntelligence,
    IUnitOfWork uow)
    : IRequestHandler<ExtractDocumentTextCommand, string>
{
    private const string ContainerName = "process-artifacts";

    public async Task<string> Handle(ExtractDocumentTextCommand request, CancellationToken ct)
    {
        var artifact = await artifactRepo.GetByIdAsync(request.ArtifactId, ct)
                       ?? throw new KeyNotFoundException($"Artifact {request.ArtifactId} not found.");

        if (artifact.ProcessId != request.ProcessId)
            throw new KeyNotFoundException(
                $"Artifact {request.ArtifactId} does not belong to process {request.ProcessId}.");

        // Download the raw blob content
        await using var stream = await blobService.DownloadAsync(ContainerName, artifact.BlobPath, ct);

        // Run document intelligence
        var extractedText = await docIntelligence.ExtractTextAsync(stream, artifact.FileName, ct);

        // Persist the extracted text on the artifact
        artifact.SetExtractedText(extractedText);
        await uow.SaveChangesAsync(ct);

        return extractedText;
    }
}
