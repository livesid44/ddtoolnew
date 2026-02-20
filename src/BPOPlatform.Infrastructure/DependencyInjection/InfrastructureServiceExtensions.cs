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
        // ── EF Core (Azure SQL) ───────────────────────────────────────────────
        services.AddDbContext<BPODbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOpts => sqlOpts.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProcessRepository, ProcessRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();

        // ── Azure Blob Storage ────────────────────────────────────────────────
        var blobConnectionString = configuration.GetConnectionString("BlobStorage");
        var blobEndpoint = configuration["AzureStorage:ServiceUri"];

        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            // Production: use Managed Identity
            services.AddSingleton(_ => new BlobServiceClient(new Uri(blobEndpoint), new DefaultAzureCredential()));
        }
        else if (!string.IsNullOrEmpty(blobConnectionString))
        {
            // Development: use connection string (Azurite emulator)
            services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));
        }

        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();

        // ── Azure OpenAI ──────────────────────────────────────────────────────
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));

        var openAiEndpoint = configuration[$"{AzureOpenAiOptions.SectionName}:Endpoint"];
        if (!string.IsNullOrEmpty(openAiEndpoint))
        {
            services.AddSingleton(_ => new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));
            services.AddScoped<IAiAnalysisService, AzureOpenAiAnalysisService>();
        }

        return services;
    }
}
