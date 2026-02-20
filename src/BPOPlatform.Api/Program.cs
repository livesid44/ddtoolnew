using BPOPlatform.Api.Hubs;
using BPOPlatform.Api.Middleware;
using BPOPlatform.Application.DependencyInjection;
using BPOPlatform.Infrastructure.DependencyInjection;
using BPOPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console();

var appInsightsConnStr = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnStr) && !appInsightsConnStr.StartsWith("__"))
    loggerConfig.WriteTo.ApplicationInsights(appInsightsConnStr, TelemetryConverter.Traces);

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

// ── Authentication (Azure AD / Entra ID) ─────────────────────────────────────
// In Production: enforce Azure AD JWT auth on all controllers.
// In Development: auth middleware is registered but controllers have no [Authorize],
// so the API is accessible without a token for local development.
if (!builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    builder.Services.AddAuthorization();
}
else
{
    // ── Development auth bypass ───────────────────────────────────────────────
    // DevBypassAuthHandler: populates ctx.User so controller code can read claims.
    // DevPermissivePolicyProvider: replaces the entire authorization policy system so
    //   [Authorize] (with or without a named policy) always succeeds without a real token.
    builder.Services.AddAuthentication("DevBypass")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                   DevBypassAuthHandler>("DevBypass", _ => { });
    builder.Services.AddAuthorization();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, DevPermissivePolicyProvider>();
}

// ── Application & Infrastructure layers ──────────────────────────────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BPO Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your Azure AD JWT token (not required in Development)."
    });
    c.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── SignalR ────────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BPODbContext>("database");

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", p =>
{
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                         ?? (builder.Environment.IsDevelopment()
                             ? ["http://localhost:3000", "http://localhost:5500", "http://127.0.0.1:5500"]
                             : []);
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials(); // Required for SignalR WebSocket handshake
}));

var app = builder.Build();

// ── Auto-migrate in Development ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BPODbContext>();
    // Use EnsureCreated for InMemory provider (doesn't support migrations),
    // Migrate for relational providers (SQLite, SQL Server).
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
