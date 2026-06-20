using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Storage;

public sealed class AzureBlobStorageService(IOptions<BlobStorageOptions> options) : IBlobStorageService
{
    private readonly BlobStorageOptions _options = options.Value;
    private readonly BlobContainerClient _containerClient = CreateContainerClient(options.Value);

    private static BlobContainerClient CreateContainerClient(BlobStorageOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContainerName);

        var blobServiceClient = new BlobServiceClient(options.ConnectionString);
        return blobServiceClient.GetBlobContainerClient(options.ContainerName);
    }

    public async Task EnsureContainerAsync(CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
    }

    public (Uri UploadUrl, DateTime ExpiresAtUtc) GenerateUploadSasUri(string blobName, string contentType)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.SasExpiryMinutes);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
            ContentType = contentType,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        return (blobClient.GenerateSasUri(sasBuilder), expiresAtUtc);
    }

    public (Uri DownloadUrl, DateTime ExpiresAtUtc) GenerateReadSasUri(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.SasExpiryMinutes);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return (blobClient.GenerateSasUri(sasBuilder), expiresAtUtc);
    }

    public async Task<bool> BlobExistsAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        return await blobClient.ExistsAsync(cancellationToken);
    }

    public async Task<long?> GetBlobSizeAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.ContentLength;
    }
}
