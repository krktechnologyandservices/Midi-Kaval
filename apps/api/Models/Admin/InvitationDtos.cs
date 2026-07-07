using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Admin;

public sealed record SendInvitationRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(50)] string Role,
    bool Include2faInstructions = false
);

public sealed record InvitationSummary(
    Guid Id,
    string TargetEmail,
    string Role,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? ConfirmedAtUtc,
    string? InvitedByUserEmail = null,
    string? InvitedByUserName = null,
    // When Status == "confirmed", the invited user has submitted the sign-up form but their
    // account only becomes active once they click the separate confirmation-email link. This
    // is that link's actual consumption time — null means the account is still not yet active,
    // even though the invitation itself already reads "confirmed".
    DateTime? EmailConfirmedAtUtc = null
);

public sealed record InvitationListResult(
    List<InvitationSummary> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record SendInvitationResponse(
    Guid Id,
    string TargetEmail,
    string Role,
    string Message
);

public sealed record ResendInvitationResponse(
    Guid Id,
    string TargetEmail,
    DateTime NewExpiresAtUtc,
    string Message
);
