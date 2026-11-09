using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Test double for Azure Blob Storage: keeps uploaded images in memory.</summary>
public class InMemoryImageStorage : IImageStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _blobs = new();

    public int Count => _blobs.Count;

    public async Task SaveAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _blobs[blobName] = (buffer.ToArray(), contentType);
    }

    public Task<ImageDownload?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        if (_blobs.TryGetValue(blobName, out var blob))
        {
            return Task.FromResult<ImageDownload?>(new ImageDownload(new MemoryStream(blob.Bytes), blob.ContentType));
        }
        return Task.FromResult<ImageDownload?>(null);
    }

    public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}
//ST10486532
//Author: Gift Tshwaane