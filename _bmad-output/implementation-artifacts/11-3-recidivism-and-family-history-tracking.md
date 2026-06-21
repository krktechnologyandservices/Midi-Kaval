---
baseline_commit: NO_VCS
---

# Story 11.3: Recidivism and Family History Tracking

Status: done

## Story

As a **Project Coordinator**,
I want to track history of crime in family and recidivism counts,
So that the profile report shows repeat/first-time and recidivism data.

## Acceptance Criteria

1. **Given** the Case entity
   **When** viewing/editing a Case
   **Then** the following fields are available:
   - `FamilyHistoryOfCrime` — bool (default false)
   - `RecidivismBeforeCount` — int? (number of re-offences before intervention)
   - `RecidivismAfterCount` — int? (number of re-offences after intervention)
   **And** `IsFirstTimeOffender` (existing field) maps to "Frequency of offences: Repeat/First time"

2. **Given** an existing Case (created before this migration)
   **When** EF Core migration runs
   **Then** the new columns are added as nullable (or with default); no existing data is lost or corrupted
   **And** no seed data or backfill is required

3. **Given** a `POST /api/v1/cases` or merge request with `FamilyHistoryOfCrime`, `RecidivismBeforeCount`, `RecidivismAfterCount`
   **When** the endpoint processes the request
   **Then** the values are stored and returned in the response DTO
   **And** invalid values (e.g., negative counts) return 422

4. **Given** a Case with recidivism fields set
   **When** I view Case detail (`GET /api/v1/cases/{id}`), list/search results, or export
   **Then** the new fields are included in the DTOs

5. **Given** `RecidivismBeforeCount` and `RecidivismAfterCount`
   **When** creating or merging a Case
   **Then** negative values are rejected with a 422 business rule error

## Tasks / Subtasks

- [x] Add `FamilyHistoryOfCrime`, `RecidivismBeforeCount`, `RecidivismAfterCount` properties to `Case.cs` entity (AC: 1)
- [x] Configure simple scalar properties in `CaseConfiguration.cs` (AC: 1, 2)
- [x] Add fields to DTOs: `CreateCaseRequest`, `CaseDto`, `CaseDetailDto`, `CaseSummaryDto`, `CaseExportRowDto` (AC: 3, 4)
- [x] Update `ValidatedCreateCaseRequest` record in `CaseService.cs` (AC: 3)
- [x] Update `ValidateIntakeRequestAsync` — parse and validate new fields (AC: 3, 5)
- [x] Update `CreateAsync` — assign new entity properties (AC: 3)
- [x] Update `ToDto` — map new fields to `CaseDto` (AC: 4)
- [x] Update `BuildDetailDtoAsync` — map new fields to `CaseDetailDto` (AC: 4)
- [x] Update `SearchAsync` SELECT — include new fields in `CaseSummaryDto` projection (AC: 4)
- [x] Update `ExportAsync` SELECT — include new fields in `CaseExportRowDto` projection (AC: 4)
- [x] Update `MergeAsync` — fill-empty merge logic for new fields (AC: 1)
- [x] Update `CaseDtoMapper.ToCaseSummary` — map new fields (AC: 4)
- [x] Update `CaseExcelExporter.Headers` and cell writes — add 3 new column headers (AC: 4)
- [x] Update `CasePdfExporter.Headers` and cell writes — add 3 new column headers (AC: 4)
- [x] Create EF Core migration (AC: 2)
- [x] Build and verify all projects compile

## Dev Notes

### CRITICAL: This story uses simple scalar types — NOT enums or FK references

Unlike Stories 11.1 (enum types) and 11.2 (FK references to Legends), this story adds:
- **`bool`** with `HasDefaultValue(false)` for `FamilyHistoryOfCrime`
- **`int?`** (nullable int) for `RecidivismBeforeCount` and `RecidivismAfterCount`

No new `using` directives needed (no new namespaces required).
No new navigation properties.
No new DbSets required.

