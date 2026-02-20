using BPOPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BPOPlatform.IntegrationTests;

/// <summary>
/// WebApplicationFactory that replaces the real database with EF Core InMemory
/// for fast, isolated integration tests. The Development auth bypass
/// (DevPermissivePolicyProvider) is active, so no JWT token is required.
/// </summary>
public class BpoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Each test class gets its own isolated DB name
    private readonly string _dbName = $"IntegrationTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext options (SQLite or SQL Server)
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<BPODbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Also remove the DbContext registration itself
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(BPODbContext));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // Register EF Core InMemory for tests
            services.AddDbContext<BPODbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));
        });
    }

    public async Task InitializeAsync()
    {
        // EnsureCreated creates the schema for InMemory (Migrate() is not supported)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BPODbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}
