using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Storage.Blobs;
using BPOPlatform.Domain.Interfaces;
using BPOPlatform.Infrastructure.Persistence;
using BPOPlatform.Infrastructure.Persistence.Repositories;
using BPOPlatform.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BPOPlatform.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all Infrastructure layer services: EF Core, Azure Blob, Azure OpenAI.
    /// Call from Program.cs: <c>builder.Services.AddInfrastructureServices(configuration);</c>
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── EF Core ───────────────────────────────────────────────────────────
        // Supports both SQL Server (Azure) and SQLite (local dev / tests).
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        if (connectionString.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            // SQLite – used for local development and in-process testing
            services.AddDbContext<BPODbContext>(opts =>
                opts.UseSqlite(connectionString));
        }
        else if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.StartsWith("__"))
        {
            // Azure SQL Server (production / staging)
            services.AddDbContext<BPODbContext>(opts =>
                opts.UseSqlServer(
                    connectionString,
                    sqlOpts => sqlOpts.EnableRetryOnFailure(maxRetryCount: 5)));
        }
        else
        {
            // No valid connection string → SQLite in-memory for local/CI (applies when appsettings uses placeholder)
            services.AddDbContext<BPODbContext>(opts =>
                opts.UseSqlite("DataSource=:memory:"));
        }

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProcessRepository, ProcessRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IWorkflowStepRepository, WorkflowStepRepository>();
        services.AddScoped<IKanbanCardRepository, KanbanCardRepository>();

        var blobConnectionString = configuration.GetConnectionString("BlobStorage");
        var blobEndpoint = configuration["AzureStorage:ServiceUri"];

        if (!string.IsNullOrWhiteSpace(blobEndpoint)
            && !blobEndpoint.StartsWith("__")
            && Uri.TryCreate(blobEndpoint, UriKind.Absolute, out _))
        {
            // Production: use Managed Identity
            services.AddSingleton(_ => new BlobServiceClient(new Uri(blobEndpoint), new DefaultAzureCredential()));
            services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        }
        else if (!string.IsNullOrWhiteSpace(blobConnectionString) && !blobConnectionString.StartsWith("__"))
        {
            // Development: use connection string (Azurite emulator)
            services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));
            services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        }
        else
        {
            // Fallback: local filesystem (no Blob Storage configured)
            services.AddScoped<IBlobStorageService, LocalBlobStorageService>();
        }

        // ── Azure OpenAI ──────────────────────────────────────────────────────
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));

        var openAiEndpoint = configuration[$"{AzureOpenAiOptions.SectionName}:Endpoint"];
        if (!string.IsNullOrWhiteSpace(openAiEndpoint)
            && !openAiEndpoint.StartsWith("__")
            && Uri.TryCreate(openAiEndpoint, UriKind.Absolute, out _))
        {
            services.AddSingleton(_ => new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));
            services.AddScoped<IAiAnalysisService, AzureOpenAiAnalysisService>();
        }
        else
        {
            // Fallback: mock AI service (no Azure OpenAI configured)
            services.AddScoped<IAiAnalysisService, MockAiAnalysisService>();
        }

        return services;
    }
}
