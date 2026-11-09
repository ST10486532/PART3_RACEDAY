namespace RaceDay.Api.Services;

/// <summary>An image read back from storage.</summary>
public record ImageDownload(Stream Content, string ContentType);

/// <summary>
/// Abstraction over file storage so controllers never depend on Azure directly.
/// Production uses Azure Blob Storage; tests use an in-memory fake.
/// </summary>
public interface IImageStorage
{
    Task SaveAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the blob does not exist.</summary>
    Task<ImageDownload?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken = default);
}
//ST10486532
//Author: Gift Tshwaane