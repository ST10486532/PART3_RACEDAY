using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Stores images in a private Azure Blob Storage container. Images are never exposed by public blob URLs:
/// the API streams them back to callers, so the MVC application never talks to Azure directly.
/// </summary>
public class AzureBlobImageStorage : IImageStorage
{
    private readonly BlobContainerClient _container;
    private bool _containerReady;

    public AzureBlobImageStorage(IConfiguration config)
    {
        var connectionString = config["AzureBlob:ConnectionString"] ?? "UseDevelopmentStorage=true";
        var containerName = config["AzureBlob:ContainerName"] ?? "raceday-images";
        _container = new BlobContainerClient(connectionString, containerName);
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerReady) return;
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerReady = true;
    }

    public async Task SaveAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var blob = _container.GetBlobClient(blobName);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };
        await blob.UploadAsync(content, options, cancellationToken);
    }

    public async Task<ImageDownload?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = _container.GetBlobClient(blobName);
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new ImageDownload(response.Value.Content, response.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken = default)
    {
        await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
//ST10486532
//Author: Gift Tshwaane