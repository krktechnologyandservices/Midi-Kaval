namespace MidiKaval.Api.Models.Notifications;

public sealed class NotificationDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class NotificationListResultDto
{
    public IReadOnlyList<NotificationDto> Items { get; set; } = Array.Empty<NotificationDto>();
}
