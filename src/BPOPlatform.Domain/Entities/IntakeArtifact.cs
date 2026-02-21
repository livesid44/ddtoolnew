using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// A file uploaded during the Intake process (before being promoted to a full ProcessArtifact).
/// </summary>
public class IntakeArtifact : BaseEntity
{
    public Guid IntakeRequestId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public ArtifactType ArtifactType { get; private set; }
    public string BlobPath { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }

    /// <summary>Text extracted from the document / audio transcription. Null until extraction runs.</summary>
    public string? ExtractedText { get; private set; }

    private IntakeArtifact() { }

    public static IntakeArtifact Create(Guid intakeRequestId, string fileName, ArtifactType type, string blobPath, long fileSizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        return new IntakeArtifact
        {
            IntakeRequestId = intakeRequestId,
            FileName = fileName,
            ArtifactType = type,
            BlobPath = blobPath,
            FileSizeBytes = fileSizeBytes
        };
    }

    public void SetExtractedText(string text)
    {
        ExtractedText = text;
        UpdatedAt = DateTime.UtcNow;
    }
}
