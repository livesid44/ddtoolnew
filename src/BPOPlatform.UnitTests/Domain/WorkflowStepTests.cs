using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using FluentAssertions;

namespace BPOPlatform.UnitTests.Domain;

/// <summary>Tests for the WorkflowStep entity.</summary>
public class WorkflowStepTests
{
    [Fact]
    public void Create_WithValidArguments_ReturnsStep()
    {
        var processId = Guid.NewGuid();
        var step = WorkflowStep.Create(processId, 1, "Meta Information", ProcessStatus.Draft);

        step.ProcessId.Should().Be(processId);
        step.StepOrder.Should().Be(1);
        step.StepName.Should().Be("Meta Information");
        step.RequiredStatus.Should().Be(ProcessStatus.Draft);
        step.IsCompleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidStepName_Throws(string? name)
    {
        var act = () => WorkflowStep.Create(Guid.NewGuid(), 1, name!, ProcessStatus.Draft);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Complete_SetsCompletedFlagsAndTimestamp()
    {
        var step = WorkflowStep.Create(Guid.NewGuid(), 1, "Meta Information", ProcessStatus.Draft);

        step.Complete("john.doe", "Completed meta data entry.");

        step.IsCompleted.Should().BeTrue();
        step.CompletedBy.Should().Be("john.doe");
        step.Notes.Should().Be("Completed meta data entry.");
        step.CompletedAt.Should().NotBeNull();
        step.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Complete_WithInvalidCompletedBy_Throws(string? completedBy)
    {
        var step = WorkflowStep.Create(Guid.NewGuid(), 1, "Step", ProcessStatus.Draft);
        var act = () => step.Complete(completedBy!);
        act.Should().Throw<ArgumentException>();
    }
}
