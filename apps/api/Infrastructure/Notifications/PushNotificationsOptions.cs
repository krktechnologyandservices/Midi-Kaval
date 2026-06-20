namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class PushNotificationsOptions
{
    public const string SectionName = "PushNotifications";

    public bool Enabled { get; set; }

    public string? ProjectId { get; set; }

    public string? CredentialsPath { get; set; }

    public string? CredentialsJson { get; set; }

    public bool IsConfigured() =>
        Enabled
        && !string.IsNullOrWhiteSpace(ProjectId)
        && (!string.IsNullOrWhiteSpace(CredentialsPath) || !string.IsNullOrWhiteSpace(CredentialsJson));
}
