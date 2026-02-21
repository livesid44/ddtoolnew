using BPOPlatform.Api.Hubs;
using BPOPlatform.Api.Middleware;
using BPOPlatform.Application.DependencyInjection;
using BPOPlatform.Infrastructure.DependencyInjection;
using BPOPlatform.Infrastructure.Persistence;
using BPOPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;

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

// ── JWT / Auth config ─────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BPOPlatform";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BPOPlatformClients";
var hasLocalJwt = !string.IsNullOrWhiteSpace(jwtSecret) && !jwtSecret.StartsWith("__");

// ── Authentication ────────────────────────────────────────────────────────────
if (!builder.Environment.IsDevelopment())
{
    // Production: accept BOTH Azure AD tokens AND locally-issued tokens.
    // AddMicrosoftIdentityWebApi returns a different builder type, so we capture
    // the base AuthenticationBuilder first, then chain additional schemes onto it.
    var authBuilder = builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = "MultiScheme";
            options.DefaultChallengeScheme = "MultiScheme";
        });

    authBuilder.AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("AzureAd"),
        jwtBearerScheme: "AzureAd");

    if (hasLocalJwt)
    {
        authBuilder.AddJwtBearer("LocalJwt", opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });
    }

    // MultiScheme: forward to LocalJwt first; if that fails, AzureAd picks it up.
    // In practice, the issuer claim differentiates the two token types.
    authBuilder.AddPolicyScheme("MultiScheme", "AzureAd or LocalJwt", opts =>
    {
        opts.ForwardDefaultSelector = context =>
        {
            var auth = context.Request.Headers.Authorization.FirstOrDefault();
            if (auth?.StartsWith("Bearer ") == true)
            {
                // Decode issuer without full validation to route correctly
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    if (handler.CanReadToken(auth["Bearer ".Length..]))
                    {
                        var jwt = handler.ReadJwtToken(auth["Bearer ".Length..]);
                        if (jwt.Issuer == jwtIssuer) return "LocalJwt";
                    }
                }
                catch { /* Fall through to AzureAd */ }
            }
            return "AzureAd";
        };
    });

    builder.Services.AddAuthorization(opts => BuildPolicies(opts, hasLocalJwt));
}
else
{
    // ── Development auth bypass ───────────────────────────────────────────────
    // DevBypassAuthHandler: populates ctx.User as SuperAdmin for all requests.
    // LocalJwt: also registered so the actual login flow can be tested locally.
    var devAuthBuilder = builder.Services
        .AddAuthentication("DevBypass")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                   DevBypassAuthHandler>("DevBypass", _ => { });

    if (hasLocalJwt)
    {
        devAuthBuilder.AddJwtBearer("LocalJwt", opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });
    }

    builder.Services.AddAuthorization();
    // DevPermissivePolicyProvider makes ALL [Authorize] checks succeed without a real token.
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, DevPermissivePolicyProvider>();
}

// ── Application & Infrastructure layers ──────────────────────────────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ── Current User service (depends on IHttpContextAccessor, registered here) ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<BPOPlatform.Domain.Interfaces.ICurrentUserService,
                            BPOPlatform.Api.Middleware.CurrentUserService>();

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
        Description = "Enter a JWT token from POST /api/v1/auth/login (local) or Azure AD. Not required in Development."
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

// ── Authorization policy helper ───────────────────────────────────────────────
static void BuildPolicies(AuthorizationOptions opts, bool hasLocalJwt)
{
    var schemes = hasLocalJwt
        ? new[] { "AzureAd", "LocalJwt" }
        : new[] { "AzureAd" };

    // Default policy – any authenticated user via any supported scheme
    opts.DefaultPolicy = new AuthorizationPolicyBuilder(schemes)
        .RequireAuthenticatedUser()
        .Build();

    // SuperAdmin-only endpoints
    opts.AddPolicy("SuperAdminOnly", p => p
        .AddAuthenticationSchemes(schemes)
        .RequireAuthenticatedUser()
        .RequireRole("SuperAdmin"));

    // Any authenticated user (same as Default, explicit alias)
    opts.AddPolicy("AnyUser", p => p
        .AddAuthenticationSchemes(schemes)
        .RequireAuthenticatedUser());
}
