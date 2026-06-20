namespace MidiKaval.Api.Models.Notifications;

public sealed class RegisterUserDeviceRequest
{
    public string DeviceInstallId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PushToken { get; set; } = string.Empty;
}

public sealed class UserDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceInstallId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime LastRegisteredAtUtc { get; set; }
}
