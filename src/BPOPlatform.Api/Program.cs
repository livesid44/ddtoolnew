using BPOPlatform.Application.DependencyInjection;
using BPOPlatform.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(
        ctx.Configuration["ApplicationInsights:ConnectionString"] ?? string.Empty,
        TelemetryConverter.Traces));

// ── Authentication (Azure AD / Entra ID) ─────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

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
        Description = "Enter your Azure AD JWT token."
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

// ── CORS (configure per environment) ─────────────────────────────────────────
builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", p =>
    p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
