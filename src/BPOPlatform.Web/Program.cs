using Microsoft.Extensions.FileProviders;

// ── BPOPlatform.Web ───────────────────────────────────────────────────────────
// Local-development static file host for the HTML/CSS/JS frontend.
// The HTML pages live at the repository root (index.html, dashboard.html …).
// In production these files are deployed to Azure Static Web Apps instead.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Resolve the repository root: this project lives at  src/BPOPlatform.Web/
// so the repo root is exactly two directories up from ContentRootPath.
var repoRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));

if (!File.Exists(Path.Combine(repoRoot, "index.html")))
{
    // Fallback: already at the repo root (e.g. running from a custom working dir)
    repoRoot = builder.Environment.ContentRootPath;

    if (!File.Exists(Path.Combine(repoRoot, "index.html")))
    {
        throw new InvalidOperationException(
            $"Cannot find 'index.html' in '{repoRoot}'. " +
            "Ensure the project is run from 'src/BPOPlatform.Web/' so the " +
            "static frontend files at the repository root can be located.");
    }
}

var fileProvider = new PhysicalFileProvider(repoRoot);

// Serve index.html for requests to "/"
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = fileProvider,
    DefaultFileNames = ["index.html"]
});

// Serve all static assets (HTML, CSS, JS, images, fonts …)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider
});

app.Run();