### Pattern Comparison

| Story | Pattern | DB Column Type |
|-------|---------|----------------|
| 11.1 | `HasConversion<string>()` + nullable enums | `varchar` |
| 11.2 | `HasForeignKey()` + `Guid?` + nav properties | `uuid` + FK constraint |
| **11.3** | **Simple scalars — `bool` + `int?`** | **`boolean` + `integer`** |

### Detailed Code Analysis

#### 1. Case Entity — Add Properties

In `apps/api/Domain/Entities/Case.cs`, add after `EducationLevel` navigation properties (after line 26):

```csharp
public bool FamilyHistoryOfCrime { get; set; }
public int? RecidivismBeforeCount { get; set; }
public int? RecidivismAfterCount { get; set; }
```

**Important:** `FamilyHistoryOfCrime` is a non-nullable `bool` with default `false`. `RecidivismBeforeCount` and `RecidivismAfterCount` are nullable `int?` — they should not have a default value in C# (EF will use NULL in DB).

Current entity structure (lines 21-26):
```csharp
public Guid? OccupationId { get; set; }
public Guid? EducationLevelId { get; set; }

// Navigation properties
public Occupation? Occupation { get; set; }
public EducationLevel? EducationLevel { get; set; }
```

#### 2. CaseConfiguration — Property Configuration

In `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`, add after the EducationLevel FK config (after line 71):

```csharp
// Simple scalar fields for Recidivism and Family History (Story 11.3)
builder.Property(c => c.FamilyHistoryOfCrime)
    .HasDefaultValue(false);

builder.Property(c => c.RecidivismBeforeCount);

builder.Property(c => c.RecidivismAfterCount);
```

**Important:** `FamilyHistoryOfCrime` gets `.HasDefaultValue(false)` so existing rows get `false` automatically. `RecidivismBeforeCount` and `RecidivismAfterCount` are simple nullable integers with no special config.

#### 3. DTO Changes

##### `CreateCaseRequest` (apps/api/Models/Cases/CaseDtos.cs)
Add after `EducationLevelId` (current line 17):
```csharp
public bool? FamilyHistoryOfCrime { get; set; }
public int? RecidivismBeforeCount { get; set; }
public int? RecidivismAfterCount { get; set; }
```

**Note:** `FamilyHistoryOfCrime` is `bool?` in the request DTO (optional input). If null, defaults to `false`.

##### `CaseDetailDto` (current lines 69-72)
Add after `EducationLevelName`:
```csharp
public bool FamilyHistoryOfCrime { get; set; }
public int? RecidivismBeforeCount { get; set; }
public int? RecidivismAfterCount { get; set; }
```

##### `CaseDto` (current lines 116-119)
Add after `EducationLevelName`:
```csharp
public bool FamilyHistoryOfCrime { get; set; }
public int? RecidivismBeforeCount { get; set; }
public int? RecidivismAfterCount { get; set; }
```

##### `CaseSummaryDto` (current lines 146-149)
Add after `EducationLevelName`:
```csharp
public bool FamilyHistoryOfCrime { get; set; }
public int? RecidivismBeforeCount { get; set; }
public int? RecidivismAfterCount { get; set; }
```

##### `CaseSearchFiltersDto` (current lines 169-170)
_No changes needed — search filtering by recidivism fields is not required by ACs._


##### `CaseExportRowDto` (apps/api/Models/Cases/CaseExportRowDto.cs)
Add after `EducationLevel` (current line 15):
```csharp
public bool FamilyHistoryOfCrime { get; init; }
public int? RecidivismBeforeCount { get; init; }
public int? RecidivismAfterCount { get; init; }
```

##### `CaseSearchQuery` (apps/api/Models/Cases/CaseSearchQuery.cs)
_No changes needed — search filtering by recidivism fields is not required by ACs._


#### 4. CaseService.cs Changes

**No new `using` directives needed** — `bool` and `int?` are primitive types.

