using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Events;
using FluentAssertions;

namespace BPOPlatform.UnitTests.Domain;

/// <summary>Tests for the Process aggregate root.</summary>
public class ProcessTests
{
    // ── Factory method ────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidArguments_ReturnsProcess()
    {
        var process = Process.Create("Invoice AP", "AP invoice process", "Finance", "user-1");

        process.Name.Should().Be("Invoice AP");
        process.Department.Should().Be("Finance");
        process.OwnerId.Should().Be("user-1");
        process.Status.Should().Be(ProcessStatus.Draft);
        process.Id.Should().NotBe(Guid.Empty);
        process.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null, "desc", "Finance", "user-1")]
    [InlineData("", "desc", "Finance", "user-1")]
    [InlineData("  ", "desc", "Finance", "user-1")]
    [InlineData("Name", "desc", "Finance", null)]
    [InlineData("Name", "desc", "Finance", "")]
    public void Create_WithInvalidArguments_Throws(string? name, string desc, string dept, string? owner)
    {
        var act = () => Process.Create(name!, desc, dept, owner!);
        act.Should().Throw<ArgumentException>();
    }

    // ── Default workflow steps ────────────────────────────────────────────────

    [Fact]
    public void Create_SeedsDefaultFiveWorkflowSteps()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");

        process.WorkflowSteps.Should().HaveCount(5);
        process.WorkflowSteps.Select(ws => ws.StepOrder).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        process.WorkflowSteps.Select(ws => ws.ProcessId).Should().AllSatisfy(id => id.Should().Be(process.Id));
    }

    [Fact]
    public void Create_WorkflowStepsHaveCorrectNames()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");

        var steps = process.WorkflowSteps.OrderBy(ws => ws.StepOrder).ToList();
        steps[0].StepName.Should().Be("Meta Information");
        steps[1].StepName.Should().Be("Artifact Upload");
        steps[2].StepName.Should().Be("AI Validation");
        steps[3].StepName.Should().Be("Review & Approval");
        steps[4].StepName.Should().Be("Deployment");
    }

    [Fact]
    public void Create_RaisesProcessCreatedDomainEvent()
    {
        var process = Process.Create("Invoice AP", "desc", "Finance", "user-1");

        process.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcessCreatedEvent>()
            .Which.ProcessName.Should().Be("Invoice AP");
    }

    // ── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public void AdvanceStatus_UpdatesStatusAndRaisesEvent()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        process.ClearDomainEvents();

        process.AdvanceStatus(ProcessStatus.InProgress);

        process.Status.Should().Be(ProcessStatus.InProgress);
        process.UpdatedAt.Should().NotBeNull();
        process.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcessStatusChangedEvent>()
            .Which.NewStatus.Should().Be(ProcessStatus.InProgress);
    }

    // ── UpdateDetails ─────────────────────────────────────────────────────────

    [Fact]
    public void UpdateDetails_ChangesNameAndDepartment()
    {
        var process = Process.Create("Old Name", "old desc", "Old Dept", "owner");

        process.UpdateDetails("New Name", "new desc", "New Dept");

        process.Name.Should().Be("New Name");
        process.Description.Should().Be("new desc");
        process.Department.Should().Be("New Dept");
        process.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateDetails_WithEmptyName_Throws()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        var act = () => process.UpdateDetails("", "desc", "Dept");
        act.Should().Throw<ArgumentException>();
    }

    // ── Scores ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 75)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    public void UpdateScores_ClampsValues(double auto, double comp)
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        process.UpdateScores(auto, comp);

        process.AutomationScore.Should().Be(Math.Clamp(auto, 0, 100));
        process.ComplianceScore.Should().Be(Math.Clamp(comp, 0, 100));
    }

    [Fact]
    public void UpdateScores_AboveMax_ClampedTo100()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        process.UpdateScores(150, 200);

        process.AutomationScore.Should().Be(100);
        process.ComplianceScore.Should().Be(100);
    }
}
