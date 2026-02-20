using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// An artifact (file) associated with a process, stored in Azure Blob Storage.
/// </summary>
public class ProcessArtifact : BaseEntity
{
    public Guid ProcessId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public ArtifactType ArtifactType { get; private set; }

    /// <summary>Relative path / blob name within the Azure Storage container.</summary>
    public string BlobPath { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }
    public bool IsAnalyzed { get; private set; }
    public double? ConfidenceScore { get; private set; }

    private ProcessArtifact() { }

    public static ProcessArtifact Create(Guid processId, string fileName, ArtifactType type, string blobPath, long fileSizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        return new ProcessArtifact
        {
            ProcessId = processId,
            FileName = fileName,
            ArtifactType = type,
            BlobPath = blobPath,
            FileSizeBytes = fileSizeBytes,
            IsAnalyzed = false
        };
    }

    public void MarkAnalyzed(double confidenceScore)
    {
        IsAnalyzed = true;
        ConfidenceScore = Math.Clamp(confidenceScore, 0, 1);
        UpdatedAt = DateTime.UtcNow;
    }
}
