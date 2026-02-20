using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Infrastructure.Persistence;
using BPOPlatform.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BPOPlatform.UnitTests.Infrastructure;

/// <summary>
/// Repository tests using the EF Core InMemory provider.
/// These verify persistence logic without a real database.
/// </summary>
public class ProcessRepositoryTests : IDisposable
{
    private readonly BPODbContext _db;
    private readonly ProcessRepository _repo;
    private readonly UnitOfWork _uow;

    public ProcessRepositoryTests()
    {
        var opts = new DbContextOptionsBuilder<BPODbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new BPODbContext(opts);
        _repo = new ProcessRepository(_db);
        _uow = new UnitOfWork(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsProcess()
    {
        var process = Process.Create("Invoice AP", "desc", "Finance", "user-1");
        await _repo.AddAsync(process);
        await _uow.SaveChangesAsync();

        var retrieved = await _repo.GetByIdAsync(process.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Invoice AP");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProcesses()
    {
        await _repo.AddAsync(Process.Create("P1", "d", "Dept", "o"));
        await _repo.AddAsync(Process.Create("P2", "d", "Dept", "o"));
        await _uow.SaveChangesAsync();

        var all = await _repo.GetAllAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByDepartmentAsync_ReturnsMatchingProcesses()
    {
        await _repo.AddAsync(Process.Create("P1", "d", "Finance", "o"));
        await _repo.AddAsync(Process.Create("P2", "d", "HR", "o"));
        await _uow.SaveChangesAsync();

        var finance = await _repo.GetByDepartmentAsync("Finance");
        finance.Should().HaveCount(1);
        finance[0].Name.Should().Be("P1");
    }

    [Fact]
    public async Task DeleteAsync_RemovesProcess()
    {
        var process = Process.Create("To Delete", "d", "Dept", "o");
        await _repo.AddAsync(process);
        await _uow.SaveChangesAsync();

        await _repo.DeleteAsync(process.Id);
        await _uow.SaveChangesAsync();

        var retrieved = await _repo.GetByIdAsync(process.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetTotalCountAsync_ReturnsCorrectCount()
    {
        await _repo.AddAsync(Process.Create("P1", "d", "Dept", "o"));
        await _repo.AddAsync(Process.Create("P2", "d", "Dept", "o"));
        await _uow.SaveChangesAsync();

        var count = await _repo.GetTotalCountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task AddProcess_WorkflowStepsPersisted()
    {
        var process = Process.Create("Invoice AP", "desc", "Finance", "user-1");
        await _repo.AddAsync(process);
        await _uow.SaveChangesAsync();

        // Reload fresh from DB
        var retrieved = await _db.Processes
            .Include(p => p.WorkflowSteps)
            .FirstAsync(p => p.Id == process.Id);

        retrieved.WorkflowSteps.Should().HaveCount(5);
        retrieved.WorkflowSteps.Select(ws => ws.StepOrder).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }
}