##### `ValidatedCreateCaseRequest` record (current lines ~1436-1451)
Add after `EducationLevelId` (current 15th parameter):
```csharp
bool FamilyHistoryOfCrime,
int? RecidivismBeforeCount,
int? RecidivismAfterCount
```

Full record signature becomes 18 parameters.

##### `ValidateIntakeRequestAsync` method (current line ~1113)

After the `EducationLevelId` validation block (after line ~1221), add parsing for the new fields:

```csharp
// FamilyHistoryOfCrime — default false
var familyHistoryOfCrime = request.FamilyHistoryOfCrime ?? false;

// RecidivismBeforeCount — validate non-negative
int? recidivismBeforeCount = request.RecidivismBeforeCount;
if (recidivismBeforeCount < 0)
{
    throw new CaseBusinessRuleException(
        $"recidivismBeforeCount must be a non-negative integer.");
}

// RecidivismAfterCount — validate non-negative
int? recidivismAfterCount = request.RecidivismAfterCount;
if (recidivismAfterCount < 0)
{
    throw new CaseBusinessRuleException(
        $"recidivismAfterCount must be a non-negative integer.");
}
```

Update the `return new ValidatedCreateCaseRequest(...)` to include the 3 new fields:
```csharp
return new ValidatedCreateCaseRequest(
    crimeNumber,
    stNumber,
    beneficiaryName,
    request.BeneficiaryAge,
    string.IsNullOrWhiteSpace(beneficiaryContact) ? null : beneficiaryContact,
    typeOfOffence,
    offenceClassification,
    domicile,
    request.IsFirstTimeOffender ?? true,
    sensitivityLevel,
    gender,
    familyType,
    economicStatus,
    occupationId,
    educationLevelId,
    familyHistoryOfCrime,
    recidivismBeforeCount,
    recidivismAfterCount);
```

##### `CreateAsync` method — entity initialization (current lines ~39-63)

In the entity initialization block, add after `EducationLevelId = validated.EducationLevelId,` (current line 55):
```csharp
FamilyHistoryOfCrime = validated.FamilyHistoryOfCrime,
RecidivismBeforeCount = validated.RecidivismBeforeCount,
RecidivismAfterCount = validated.RecidivismAfterCount,
```

##### `ToDto` method (current line ~1303)

Add after `EducationLevelName = entity.EducationLevel?.Name,` (current line 1318):
```csharp
FamilyHistoryOfCrime = entity.FamilyHistoryOfCrime,
RecidivismBeforeCount = entity.RecidivismBeforeCount,
RecidivismAfterCount = entity.RecidivismAfterCount,
```

##### `BuildDetailDtoAsync` (current line ~1321)

Add after `EducationLevelName = entity.EducationLevel?.Name,` (current line 1383):
```csharp
FamilyHistoryOfCrime = entity.FamilyHistoryOfCrime,
RecidivismBeforeCount = entity.RecidivismBeforeCount,
RecidivismAfterCount = entity.RecidivismAfterCount,
```

##### `SearchAsync` — SELECT projection (current line ~739)

Add after `EducationLevelId = c.EducationLevelId,` (current line 764):
```csharp
FamilyHistoryOfCrime = c.FamilyHistoryOfCrime,
RecidivismBeforeCount = c.RecidivismBeforeCount,
RecidivismAfterCount = c.RecidivismAfterCount,
```

**Note:** These are simple scalars — no need for the two-pass name resolution approach used in Story 11.2 for Legend names.

##### `ExportAsync` — SELECT projection (current line ~822)

Add after `EducationLevel = c.EducationLevel != null ? c.EducationLevel.Name : null,` (current line 840):
```csharp
FamilyHistoryOfCrime = c.FamilyHistoryOfCrime,
RecidivismBeforeCount = c.RecidivismBeforeCount,
RecidivismAfterCount = c.RecidivismAfterCount,
```

##### `MergeAsync` — fill-empty logic (after line ~330)

