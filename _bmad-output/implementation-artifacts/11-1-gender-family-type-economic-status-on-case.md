---
baseline_commit: NO_VCS
---

# Story 11.1: Gender, Family Type & Economic Status on Case

Status: done

## Story

As a **Project Coordinator**,
I want to record Gender, Family Type, and Economic Status for each child,
so that the socio-demographic profile report is complete.

## Acceptance Criteria

1. **Given** the Case entity exists with the current fields
   **When** creating or updating a Case
   **Then** three new optional fields are available:
   - `Gender` — an enum with values: `Male`, `Female`, `Transgender`
   - `FamilyType` — an enum with values: `Joint`, `Nuclear`, `SingleParent`, `Others`
   - `EconomicStatus` — an enum with values: `APL`, `BPL`
   **And** each field is stored in the DB as a string column with enum-to-string conversion
   **And** each field is optional/nullable for backward compatibility with existing Cases

2. **Given** an existing Case (created before this migration)
   **When** EF Core migration runs
   **Then** the new columns are added with nullable defaults; no existing data is lost or corrupted
   **And** no seed data or backfill is required

3. **Given** a `POST /api/v1/cases` request with valid values for the new fields
   **When** the Create endpoint processes the request
   **Then** the values are stored and returned in the response DTO
   **And** invalid enum string values return 400 Bad Request with a descriptive message

4. **Given** a Case with Gender, FamilyType, and EconomicStatus set
   **When** I view Case detail (`GET /api/v1/cases/{id}`), list/search results, or export
   **Then** the new fields are included in the DTOs

5. **Given** the search endpoint `GET /api/v1/cases/search`
   **When** I filter by `gender`, `familyType`, or `economicStatus`
   **Then** results are filtered accordingly
   **And** invalid filter values return 400

## Tasks / Subtasks

- [x] Create enum files: `Gender.cs`, `FamilyType.cs`, `EconomicStatus.cs` (AC: 1)
- [x] Add properties to `Case.cs` entity (AC: 1)
- [x] Configure new columns in `CaseConfiguration.cs` (AC: 1, 2)
- [x] Add fields to DTOs: `CreateCaseRequest`, `CaseDto`, `CaseDetailDto`, `CaseSummaryDto`, `CaseSearchFiltersDto`, `CaseExportRowDto`, `CaseSearchQuery` (AC: 3, 4, 5)
- [x] Add fields to `ValidatedCreateCaseRequest` record in `CaseService.cs` (AC: 3)
- [x] Update `ValidateIntakeRequest` in `CaseService.cs` — parse enum values from request (AC: 3)
- [x] Update `ToDto` in `CaseService.cs` — map new fields to `CaseDto` (AC: 4)
- [x] Update `BuildDetailDtoAsync` in `CaseService.cs` — map new fields to `CaseDetailDto` (AC: 4)
- [x] Update `SearchAsync` SELECT in `CaseService.cs` — include new fields in `CaseSummaryDto` projection (AC: 4)
- [x] Update `ExportAsync` SELECT in `CaseService.cs` — include new fields in `CaseExportRowDto` projection (AC: 4)
- [x] Update `ParsedSearchFilters` record and `ValidateSearchFilters` method — parse new filter values (AC: 5)
- [x] Update `ApplySearchFilters` method — apply new filter predicates (AC: 5)
- [x] Update `CaseDtoMapper.ToCaseSummary` — map new fields (AC: 4)
- [x] Update `CaseExcelExporter.Headers` — add new column headers (AC: 4)
- [x] Update `CasePdfExporter.Headers` — add new column headers (AC: 4)
- [x] Create EF Core migration (AC: 2)
- [x] Build and verify all projects compile

### Review Findings

- [x] [Review][Patch] **Excel & PDF exporters: cell data writes don't include Gender/FamilyType/EconomicStatus** [`CaseExcelExporter.cs:42-51`, `CasePdfExporter.cs:57-69`] — Headers array updated to 13 columns, but data loop still writes only 10 cells at hardcoded indices. VisitCount lands under "Gender" header, NextVisitDueAtUtc under "Family Type", UpdatedAtUtc under "Economic Status". The three new demographic values are never written to any cell. Violates AC4. **Fixed**: Added Gender (col 8), FamilyType (col 9), EconomicStatus (col 10) cell writes; shifted VisitCount to col 11, NextVisitDue to col 12, UpdatedAt to col 13.

