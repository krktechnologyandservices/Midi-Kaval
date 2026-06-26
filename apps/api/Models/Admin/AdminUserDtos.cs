namespace MidiKaval.Api.Models.Admin;

public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    bool IsSuspended,
    DateTime? SuspendedAtUtc,
    DateTime CreatedAtUtc
);

public sealed record AdminUserListResult(
    List<AdminUserSummary> Items,
    int TotalCount,
    int Page,
    int PageSize
);
