using System.Text.Json.Serialization;

namespace MidiKaval.Api.Infrastructure.Migration;

/// <summary>Machine-readable mapping rule from a legacy Excel column to a Kaval Case field.</summary>
public sealed class MappingRule
{
    public required string LegacyColumn { get; init; }
    public required string? TargetField { get; init; }
    public required string Transform { get; init; } // "direct" | "trimUpper" | "parseInt" | "enum" | "truncate" | "boolYesNo" | "default"
    public required string FieldType { get; init; } // "string" | "int" | "enum" | "bool"
    public required bool IsRequired { get; init; }
    public string? NullDefault { get; init; }
    public string? MaxLength { get; init; }
    public string? EnumValues { get; init; } // comma-separated for enum types
    public string? Notes { get; init; }
}

/// <summary>Loaded mapping specification from docs/excel-migration/mapping-specification.md.</summary>
public sealed class MappingSpecification
{
    public required IReadOnlyList<MappingRule> ColumnMappings { get; init; } = [];
    public required IReadOnlyList<EnumMapping> EnumMappings { get; init; } = [];
}

/// <summary>Maps a legacy enum value to a Kaval enum string.</summary>
public sealed class EnumMapping
{
    public required string TargetField { get; init; }
    public required string LegacyValue { get; init; }
    public required string KavalValue { get; init; }
}
