namespace MidiKaval.Api.Domain.Entities;

public sealed class ConfirmationToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? InvitationId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int DeliveryAttempts { get; set; }
    public DateTime? LastDeliveryAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Invitation? Invitation { get; set; }
}
