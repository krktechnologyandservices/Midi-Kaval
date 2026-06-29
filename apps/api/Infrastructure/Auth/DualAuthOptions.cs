namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class DualAuthOptions
{
    public const string SectionName = "DualAuth";

    public bool Enabled { get; set; } = false;
}
