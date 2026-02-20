using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.SpeechTranscription.Commands;

/// <summary>
/// Transcribes an audio artifact (MP3, WAV, M4A) using Azure AI Speech Services.
/// The transcript is stored on the <see cref="Domain.Entities.ProcessArtifact"/> record
/// and used as context for subsequent AI analysis.
/// When Speech Services is not configured, a mock service is used instead.
/// </summary>
public record TranscribeAudioCommand(Guid ProcessId, Guid ArtifactId) : IRequest<string>;

public class TranscribeAudioCommandHandler(
    IArtifactRepository artifactRepo,
    IBlobStorageService blobService,
    ISpeechTranscriptionService speechService,
    IUnitOfWork uow)
    : IRequestHandler<TranscribeAudioCommand, string>
{
    private const string ContainerName = "process-artifacts";

    public async Task<string> Handle(TranscribeAudioCommand request, CancellationToken ct)
    {
        var artifact = await artifactRepo.GetByIdAsync(request.ArtifactId, ct)
                       ?? throw new KeyNotFoundException($"Artifact {request.ArtifactId} not found.");

        if (artifact.ProcessId != request.ProcessId)
            throw new KeyNotFoundException(
                $"Artifact {request.ArtifactId} does not belong to process {request.ProcessId}.");

        // Download the raw audio blob
        await using var stream = await blobService.DownloadAsync(ContainerName, artifact.BlobPath, ct);

        // Infer MIME type from file extension (fallback to audio/wav)
        var contentType = Path.GetExtension(artifact.FileName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            _ => "audio/wav"
        };

        // Transcribe
        var transcript = await speechService.TranscribeAsync(stream, contentType, ct);

        // Persist the transcript on the artifact
        artifact.SetExtractedText(transcript);
        await uow.SaveChangesAsync(ct);

        return transcript;
    }
}
