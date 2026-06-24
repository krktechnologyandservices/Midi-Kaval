using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Vendor;

public record CreateOrganisationRequest(
    [Required, StringLength(256, MinimumLength = 1)] string Name,
    [Required, EmailAddress, StringLength(320, MinimumLength = 1)] string TargetDirectorEmail
);
