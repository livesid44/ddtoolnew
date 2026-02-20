using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Development/test fallback implementation of <see cref="IBlobStorageService"/>.
/// Stores files in the OS temp directory under <c>bpo-artifacts/{container}/</c>.
/// Registered automatically when no Azure Blob Storage connection string is configured.
/// </summary>
public class LocalBlobStorageService(ILogger<LocalBlobStorageService> logger) : IBlobStorageService
{
    private static readonly string BasePath = Path.Combine(Path.GetTempPath(), "bpo-artifacts");

    public async Task<string> UploadAsync(
        string containerName, string blobName, Stream content,
        string contentType, CancellationToken ct = default)
    {
        var dir = Path.Combine(BasePath, containerName);
        Directory.CreateDirectory(dir);

        // Replace path separators so we don't create subdirectories in the temp folder
        var safeFileName = blobName.Replace('/', '_').Replace('\\', '_');
        var filePath = Path.Combine(dir, safeFileName);

        await using var file = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(file, ct);

        logger.LogInformation("[LocalBlob] Saved {BlobName} → {Path}", blobName, filePath);
        return blobName;
    }

    public Task<Uri> GetDownloadUrlAsync(
        string containerName, string blobName,
        TimeSpan expiry, CancellationToken ct = default)
    {
        var safeFileName = blobName.Replace('/', '_').Replace('\\', '_');
        var filePath = Path.Combine(BasePath, containerName, safeFileName);
        return Task.FromResult(new Uri($"file://{filePath}"));
    }

    public Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var safeFileName = blobName.Replace('/', '_').Replace('\\', '_');
        var filePath = Path.Combine(BasePath, containerName, safeFileName);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"[LocalBlob] Blob not found: {filePath}");
        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var safeFileName = blobName.Replace('/', '_').Replace('\\', '_');
        var filePath = Path.Combine(BasePath, containerName, safeFileName);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }
}
