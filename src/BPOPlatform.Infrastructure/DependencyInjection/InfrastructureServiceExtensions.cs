using Azure.AI.DocumentIntelligence;
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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IIntakeRepository, IntakeRepository>();

        // ── Auth & Identity ────────────────────────────────────────────────────
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        // ICurrentUserService is registered by the API project (requires IHttpContextAccessor)

        // ── LDAP ──────────────────────────────────────────────────────────────
        services.Configure<LdapSettings>(configuration.GetSection(LdapSettings.SectionName));
        var ldapHost = configuration[$"{LdapSettings.SectionName}:Host"];
        if (!string.IsNullOrWhiteSpace(ldapHost) && !ldapHost.StartsWith("__"))
            services.AddScoped<ILdapAuthService, LdapAuthService>();
        else
            services.AddScoped<ILdapAuthService, MockLdapAuthService>();

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

        // ── Document Intelligence ─────────────────────────────────────────────
        services.Configure<DocumentIntelligenceOptions>(
            configuration.GetSection(DocumentIntelligenceOptions.SectionName));

        var docIntelEndpoint = configuration[$"{DocumentIntelligenceOptions.SectionName}:Endpoint"];
        if (!string.IsNullOrWhiteSpace(docIntelEndpoint)
            && !docIntelEndpoint.StartsWith("__")
            && Uri.TryCreate(docIntelEndpoint, UriKind.Absolute, out var docIntelUri))
        {
            services.AddSingleton(_ => new DocumentIntelligenceClient(docIntelUri, new DefaultAzureCredential()));
            services.AddScoped<IDocumentIntelligenceService, AzureDocumentIntelligenceService>();
        }
        else
        {
            services.AddScoped<IDocumentIntelligenceService, MockDocumentIntelligenceService>();
        }

        // ── Speech Services ───────────────────────────────────────────────────
        services.Configure<SpeechServicesOptions>(
            configuration.GetSection(SpeechServicesOptions.SectionName));

        var speechKey = configuration[$"{SpeechServicesOptions.SectionName}:SubscriptionKey"];
        if (!string.IsNullOrWhiteSpace(speechKey) && !speechKey.StartsWith("__"))
        {
            services.AddHttpClient("SpeechServices");
            services.AddScoped<ISpeechTranscriptionService, AzureSpeechTranscriptionService>();
        }
        else
        {
            services.AddScoped<ISpeechTranscriptionService, MockSpeechTranscriptionService>();
        }

        // ── Document Generation ───────────────────────────────────────────────
        // Reuse the Azure OpenAI client if it was already registered; otherwise use template fallback.
        var openAiEndpoint2 = configuration[$"{AzureOpenAiOptions.SectionName}:Endpoint"];
        if (!string.IsNullOrWhiteSpace(openAiEndpoint2)
            && !openAiEndpoint2.StartsWith("__")
            && Uri.TryCreate(openAiEndpoint2, UriKind.Absolute, out _))
        {
            services.AddScoped<IDocumentGenerationService, OpenAiDocumentGenerationService>();
        }
        else
        {
            services.AddScoped<IDocumentGenerationService, MarkdownDocumentGenerationService>();
        }

        // ── External Ticketing (Power Automate) ───────────────────────────────
        services.Configure<PowerAutomateOptions>(
            configuration.GetSection(PowerAutomateOptions.SectionName));

        var flowUrl = configuration[$"{PowerAutomateOptions.SectionName}:FlowUrl"];
        if (!string.IsNullOrWhiteSpace(flowUrl)
            && !flowUrl.StartsWith("__")
            && Uri.TryCreate(flowUrl, UriKind.Absolute, out _))
        {
            services.AddHttpClient("PowerAutomate");
            services.AddScoped<IExternalTicketingService, PowerAutomateTicketingService>();
        }
        else
        {
            services.AddScoped<IExternalTicketingService, NoOpTicketingService>();
        }

        // ── Intake Chat Service ───────────────────────────────────────────────
        var openAiForIntake = configuration["AzureOpenAI:Endpoint"];
        if (!string.IsNullOrWhiteSpace(openAiForIntake)
            && !openAiForIntake.StartsWith("__")
            && Uri.TryCreate(openAiForIntake, UriKind.Absolute, out _))
        {
            services.AddScoped<IIntakeChatService, AzureIntakeChatService>();
        }
        else
        {
            services.AddScoped<IIntakeChatService, MockIntakeChatService>();
        }

        return services;
    }
}
