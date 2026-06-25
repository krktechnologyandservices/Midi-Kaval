namespace MidiKaval.Api.Domain.Entities;

public sealed class Invitation
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public string TargetEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = InvitationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }

    public Organisation Organisation { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
}

public static class InvitationStatus
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Expired = "expired";
}