- [x] [Review][Patch] **MergeAsync silently drops Gender, FamilyType, EconomicStatus** [`CaseService.cs:271-290`] — `validated.Gender`, `validated.FamilyType`, `validated.EconomicStatus` are parsed and available but never assigned to the target entity during merge. A draft containing this demographic data silently loses it. **Fixed**: Added fill-empty merge logic for all three fields and included them in the draftSnapshot audit metadata.

- [x] [Review][Defer] **Missing DB indexes on Gender, FamilyType, EconomicStatus** — `ApplySearchFilters` adds exact-match WHERE clauses for these columns. Without indexes, filtering by these fields degrades to sequential scans on large tables. Deferred: performance optimization, not a correctness bug for current data volumes.

- [x] [Review][Defer] **PII redaction gap: demographic fields not redacted for field workers on POCSO cases** [`CaseDtoMapper.cs:29-31`, `CaseService.cs:~1219-1221`] — BeneficiaryName is redacted via BeneficiaryDisplayFormatter for field workers on POCSO cases, but Gender, FamilyType, and EconomicStatus are written unconditionally. Potential privacy policy violation. Deferred: pre-existing POCSO redaction concern, not introduced by this story.

- [x] [Review][Defer] **Inconsistent null-handling patterns** [`CaseService.cs:691-693` vs `CaseDtoMapper.cs:29-31`] — Some paths use ternary (`c.Gender != null ? c.Gender.ToString()! : null`) while others use null-conditional (`entity.Gender?.ToString()`). Maintenance hazard for future extensions. Deferred: low severity, both produce correct results.

## Dev Notes

### Detailed Code Analysis

#### 1. Enum Pattern (FOLLOW THIS EXACTLY)

Create 3 files in `apps/api/Domain/Enums/` following the existing pattern:

```csharp
// apps/api/Domain/Enums/Gender.cs
namespace MidiKaval.Api.Domain.Enums;
public enum Gender { Male, Female, Transgender }
```

```csharp
// apps/api/Domain/Enums/FamilyType.cs
namespace MidiKaval.Api.Domain.Enums;
public enum FamilyType { Joint, Nuclear, SingleParent, Others }
```

```csharp
// apps/api/Domain/Enums/EconomicStatus.cs
namespace MidiKaval.Api.Domain.Enums;
public enum EconomicStatus { APL, BPL }
```

#### 2. Case Entity — Add Properties

In `apps/api/Domain/Entities/Case.cs`, add after `Domicile`:

```csharp
public Gender? Gender { get; set; }
public FamilyType? FamilyType { get; set; }
public EconomicStatus? EconomicStatus { get; set; }
```

The existing properties around line 16 are:
```
public Domicile Domicile { get; set; }     // line 16 — non-nullable
public bool IsFirstTimeOffender { get; set; } = true;  // line 17
```

New fields should be nullable (`Gender?`, `FamilyType?`, `EconomicStatus?`) to avoid breaking existing data.

#### 3. CaseConfiguration — Column Config

In `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`, add after the Domicile config (around line 42):

```csharp
builder.Property(c => c.Gender)
    .HasConversion<string>()
    .HasMaxLength(16);

builder.Property(c => c.FamilyType)
    .HasConversion<string>()
    .HasMaxLength(32);

builder.Property(c => c.EconomicStatus)
    .HasConversion<string>()
    .HasMaxLength(16);
```

All three are nullable by convention (no `.IsRequired()`).

#### 4. DTO Changes

##### `CreateCaseRequest` (apps/api/Models/Cases/CaseDtos.cs)
Add after `Domicile` (line 12):
```csharp
public string? Gender { get; set; }
public string? FamilyType { get; set; }
public string? EconomicStatus { get; set; }
```

##### `CaseDto` (lines 92-101)
Add after `BeneficiaryName` (line 97):
```csharp
public string? Gender { get; set; }
public string? FamilyType { get; set; }
public string? EconomicStatus { get; set; }
```

