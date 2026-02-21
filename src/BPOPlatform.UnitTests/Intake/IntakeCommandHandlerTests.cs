using BPOPlatform.Application.Intake.Commands;
using BPOPlatform.Application.Intake.Queries;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BPOPlatform.UnitTests.Intake;

public class IntakeCommandHandlerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mock<IUnitOfWork> FreshUow()
    {
        var m = new Mock<IUnitOfWork>();
        m.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        return m;
    }

    private static IntakeRequest CreateDraftIntake(string title = "Invoice Processing")
        => IntakeRequest.Create(title, "user-1");

    private static IntakeRequest CreateSubmittedIntake()
    {
        var i = CreateDraftIntake();
        i.UpdateMeta("Invoice Processing", "Automate AP workflow", "Finance", "London", null, null, "High");
        i.Submit();
        return i;
    }

    private static IntakeRequest CreateAnalysedIntake()
    {
        var i = CreateSubmittedIntake();
        i.SetAnalysisResults("Brief.", "[\"cp1\"]", "[\"act1\"]");
        return i;
    }

    // ── StartIntakeCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task StartIntake_CreatesIntakeAndReturnsGreeting()
    {
        var repo = new Mock<IIntakeRepository>();
        var chat = new Mock<IIntakeChatService>();
        chat.Setup(c => c.SendMessageAsync(
                It.IsAny<IReadOnlyList<IntakeChatMessage>>(),
                It.IsAny<string>(),
                It.IsAny<IntakeMetaFields>(),
                default))
            .ReturnsAsync(new IntakeChatServiceResponse("Hello! What is the process name?", new IntakeMetaFields(), false));

        var handler = new StartIntakeCommandHandler(repo.Object, chat.Object, FreshUow().Object);
        var result = await handler.Handle(new StartIntakeCommand("user-1"), default);

        result.OwnerId.Should().Be("user-1");
        result.Status.Should().Be("Draft");
        repo.Verify(r => r.AddAsync(It.IsAny<IntakeRequest>(), default), Times.Once);
    }

    // ── SendIntakeChatCommand ────────────────────────────────────────────────

    [Fact]
    public async Task SendChat_UpdatesFieldsAndReturnsChatResponse()
    {
        var intake = CreateDraftIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var updatedFields = new IntakeMetaFields("Invoice Processing", "Finance", null, null, null, null, null);
        var chat = new Mock<IIntakeChatService>();
        chat.Setup(c => c.SendMessageAsync(
                It.IsAny<IReadOnlyList<IntakeChatMessage>>(),
                "Invoice Processing",
                It.IsAny<IntakeMetaFields>(),
                default))
            .ReturnsAsync(new IntakeChatServiceResponse("Got it. Which department?", updatedFields, false));

        var handler = new SendIntakeChatCommandHandler(repo.Object, chat.Object, FreshUow().Object);
        var result = await handler.Handle(new SendIntakeChatCommand(intake.Id, "Invoice Processing"), default);

        result.AssistantMessage.Should().Be("Got it. Which department?");
        result.IsComplete.Should().BeFalse();
        result.CurrentFields.Title.Should().Be("Invoice Processing");
    }

    [Fact]
    public async Task SendChat_ThrowsWhenIntakeNotDraft()
    {
        var intake = CreateSubmittedIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var handler = new SendIntakeChatCommandHandler(repo.Object, new Mock<IIntakeChatService>().Object, FreshUow().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SendIntakeChatCommand(intake.Id, "hello"), default));
    }

    // ── SubmitIntakeMetaCommand ──────────────────────────────────────────────

    [Fact]
    public async Task SubmitMeta_AdvancesStatusToSubmitted()
    {
        var intake = CreateDraftIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var handler = new SubmitIntakeMetaCommandHandler(repo.Object, FreshUow().Object);
        var result = await handler.Handle(
            new SubmitIntakeMetaCommand(intake.Id, "Invoice Processing", "AP automation", "Finance", "London", null, null, "High"),
            default);

        result.Status.Should().Be("Submitted");
        result.Title.Should().Be("Invoice Processing");
        result.Department.Should().Be("Finance");
    }

    [Fact]
    public async Task SubmitMeta_ThrowsWhenIntakeNotFound()
    {
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((IntakeRequest?)null);

        var handler = new SubmitIntakeMetaCommandHandler(repo.Object, FreshUow().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new SubmitIntakeMetaCommand(Guid.NewGuid(), "T", null, "D", null, null, null), default));
    }

    // ── UploadIntakeArtifactCommand ──────────────────────────────────────────

    [Fact]
    public async Task UploadArtifact_AddsArtifactToIntake()
    {
        var intake = CreateSubmittedIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
            .ReturnsAsync("intake/test-path.pdf");

        var handler = new UploadIntakeArtifactCommandHandler(repo.Object, blob.Object, FreshUow().Object);
        var result = await handler.Handle(
            new UploadIntakeArtifactCommand(intake.Id, "invoice.pdf", ArtifactType.Pdf, new byte[] { 1, 2, 3 }, "application/pdf", 3),
            default);

        result.FileName.Should().Be("invoice.pdf");
        result.ArtifactType.Should().Be("Pdf");
        result.IntakeRequestId.Should().Be(intake.Id);
    }

    [Fact]
    public async Task UploadArtifact_ThrowsForPromotedIntake()
    {
        var intake = CreateAnalysedIntake();
        intake.MarkPromoted(Guid.NewGuid());
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var handler = new UploadIntakeArtifactCommandHandler(
            repo.Object, new Mock<IBlobStorageService>().Object, FreshUow().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UploadIntakeArtifactCommand(intake.Id, "f.pdf", ArtifactType.Pdf, [1], "application/pdf", 1), default));
    }

    // ── AnalyseIntakeCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task AnalyseIntake_SetsAnalysisResultsAndReturnsDto()
    {
        var intake = CreateSubmittedIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var chat = new Mock<IIntakeChatService>();
        chat.Setup(c => c.AnalyseIntakeAsync(intake.Title, intake.Description, It.IsAny<IEnumerable<string>>(), default))
            .ReturnsAsync(new IntakeAnalysisResult(
                "This is the brief.",
                new[] { "Checkpoint 1", "Checkpoint 2" },
                new[] { "Action 1", "Action 2" }));

        var handler = new AnalyseIntakeCommandHandler(repo.Object, chat.Object, FreshUow().Object);
        var result = await handler.Handle(new AnalyseIntakeCommand(intake.Id), default);

        result.Brief.Should().Be("This is the brief.");
        result.Checkpoints.Should().HaveCount(2);
        result.Actionables.Should().HaveCount(2);
        intake.Status.Should().Be(IntakeStatus.Analysed);
    }

    // ── PromoteIntakeCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task PromoteIntake_CreatesProcessAndMarksPromoted()
    {
        var intake = CreateAnalysedIntake();
        var intakeRepo = new Mock<IIntakeRepository>();
        intakeRepo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var processRepo = new Mock<IProcessRepository>();
        var artifactRepo = new Mock<IArtifactRepository>();

        var handler = new PromoteIntakeCommandHandler(
            intakeRepo.Object, processRepo.Object, artifactRepo.Object, FreshUow().Object);

        var result = await handler.Handle(new PromoteIntakeCommand(intake.Id), default);

        result.Name.Should().Be("Invoice Processing");
        result.Department.Should().Be("Finance");
        intake.Status.Should().Be(IntakeStatus.Promoted);
        intake.PromotedProcessId.Should().NotBeNull();
    }

    [Fact]
    public async Task PromoteIntake_ThrowsWhenNotAnalysed()
    {
        var intake = CreateSubmittedIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var handler = new PromoteIntakeCommandHandler(
            repo.Object, new Mock<IProcessRepository>().Object,
            new Mock<IArtifactRepository>().Object, FreshUow().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PromoteIntakeCommand(intake.Id), default));
    }

    // ── GetIntakeByIdQuery ───────────────────────────────────────────────────

    [Fact]
    public async Task GetIntakeById_ReturnsDto()
    {
        var intake = CreateDraftIntake();
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByIdAsync(intake.Id, default)).ReturnsAsync(intake);

        var handler = new GetIntakeByIdQueryHandler(repo.Object);
        var result = await handler.Handle(new GetIntakeByIdQuery(intake.Id), default);

        result.Id.Should().Be(intake.Id);
        result.Title.Should().Be("Invoice Processing");
    }

    // ── GetMyIntakesQuery ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyIntakes_ReturnsAllForOwner()
    {
        var intake1 = CreateDraftIntake("Process A");
        var intake2 = CreateDraftIntake("Process B");
        var repo = new Mock<IIntakeRepository>();
        repo.Setup(r => r.GetByOwnerAsync("user-1", default))
            .ReturnsAsync(new[] { intake1, intake2 });

        var handler = new GetMyIntakesQueryHandler(repo.Object);
        var result = await handler.Handle(new GetMyIntakesQuery("user-1"), default);

        result.Should().HaveCount(2);
    }

    // ── Domain entity invariants ─────────────────────────────────────────────

    [Fact]
    public void IntakeRequest_Submit_FailsWhenAlreadySubmitted()
    {
        var intake = CreateDraftIntake();
        intake.UpdateMeta("T", "D", "Dept", null, null, null, null);
        intake.Submit();

        Assert.Throws<InvalidOperationException>(() => intake.Submit());
    }

    [Fact]
    public void IntakeRequest_MarkPromoted_FailsWhenNotAnalysed()
    {
        var intake = CreateSubmittedIntake();
        Assert.Throws<InvalidOperationException>(() => intake.MarkPromoted(Guid.NewGuid()));
    }

    [Fact]
    public void IntakeArtifact_Create_SetsAllProperties()
    {
        var intakeId = Guid.NewGuid();
        var artifact = IntakeArtifact.Create(intakeId, "test.pdf", ArtifactType.Pdf, "blob/path", 1024);

        artifact.IntakeRequestId.Should().Be(intakeId);
        artifact.FileName.Should().Be("test.pdf");
        artifact.ArtifactType.Should().Be(ArtifactType.Pdf);
        artifact.BlobPath.Should().Be("blob/path");
        artifact.FileSizeBytes.Should().Be(1024);
        artifact.ExtractedText.Should().BeNull();
    }

    [Fact]
    public void IntakeArtifact_SetExtractedText_UpdatesProperty()
    {
        var artifact = IntakeArtifact.Create(Guid.NewGuid(), "doc.pdf", ArtifactType.Pdf, "blob", 100);
        artifact.SetExtractedText("Extracted content here");
        artifact.ExtractedText.Should().Be("Extracted content here");
    }
}