Add after the `EducationLevelId` fill-empty block:
```csharp
if (!entity.FamilyHistoryOfCrime && validated.FamilyHistoryOfCrime)
{
    entity.FamilyHistoryOfCrime = validated.FamilyHistoryOfCrime;
    fieldsChanged = true;
}

if (entity.RecidivismBeforeCount is null && validated.RecidivismBeforeCount is not null)
{
    entity.RecidivismBeforeCount = validated.RecidivismBeforeCount;
    fieldsChanged = true;
}

if (entity.RecidivismAfterCount is null && validated.RecidivismAfterCount is not null)
{
    entity.RecidivismAfterCount = validated.RecidivismAfterCount;
    fieldsChanged = true;
}
```

**Important:** For `FamilyHistoryOfCrime`, the fill-empty condition is "only set if entity has `false` and incoming has `true`" — this allows filling `false → true` but never overwriting `true → false` via merge. This matches the existing fill-empty pattern for optional demographic fields.

Also update the `MergeAsync` audit event `draftSnapshot` dictionary (current lines ~352-366) to include:
```csharp
["familyHistoryOfCrime"] = validated.FamilyHistoryOfCrime,
["recidivismBeforeCount"] = validated.RecidivismBeforeCount,
["recidivismAfterCount"] = validated.RecidivismAfterCount,
```

#### 5. CaseDtoMapper.cs

In `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`, in `ToCaseSummary` (line 8), add after `EducationLevelName = entity.EducationLevel?.Name,` (line 35):
```csharp
FamilyHistoryOfCrime = entity.FamilyHistoryOfCrime,
RecidivismBeforeCount = entity.RecidivismBeforeCount,
RecidivismAfterCount = entity.RecidivismAfterCount,
```

#### 6. Excel & PDF Export Headers — Column Layout

##### Current column layout (15 columns after Story 11.2):
| Index | Column |
|-------|--------|
| 1-10 | (existing) Crime Number, ST Number, Beneficiary Name, Stage, Offence Type, Classification, Area, Gender, Family Type, Economic Status |
| 11 | Occupation |
| 12 | Education Level |
| 13 | VisitCount |
| 14 | NextVisitDueAtUtc |
| 15 | UpdatedAtUtc |

##### New column layout (18 columns):
Insert 3 new columns after "Education Level":
| Index | Column |
|-------|--------|
| 1-10 | (unchanged) |
| 11 | Occupation |
| 12 | Education Level |
| **13** | **Family History of Crime** |
| **14** | **Recidivism (Before)** |
| **15** | **Recidivism (After)** |
| 16 | VisitCount |
| 17 | NextVisitDueAtUtc |
| 18 | UpdatedAtUtc |

##### `CaseExcelExporter.cs` Headers array (current 15 entries → 18):
Add after `"Education Level"`:
```csharp
"Family History of Crime",
"Recidivism (Before)",
"Recidivism (After)",
```

Update cell data writes. Add after the EducationLevel cell write:
```csharp
worksheet.Cell(excelRow, 13).Value = row.FamilyHistoryOfCrime ? "Yes" : "No";
worksheet.Cell(excelRow, 14).Value = row.RecidivismBeforeCount?.ToString() ?? string.Empty;
worksheet.Cell(excelRow, 15).Value = row.RecidivismAfterCount?.ToString() ?? string.Empty;
```

Shift remaining cells:
```csharp
worksheet.Cell(excelRow, 16).Value = row.VisitCount;
worksheet.Cell(excelRow, 17).Value = row.NextVisitDueAtUtc?.ToString("O") ?? string.Empty;
worksheet.Cell(excelRow, 18).Value = row.UpdatedAtUtc.ToString("O");
```

**Total columns:** 18

##### `CasePdfExporter.cs` Headers array:
Add same 3 headers and table cells, shifting remaining cells by 3.

#### 7. Migration

Create a new EF Core migration with a descriptive name:
```bash
dotnet ef migrations add AddCaseRecidivismAndFamilyHistory --context AppDbContext
```