##### `CaseDetailDto` (lines 39-61)
Add after `BeneficiaryName` or `Domicile`:
```csharp
public string? Gender { get; set; }
public string? FamilyType { get; set; }
public string? EconomicStatus { get; set; }
```

##### `CaseSummaryDto` (lines 103-124)
Add after `Domicile` (line 112):
```csharp
public string? Gender { get; set; }
public string? FamilyType { get; set; }
public string? EconomicStatus { get; set; }
```

##### `CaseSearchFiltersDto` (lines 133-143)
Add after `Domicile` (line 139):
```csharp
public string? Gender { get; set; }
public string? FamilyType { get; set; }
public string? EconomicStatus { get; set; }
```

##### `CaseExportRowDto` (apps/api/Models/Cases/CaseExportRowDto.cs)
Add after `Domicile` (line 11):
```csharp
public string? Gender { get; init; }
public string? FamilyType { get; init; }
public string? EconomicStatus { get; init; }
```

##### `CaseSearchQuery` (apps/api/Models/Cases/CaseSearchQuery.cs)
Add after `Domicile` (line 9):
```csharp
public string? Gender { get; init; }
public string? FamilyType { get; init; }
public string? EconomicStatus { get; init; }
```

#### 5. CaseService.cs Changes

##### `ValidatedCreateCaseRequest` record (line 1172)
Add after `Domicile Domicile`:
```csharp
Gender? Gender,
FamilyType? FamilyType,
EconomicStatus? EconomicStatus,
```

##### `ValidateIntakeRequest` method (line 928)
After the domicile parsing block (around line 980), add:
```csharp
Gender? gender = null;
if (!string.IsNullOrWhiteSpace(request.Gender))
{
    if (!TryParseEnum(request.Gender, out Gender parsedGender))
    {
        throw new CaseValidationException(
            "gender must be one of: Male, Female, Transgender.");
    }
    gender = parsedGender;
}

FamilyType? familyType = null;
if (!string.IsNullOrWhiteSpace(request.FamilyType))
{
    if (!TryParseEnum(request.FamilyType, out FamilyType parsedFt))
    {
        throw new CaseValidationException(
            "familyType must be one of: Joint, Nuclear, SingleParent, Others.");
    }
    familyType = parsedFt;
}

EconomicStatus? economicStatus = null;
if (!string.IsNullOrWhiteSpace(request.EconomicStatus))
{
    if (!TryParseEnum(request.EconomicStatus, out EconomicStatus parsedEs))
    {
        throw new CaseValidationException(
            "economicStatus must be one of: APL, BPL.");
    }
    economicStatus = parsedEs;
}
```

Update the `return new ValidatedCreateCaseRequest` at line 990 to include the 3 new fields.

##### `CreateAsync` method (line 26)
In the entity initialization block (lines 38-57), after `Domicile = validated.Domicile` add:
```csharp
Gender = validated.Gender,
FamilyType = validated.FamilyType,
EconomicStatus = validated.EconomicStatus,
```

##### `ToDto` method (line 1053)
Add after `CurrentStage` mapping:
```csharp
Gender = entity.Gender?.ToString(),
FamilyType = entity.FamilyType?.ToString(),
EconomicStatus = entity.EconomicStatus?.ToString(),
```

##### `BuildDetailDtoAsync` (line 1064) — there is a large DTO inline; add the 3 fields with `.ToString()` similar to other enum fields in the method.

##### `SearchAsync` — SELECT projection (line 667)
Add after `Domicile = c.Domicile.ToString(),`:
```csharp
Gender = c.Gender.ToString(),
FamilyType = c.FamilyType.ToString(),
EconomicStatus = c.EconomicStatus.ToString(),
```

Note: Since `Gender`, `FamilyType`, `EconomicStatus` are nullable enums, `c.Gender.ToString()` on a null value will produce `""`. To handle this properly, use:
```csharp
Gender = c.Gender != null ? c.Gender.ToString()! : null,
FamilyType = c.FamilyType != null ? c.FamilyType.ToString()! : null,
EconomicStatus = c.EconomicStatus != null ? c.EconomicStatus.ToString()! : null,
```

