namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class RefreshTokenRecord
{
    public Guid UserId { get; init; }
    public int TokenVersion { get; init; }
}

public enum RefreshTokenConsumeResult
{
    NotFound,
    Success,
    ReuseDetected,
}

public sealed class RefreshTokenConsumeOutcome
{
    public RefreshTokenConsumeResult Result { get; init; }
    public RefreshTokenRecord? Record { get; init; }
}