This migration will:
- Add `family_history_of_crime` column (`boolean`, default `false`) to `cases` table
- Add `recidivism_before_count` column (`integer`, nullable) to `cases` table
- Add `recidivism_after_count` column (`integer`, nullable) to `cases` table

### Project Structure Notes

- No new enum files needed (simple scalar types only)
- No new DbSets required
- No new `using` directives required
- No new navigation properties
- No Legend entity interaction needed

### Validation Rules Summary

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `FamilyHistoryOfCrime` | bool | No (default `false`) | None needed |
| `RecidivismBeforeCount` | int? | No | Must be >= 0 if provided |
| `RecidivismAfterCount` | int? | No | Must be >= 0 if provided |

### Testing Standards Summary

- Unit tests should verify:
  - `ValidateIntakeRequestAsync` rejects negative `RecidivismBeforeCount`/`RecidivismAfterCount` (422)
  - `ValidateIntakeRequestAsync` accepts null values (backward compatibility)
  - `ValidateIntakeRequestAsync` defaults `FamilyHistoryOfCrime` to `false` when not provided
  - `MergeAsync` fill-empty logic works correctly for all three new fields

- Integration tests (if Docker available):
  - Create a Case with `FamilyHistoryOfCrime=true` → verify response includes it
  - Create a Case without any recidivism fields → verify defaults (backward compatibility)
  - Create a Case with negative recidivism count → verify 422
  - Merge a Case with `FamilyHistoryOfCrime=true` → verify field is set
  - Export → verify new columns appear in Excel/PDF output

- See existing test patterns in `tests/api.integration/`

### Previous Story Intelligence (Stories 11.1 and 11.2)

**Key learnings:**

1. **Pattern progression:** Story 11.1 used `HasConversion<string>()` enum pattern. Story 11.2 used FK reference pattern. **Story 11.3 uses simple scalar types** — the simplest of the three.

2. **DTO patterns:** All DTOs (`CreateCaseRequest`, `CaseDto`, `CaseDetailDto`, `CaseSummaryDto`, `CaseSearchFiltersDto`, `CaseExportRowDto`, `CaseSearchQuery`) need the new fields added after the existing socio-demographic fields.

3. **Review finding — Export cell indices:** Story 11.1 had a bug where exporter headers and cell data writes were misaligned. **FOLLOW THE COLUMN LAYOUT CAREFULLY** — 3 new columns means shifting VisitCount from col 13 to col 16, NextVisitDue to col 17, UpdatedAt to col 18.

4. **Review finding — MergeAsync:** Ensure fill-empty logic is added for all three new fields, following the same pattern as Stories 11.1 and 11.2.

5. **No new DB indexes needed** — `bool` and `int` columns don't need explicit indexes for current data volumes.

6. **PII redaction:** Recidivism counts and family history are less sensitive than Gender/FamilyType, but follow the same approach as previous stories — no redaction needed.

7. **No async DB work:** Unlike Story 11.2, this story does NOT need async DB lookups in validation. All validations are synchronous (range checks on int fields).

### Git Intelligence Summary

This is the third socio-demographic field addition to the Case entity in Epic 11. The previous two stories (11.1 and 11.2) established clear patterns for:
- Adding entity properties
- Configuring them in `CaseConfiguration.cs`
- Propagating through all DTOs
- Updating all CaseService sections
- Updating both exporters
- Creating migrations

The expected file count is similar to Stories 11.1 and 11.2 (~10 modified files + 2 new migration files).

### References