##### `ExportAsync` — SELECT projection (line 727)
Add after `Domicile = c.Domicile.ToString()`:
```csharp
Gender = c.Gender != null ? c.Gender.ToString()! : null,
FamilyType = c.FamilyType != null ? c.FamilyType.ToString()! : null,
EconomicStatus = c.EconomicStatus != null ? c.EconomicStatus.ToString()! : null,
```

##### `ParsedSearchFilters` record (line 772)
Add after `Domicile? Domicile`:
```csharp
Gender? Gender,
FamilyType? FamilyType,
EconomicStatus? EconomicStatus,
```

##### `ValidateSearchFilters` method (line 777)
After the domicile block (lines 803-813), add:
```csharp
Gender? gender = null;
if (!string.IsNullOrWhiteSpace(query.Gender))
{
    if (!TryParseEnum(query.Gender, out Gender parsedGender))
    {
        throw new CaseValidationException(
            "gender must be one of: Male, Female, Transgender.");
    }
    gender = parsedGender;
}

FamilyType? familyType = null;
if (!string.IsNullOrWhiteSpace(query.FamilyType))
{
    if (!TryParseEnum(query.FamilyType, out FamilyType parsedFamilyType))
    {
        throw new CaseValidationException(
            "familyType must be one of: Joint, Nuclear, SingleParent, Others.");
    }
    familyType = parsedFamilyType;
}

EconomicStatus? economicStatus = null;
if (!string.IsNullOrWhiteSpace(query.EconomicStatus))
{
    if (!TryParseEnum(query.EconomicStatus, out EconomicStatus parsedEs))
    {
        throw new CaseValidationException(
            "economicStatus must be one of: APL, BPL.");
    }
    economicStatus = parsedEs;
}
```

Update the `return new ParsedSearchFilters(...)` to include 3 new fields.

##### `ApplySearchFilters` method (line 818)
After the domicile filter block (after line 858), add:
```csharp
if (parsed.Gender is not null)
{
    cases = cases.Where(c => c.Gender == parsed.Gender);
}

if (parsed.FamilyType is not null)
{
    cases = cases.Where(c => c.FamilyType == parsed.FamilyType);
}

if (parsed.EconomicStatus is not null)
{
    cases = cases.Where(c => c.EconomicStatus == parsed.EconomicStatus);
}
```

Also in the `Q` text search block (line 832), consider adding the new fields to the OR search if needed (not strictly required — text search on enum values would be unusual, but could be added for consistency).

#### 6. CaseDtoMapper.cs

In `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`, add to `ToCaseSummary` after `Domicile = entity.Domicile.ToString(),` (line 17):
```csharp
Gender = entity.Gender?.ToString(),
FamilyType = entity.FamilyType?.ToString(),
EconomicStatus = entity.EconomicStatus?.ToString(),
```

#### 7. Excel & PDF Export Headers

##### `CaseExcelExporter.cs` (apps/api/Infrastructure/Cases/CaseExcelExporter.cs)
Add to the `Headers` array (around line 8) after `"Area (domicile)"`:
```csharp
"Gender",
"Family Type",
"Economic Status",
```

##### `CasePdfExporter.cs` (apps/api/Infrastructure/Cases/CasePdfExporter.cs)
Add to the `Headers` array (around line 10) similarly.

#### 8. Migration
Create a new EF Core migration with a descriptive name (e.g., `AddCaseGenderFamilyTypeEconomicStatus`):
```bash
dotnet ef migrations add AddCaseGenderFamilyTypeEconomicStatus --context AppDbContext
```

### Project Structure Notes

- All new files go under existing folders following established patterns
- No new NuGet packages required
- No new DbSets required (adding columns to existing `cases` table)
- Enum files follow exact same pattern as existing enums in `Domain/Enums/`
- All enum-to-string conversions follow the existing `HasConversion<string>()` pattern used by `OffenceClassification`, `Domicile`, `CurrentStage`, etc.

### Testing Standards Summary

