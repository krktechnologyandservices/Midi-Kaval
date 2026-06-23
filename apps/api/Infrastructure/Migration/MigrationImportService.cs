using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Migration;

namespace MidiKaval.Api.Infrastructure.Migration;

/// <summary>Processes a legacy Excel file, mapping rows to Cases per the mapping specification.
/// Supports dry-run mode where validation runs but no data is written.</summary>
public sealed class MigrationImportService(
    AppDbContext db,
    MappingSpecLoader specLoader,
    ILogger<MigrationImportService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int SaveBatchSize = 100;

    public async Task<MigrationImportResultDto> ImportAsync(
        IXLWorkbook workbook,
        bool dryRun,
        Guid organisationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var spec = specLoader.Load();
        var ws = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("The workbook contains no worksheets.");

        var headerRow = ws.Row(1);
        var totalColumns = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        var columnRules = new List<(int Index, MappingRule Rule)>();
        for (int i = 1; i <= totalColumns; i++)
        {
            var name = headerRow.Cell(i).GetString().Trim().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(name)) continue;

            var rule = spec.ColumnMappings.FirstOrDefault(r =>
                r.LegacyColumn.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (rule is not null)
                columnRules.Add((i, rule));
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow < 2)
        {
            return new MigrationImportResultDto
            {
                TotalRows = 0,
                Created = 0,
                Skipped = [],
                Errors = [],
                ImportedAtUtc = DateTime.UtcNow,
            };
        }

        var totalRows = lastRow - 1;
        var skipped = new List<MigrationImportRowResultDto>();
        var errors = new List<MigrationImportRowResultDto>();
        var created = 0;
        var now = DateTime.UtcNow;

        // Pre-load existing crime/ST numbers for the organisation (patch: N+1 fix)
        var existingCrimeNumbers = await db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .Select(c => new { c.CrimeNumber, c.StNumber })
            .ToListAsync(cancellationToken);
        var existingCrimeSet = existingCrimeNumbers
            .Select(c => c.CrimeNumber)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingStSet = existingCrimeNumbers
            .Select(c => c.StNumber)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Track intra-file crime/ST numbers for dry-run duplicate detection (patch)
        var seenCrimeNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenStNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Wrap the import in a transaction for rollback on failure (patch)
        using var transaction = dryRun
            ? null
            : await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            for (int rowIdx = 2; rowIdx <= lastRow; rowIdx++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = ws.Row(rowIdx);
                var rowResult = await ProcessRowAsync(
                    row, rowIdx, columnRules, spec, organisationId, actorUserId,
                    dryRun, now, existingCrimeSet, existingStSet,
                    seenCrimeNumbers, seenStNumbers, cancellationToken);

                switch (rowResult.Status)
                {
                    case "created":
                        created++;
                        break;
                    case "skipped":
                        skipped.Add(rowResult);
                        break;
                    case "error":
                        errors.Add(rowResult);
                        break;
                }

                // Batch SaveChangesAsync every N rows (patch)
                if (!dryRun && created > 0 && created % SaveBatchSize == 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            // Flush remaining changes
            if (!dryRun)
            {
                await db.SaveChangesAsync(cancellationToken);

                // Audit event: import completed summary
                db.AuditEvents.Add(new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = organisationId,
                    ActorUserId = actorUserId,
                    SubjectUserId = null,
                    EventType = AuditEventTypes.MigrationImportCompleted,
                    MetadataJson = JsonSerializer.Serialize(
                        new Dictionary<string, object?>
                        {
                            ["totalRows"] = totalRows,
                            ["created"] = created,
                            ["skipped"] = skipped.Count,
                            ["errors"] = errors.Count,
                        },
                        JsonOptions),
                    CreatedAtUtc = now,
                });
                await db.SaveChangesAsync(cancellationToken);

                await transaction!.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        logger.LogInformation(
            "Migration import {Mode}: {Created} created, {Skipped} skipped, {Errors} errors out of {Total} rows",
            dryRun ? "dry-run" : "live", created, skipped.Count, errors.Count, totalRows);

        return new MigrationImportResultDto
        {
            TotalRows = totalRows,
            Created = dryRun ? 0 : created,
            Skipped = skipped,
            Errors = errors,
            ImportedAtUtc = now,
        };
    }

    private async Task<MigrationImportRowResultDto> ProcessRowAsync(
        IXLRow row,
        int rowIndex,
        List<(int Index, MappingRule Rule)> columnRules,
        MappingSpecification spec,
        Guid organisationId,
        Guid actorUserId,
        bool dryRun,
        DateTime now,
        HashSet<string> existingCrimeSet,
        HashSet<string> existingStSet,
        HashSet<string> seenCrimeNumbers,
        HashSet<string> seenStNumbers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var caseValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        // Extract and transform values for each mapped column
        foreach (var (colIndex, rule) in columnRules)
        {
            var rawValue = row.Cell(colIndex).GetString().Trim();
            var transformed = TransformField(rawValue, rule, spec, warnings);
            if (rule.TargetField is not null)
                caseValues[rule.TargetField] = transformed;
        }

        // Validate required fields
        var missingRequired = new List<string>();
        foreach (var rule in spec.ColumnMappings.Where(r => r.IsRequired && r.TargetField is not null))
        {
            if (!caseValues.TryGetValue(rule.TargetField!, out var val) ||
                val is null ||
                (val is string s && string.IsNullOrWhiteSpace(s)))
            {
                missingRequired.Add(rule.TargetField!);
            }
        }

        if (missingRequired.Count > 0)
        {
            return new MigrationImportRowResultDto
            {
                RowIndex = rowIndex,
                CrimeNumber = caseValues.GetValueOrDefault("crimeNumber")?.ToString(),
                StNumber = caseValues.GetValueOrDefault("stNumber")?.ToString(),
                Status = "error",
                Reason = $"Missing required fields: {string.Join(", ", missingRequired)}",
            };
        }

        var crimeNumber = caseValues.GetValueOrDefault("crimeNumber")?.ToString() ?? "";
        var stNumber = caseValues.GetValueOrDefault("stNumber")?.ToString() ?? "";

        // Check for unmapped enum values (patch)
        if (warnings.Count > 0)
        {
            return new MigrationImportRowResultDto
            {
                RowIndex = rowIndex,
                CrimeNumber = crimeNumber,
                StNumber = stNumber,
                Status = "error",
                Reason = $"Data mapping issue: {string.Join("; ", warnings)}",
            };
        }

        // Duplicate check: DB + intra-file (patch)
        var isDuplicate = IsDuplicate(crimeNumber, stNumber, existingCrimeSet, existingStSet);

        if (!isDuplicate && dryRun)
        {
            isDuplicate = IsIntraFileDuplicate(crimeNumber, stNumber, seenCrimeNumbers, seenStNumbers);
        }

        if (isDuplicate)
        {
            return new MigrationImportRowResultDto
            {
                RowIndex = rowIndex,
                CrimeNumber = crimeNumber,
                StNumber = stNumber,
                Status = "skipped",
                Reason = $"Duplicate crime number ({crimeNumber}) or ST number ({stNumber})",
            };
        }

        // Track seen identifiers for intra-file duplicate detection
        if (!string.IsNullOrEmpty(crimeNumber))
            seenCrimeNumbers.Add(crimeNumber);
        if (!string.IsNullOrEmpty(stNumber))
            seenStNumbers.Add(stNumber);

        if (dryRun)
        {
            return new MigrationImportRowResultDto
            {
                RowIndex = rowIndex,
                CrimeNumber = crimeNumber,
                StNumber = stNumber,
                Status = "created",
                Reason = "",
            };
        }

        // Create the Case
        var caseId = Guid.NewGuid();
        var entity = new Case
        {
            Id = caseId,
            OrganisationId = organisationId,
            CrimeNumber = crimeNumber,
            StNumber = stNumber,
            BeneficiaryName = caseValues.GetValueOrDefault("beneficiaryName")?.ToString() ?? "",
            BeneficiaryAge = caseValues.GetValueOrDefault("beneficiaryAge") as int?,
            BeneficiaryContact = caseValues.GetValueOrDefault("beneficiaryContact")?.ToString(),
            TypeOfOffence = caseValues.GetValueOrDefault("typeOfOffence")?.ToString() ?? "",
            OffenceClassification = ParseEnum<OffenceClassification>(caseValues.GetValueOrDefault("offenceClassification")?.ToString()),
            Domicile = ParseEnum<Domicile>(caseValues.GetValueOrDefault("domicile")?.ToString()),
            IsFirstTimeOffender = caseValues.GetValueOrDefault("isFirstTimeOffender") as bool? ?? true,
            // New socio-demographic fields (Epic 11)
            Gender = ParseNullableEnum<Gender>(caseValues.GetValueOrDefault("gender")?.ToString()),
            FamilyType = ParseNullableEnum<FamilyType>(caseValues.GetValueOrDefault("familyType")?.ToString()),
            EconomicStatus = ParseNullableEnum<EconomicStatus>(caseValues.GetValueOrDefault("economicStatus")?.ToString()),
            OccupationId = null,
            EducationLevelId = null,
            RecidivismBeforeCount = caseValues.GetValueOrDefault("recidivismBeforeCount") as int?,
            RecidivismAfterCount = caseValues.GetValueOrDefault("recidivismAfterCount") as int?,
            FamilyHistoryOfCrime = caseValues.GetValueOrDefault("familyHistoryOfCrime") as bool? ?? false,
            CurrentStage = CaseStage.ProcessInitiation,
            VisitCount = 0,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Cases.Add(entity);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseImported,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["crimeNumber"] = crimeNumber,
                    ["stNumber"] = stNumber,
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        // Track in DB set for subsequent duplicate checks within the same import
        if (!string.IsNullOrEmpty(crimeNumber))
            existingCrimeSet.Add(crimeNumber);
        if (!string.IsNullOrEmpty(stNumber))
            existingStSet.Add(stNumber);

        return new MigrationImportRowResultDto
        {
            RowIndex = rowIndex,
            CrimeNumber = crimeNumber,
            StNumber = stNumber,
            Status = "created",
            Reason = "",
        };
    }

    private static bool IsDuplicate(
        string crimeNumber, string stNumber,
        HashSet<string> existingCrimeSet, HashSet<string> existingStSet)
    {
        if (!string.IsNullOrEmpty(crimeNumber) && existingCrimeSet.Contains(crimeNumber))
            return true;
        if (!string.IsNullOrEmpty(stNumber) && existingStSet.Contains(stNumber))
            return true;
        return false;
    }

    private static bool IsIntraFileDuplicate(
        string crimeNumber, string stNumber,
        HashSet<string> seenCrimeNumbers, HashSet<string> seenStNumbers)
    {
        if (!string.IsNullOrEmpty(crimeNumber) && seenCrimeNumbers.Contains(crimeNumber))
            return true;
        if (!string.IsNullOrEmpty(stNumber) && seenStNumbers.Contains(stNumber))
            return true;
        return false;
    }

    private static object? TransformField(string rawValue, MappingRule rule, MappingSpecification spec, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(rawValue) && rule.NullDefault is not null)
            return rule.NullDefault == "null" ? null : rule.NullDefault;

        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var trimmed = rawValue.Trim();

        return rule.Transform switch
        {
            "trimUpper" => trimmed.ToUpperInvariant(),
            "direct" => trimmed,
            "parseInt" => int.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var age)
                ? age
                : null,
            "enum" => MapEnumValue(trimmed, rule, spec, warnings),
            "truncate" when rule.MaxLength is not null && int.TryParse(rule.MaxLength, out var maxLen)
                => trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed,
            "truncate" => trimmed,
            "boolYesNo" => trimmed.Equals("Yes", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Y", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("True", StringComparison.OrdinalIgnoreCase),
            _ => trimmed,
        };
    }

    private static string? MapEnumValue(string trimmedValue, MappingRule rule, MappingSpecification spec, List<string> warnings)
    {
        if (rule.TargetField is null) return null;

        // Try exact match via enum mappings first
        var mapping = spec.EnumMappings
            .FirstOrDefault(e =>
                e.TargetField.Equals(rule.TargetField, StringComparison.OrdinalIgnoreCase) &&
                e.LegacyValue.Equals(trimmedValue, StringComparison.OrdinalIgnoreCase));

        if (mapping is not null)
            return mapping.KavalValue;

        // Check if the value matches a direct enum value
        var enumValues = rule.EnumValues?.Split(',').Select(v => v.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        if (enumValues.Contains(trimmedValue))
            return trimmedValue;

        // For optional fields, unrecognised values silently produce null (not a rejection)
        if (!rule.IsRequired)
            return null;

        // Unmapped value on required field — add warning (row will be rejected)
        warnings.Add($"Column '{rule.LegacyColumn}' has unrecognised value '{trimmedValue}'. Expected one of: {string.Join(", ", enumValues)}");
        return null;
    }

    private static T ParseEnum<T>(string? value) where T : struct, Enum
    {
        if (value is null) return default;
        return Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : default;
    }

    private static T? ParseNullableEnum<T>(string? value) where T : struct, Enum
    {
        if (value is null) return null;
        return Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : null;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (var current = (Exception?)ex; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException { SqlState: "23505" })
                return true;
        }
        return false;
    }

    /// <summary>Validates that the stream contains a valid .xlsx file by checking PK\x03\x04 magic bytes.
    /// Must be called after CopyToAsync — stream must be at position 0 and fully buffered in memory.</summary>
    private static readonly byte[] XlsxMagicBytes = [0x50, 0x4B, 0x03, 0x04];

    internal static void ValidateFileFormat(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        if (!stream.CanSeek)
            throw new ArgumentException("Stream must support seeking.", nameof(stream));

        try
        {
            if (stream.Length < 4)
                throw new CaseValidationException("Invalid file format. Please upload a .xlsx file.");

            Span<byte> header = stackalloc byte[4];
            var read = stream.Read(header);

            if (read < 4 || !header.SequenceEqual(XlsxMagicBytes))
                throw new CaseValidationException("Invalid file format. Please upload a .xlsx file.");
        }
        finally
        {
            stream.Position = 0;
        }
    }
}
