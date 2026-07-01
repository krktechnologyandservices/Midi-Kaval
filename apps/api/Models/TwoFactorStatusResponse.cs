namespace MidiKaval.Api.Models;

public sealed record TwoFactorStatusResponse
{
    public bool Enrolled { get; init; }
    public DateTime? EnrolledAt { get; init; }
}
