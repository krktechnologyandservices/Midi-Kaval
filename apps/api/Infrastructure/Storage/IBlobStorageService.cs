namespace MidiKaval.Api.Infrastructure.Storage;

public interface IBlobStorageService
{
    Task EnsureContainerAsync(CancellationToken cancellationToken = default);

    (Uri UploadUrl, DateTime ExpiresAtUtc) GenerateUploadSasUri(string blobName, string contentType);

    (Uri DownloadUrl, DateTime ExpiresAtUtc) GenerateReadSasUri(string blobName);

    Task<bool> BlobExistsAsync(string blobName, CancellationToken cancellationToken = default);

    Task<long?> GetBlobSizeAsync(string blobName, CancellationToken cancellationToken = default);
}
