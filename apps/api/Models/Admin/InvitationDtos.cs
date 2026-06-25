using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Admin;

public sealed record SendInvitationRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(50)] string Role
);

public sealed record InvitationSummary(
    Guid Id,
    string TargetEmail,
    string Role,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? ConfirmedAtUtc
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
