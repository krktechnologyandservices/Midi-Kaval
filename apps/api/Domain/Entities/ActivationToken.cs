namespace MidiKaval.Api.Domain.Entities;

public class ActivationToken
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string TargetEmail { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int DeliveryAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Organisation Organisation { get; set; } = null!;
}