- [Source: `apps/api/Domain/Entities/Case.cs`] — Entity (add FamilyHistoryOfCrime, RecidivismBeforeCount, RecidivismAfterCount)
- [Source: `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`] — Property configuration
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs`] — All service sections
- [Source: `apps/api/Models/Cases/CaseDtos.cs`] — All case DTOs (no changes needed for CaseSearchFiltersDto or CaseSearchQuery)
- [Source: `apps/api/Models/Cases/CaseExportRowDto.cs`] — Export row DTO
- [Source: `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`] — DTO mapper
- [Source: `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`] — Excel export
- [Source: `apps/api/Infrastructure/Cases/CasePdfExporter.cs`] — PDF export
- [Source: `_bmad-output/implementation-artifacts/11-2-occupation-and-education-level-on-case.md`] — Previous story (pattern reference)
- [Source: `_bmad-output/implementation-artifacts/11-1-gender-family-type-economic-status-on-case.md`] — First story (enum pattern reference)
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 11`] — Story definition

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

- ✅ Implemented Story 11.3: Added `FamilyHistoryOfCrime`, `RecidivismBeforeCount`, `RecidivismAfterCount` to Case entity
- ✅ Updated all DTOs (`CreateCaseRequest`, `CaseDetailDto`, `CaseDto`, `CaseSummaryDto`, `CaseExportRowDto`)
- ✅ Added validation in `ValidateIntakeRequestAsync` — rejects negative recidivism counts with 422
- ✅ Updated `CreateAsync`, `MergeAsync` (fill-empty logic + audit snapshot), `ToDto`, `BuildDetailDtoAsync`
- ✅ Updated `SearchAsync` SELECT projection with 3 new scalar fields
- ✅ Updated `ExportAsync` SELECT projection with 3 new fields
- ✅ Updated `CaseDtoMapper.ToCaseSummary` with new fields
- ✅ Updated Excel and PDF exporters (headers + cell writes, 15→18 columns)
- ✅ Created migration `20260621011939_AddCaseRecidivismAndFamilyHistory`
- ✅ Build succeeded with 0 errors; 39/39 unit tests pass
- ✅ Code review patches applied: Added `CaseBusinessRuleException` catch + 422 `ProducesResponseType` to Create endpoint; added explicit `= false` default to `FamilyHistoryOfCrime`; added `.Include()` for Occupation/EducationLevel in VisitService and CourtSittingService

### File List

**New files:**
- `apps/api/Migrations/20260621011939_AddCaseRecidivismAndFamilyHistory.cs`
- `apps/api/Migrations/20260621011939_AddCaseRecidivismAndFamilyHistory.Designer.cs`

**Modified files:** (no changes needed for CaseSearchFiltersDto, CaseSearchQuery, ParsedSearchFilters, ValidateSearchFilters, or ApplySearchFilters)
- `apps/api/Domain/Entities/Case.cs`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`
- `apps/api/Models/Cases/CaseDtos.cs`
- `apps/api/Models/Cases/CaseExportRowDto.cs`
- `apps/api/Infrastructure/Cases/CaseService.cs`
- `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`
- `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`
- `apps/api/Infrastructure/Cases/CasePdfExporter.cs`
- `apps/api/Migrations/AppDbContextModelSnapshot.cs`

### Review Findings

- [x] [Review][Patch] Create endpoint missing `CaseBusinessRuleException` catch — negative recidivism returns 500 not 422 [CasesController.cs:727-733]
- [x] [Review][Patch] Create endpoint missing 422 `ProducesResponseType` — OpenAPI spec incomplete [CasesController.cs:706-712]
- [x] [Review][Patch] `FamilyHistoryOfCrime` lacks explicit C# default initializer — latent schema divergence risk [Case.cs]
- [x] [Review][Patch] Other services calling `ToCaseSummary` may omit `.Include()` for Occupation/EducationLevel — silently null names [CaseDtoMapper.cs:32-38]
- [x] [Review][Defer] Hardcoded Excel column indices extended from 15 to 18 columns — pre-existing pattern [CaseExcelExporter.cs]
- [x] [Review][Defer] No DB-level CHECK constraint for non-negative recidivism — app-layer validation exists, pre-existing pattern [CaseConfiguration.cs:77-79]
- [x] [Review][Defer] No support for null search filters — cannot search for unset OccupationId/EducationLevelId — pre-existing limitation
