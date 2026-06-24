namespace MidiKaval.Api.Models.Vendor;

public record CreateOrganisationResponse(
    Guid OrganisationId,
    string Name,
    string Status  // "activation_sent" | "delivery_failed"
);
