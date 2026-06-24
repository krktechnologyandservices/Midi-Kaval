using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MidiKaval.Api.Models.Auth;

/// <summary>Request to activate an organisation using an activation link.</summary>
public sealed class ActivateOrganisationRequest
{
    /// <summary>Raw activation token from the URL.</summary>
    [Required, JsonRequired]
    public string Token { get; set; } = string.Empty;

    /// <summary>HMAC signature from the URL.</summary>
    [Required, JsonRequired]
    public string Signature { get; set; } = string.Empty;

    /// <summary>Full name of the first Director (will be split into first/last).</summary>
    [Required, StringLength(256, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Password meeting minimum policy (8+ chars, upper, lower, digit).</summary>
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Response returned after successful organisation activation.</summary>
public sealed class ActivateOrganisationResponse
{
    /// <summary>Newly created Director user ID.</summary>
    public Guid UserId { get; set; }

    /// <summary>Activated organisation ID.</summary>
    public Guid OrganisationId { get; set; }

    /// <summary>Activated organisation name.</summary>
    public string OrganisationName { get; set; } = string.Empty;

    /// <summary>Success message for the frontend.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Preliminary validation response — does NOT consume the token.</summary>
public sealed class ValidateActivationLinkResponse
{
    /// <summary>Target email from the activation token.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Organisation name associated with the token.</summary>
    public string OrganisationName { get; set; } = string.Empty;
}
