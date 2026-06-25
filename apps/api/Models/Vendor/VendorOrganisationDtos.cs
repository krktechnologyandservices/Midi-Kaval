using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Vendor;

public sealed record VendorOrganisationSummary(
    Guid Id,
    string Name,
    bool IsActive,
    int DirectorCount,
    bool HasPendingRecovery,
    DateTime CreatedAtUtc
);

public sealed record VendorOrganisationDetail(
    Guid Id,
    string Name,
    bool IsActive,
    int DirectorCount,
    bool HasPendingRecovery,
    string? LastKnownDirectorName,
    DateTime? LastKnownDirectorActiveAt,
    DateTime CreatedAtUtc
);

public sealed record ReissueActivationRequest(
    [Required, EmailAddress, StringLength(320)] string TargetDirectorEmail
);

public sealed record ReissueActivationResponse(
    string Status,
    string TargetDirectorEmail
);
