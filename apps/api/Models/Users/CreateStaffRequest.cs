using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Users;

public record CreateStaffRequest(
    [Required][EmailAddress][MaxLength(320)] string Email,
    [Required][MaxLength(128)] string FirstName,
    [Required][MaxLength(128)] string LastName,
    [Required] string Role,
    [Phone][MaxLength(30)] string? PhoneNumber);
