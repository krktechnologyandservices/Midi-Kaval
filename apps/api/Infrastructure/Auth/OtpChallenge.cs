namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class OtpChallenge
{
    public Guid UserId { get; init; }
    public Guid OrganisationId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string OtpHash { get; init; } = string.Empty;
    public int TokenVersion { get; init; }
    public int FailedAttempts { get; set; }
}
