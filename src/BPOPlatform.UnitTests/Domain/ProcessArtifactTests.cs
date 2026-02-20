using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using FluentAssertions;

namespace BPOPlatform.UnitTests.Domain;

/// <summary>Tests for ProcessArtifact value object.</summary>
public class ProcessArtifactTests
{
    [Fact]
    public void Create_WithValidArguments_ReturnsArtifact()
    {
        var processId = Guid.NewGuid();
        var artifact = ProcessArtifact.Create(processId, "invoice.pdf", ArtifactType.Pdf, "blobs/invoice.pdf", 102_400);

        artifact.ProcessId.Should().Be(processId);
        artifact.FileName.Should().Be("invoice.pdf");
        artifact.ArtifactType.Should().Be(ArtifactType.Pdf);
        artifact.BlobPath.Should().Be("blobs/invoice.pdf");
        artifact.FileSizeBytes.Should().Be(102_400);
        artifact.IsAnalyzed.Should().BeFalse();
        artifact.ConfidenceScore.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "blob/path")]
    [InlineData("", "blob/path")]
    [InlineData("file.pdf", null)]
    [InlineData("file.pdf", "")]
    public void Create_WithInvalidArguments_Throws(string? fileName, string? blobPath)
    {
        var act = () => ProcessArtifact.Create(Guid.NewGuid(), fileName!, ArtifactType.Pdf, blobPath!, 100);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAnalyzed_SetsIsAnalyzedAndScore()
    {
        var artifact = ProcessArtifact.Create(Guid.NewGuid(), "file.mp4", ArtifactType.Video, "blobs/file.mp4", 5_000_000);

        artifact.MarkAnalyzed(0.87);

        artifact.IsAnalyzed.Should().BeTrue();
        artifact.ConfidenceScore.Should().Be(0.87);
        artifact.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAnalyzed_ScoreAbove1_ClampedTo1()
    {
        var artifact = ProcessArtifact.Create(Guid.NewGuid(), "file.pdf", ArtifactType.Pdf, "b/f.pdf", 100);
        artifact.MarkAnalyzed(1.5);
        artifact.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void MarkAnalyzed_ScoreBelow0_ClampedTo0()
    {
        var artifact = ProcessArtifact.Create(Guid.NewGuid(), "file.pdf", ArtifactType.Pdf, "b/f.pdf", 100);
        artifact.MarkAnalyzed(-0.3);
        artifact.ConfidenceScore.Should().Be(0.0);
    }
}
