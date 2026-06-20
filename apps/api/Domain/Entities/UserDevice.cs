namespace MidiKaval.Api.Domain.Entities;

public sealed class UserDevice
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid UserId { get; set; }
    public string DeviceInstallId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PushToken { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime LastRegisteredAtUtc { get; set; }
}
