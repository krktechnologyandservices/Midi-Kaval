using System.Text;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (options.AccessTokenMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Jwt:AccessTokenMinutes must be positive.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey)
            || Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            return ValidateOptionsResult.Fail("Jwt:SigningKey must be at least 32 bytes.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail("Jwt:Issuer and Jwt:Audience are required.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class OtpOptionsValidator : IValidateOptions<OtpOptions>
{
    public ValidateOptionsResult Validate(string? name, OtpOptions options)
    {
        if (options.ExpiryMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Otp:ExpiryMinutes must be positive.");
        }

        if (options.MaxAttempts <= 0)
        {
            return ValidateOptionsResult.Fail("Otp:MaxAttempts must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
