using BPOPlatform.Application.Processes.Commands;
using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace BPOPlatform.UnitTests.Application;

/// <summary>Tests for Process command handlers using mocked repositories.</summary>
public class ProcessCommandHandlerTests
{
    private readonly Mock<IProcessRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    // ── CreateProcessCommandHandler ───────────────────────────────────────────

    [Fact]
    public async Task CreateProcess_ValidCommand_SavesAndReturnsDto()
    {
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Process>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new CreateProcessCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new CreateProcessCommand("Invoice AP", "Accounts Payable", "Finance", "user-1");

        var result = await handler.Handle(command, default);

        result.Should().NotBeNull();
        result.Name.Should().Be("Invoice AP");
        result.Department.Should().Be("Finance");
        result.Status.Should().Be(ProcessStatus.Draft);
        _repoMock.Verify(r => r.AddAsync(It.Is<Process>(p => p.Name == "Invoice AP"), default), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── UpdateProcessCommandHandler ───────────────────────────────────────────

    [Fact]
    public async Task UpdateProcess_ExistingId_UpdatesAndReturnsDto()
    {
        var process = Process.Create("Old Name", "old desc", "Old Dept", "user-1");
        _repoMock.Setup(r => r.GetByIdAsync(process.Id, default)).ReturnsAsync(process);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new UpdateProcessCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new UpdateProcessCommand(process.Id, "New Name", "new desc", "New Dept");

        var result = await handler.Handle(command, default);

        result.Name.Should().Be("New Name");
        result.Description.Should().Be("new desc");
        result.Department.Should().Be("New Dept");
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateProcess_NotFound_ThrowsKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Process?)null);

        var handler = new UpdateProcessCommandHandler(_repoMock.Object, _uowMock.Object);
        var act = () => handler.Handle(new UpdateProcessCommand(id, "N", "D", "Dp"), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteProcessCommandHandler ───────────────────────────────────────────

    [Fact]
    public async Task DeleteProcess_ExistingId_DeletesProcess()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        _repoMock.Setup(r => r.GetByIdAsync(process.Id, default)).ReturnsAsync(process);
        _repoMock.Setup(r => r.DeleteAsync(process.Id, default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new DeleteProcessCommandHandler(_repoMock.Object, _uowMock.Object);
        await handler.Handle(new DeleteProcessCommand(process.Id), default);

        _repoMock.Verify(r => r.DeleteAsync(process.Id, default), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DeleteProcess_NotFound_ThrowsKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((Process?)null);

        var handler = new DeleteProcessCommandHandler(_repoMock.Object, _uowMock.Object);
        var act = () => handler.Handle(new DeleteProcessCommand(id), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── AdvanceProcessStatusCommandHandler ───────────────────────────────────

    [Fact]
    public async Task AdvanceStatus_ValidStatus_UpdatesProcess()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        _repoMock.Setup(r => r.GetByIdAsync(process.Id, default)).ReturnsAsync(process);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new AdvanceProcessStatusCommandHandler(_repoMock.Object, _uowMock.Object);
        var result = await handler.Handle(new AdvanceProcessStatusCommand(process.Id, "InProgress"), default);

        result.Status.Should().Be(ProcessStatus.InProgress);
    }

    [Fact]
    public async Task AdvanceStatus_InvalidStatusString_ThrowsArgumentException()
    {
        var process = Process.Create("Name", "desc", "Dept", "owner");
        _repoMock.Setup(r => r.GetByIdAsync(process.Id, default)).ReturnsAsync(process);

        var handler = new AdvanceProcessStatusCommandHandler(_repoMock.Object, _uowMock.Object);
        var act = () => handler.Handle(new AdvanceProcessStatusCommand(process.Id, "NotAValidStatus"), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
