using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IBlobStorageService"/>.
/// Uses Managed Identity (Azure.Identity) in production; connection string in development.
/// </summary>
public class AzureBlobStorageService(BlobServiceClient blobClient, ILogger<AzureBlobStorageService> logger)
    : IBlobStorageService
{
    public async Task<string> UploadAsync(string containerName, string blobName, Stream content,
        string contentType, CancellationToken ct = default)
    {
        var container = blobClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        logger.LogInformation("Uploaded blob {BlobName} to container {Container}", blobName, containerName);
        return blobName;
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var container = blobClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobName);
        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
        logger.LogInformation("Downloaded blob {BlobName} from container {Container}", blobName, containerName);
        return response.Value.Content;
    }

    public async Task<Uri> GetDownloadUrlAsync(string containerName, string blobName,
        TimeSpan expiry, CancellationToken ct = default)
    {
        var container = blobClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobName);

        if (blob.CanGenerateSasUri)
        {
            var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiry));
            return await Task.FromResult(sasUri);
        }

        // Fallback: return the plain URI (works when using emulator / managed identity with public container)
        return await Task.FromResult(blob.Uri);
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var container = blobClient.GetBlobContainerClient(containerName);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
        logger.LogInformation("Deleted blob {BlobName} from container {Container}", blobName, containerName);
    }
}
