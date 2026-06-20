namespace MidiKaval.Api.Models.Audit;

public record AuditEventDto(
    Guid Id,
    string EventType,
    DateTime CreatedAtUtc,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorName,
    Guid? SubjectUserId,
    string? SubjectEmail,
    string? SubjectName,
    object? Metadata);
