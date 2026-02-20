using BPOPlatform.Application.Processes.Queries;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace BPOPlatform.UnitTests.Application;

/// <summary>Tests for Process and WorkflowStep query handlers.</summary>
public class ProcessQueryHandlerTests
{
    private readonly Mock<IProcessRepository> _repoMock = new();
    private readonly Mock<IWorkflowStepRepository> _stepRepoMock = new();

    // ── GetAllProcessesQueryHandler ───────────────────────────────────────────

    [Fact]
    public async Task GetAllProcesses_NoFilter_ReturnsAllProcesses()
    {
        var processes = new List<Process>
        {
            Process.Create("AP Invoice", "desc", "Finance", "owner"),
            Process.Create("HR Onboarding", "desc", "HR", "owner")
        };
        _repoMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync(processes);

        var handler = new GetAllProcessesQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetAllProcessesQuery(), default);

        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().BeEquivalentTo(["AP Invoice", "HR Onboarding"]);
    }

    [Fact]
    public async Task GetAllProcesses_WithDepartmentFilter_ReturnsFiltered()
    {
        var financeProcess = Process.Create("AP Invoice", "desc", "Finance", "owner");
        _repoMock.Setup(r => r.GetByDepartmentAsync("Finance", default))
            .ReturnsAsync(new List<Process> { financeProcess });

        var handler = new GetAllProcessesQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetAllProcessesQuery("Finance"), default);

        result.Should().HaveCount(1);
        result[0].Department.Should().Be("Finance");
    }

    // ── GetProcessByIdQueryHandler ─────────────────────────────────────────────

    [Fact]
    public async Task GetProcessById_ExistingId_ReturnsDto()
    {
        var process = Process.Create("Invoice AP", "desc", "Finance", "owner");
        _repoMock.Setup(r => r.GetByIdAsync(process.Id, default)).ReturnsAsync(process);

        var handler = new GetProcessByIdQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetProcessByIdQuery(process.Id), default);

        result.Id.Should().Be(process.Id);
        result.Name.Should().Be("Invoice AP");
    }

    [Fact]
    public async Task GetProcessById_NotFound_ThrowsKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Process?)null);

        var handler = new GetProcessByIdQueryHandler(_repoMock.Object);
        var act = () => handler.Handle(new GetProcessByIdQuery(id), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetWorkflowStepsQueryHandler ──────────────────────────────────────────

    [Fact]
    public async Task GetWorkflowSteps_ReturnsStepsOrderedByStepOrder()
    {
        var processId = Guid.NewGuid();
        var steps = new List<WorkflowStep>
        {
            WorkflowStep.Create(processId, 3, "AI Validation", ProcessStatus.UnderReview),
            WorkflowStep.Create(processId, 1, "Meta Information", ProcessStatus.Draft),
            WorkflowStep.Create(processId, 2, "Artifact Upload", ProcessStatus.InProgress),
        };
        _stepRepoMock.Setup(r => r.GetByProcessIdAsync(processId, default)).ReturnsAsync(steps);

        var handler = new GetWorkflowStepsQueryHandler(_stepRepoMock.Object);
        var result = await handler.Handle(new GetWorkflowStepsQuery(processId), default);

        result.Should().HaveCount(3);
        result[0].StepOrder.Should().Be(1);
        result[1].StepOrder.Should().Be(2);
        result[2].StepOrder.Should().Be(3);
    }
}
