namespace MidiKaval.Api.Infrastructure.Storage;

public interface IBlobStorageService
{
    Task EnsureContainerAsync(CancellationToken cancellationToken = default);

    Task UploadAsync(
        string blobName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Returns null if the blob does not exist.</summary>
    Task<byte[]?> DownloadAsync(string blobName, CancellationToken cancellationToken = default);

    Task<bool> BlobExistsAsync(string blobName, CancellationToken cancellationToken = default);
}
