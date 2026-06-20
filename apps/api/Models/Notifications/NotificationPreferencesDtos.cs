namespace MidiKaval.Api.Models.Notifications;

public sealed class NotificationPreferencesDto
{
    public bool PushEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public NotificationPreferenceChannelsDto Channels { get; set; } = new();
}

public sealed class NotificationPreferenceChannelsDto
{
    public bool Visits { get; set; }
    public bool Court { get; set; }
    public bool Interventions { get; set; }
    public bool Claims { get; set; }
    public bool Reports { get; set; }
    public bool Assignments { get; set; }
}
