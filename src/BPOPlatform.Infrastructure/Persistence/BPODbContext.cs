using BPOPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BPOPlatform.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the BPO Platform.
/// Connection string is configured via <c>ConnectionStrings:DefaultConnection</c> in appsettings.
/// </summary>
public class BPODbContext(DbContextOptions<BPODbContext> options) : DbContext(options)
{
    public DbSet<Process> Processes => Set<Process>();
    public DbSet<ProcessArtifact> Artifacts => Set<ProcessArtifact>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BPODbContext).Assembly);

        // Process
        modelBuilder.Entity<Process>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.Department).HasMaxLength(100).IsRequired();
            e.HasMany(p => p.Artifacts)
             .WithOne()
             .HasForeignKey(a => a.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.WorkflowSteps)
             .WithOne()
             .HasForeignKey(ws => ws.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Ignore(p => p.DomainEvents);
        });

        // ProcessArtifact
        modelBuilder.Entity<ProcessArtifact>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.FileName).HasMaxLength(500).IsRequired();
            e.Property(a => a.BlobPath).HasMaxLength(1000).IsRequired();
            e.Ignore(a => a.DomainEvents);
        });

        // WorkflowStep
        modelBuilder.Entity<WorkflowStep>(e =>
        {
            e.HasKey(ws => ws.Id);
            e.Property(ws => ws.StepName).HasMaxLength(200).IsRequired();
            e.Ignore(ws => ws.DomainEvents);
        });
    }
}
