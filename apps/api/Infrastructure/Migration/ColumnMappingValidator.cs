using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MidiKaval.Api.Models.Migration;

namespace MidiKaval.Api.Infrastructure.Migration;

/// <summary>Validates a legacy Excel file against the mapping specification.
/// This is a read-only operation — no database access.</summary>
public sealed class ColumnMappingValidator(MappingSpecification spec)
{
    /// <summary>Validate the Excel columns against the mapping spec.</summary>
    public MigrationValidationResultDto Validate(IXLWorkbook workbook)
    {
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
        {
            return new MigrationValidationResultDto
            {
                TotalColumns = 0,
                MatchedColumns = 0,
                UnmatchedColumns = 0,
                MissingRequiredFields = 0,
                DataTypeWarnings = 0,
                Matched = [],
                Unmatched = [],
                MissingFields = [],
                Warnings = [],
                ValidatedAtUtc = DateTime.UtcNow,
            };
        }
        var headerRow = ws.Row(1);
        var totalColumns = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var legacyColumns = Enumerable.Range(1, totalColumns)
            .Select(col => (Index: col, Name: headerRow.Cell(col).GetString().Trim().TrimStart('\uFEFF')))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();

        var matched = new List<MigrationColumnInfo>();
        var unmatched = new List<MigrationColumnInfo>();
        var missingFields = new List<MigrationMissingFieldInfo>();
        var warnings = new List<MigrationWarningInfo>();

        // Classify each legacy column
        foreach (var (index, name) in legacyColumns)
        {
            var rule = spec.ColumnMappings.FirstOrDefault(r =>
                r.LegacyColumn.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (rule is not null && rule.TargetField is not null)
            {
                matched.Add(new MigrationColumnInfo
                {
                    LegacyColumn = name,
                    TargetField = rule.TargetField,
                    Status = "matched",
                });
            }
            else
            {
                unmatched.Add(new MigrationColumnInfo
                {
                    LegacyColumn = name,
                    TargetField = null,
                    Status = "unmatched",
                });
            }
        }

        // Check for required Case fields with no legacy source
        var matchedTargets = matched
            .Where(m => m.TargetField is not null)
            .Select(m => m.TargetField)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in spec.ColumnMappings)
        {
            if (rule.IsRequired && rule.TargetField is not null && !matchedTargets.Contains(rule.TargetField))
            {
                missingFields.Add(new MigrationMissingFieldInfo
                {
                    FieldName = rule.TargetField,
                    Severity = "error",
                    Message = $"Required Case field '{rule.TargetField}' has no matching legacy column. Expected legacy column: '{rule.LegacyColumn}'.",
                });
            }
        }

        // Data type spot-checks on first data row (row 2)
        if (legacyColumns.Count > 0)
        {
            var dataRow = ws.Row(2);
            foreach (var (index, name) in legacyColumns)
            {
                var rule = spec.ColumnMappings.FirstOrDefault(r =>
                    r.LegacyColumn.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (rule is null) continue;

                var cell = dataRow.Cell(index);
                var rawValue = cell.GetString().Trim();

                if (string.IsNullOrWhiteSpace(rawValue))
                    continue;

                if (rule.FieldType == "int" && !int.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    warnings.Add(new MigrationWarningInfo
                    {
                        ColumnIndex = index,
                        ColumnName = name,
                        RowNumber = 2,
                        Message = $"Column '{name}' expected numeric (int) value but found '{rawValue}'. This may cause import errors.",
                    });
                }

                if (rule.FieldType == "enum" && rule.EnumValues is not null)
                {
                    var enumValues = rule.EnumValues.Split(',').Select(v => v.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!enumValues.Contains(rawValue))
                    {
                        var kavalValues = spec.EnumMappings
                            .Where(e => e.TargetField.Equals(rule.TargetField, StringComparison.OrdinalIgnoreCase))
                            .Select(e => e.LegacyValue)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        if (!kavalValues.Contains(rawValue) && !enumValues.Contains(rawValue))
                        {
                            warnings.Add(new MigrationWarningInfo
                            {
                                ColumnIndex = index,
                                ColumnName = name,
                                RowNumber = 2,
                                Message = $"Column '{name}' has value '{rawValue}' which does not match any known enum mapping for '{rule.TargetField}'.",
                            });
                        }
                    }
                }
            }
        }

        return new MigrationValidationResultDto
        {
            TotalColumns = totalColumns,
            MatchedColumns = matched.Count,
            UnmatchedColumns = unmatched.Count,
            MissingRequiredFields = missingFields.Count,
            DataTypeWarnings = warnings.Count,
            Matched = matched,
            Unmatched = unmatched,
            MissingFields = missingFields,
            Warnings = warnings,
            ValidatedAtUtc = DateTime.UtcNow,
        };
    }
}
