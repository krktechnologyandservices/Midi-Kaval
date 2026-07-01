namespace MidiKaval.Api.Models.Admin;

public sealed record BypassCodeResponse(
    string BypassCode,
    int ExpiresInSeconds
);
