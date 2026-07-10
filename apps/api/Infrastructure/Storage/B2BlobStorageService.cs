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
        var buckets = await _client.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets.Any(b => b.BucketName == _bucketName))
        {
            return;
        }

        await _client.PutBucketAsync(_bucketName, cancellationToken);
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
