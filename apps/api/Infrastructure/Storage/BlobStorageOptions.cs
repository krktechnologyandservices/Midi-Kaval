namespace MidiKaval.Api.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; init; } = string.Empty;
    public string ContainerName { get; init; } = "attachments";
    public int SasExpiryMinutes { get; init; } = 15;
    public long MaxUploadBytes { get; init; } = 10_485_760;
}
