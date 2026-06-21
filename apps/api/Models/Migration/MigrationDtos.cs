namespace MidiKaval.Api.Models.Migration;

/// <summary>Request to validate a legacy Excel file against the mapping spec.</summary>
public sealed class MigrationValidationResultDto
{
    public required int TotalColumns { get; init; }
    public required int MatchedColumns { get; init; }
    public required int UnmatchedColumns { get; init; }
    public required int MissingRequiredFields { get; init; }
    public required int DataTypeWarnings { get; init; }
    public required IReadOnlyList<MigrationColumnInfo> Matched { get; init; } = [];
    public required IReadOnlyList<MigrationColumnInfo> Unmatched { get; init; } = [];
    public required IReadOnlyList<MigrationMissingFieldInfo> MissingFields { get; init; } = [];
    public required IReadOnlyList<MigrationWarningInfo> Warnings { get; init; } = [];
    public required DateTime ValidatedAtUtc { get; init; }
    public bool IsValid => MissingRequiredFields == 0;
}

public sealed class MigrationColumnInfo
{
    public required string LegacyColumn { get; init; }
    public required string? TargetField { get; init; }
    public required string Status { get; init; } // "matched" | "unmatched"
}

public sealed class MigrationMissingFieldInfo
{
    public required string FieldName { get; init; }
    public required string Severity { get; init; } // "error" | "warning"
    public required string Message { get; init; }
}

public sealed class MigrationWarningInfo
{
    public required int ColumnIndex { get; init; }
    public required string ColumnName { get; init; }
    public required int? RowNumber { get; init; }
    public required string Message { get; init; }
}

// ── Import DTOs (Story 10.2) ──

/// <summary>Request payload for the import endpoint.</summary>
public sealed class MigrationImportRequest
{
    public bool DryRun { get; init; }
}

/// <summary>Result of a legacy Excel import run.</summary>
public sealed class MigrationImportResultDto
{
    public required int TotalRows { get; init; }
    public required int Created { get; init; }
    public required IReadOnlyList<MigrationImportRowResultDto> Skipped { get; init; } = [];
    public required IReadOnlyList<MigrationImportRowResultDto> Errors { get; init; } = [];
    public required DateTime ImportedAtUtc { get; init; }
}

/// <summary>Result for a single row during import.</summary>
public sealed class MigrationImportRowResultDto
{
    public required int RowIndex { get; init; }
    public required string? CrimeNumber { get; init; }
    public required string? StNumber { get; init; }
    public required string Status { get; init; } // "created" | "skipped" | "error"
    public required string Reason { get; init; }
}
