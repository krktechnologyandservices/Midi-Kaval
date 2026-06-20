using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Infrastructure.Cases;

public static class BeneficiaryDisplayFormatter
{
    public static string ToInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "—";
        }

        var tokens = fullName.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return "—";
        }

        if (tokens.Length == 1)
        {
            return $"{char.ToUpperInvariant(tokens[0][0])}.";
        }

        return $"{char.ToUpperInvariant(tokens[0][0])}. {char.ToUpperInvariant(tokens[1][0])}.";
    }

    public static string FormatBeneficiaryName(Case entity, bool redactPocsoForFieldWorker)
    {
        if (redactPocsoForFieldWorker && entity.SensitivityLevel == SensitivityLevel.POCSO)
        {
            return ToInitials(entity.BeneficiaryName);
        }

        return entity.BeneficiaryName;
    }
}
