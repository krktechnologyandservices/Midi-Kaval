using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Users;

public record UpdateStaffRequest(
    [Required][MaxLength(128)] string FirstName,
    [Required][MaxLength(128)] string LastName,
    [Phone][MaxLength(30)] string? PhoneNumber,
    string? Role);
