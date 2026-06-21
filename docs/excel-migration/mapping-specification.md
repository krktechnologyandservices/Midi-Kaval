# Legacy Excel to Kaval Case Field Mapping Specification

> **Preamble:** This is a **one-time migration** specification, not an ongoing sync channel.
> Per MVP §6.1, addendum, and decision log D-1: Excel import is a one-time migration tool only.
> Post-migration, Excel exports are **read-only reports** from Kaval only. There is no write-back
> or periodic sync. WhatsApp remains non-integration per addendum.

## Source File

The legacy Excel file `legacy-cases-export.xlsx` is provided by the pilot NGO. It contains
their current case tracking data exported from their legacy spreadsheet system.

## Column-to-Field Mapping

| # | Legacy Column | Target Field | Transform | Type | Required | Null/Empty Handling | Notes |
|---|---------------|--------------|-----------|------|----------|---------------------|-------|
| 1 | Case ID | — *(legacy-only)* | — | string | No | Ignored | Internal legacy reference; not imported to Kaval |
| 2 | CR Number | `crimeNumber` | Trim() + ToUpperInvariant() | string(64) | **Yes** | Reject row if empty | Normalise to match DB unique constraint `UNIQUE(organisation_id, crime_number)` |
| 3 | ST Number | `stNumber` | Trim() + ToUpperInvariant() | string(64) | **Yes** | Reject row if empty | Normalise to match DB unique constraint `UNIQUE(organisation_id, st_number)` |
| 4 | Beneficiary Name | `beneficiaryName` | Direct copy, trimmed | string(256) | **Yes** | Reject row if empty after trim | |
| 5 | Age | `beneficiaryAge` | Parse int; validate 0–120 | int? | No | Set to `null` if blank | Flag warning if > 18 (juvenile context) |
| 6 | Contact No | `beneficiaryContact` | Direct copy; truncate to 32 chars | string(32) | No | Set to `null` if blank | |
| 7 | Address | — *(legacy-only)* | — | string | No | Ignored | Address is not part of the Kaval Case entity in v1 |
| 8 | Type of Offence | `typeOfOffence` | Direct copy, trimmed, max 128 chars | string(128) | **Yes** | Reject row if empty | Free text until Epic 9 legends (Story 9.1). Future harmonisation is post-migration |
| 9 | Classification | `offenceClassification` | Map text → enum (see mapping table below) | enum | **Yes** | Reject row if blank or unmappable | Legacy uses terms like "Petty", "Serious", "Heinous" or variations |
| 10 | Domicile/Area | `domicile` | Map text → enum (see mapping table below) | enum | **Yes** | Reject row if blank or unmappable | Legacy uses area/region terms |
| 11 | First Offender | `isFirstTimeOffender` | Map "Yes"/"Y"/"True" → `true`; else `false` | bool | No | Default to `true` if blank | |
| 12 | Stage | — *(legacy-only)* | — | string | No | Ignored | Current case stage in legacy system. Kaval imports all cases at `ProcessInitiation`; stage transitions happen in Kaval post-migration |
| 13 | Assigned Worker | — *(legacy-only)* | — | string | No | Ignored | Historical assignment; not imported. Case is unassigned in Kaval on import |
| 14 | Date Registered | — *(legacy-only)* | — | date | No | Ignored | For reference only. Kaval uses import timestamp as `CreatedAtUtc` |
| 15 | Remarks | — *(legacy-only)* | — | string | No | Ignored | Free-text notes from legacy system. Not imported (Kaval notes system in Epic 4) |

### Enum Value Mapping — Offence Classification

| Legacy Value | Kaval Enum | Case-Insensitive? | Notes |
|-------------|------------|-------------------|-------|
| Petty | `Petty` | Yes | |
| Minor | `Petty` | Yes | Common alternate term |
| Serious | `Serious` | Yes | |
| Grave | `Serious` | Yes | Alternate term used by some NGOs |
| Heinous | `Heinous` | Yes | |
| Heinous/Grave | `Heinous` | Yes | Compound term |
| *Any other value* | **Reject** | — | Row must be flagged as unmappable |

### Enum Value Mapping — Domicile

| Legacy Value | Kaval Enum | Case-Insensitive? | Notes |
|-------------|------------|-------------------|-------|
| Urban | `Urban` | Yes | |
| City | `Urban` | Yes | Alternate |
| Rural | `Rural` | Yes | |
| Village | `Rural` | Yes | Alternate |
| Coastal | `Coastal` | Yes | |
| Coastal Area | `Coastal` | Yes | |
| Tribal | `Tribal` | Yes | |
| Tribal Area | `Tribal` | Yes | |
| Slum | `Slum` | Yes | |
| *Any other value* | **Reject** | — | Row must be flagged as unmappable |

## System-Generated Fields (Not from Legacy Excel)

The following Case fields are populated by the system at import time and have no source in the legacy Excel:

| Field | Value | Rationale |
|-------|-------|-----------|
| `id` | `Guid.NewGuid()` | UUID v4 per architecture |
| `organisation_id` | From JWT claims of the importing Director | Tenant-isolated per org |
| `currentStage` | `CaseStage.ProcessInitiation` | All legacy cases enter at first stage; transitions happen in Kaval |
| `visitCount` | `0` | Visits were tracked outside Kaval schema |
| `createdByUserId` | Actor user ID from JWT | Director who runs the import |
| `createdAtUtc` | `DateTime.UtcNow` | Import timestamp, not original legacy date |
| `updatedAtUtc` | `DateTime.UtcNow` | Same as created (newly created in Kaval) |
| `isFirstTimeOffender` | `true` (default if column blank) | Conservative default per Story 2.1 |
| `sensitivityLevel` | `Standard` | No sensitivity data in legacy Excel |

## Out-of-Scope Data (Not Imported)

The following data exists in the legacy Excel but is **out of scope** for the one-time Case import.
These must be captured fresh in Kaval post-migration:

- **Visit logs / field visit records** — Not part of the Case aggregate
- **Court dates / sitting records** — To be entered in Kaval court sittings (Epic 5)
- **Case notes / intervention records** — To be entered in Kaval notes system (Epic 4)
- **Historical assignment records** — Re-assignment happens in Kaval post-migration
- **Beneficiary address** — Not in v1 Case schema
- **Remarks / free-text notes** — Not imported; use Kaval note system instead

## Validation Rules Summary

| Rule | Check | Action on Failure |
|------|-------|-------------------|
| Required field presence | `crimeNumber`, `stNumber`, `beneficiaryName`, `typeOfOffence`, `offenceClassification`, `domicile` all have non-empty values | Skip row, report error |
| Field length | `crimeNumber` ≤ 64, `stNumber` ≤ 64, `beneficiaryName` ≤ 256, `typeOfOffence` ≤ 128, `beneficiaryContact` ≤ 32 | Truncate with warning or reject |
| Enum parity | `offenceClassification` and `domicile` values match known enum mapping | Skip row, report unmappable value |
| Age range | `age` 0–120 if present | Flag warning if out of range; skip if non-numeric |
| Unique constraint | `crimeNumber` and `stNumber` unique per org | Skip duplicate row, report as skipped |
