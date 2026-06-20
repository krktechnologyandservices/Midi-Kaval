namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
}

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
}

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Auth";

    public int RateLimitPermitLimit { get; set; } = 10;
    public int RateLimitWindowSeconds { get; set; } = 60;
}

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int ExpiryDays { get; set; } = 7;
    public int MaxActivePerUser { get; set; } = 5;
}

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int ExpiryMinutes { get; set; } = 60;
    public string WebResetUrl { get; set; } = "http://localhost:4200/reset-password";
}
