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
    public DbSet<KanbanCard> KanbanCards => Set<KanbanCard>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<IntakeRequest> IntakeRequests => Set<IntakeRequest>();
    public DbSet<IntakeArtifact> IntakeArtifacts => Set<IntakeArtifact>();

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
            e.Property(a => a.ExtractedText);  // nullable, no max length (can be large)
            e.Ignore(a => a.DomainEvents);
        });

        // WorkflowStep
        modelBuilder.Entity<WorkflowStep>(e =>
        {
            e.HasKey(ws => ws.Id);
            e.Property(ws => ws.StepName).HasMaxLength(200).IsRequired();
            e.Ignore(ws => ws.DomainEvents);
        });

        // KanbanCard
        modelBuilder.Entity<KanbanCard>(e =>
        {
            e.HasKey(k => k.Id);
            e.Property(k => k.Title).HasMaxLength(200).IsRequired();
            e.Property(k => k.Description).HasMaxLength(2000);
            e.Property(k => k.Column).HasMaxLength(50).IsRequired();
            e.Property(k => k.AssignedTo).HasMaxLength(200);
            e.HasIndex(k => new { k.ProcessId, k.Column });
            e.Ignore(k => k.DomainEvents);
        });

        // ApplicationUser
        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(200);
            e.Property(u => u.Role).HasMaxLength(50).IsRequired();
            e.Property(u => u.LdapDomain).HasMaxLength(200);
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Ignore(u => u.DomainEvents);
        });

        // IntakeRequest
        modelBuilder.Entity<IntakeRequest>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(2000);
            e.Property(r => r.Department).HasMaxLength(100);
            e.Property(r => r.Location).HasMaxLength(200);
            e.Property(r => r.BusinessUnit).HasMaxLength(200);
            e.Property(r => r.ContactEmail).HasMaxLength(200);
            e.Property(r => r.QueuePriority).HasMaxLength(20).IsRequired();
            e.Property(r => r.OwnerId).HasMaxLength(100).IsRequired();
            e.Property(r => r.ChatHistoryJson);       // unbounded JSON
            e.Property(r => r.AiBrief);               // nullable
            e.Property(r => r.AiCheckpointsJson);     // nullable JSON
            e.Property(r => r.AiActionablesJson);     // nullable JSON
            e.HasMany(r => r.Artifacts)
             .WithOne()
             .HasForeignKey(a => a.IntakeRequestId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.OwnerId);
            e.Ignore(r => r.DomainEvents);
        });

        // IntakeArtifact
        modelBuilder.Entity<IntakeArtifact>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.FileName).HasMaxLength(500).IsRequired();
            e.Property(a => a.BlobPath).HasMaxLength(1000).IsRequired();
            e.Property(a => a.ExtractedText); // nullable, no length limit
            e.Ignore(a => a.DomainEvents);
        });
    }
}
