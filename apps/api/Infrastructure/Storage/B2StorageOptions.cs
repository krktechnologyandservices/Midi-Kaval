namespace MidiKaval.Api.Infrastructure.Storage;

public sealed class B2StorageOptions
{
    public const string SectionName = "B2Storage";

    public string Region { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = "attachments";
    public long MaxUploadBytes { get; init; } = 10_485_760;
}
