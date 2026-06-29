using System.ComponentModel.DataAnnotations;

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
    /// <summary>Challenge identifier for OTP verification (null when requiresTotp is true — use TotpChallengeId instead).</summary>
    public Guid? ChallengeId { get; set; }

    /// <summary>Challenge identifier for TOTP verification (binds TOTP step to password step).</summary>
    public Guid? TotpChallengeId { get; set; }

    /// <summary>Seconds until the challenge expires.</summary>
    public int ExpiresInSeconds { get; set; }

    /// <summary>User id returned when requiresTotp is true (for verify-totp-login).</summary>
    public Guid UserId { get; set; }

    /// <summary>Token version at time of login (for TOTP login verification).</summary>
    public int TokenVersion { get; set; }

    /// <summary>If true, skip email OTP and prompt for TOTP code instead.</summary>
    public bool RequiresTotp { get; set; }
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

/// <summary>TOTP verification request for login (unauthenticated).</summary>
public sealed class VerifyTotpLoginRequest
{
    /// <summary>User id from the login response.</summary>
    public Guid UserId { get; set; }

    /// <summary>Challenge id that binds this request to the password login step.</summary>
    public Guid TotpChallengeId { get; set; }

    /// <summary>Token version at login time (checked to detect 2FA reset).</summary>
    public int TokenVersion { get; set; }

    /// <summary>Six-digit TOTP code from authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>Validates an invitation link without consuming the token.</summary>
public sealed record ValidateInvitationLinkResponse(
    string Email,
    string OrganisationName,
    string Role,
    bool IsValid
);

/// <summary>Invitation acceptance request.</summary>
public sealed class AcceptInvitationRequest
{
    /// <summary>Invitation token from the invite link.</summary>
    [Required(ErrorMessage = "Token is required.")]
    public string Token { get; set; } = string.Empty;

    /// <summary>HMAC signature for the token.</summary>
    [Required(ErrorMessage = "Signature is required.")]
    public string Signature { get; set; } = string.Empty;

    /// <summary>User's full name.</summary>
    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    [MaxLength(256, ErrorMessage = "Full name must be 256 characters or fewer.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Account password.</summary>
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Invitation acceptance response.</summary>
public sealed record AcceptInvitationResponse(
    string Email,
    string OrganisationName,
    string Message
);

/// <summary>Email confirmation request.</summary>
public sealed record ConfirmEmailRequest(
    string Token,
    string Signature
);

/// <summary>Email confirmation response.</summary>
public sealed record ConfirmEmailResponse(
    string Message
);

/// <summary>TOTP enrollment verification request (authenticated).</summary>
public sealed class VerifyTotpRequest
{
    /// <summary>Six-digit TOTP code from authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}