- Unit tests should verify:
  - `ValidateIntakeRequest` parses valid enum strings and throws on invalid ones
  - Search filters (`ValidateSearchFilters`) parse valid filter values and throw on invalid ones
  - `ApplySearchFilters` correctly filters by each new field
- Integration tests (if Docker available):
  - Create a Case with new fields → verify response contains them
  - Create a Case without new fields → verify they are null → verify backward compatibility
  - Search by new fields → verify filtered results
  - Export → verify new columns appear in Excel/PDF output
- See existing test patterns in `tests/api.integration/`

### Previous Story Intelligence

This is the first story in Epic 11, so no previous story in this epic. Key learnings from previous epics:

- **Epic 10 (Migration):** Established that `CaseConfiguration.cs` uses `HasConversion<string>()` for enums, `HasMaxLength()` constraints, and `HasDefaultValue()` for booleans. The `CaseService.cs` has a well-established pattern of `ValidateIntakeRequest` → `ValidatedCreateCaseRequest` → entity mapping.
- **Enum parsing pattern:** `TryParseEnum<T>()` is used consistently in `CaseService.cs` for both intake validation and search filter validation — always use this existing helper instead of `Enum.TryParse` directly.
- **DTO to string mapping:** All enum DTO fields are mapped via `.ToString()` — nullable enums need null-check before calling `.ToString()` (e.g., `entity.Gender?.ToString()`).

### References

- [Source: `apps/api/Domain/Enums/Domicile.cs`] — Existing enum pattern
- [Source: `apps/api/Domain/Entities/Case.cs`] — Entity with existing field patterns
- [Source: `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`] — Column configuration pattern
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:928-1000`] — `ValidateIntakeRequest` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:1172-1182`] — `ValidatedCreateCaseRequest` record
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:772-775`] — `ParsedSearchFilters` record
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:777-815`] — `ValidateSearchFilters` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:818-880`] — `ApplySearchFilters` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:1053-1062`] — `ToDto` method
- [Source: `apps/api/Models/Cases/CaseDtos.cs`] — All Case DTOs
- [Source: `apps/api/Models/Cases/CaseSearchQuery.cs`] — Search query model
- [Source: `apps/api/Models/Cases/CaseExportRowDto.cs`] — Export row DTO
- [Source: `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`] — DTO mapper
- [Source: `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`] — Excel export
- [Source: `apps/api/Infrastructure/Cases/CasePdfExporter.cs`] — PDF export
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 11`] — Story definition

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

All 17 tasks completed. Implementation added 3 nullable enum fields (Gender, FamilyType, EconomicStatus) to the Case entity with full DTO, search, validation, and export support across the API.

### File List

**New files:**
- `apps/api/Domain/Enums/Gender.cs`
- `apps/api/Domain/Enums/FamilyType.cs`
- `apps/api/Domain/Enums/EconomicStatus.cs`
- `apps/api/Migrations/20260620202159_AddCaseGenderFamilyTypeEconomicStatus.cs`
- `apps/api/Migrations/20260620202159_AddCaseGenderFamilyTypeEconomicStatus.Designer.cs`

**Modified files:**
- `apps/api/Domain/Entities/Case.cs`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`
- `apps/api/Models/Cases/CaseDtos.cs`
- `apps/api/Models/Cases/CaseSearchQuery.cs`
- `apps/api/Models/Cases/CaseExportRowDto.cs`
- `apps/api/Infrastructure/Cases/CaseService.cs`
- `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`
- `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`
- `apps/api/Infrastructure/Cases/CasePdfExporter.cs`

**New files:**
- `apps/api/Domain/Enums/Gender.cs`
- `apps/api/Domain/Enums/FamilyType.cs`
- `apps/api/Domain/Enums/EconomicStatus.cs`
- EF Core migration (generated)

**Modified files:**
- `apps/api/Domain/Entities/Case.cs`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`
- `apps/api/Models/Cases/CaseDtos.cs`
- `apps/api/Models/Cases/CaseSearchQuery.cs`
- `apps/api/Models/Cases/CaseExportRowDto.cs`
- `apps/api/Infrastructure/Cases/CaseService.cs`
- `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`
- `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`
- `apps/api/Infrastructure/Cases/CasePdfExporter.cs`
