using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Migration;

/// <summary>Loads the mapping specification from a JSON config file or hardcoded defaults.</summary>
public sealed class MappingSpecLoader
{
    private readonly MappingSpecOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MappingSpecLoader(IOptions<MappingSpecOptions> options)
    {
        _options = options.Value;
    }

    public MappingSpecification Load()
    {
        // Try loading from JSON config file first
        if (!string.IsNullOrEmpty(_options.SpecFilePath))
        {
            var spec = TryLoadFromJson(_options.SpecFilePath);
            if (spec is not null)
                return spec;

            // AC5: If the spec file path is configured but the file can't be found/loaded,
            // throw so the controller can return 400
            throw new InvalidOperationException(
                $"Mapping specification file not found or could not be loaded at '{_options.SpecFilePath}'. " +
                "Ensure Story 10.1 is complete and the mapping spec file exists.");
        }

        // Hardcoded defaults (only reached when SpecFilePath is empty — dev/test mode)
        return new MappingSpecification
        {
            ColumnMappings =
            [
                new MappingRule { LegacyColumn = "CR Number", TargetField = "crimeNumber", Transform = "trimUpper", FieldType = "string", IsRequired = true, MaxLength = "64", Notes = "Trim + ToUpperInvariant. Unique per org." },
                new MappingRule { LegacyColumn = "ST Number", TargetField = "stNumber", Transform = "trimUpper", FieldType = "string", IsRequired = true, MaxLength = "64", Notes = "Trim + ToUpperInvariant. Unique per org." },
                new MappingRule { LegacyColumn = "Beneficiary Name", TargetField = "beneficiaryName", Transform = "direct", FieldType = "string", IsRequired = true, MaxLength = "256" },
                new MappingRule { LegacyColumn = "Age", TargetField = "beneficiaryAge", Transform = "parseInt", FieldType = "int", IsRequired = false, NullDefault = "null" },
                new MappingRule { LegacyColumn = "Contact No", TargetField = "beneficiaryContact", Transform = "truncate", FieldType = "string", IsRequired = false, MaxLength = "32", NullDefault = "null" },
                new MappingRule { LegacyColumn = "Type of Offence", TargetField = "typeOfOffence", Transform = "direct", FieldType = "string", IsRequired = true, MaxLength = "128" },
                new MappingRule { LegacyColumn = "Classification", TargetField = "offenceClassification", Transform = "enum", FieldType = "enum", IsRequired = true, EnumValues = "Petty,Serious,Heinous" },
                new MappingRule { LegacyColumn = "Domicile/Area", TargetField = "domicile", Transform = "enum", FieldType = "enum", IsRequired = true, EnumValues = "Urban,Rural,Coastal,Tribal,Slum" },
                new MappingRule { LegacyColumn = "First Offender", TargetField = "isFirstTimeOffender", Transform = "boolYesNo", FieldType = "bool", IsRequired = false, NullDefault = "true" },
                // New socio-demographic fields (Epic 11)
                new MappingRule { LegacyColumn = "Gender", TargetField = "gender", Transform = "enum", FieldType = "enum", IsRequired = false, NullDefault = "null" },
                new MappingRule { LegacyColumn = "Family Type", TargetField = "familyType", Transform = "enum", FieldType = "enum", IsRequired = false, NullDefault = "null" },
                new MappingRule { LegacyColumn = "Economic Status", TargetField = "economicStatus", Transform = "enum", FieldType = "enum", IsRequired = false, NullDefault = "null" },
                new MappingRule { LegacyColumn = "Occupation", TargetField = "occupationText", Transform = "direct", FieldType = "string", IsRequired = false, Notes = "Informational only — not stored on Case. Post-migration legend cross-referencing needed." },
                new MappingRule { LegacyColumn = "Education Level", TargetField = "educationLevelText", Transform = "direct", FieldType = "string", IsRequired = false, Notes = "Informational only — not stored on Case. Post-migration legend cross-referencing needed." },
                new MappingRule { LegacyColumn = "Recidivism Before", TargetField = "recidivismBeforeCount", Transform = "parseInt", FieldType = "int", IsRequired = false, NullDefault = "null" },
                new MappingRule { LegacyColumn = "Recidivism After", TargetField = "recidivismAfterCount", Transform = "parseInt", FieldType = "int", IsRequired = false, NullDefault = "null" },
                new MappingRule { LegacyColumn = "Family History of Crime", TargetField = "familyHistoryOfCrime", Transform = "boolYesNo", FieldType = "bool", IsRequired = false, NullDefault = "false" },
            ],
            EnumMappings =
            [
                new EnumMapping { TargetField = "offenceClassification", LegacyValue = "Petty", KavalValue = "Petty" },
                new EnumMapping { TargetField = "offenceClassification", LegacyValue = "Minor", KavalValue = "Petty" },
                new EnumMapping { TargetField = "offenceClassification", LegacyValue = "Serious", KavalValue = "Serious" },
                new EnumMapping { TargetField = "offenceClassification", LegacyValue = "Grave", KavalValue = "Serious" },
                new EnumMapping { TargetField = "offenceClassification", LegacyValue = "Heinous", KavalValue = "Heinous" },
                new EnumMapping { TargetField = "offenceClassification", LegacyValue = "Heinous/Grave", KavalValue = "Heinous" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Urban", KavalValue = "Urban" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "City", KavalValue = "Urban" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Rural", KavalValue = "Rural" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Village", KavalValue = "Rural" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Coastal", KavalValue = "Coastal" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Coastal Area", KavalValue = "Coastal" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Tribal", KavalValue = "Tribal" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Tribal Area", KavalValue = "Tribal" },
                new EnumMapping { TargetField = "domicile", LegacyValue = "Slum", KavalValue = "Slum" },
                // New enum mappings (Epic 11 — Gender)
                new EnumMapping { TargetField = "gender", LegacyValue = "Male", KavalValue = "Male" },
                new EnumMapping { TargetField = "gender", LegacyValue = "M", KavalValue = "Male" },
                new EnumMapping { TargetField = "gender", LegacyValue = "Female", KavalValue = "Female" },
                new EnumMapping { TargetField = "gender", LegacyValue = "F", KavalValue = "Female" },
                new EnumMapping { TargetField = "gender", LegacyValue = "Transgender", KavalValue = "Transgender" },
                new EnumMapping { TargetField = "gender", LegacyValue = "TG", KavalValue = "Transgender" },
                new EnumMapping { TargetField = "gender", LegacyValue = "Trans", KavalValue = "Transgender" },
                // New enum mappings (Epic 11 — FamilyType)
                new EnumMapping { TargetField = "familyType", LegacyValue = "Joint", KavalValue = "Joint" },
                new EnumMapping { TargetField = "familyType", LegacyValue = "Nuclear", KavalValue = "Nuclear" },
                new EnumMapping { TargetField = "familyType", LegacyValue = "Single Parent", KavalValue = "SingleParent" },
                new EnumMapping { TargetField = "familyType", LegacyValue = "SingleParent", KavalValue = "SingleParent" },
                new EnumMapping { TargetField = "familyType", LegacyValue = "Others", KavalValue = "Others" },
                new EnumMapping { TargetField = "familyType", LegacyValue = "Other", KavalValue = "Others" },
                // New enum mappings (Epic 11 — EconomicStatus)
                new EnumMapping { TargetField = "economicStatus", LegacyValue = "APL", KavalValue = "APL" },
                new EnumMapping { TargetField = "economicStatus", LegacyValue = "BPL", KavalValue = "BPL" },
                new EnumMapping { TargetField = "economicStatus", LegacyValue = "Below Poverty Line", KavalValue = "BPL" },
                new EnumMapping { TargetField = "economicStatus", LegacyValue = "Above Poverty Line", KavalValue = "APL" },
            ],
        };
    }

    private MappingSpecification? TryLoadFromJson(string relativePath)
    {
        // Try several base directories to locate the file:
        // 1. AppContext.BaseDirectory (bin output — file copied to output)
        // 2. Directory.GetCurrentDirectory() (project root)
        // 3. Walk up from current directory looking for the solution root
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
        };

        // Walk up max 5 levels looking for the docs folder
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 5 && dir is not null; i++, dir = dir.Parent)
        {
            candidates.Add(Path.Combine(dir.FullName, relativePath));
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<MappingSpecification>(json, JsonOptions);
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }

        return null;
    }
}

public sealed class MappingSpecOptions
{
    public const string SectionName = "MappingSpec";
    public string SpecFilePath { get; set; } = "docs/excel-migration/mapping-spec.json";
}
