using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MidiKaval.Api.Models.Admin;

public sealed record SuspendUserRequest(
    [MaxLength(500)]
    string? Reason
);

public sealed record SuspendUserResponse(
    Guid Id,
    bool IsSuspended,
    [property: JsonPropertyName("actionedAtUtc")] DateTime SuspendedAtUtc,
    string Message
);

public sealed record ReactivateUserResponse(
    Guid Id,
    bool IsSuspended,
    [property: JsonPropertyName("actionedAtUtc")] DateTime ReactivatedAtUtc,
    string Message
);

public sealed record DeleteUserRequest(
    [Required] string ConfirmationEmail
);

public sealed record DeleteUserResponse(
    Guid Id,
    [property: JsonPropertyName("deletedAtUtc")] DateTime DeletedAtUtc,
    string Message
);

public sealed record ResetTwoFactorResponse(
    Guid Id,
    string Message
);
