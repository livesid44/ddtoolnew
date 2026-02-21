using BPOPlatform.Application.DocumentIntelligence.Commands;
using BPOPlatform.Application.SpeechTranscription.Commands;
using BPOPlatform.Application.Tickets.Commands;
using BPOPlatform.Application.Documents.DTOs;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace BPOPlatform.UnitTests.Application;

/// <summary>Unit tests for the Phase 4 command handlers.</summary>
public class Phase4CommandHandlerTests
{
    // ── ExtractDocumentTextCommandHandler ─────────────────────────────────────

    [Fact]
    public async Task ExtractDocumentText_ValidArtifact_ReturnsExtractedText()
    {
        var processId = Guid.NewGuid();
        var artifact = ProcessArtifact.Create(processId, "invoice.pdf", ArtifactType.Pdf, "path/invoice.pdf", 1024);

        var artifactRepo = new Mock<IArtifactRepository>();
        artifactRepo.Setup(r => r.GetByIdAsync(artifact.Id, default)).ReturnsAsync(artifact);

        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.DownloadAsync("process-artifacts", artifact.BlobPath, default))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        var docIntel = new Mock<IDocumentIntelligenceService>();
        docIntel.Setup(d => d.ExtractTextAsync(It.IsAny<Stream>(), "invoice.pdf", default))
            .ReturnsAsync("Extracted invoice text");

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new ExtractDocumentTextCommandHandler(
            artifactRepo.Object, blob.Object, docIntel.Object, uow.Object);

        var result = await handler.Handle(new ExtractDocumentTextCommand(processId, artifact.Id), default);

        result.Should().Be("Extracted invoice text");
        artifact.ExtractedText.Should().Be("Extracted invoice text");
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ExtractDocumentText_ArtifactNotFound_ThrowsKeyNotFoundException()
    {
        var artifactRepo = new Mock<IArtifactRepository>();
        artifactRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((ProcessArtifact?)null);

        var handler = new ExtractDocumentTextCommandHandler(
            artifactRepo.Object, new Mock<IBlobStorageService>().Object,
            new Mock<IDocumentIntelligenceService>().Object, new Mock<IUnitOfWork>().Object);

        var act = () => handler.Handle(new ExtractDocumentTextCommand(Guid.NewGuid(), Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ExtractDocumentText_ArtifactWrongProcess_ThrowsKeyNotFoundException()
    {
        var artifact = ProcessArtifact.Create(Guid.NewGuid(), "doc.pdf", ArtifactType.Pdf, "path/doc.pdf", 512);

        var artifactRepo = new Mock<IArtifactRepository>();
        artifactRepo.Setup(r => r.GetByIdAsync(artifact.Id, default)).ReturnsAsync(artifact);

        var handler = new ExtractDocumentTextCommandHandler(
            artifactRepo.Object, new Mock<IBlobStorageService>().Object,
            new Mock<IDocumentIntelligenceService>().Object, new Mock<IUnitOfWork>().Object);

        // Pass a different processId
        var act = () => handler.Handle(new ExtractDocumentTextCommand(Guid.NewGuid(), artifact.Id), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── TranscribeAudioCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task TranscribeAudio_Mp3Artifact_ReturnsTranscript()
    {
        var processId = Guid.NewGuid();
        var artifact = ProcessArtifact.Create(processId, "recording.mp3", ArtifactType.Audio, "path/recording.mp3", 2048);

        var artifactRepo = new Mock<IArtifactRepository>();
        artifactRepo.Setup(r => r.GetByIdAsync(artifact.Id, default)).ReturnsAsync(artifact);

        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.DownloadAsync("process-artifacts", artifact.BlobPath, default))
            .ReturnsAsync(new MemoryStream(new byte[200]));

        var speech = new Mock<ISpeechTranscriptionService>();
        speech.Setup(s => s.TranscribeAsync(It.IsAny<Stream>(), "audio/mpeg", default))
            .ReturnsAsync("Hello, this is a test transcript.");

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new TranscribeAudioCommandHandler(
            artifactRepo.Object, blob.Object, speech.Object, uow.Object);

        var result = await handler.Handle(new TranscribeAudioCommand(processId, artifact.Id), default);

        result.Should().Be("Hello, this is a test transcript.");
        artifact.ExtractedText.Should().Be("Hello, this is a test transcript.");
        // Speech should be called with the correct content type for .mp3
        speech.Verify(s => s.TranscribeAsync(It.IsAny<Stream>(), "audio/mpeg", default), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── CreateTicketCommandHandler ────────────────────────────────────────────

    [Fact]
    public async Task CreateTicket_ValidCommand_ReturnsTicketDto()
    {
        var process = BPOPlatform.Domain.Entities.Process.Create("AP Process", "desc", "Finance", "owner");

        var processRepo = new Mock<IProcessRepository>();
        processRepo.Setup(r => r.GetByIdAsync(process.Id, default))
            .ReturnsAsync(process);

        var ticketSvc = new Mock<IExternalTicketingService>();
        ticketSvc.Setup(t => t.CreateTicketAsync("Fix AP bug", "Details here", process.Id.ToString(), "High", default))
            .ReturnsAsync(new ExternalTicket("TKT-001", "https://example.com/TKT-001", "Created"));

        var handler = new CreateTicketCommandHandler(processRepo.Object, ticketSvc.Object);
        var result = await handler.Handle(
            new CreateTicketCommand(process.Id, "Fix AP bug", "Details here", "High"), default);

        result.Should().NotBeNull();
        result.TicketId.Should().Be("TKT-001");
        result.Url.Should().Be("https://example.com/TKT-001");
        result.Status.Should().Be("Created");
        result.ProcessId.Should().Be(process.Id.ToString());
    }

    [Fact]
    public async Task CreateTicket_ProcessNotFound_ThrowsKeyNotFoundException()
    {
        var processRepo = new Mock<IProcessRepository>();
        processRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((BPOPlatform.Domain.Entities.Process?)null);

        var handler = new CreateTicketCommandHandler(processRepo.Object, new Mock<IExternalTicketingService>().Object);
        var act = () => handler.Handle(
            new CreateTicketCommand(Guid.NewGuid(), "Title", "Desc"), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
