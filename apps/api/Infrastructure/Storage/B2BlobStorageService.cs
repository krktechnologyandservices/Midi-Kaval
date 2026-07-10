using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Storage;

// Backblaze B2 exposes an S3-compatible API, so the AWS SDK works against it directly —
// just point it at the bucket's region-specific endpoint with path-style addressing,
// per Backblaze's documented S3-compatibility settings.
public sealed class B2BlobStorageService : IBlobStorageService
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;

    public B2BlobStorageService(IOptions<B2StorageOptions> options)
    {
        var opts = options.Value;
        _bucketName = opts.BucketName;

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://s3.{opts.Region}.backblazeb2.com",
            ForcePathStyle = true,
            AuthenticationRegion = opts.Region,
        };

        _client = new AmazonS3Client(
            new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey),
            config);
    }

    public async Task EnsureContainerAsync(CancellationToken cancellationToken = default)
    {
        // A B2 application key scoped to a single bucket (the recommended, least-privilege
        // setup) generally can't enumerate other buckets via ListBuckets, so that can't be
        // used as an existence check here. Try to create it instead and treat "already
        // exists" as success — this bucket is normally created once by hand in the
        // Backblaze dashboard, so this is expected to no-op on every real deploy.
        try
        {
            await _client.PutBucketAsync(_bucketName, cancellationToken);
        }
        catch (Exception ex) when (
            ex is Amazon.S3.Model.BucketAlreadyExistsException
            or Amazon.S3.Model.BucketAlreadyOwnedByYouException)
        {
        }
    }

    public async Task UploadAsync(
        string blobName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = blobName,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
    }

    public async Task<byte[]?> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetObjectAsync(_bucketName, blobName, cancellationToken);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> BlobExistsAsync(string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucketName, blobName, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
