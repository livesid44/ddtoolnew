using Azure.Identity;
using Azure.Storage.Blobs;
using BPOPlatform.Domain.Interfaces;
using BPOPlatform.Infrastructure.Persistence;
using BPOPlatform.Infrastructure.Persistence.Repositories;
using BPOPlatform.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.AI.OpenAI;

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
        else
        {
            // Azure SQL Server (production / staging)
            services.AddDbContext<BPODbContext>(opts =>
                opts.UseSqlServer(
                    connectionString,
                    sqlOpts => sqlOpts.EnableRetryOnFailure(maxRetryCount: 5)));
        }

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProcessRepository, ProcessRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IWorkflowStepRepository, WorkflowStepRepository>();

        // ── Azure Blob Storage ────────────────────────────────────────────────
        var blobConnectionString = configuration.GetConnectionString("BlobStorage");
        var blobEndpoint = configuration["AzureStorage:ServiceUri"];

        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            // Production: use Managed Identity
            services.AddSingleton(_ => new BlobServiceClient(new Uri(blobEndpoint), new DefaultAzureCredential()));
            services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        }
        else if (!string.IsNullOrEmpty(blobConnectionString))
        {
            // Development: use connection string (Azurite emulator)
            services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));
            services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        }
        // If neither is configured, IBlobStorageService is simply not registered (no crash).

        // ── Azure OpenAI ──────────────────────────────────────────────────────
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));

        var openAiEndpoint = configuration[$"{AzureOpenAiOptions.SectionName}:Endpoint"];
        if (!string.IsNullOrEmpty(openAiEndpoint))
        {
            services.AddSingleton(_ => new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));
            services.AddScoped<IAiAnalysisService, AzureOpenAiAnalysisService>();
        }
        // If not configured, IAiAnalysisService is simply not registered.

        return services;
    }
}
