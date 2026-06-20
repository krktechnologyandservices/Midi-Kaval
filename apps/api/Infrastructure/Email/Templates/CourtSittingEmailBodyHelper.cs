using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal static class CourtSittingEmailBodyHelper
{
    internal const string PocsoPurposeLine = "Purpose: See Kaval Online (POCSO case).";

    public static string FormatPurposeLine(string purpose, SensitivityLevel sensitivityLevel) =>
        sensitivityLevel == SensitivityLevel.POCSO
            ? PocsoPurposeLine
            : $"Purpose: {purpose}";
}
