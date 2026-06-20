namespace MidiKaval.Api.Models.Users;

public record StaffDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
