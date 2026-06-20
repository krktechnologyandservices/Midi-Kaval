namespace MidiKaval.Api.Models.Auth;

/// <summary>Login request with email and password.</summary>
public sealed class LoginRequest
{
    /// <summary>User email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>User password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Login challenge issued after password validation.</summary>
public sealed class LoginResponse
{
    /// <summary>Challenge identifier for OTP verification.</summary>
    public Guid ChallengeId { get; set; }

    /// <summary>Seconds until the OTP challenge expires.</summary>
    public int ExpiresInSeconds { get; set; }
}

/// <summary>OTP verification request.</summary>
public sealed class VerifyOtpRequest
{
    /// <summary>Challenge identifier from login.</summary>
    public Guid ChallengeId { get; set; }

    /// <summary>Six-digit OTP code from email.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>Authenticated user summary.</summary>
public sealed class AuthUserDto
{
    /// <summary>User identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>User email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>User role.</summary>
    public string Role { get; set; } = string.Empty;
}

/// <summary>Tokens issued after successful OTP verification.</summary>
public sealed class VerifyOtpResponse
{
    /// <summary>JWT access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Access token lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Opaque refresh token for mobile clients.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Authenticated user profile.</summary>
    public AuthUserDto User { get; set; } = new();
}

/// <summary>Refresh token request for mobile clients.</summary>
public sealed class RefreshRequest
{
    /// <summary>Opaque refresh token (optional when cookie is set).</summary>
    public string? RefreshToken { get; set; }
}

/// <summary>Logout request for mobile clients.</summary>
public sealed class LogoutRequest
{
    /// <summary>Opaque refresh token (optional when cookie is set).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Stable device install id to unregister push token on logout.</summary>
    public string? DeviceInstallId { get; set; }
}

/// <summary>Tokens issued after successful refresh.</summary>
public sealed class RefreshResponse
{
    /// <summary>JWT access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Access token lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Rotated opaque refresh token.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>Current session user profile.</summary>
public sealed class SessionUserDto
{
    /// <summary>User identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>User email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>User role.</summary>
    public string Role { get; set; } = string.Empty;
}

/// <summary>Forgot-password request.</summary>
public sealed class ForgotPasswordRequest
{
    /// <summary>Account email address.</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>Generic forgot-password acknowledgement.</summary>
public sealed class ForgotPasswordResponse
{
    /// <summary>User-facing message (always generic).</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Reset-password request.</summary>
public sealed class ResetPasswordRequest
{
    /// <summary>Opaque token from the reset email link.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>New password (min 8 characters).</summary>
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Reset-password success acknowledgement.</summary>
public sealed class ResetPasswordResponse
{
    /// <summary>User-facing success message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Step-up OTP challenge issued to an authenticated field worker.</summary>
public sealed class StepUpResponse
{
    /// <summary>Challenge identifier for step-up OTP verification.</summary>
    public Guid ChallengeId { get; set; }

    /// <summary>Seconds until the OTP challenge expires.</summary>
    public int ExpiresInSeconds { get; set; }
}

/// <summary>Step-up OTP verification request.</summary>
public sealed class VerifyStepUpRequest
{
    /// <summary>Challenge identifier from step-up.</summary>
    public Guid ChallengeId { get; set; }

    /// <summary>Six-digit OTP code from email.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>Step-up OTP verification acknowledgement.</summary>
public sealed class VerifyStepUpResponse
{
    /// <summary>Whether step-up verification succeeded.</summary>
    public bool Verified { get; set; }
}
