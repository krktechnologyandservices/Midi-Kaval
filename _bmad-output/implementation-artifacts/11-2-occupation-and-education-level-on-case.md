---
baseline_commit: NO_VCS
---

# Story 11.2: Occupation and Education Level on Case

Status: done

## Story

As a **Project Coordinator**,
I want the Occupation and Education Level of each child stored on the Case record,
so that the socio-demographic profile can be aggregated.

## Acceptance Criteria

1. **Given** the `occupations` and `education_levels` Legend tables already exist (from Story 9.1)
   **When** I create or update a Case
   **Then** I can set `OccupationId` and `EducationLevelId` as optional FK references to those Legend tables
   **And** invalid FK values return referential integrity error

2. **Given** an existing Case (created before this migration)
   **When** EF Core migration runs
   **Then** the new FK columns are added as nullable; no existing data is lost or corrupted
   **And** no seed data or backfill is required

3. **Given** a `POST /api/v1/cases` request with valid `OccupationId` and/or `EducationLevelId`
   **When** the Create endpoint processes the request
   **Then** the values are stored and returned in the response DTO with the Legend entity name included
   **And** invalid (non-existent) Legend IDs return 422 with "referenced {field} not found"

4. **Given** a Case with OccupationId and EducationLevelId set
   **When** I view Case detail (`GET /api/v1/cases/{id}`), list/search results, or export
   **Then** the new fields (both ID and Legend name) are included in the DTOs

5. **Given** the search endpoint `GET /api/v1/cases/search`
   **When** I filter by `occupationId` or `educationLevelId`
   **Then** results are filtered by the exact FK match
   **And** invalid UUID filter values return 400

## Tasks / Subtasks

- [ ] Add `OccupationId` and `EducationLevelId` properties to `Case.cs` entity (AC: 1)
- [ ] Add navigation properties to `Occupation` and `EducationLevel` on `Case.cs` (AC: 1)
- [ ] Configure FK relationships in `CaseConfiguration.cs` (AC: 1, 2)
- [ ] Add fields to DTOs: `CreateCaseRequest`, `CaseDto`, `CaseDetailDto`, `CaseSummaryDto`, `CaseSearchFiltersDto`, `CaseExportRowDto`, `CaseSearchQuery` (AC: 3, 4, 5)
- [ ] Update `ValidatedCreateCaseRequest` record in `CaseService.cs` (AC: 3)
- [ ] Update `ValidateIntakeRequest` in `CaseService.cs` — resolve Legend FK references and validate existence (AC: 3)
- [ ] Update `CreateAsync` in `CaseService.cs` — assign FK properties (AC: 3)
- [ ] Update `ToDto` in `CaseService.cs` — map new fields to `CaseDto` including Legend names (AC: 4)
- [ ] Update `BuildDetailDtoAsync` in `CaseService.cs` — map new fields to `CaseDetailDto` (AC: 4)
- [ ] Update `SearchAsync` SELECT in `CaseService.cs` — include new fields in `CaseSummaryDto` projection (AC: 4)
- [ ] Update `ExportAsync` SELECT in `CaseService.cs` — include new fields in `CaseExportRowDto` projection (AC: 4)
- [ ] Update `ParsedSearchFilters` record and `ValidateSearchFilters` method — parse new filter values (AC: 5)
- [ ] Update `ApplySearchFilters` method — apply new filter predicates (AC: 5)
- [ ] Update `MergeAsync` — fill-empty merge logic for OccupationId and EducationLevelId (AC: 1)
- [ ] Update `CaseDtoMapper.ToCaseSummary` — map new fields (AC: 4)
- [ ] Update `CaseExcelExporter.Headers` and cell writes — add new column headers (AC: 4)
- [ ] Update `CasePdfExporter.Headers` and cell writes — add new column headers (AC: 4)
- [ ] Create EF Core migration (AC: 2)
- [ ] Build and verify all projects compile

## Dev Notes

### CRITICAL: This story uses FK references to Legend tables, NOT enums

Unlike Story 11.1 (which used enum types Gender, FamilyType, EconomicStatus), this story adds **foreign key relationships** to existing Legend entities. The patterns are fundamentally different:

- Story 11.1: `HasConversion<string>()` + nullable enum properties
- Story 11.2: `HasForeignKey()` + nullable `Guid?` FK properties + navigation properties + `.Include()` in queries

### Detailed Code Analysis

#### 1. Case Entity — Add Properties and Navigation

In `apps/api/Domain/Entities/Case.cs`, add after `EconomicStatus` (line 19):

```csharp
public Guid? OccupationId { get; set; }
public Guid? EducationLevelId { get; set; }

// Navigation properties
public Occupation? Occupation { get; set; }
public EducationLevel? EducationLevel { get; set; }
```

**Important:** Navigation properties are nullable. Do NOT make them required — these fields are optional.

Current entity structure (lines 17-19):
```csharp
public Gender? Gender { get; set; }
public FamilyType? FamilyType { get; set; }
public EconomicStatus? EconomicStatus { get; set; }
```

#### 2. CaseConfiguration — FK Relationships

In `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`, add after the EconomicStatus config (after line 55):

```csharp
// FK references to Legend tables (from Story 9.1)
builder.Property(c => c.OccupationId);

builder.HasOne(c => c.Occupation)
    .WithMany()
    .HasForeignKey(c => c.OccupationId)
    .OnDelete(DeleteBehavior.Restrict);

builder.Property(c => c.EducationLevelId);

builder.HasOne(c => c.EducationLevel)
    .WithMany()
    .HasForeignKey(c => c.EducationLevelId)
    .OnDelete(DeleteBehavior.Restrict);
```

**Important:** Use `DeleteBehavior.Restrict` to prevent cascade delete of Cases when a Legend entry is soft-deactivated.

#### 3. DTO Changes

##### `CreateCaseRequest` (apps/api/Models/Cases/CaseDtos.cs, line 15)
Add after `EconomicStatus` (line 15):
```csharp
public Guid? OccupationId { get; set; }
public Guid? EducationLevelId { get; set; }
```

##### `CaseDto` (line 98-110)
Add after `EconomicStatus` (line 109):
```csharp
public Guid? OccupationId { get; set; }
public string? OccupationName { get; set; }
public Guid? EducationLevelId { get; set; }
public string? EducationLevelName { get; set; }
```

##### `CaseDetailDto` (lines 42-67)
Add after `EconomicStatus` (line 66):
```csharp
public Guid? OccupationId { get; set; }
public string? OccupationName { get; set; }
public Guid? EducationLevelId { get; set; }
public string? EducationLevelName { get; set; }
```

##### `CaseSummaryDto` (lines 112-136)
Add after `EconomicStatus` (line 135):
```csharp
public Guid? OccupationId { get; set; }
public string? OccupationName { get; set; }
public Guid? EducationLevelId { get; set; }
public string? EducationLevelName { get; set; }
```

##### `CaseSearchFiltersDto` (lines 145-158)
Add after `EconomicStatus` (line 154):
```csharp
public Guid? OccupationId { get; set; }
public Guid? EducationLevelId { get; set; }
```

##### `CaseExportRowDto` (apps/api/Models/Cases/CaseExportRowDto.cs)
Add after `EconomicStatus` (line 14):
```csharp
public string? Occupation { get; init; }
public string? EducationLevel { get; init; }
```

##### `CaseSearchQuery` (apps/api/Models/Cases/CaseSearchQuery.cs)
Add after `EconomicStatus` (line 12):
```csharp
public Guid? OccupationId { get; init; }
public Guid? EducationLevelId { get; init; }
```

#### 4. CaseService.cs Changes

##### `ValidatedCreateCaseRequest` record (line 1295)
Add after `EconomicStatus` (line 1308):
```csharp
Guid? OccupationId,
Guid? EducationLevelId
```

##### `ValidateIntakeRequest` method (line 1009)

After the EconomicStatus parsing block (after line 1090), add Legend FK validation:

```csharp
// OccupationId validation — verify Legend entity exists and belongs to same organisation
Guid? occupationId = request.OccupationId;
if (occupationId is not null)
{
    var occupation = await db.Set<Occupation>()
        .FirstOrDefaultAsync(o => o.Id == occupationId && o.OrganisationId == organisationId && o.IsActive, cancellationToken);
    if (occupation is null)
    {
        throw new CaseBusinessRuleException(
            $"referenced occupation not found for id: {occupationId}");
    }
}

// EducationLevelId validation — verify Legend entity exists and belongs to same organisation
Guid? educationLevelId = request.EducationLevelId;
if (educationLevelId is not null)
{
    var educationLevel = await db.Set<EducationLevel>()
        .FirstOrDefaultAsync(el => el.Id == educationLevelId && el.OrganisationId == organisationId && el.IsActive, cancellationToken);
    if (educationLevel is null)
    {
        throw new CaseBusinessRuleException(
            $"referenced educationLevel not found for id: {educationLevelId}");
    }
}
```

**IMPORTANT — Add `using` directive:** `Occupation` and `EducationLevel` live in the `MidiKaval.Api.Domain.Entities.Legends` namespace. Add `using MidiKaval.Api.Domain.Entities.Legends;` to the imports at the top of `CaseService.cs` (after line 6). Without this, references to `Occupation` and `EducationLevel` in the validation code will not compile.

**CRITICAL PATTERN CHANGE:** `ValidateIntakeRequest` is currently `private static`. Adding async DB validation requires it to become `private async Task<ValidatedCreateCaseRequest>` (instance method, not static, to access `db`). The method needs the caller's `organisationId` and `cancellationToken`:

```csharp
private async Task<ValidatedCreateCaseRequest> ValidateIntakeRequestAsync(
    CreateCaseRequest request, Guid organisationId, CancellationToken cancellationToken)
```

Update all callers: `CreateAsync` changes `var validated = ValidateIntakeRequest(request);` to `var validated = await ValidateIntakeRequestAsync(request, organisationId, cancellationToken);`. Same for `MergeAsync`.

Update the `return new ValidatedCreateCaseRequest(...)` at line 1104 to include the 2 new fields:
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
    educationLevelId);
```

Also update `CreateAsync` (line 26) — change `var validated = ValidateIntakeRequest(request);` to `var validated = await ValidateIntakeRequestAsync(request, organisationId, cancellationToken);`. Note `organisationId` is resolved on line 33 via `(var organisationId, var actorUserId) = ResolveActorContext()`.

##### `CreateAsync` method (line 38-60)

In the entity initialization block, add after `EconomicStatus = validated.EconomicStatus,` (line 52):
```csharp
OccupationId = validated.OccupationId,
EducationLevelId = validated.EducationLevelId,
```

##### `ToDto` method (line 1170)

Add after `EconomicStatus = entity.EconomicStatus?.ToString(),` (line 1181):
```csharp
OccupationId = entity.OccupationId,
OccupationName = entity.Occupation?.Name,
EducationLevelId = entity.EducationLevelId,
EducationLevelName = entity.EducationLevel?.Name,
```

**Important:** The `ToDto` method is used in CreateAsync response. Since the entity is already tracked, EF should load navigation properties by the time `ToDto` is called. If eager loading is needed, add `.Include(c => c.Occupation).Include(c => c.EducationLevel)` to the query that retrieves the entity before calling `ToDto`.

##### `BuildDetailDtoAsync` (line 1184-1244)

After `EconomicStatus = entity.EconomicStatus?.ToString(),` (line 1242), add:
```csharp
OccupationId = entity.OccupationId,
OccupationName = entity.Occupation?.Name,
EducationLevelId = entity.EducationLevelId,
EducationLevelName = entity.EducationLevel?.Name,
```

**Required — Update `GetDetailAsync` to eager-load navigation properties:** `GetDetailAsync` (line 446) uses `SingleOrDefaultAsync` without Include. Change it to:

```csharp
var entity = await db.Cases
    .Include(c => c.Occupation)
    .Include(c => c.EducationLevel)
    .SingleOrDefaultAsync(c => c.Id == caseId && c.OrganisationId == organisationId, cancellationToken);
```

Without this, `entity.Occupation?.Name` will always be null when `BuildDetailDtoAsync` constructs the response.

##### `SearchAsync` — SELECT projection (line 691)

The projection uses `Select(c => new CaseSummaryDto { ... })`. Since this is an EF projection (not in-memory), you CANNOT use navigation properties directly in a projection without Include. There are two approaches:

**Approach A (Recommended):** Use explicit FK IDs in the SELECT and populate names separately. This keeps the SELECT projection simple.

After `EconomicStatus = c.EconomicStatus != null ? c.EconomicStatus.ToString()! : null,` (line 714), add:
```csharp
OccupationId = c.OccupationId,
EducationLevelId = c.EducationLevelId,
```

Then, AFTER the `.ToListAsync()`, resolve names:
```csharp
// Resolve Occupation and EducationLevel names
var occupationIds = items.Where(i => i.OccupationId.HasValue).Select(i => i.OccupationId!.Value).Distinct().ToList();
var educationLevelIds = items.Where(i => i.EducationLevelId.HasValue).Select(i => i.EducationLevelId!.Value).Distinct().ToList();

var occupations = occupationIds.Count > 0
    ? await db.Set<Occupation>().Where(o => occupationIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken)
    : new Dictionary<Guid, string>();

var educationLevels = educationLevelIds.Count > 0
    ? await db.Set<EducationLevel>().Where(el => educationLevelIds.Contains(el.Id)).ToDictionaryAsync(el => el.Id, el => el.Name, cancellationToken)
    : new Dictionary<Guid, string>();

foreach (var item in items)
{
    item.OccupationName = item.OccupationId.HasValue && occupations.TryGetValue(item.OccupationId.Value, out var occName) ? occName : null;
    item.EducationLevelName = item.EducationLevelId.HasValue && educationLevels.TryGetValue(item.EducationLevelId.Value, out var elName) ? elName : null;
}
```

**Important:** `CaseSummaryDto` properties are get/set, so this is fine. Make `OccupationName` and `EducationLevelName` writable (not init-only).

##### `ExportAsync` — SELECT projection (line 754)

After `EconomicStatus = c.EconomicStatus != null ? c.EconomicStatus.ToString()! : null,` (line 768), add:
```csharp
Occupation = c.Occupation != null ? c.Occupation.Name : null,
EducationLevel = c.EducationLevel != null ? c.EducationLevel.Name : null,
```

Since the Export projection is an EF Select, Include is NOT needed — EF can follow navigation properties in a Select expression IF the entity is being tracked. For a pure projection, EF can translate `c.Occupation.Name` as a correlated subquery. **Verify this works at compile time.** If not, fall back to the same two-pass approach as `SearchAsync`.

##### `ParsedSearchFilters` record (line 802)

Add after `EconomicStatus` (line 808):
```csharp
Guid? OccupationId,
Guid? EducationLevelId
```

##### `ValidateSearchFilters` method (line 810)

After the EconomicStatus block (after line 879), add:
```csharp
Guid? occupationId = null;
if (query.OccupationId is not null)
{
    if (query.OccupationId == Guid.Empty)
    {
        throw new CaseValidationException("occupationId must be a valid UUID.");
    }
    occupationId = query.OccupationId;
}

Guid? educationLevelId = null;
if (query.EducationLevelId is not null)
{
    if (query.EducationLevelId == Guid.Empty)
    {
        throw new CaseValidationException("educationLevelId must be a valid UUID.");
    }
    educationLevelId = query.EducationLevelId;
}
```

**Note:** The model binder will reject non-GUID strings at the controller level, but this explicit `Guid.Empty` check provides a clearer error message at the service layer.

Update the `return new ParsedSearchFilters(...)` at line 881 to include:
```csharp
return new ParsedSearchFilters(currentStage, offenceClassification, domicile, gender, familyType, economicStatus, occupationId, educationLevelId);
```

##### `ApplySearchFilters` method (line 884)

After the EconomicStatus filter block (after line 940), add:
```csharp
if (parsed.OccupationId is not null)
{
    cases = cases.Where(c => c.OccupationId == parsed.OccupationId);
}

if (parsed.EducationLevelId is not null)
{
    cases = cases.Where(c => c.EducationLevelId == parsed.EducationLevelId);
}
```

##### `MergeAsync` method (line 211)

After the EconomicStatus fill-empty block (after line 302), add:
```csharp
if (entity.OccupationId is null && validated.OccupationId is not null)
{
    entity.OccupationId = validated.OccupationId;
    fieldsChanged = true;
}

if (entity.EducationLevelId is null && validated.EducationLevelId is not null)
{
    entity.EducationLevelId = validated.EducationLevelId;
    fieldsChanged = true;
}
```

Also update the MergeAsync call site — change `var validated = ValidateIntakeRequest(request);` to `var validated = await ValidateIntakeRequestAsync(request, organisationId, cancellationToken);`. Note `organisationId` is already resolved earlier in MergeAsync (line 221) via `var (organisationId, actorUserId) = ResolveActorContext()`.

#### 5. CaseDtoMapper.cs

In `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`, in `ToCaseSummary` (line 8), add after `EconomicStatus = entity.EconomicStatus?.ToString(),` (line 31):
```csharp
OccupationId = entity.OccupationId,
OccupationName = entity.Occupation?.Name,
EducationLevelId = entity.EducationLevelId,
EducationLevelName = entity.EducationLevel?.Name,
```

#### 6. Excel & PDF Export Headers

##### `CaseExcelExporter.cs` (apps/api/Infrastructure/Cases/CaseExcelExporter.cs)

Add to the `Headers` array (line 8-23) after `"Economic Status"` (line 19):
```csharp
"Occupation",
"Education Level",
```

Update cell data writes in the `for` loop (line 38-55). Add after the EconomicStatus cell write (line 51):
```csharp
worksheet.Cell(excelRow, 11).Value = row.Occupation ?? string.Empty;
worksheet.Cell(excelRow, 12).Value = row.EducationLevel ?? string.Empty;
```

Then shift VisitCount to col 13, NextVisitDue to col 14, UpdatedAt to col 15:
```csharp
worksheet.Cell(excelRow, 13).Value = row.VisitCount;
worksheet.Cell(excelRow, 14).Value = row.NextVisitDueAtUtc?.ToString("O") ?? string.Empty;
worksheet.Cell(excelRow, 15).Value = row.UpdatedAtUtc.ToString("O");
```

**Total columns:** 15

##### `CasePdfExporter.cs` (apps/api/Infrastructure/Cases/CasePdfExporter.cs)

Add to the `Headers` array (line 10-25) after `"Economic Status"` (line 21):
```csharp
"Occupation",
"Education Level",
```

Update table cell writes after the EconomicStatus cell:
```csharp
table.Cell().Padding(2).Text(row.Occupation ?? string.Empty);
table.Cell().Padding(2).Text(row.EducationLevel ?? string.Empty);
```

Then shift remaining cells.

#### 7. Migration

Create a new EF Core migration with a descriptive name:
```bash
dotnet ef migrations add AddCaseOccupationAndEducationLevel --context AppDbContext
```

This migration will:
- Add nullable `occupation_id` column (Guid?) to `cases` table
- Add nullable `education_level_id` column (Guid?) to `cases` table
- Add FK constraint to `occupations` table with `RESTRICT` delete behavior
- Add FK constraint to `education_levels` table with `RESTRICT` delete behavior

### Project Structure Notes

- No new enum files needed (this story uses FK references instead)
- No new DbSets required (Occupation and EducationLevel already registered from Story 9.1)
- No new NuGet packages required
- Legend entity files exist at `apps/api/Domain/Entities/Legends/Occupation.cs` and `apps/api/Domain/Entities/Legends/EducationLevel.cs`
- Legend entity pattern: `Id (Guid)`, `OrganisationId (Guid)`, `Name (string)`, `IsActive (bool)`, `CreatedByUserId (Guid)`, `CreatedAtUtc (DateTime)`, `UpdatedAtUtc (DateTime)`

### Testing Standards Summary

- Unit tests should verify:
  - `ValidateIntakeRequestAsync` resolves valid Legend FK IDs and rejects invalid ones (422)
  - Search filters (`ValidateSearchFilters`) parse valid UUID filter values
  - `ApplySearchFilters` correctly filters by OccupationId and EducationLevelId
  - `MergeAsync` fill-empty logic works correctly for OccupationId and EducationLevelId

- Integration tests (if Docker available):
  - Create a Legend Occupation and EducationLevel via Legend API → verify they exist
  - Create a Case with valid OccupationId → verify response contains OccupationId and OccupationName
  - Create a Case without OccupationId → verify it is null (backward compatibility)
  - Create a Case with invalid OccupationId → verify 422 error
  - Create a Case with an OccupationId from a **different organisation** → verify 422 (multi-tenancy enforcement)
  - Search by OccupationId → verify filtered results
  - Search by EducationLevelId → verify filtered results
  - Export → verify Occupation and Education Level columns appear in Excel/PDF output

- See existing test patterns in `tests/api.integration/`

### Previous Story Intelligence (Story 11.1)

**Key learnings from Story 11.1 (Gender, FamilyType, EconomicStatus):**

1. **Enum pattern (different from this story):** Story 11.1 used `HasConversion<string>()` for enum-to-string DB columns. This story uses FK references instead — fundamentally different approach.

2. **DTO patterns:** All DTOs (CreateCaseRequest, CaseDto, CaseDetailDto, CaseSummaryDto, CaseSearchFiltersDto, CaseExportRowDto, CaseSearchQuery) need the new fields added in the same positions.

3. **Review finding — Export cell indices:** Story 11.1 had a bug where `CaseExcelExporter` and `CasePdfExporter` headers were updated to 13 columns but the cell data writes still used hardcoded indices for 10 columns. **FOLLOW THE PATTERN CAREFULLY** when adding the 2 new columns:
   - Headers go from 13 → 15 columns
   - Cell data writes must map to the correct new indices (11, 12)
   - Shift VisitCount to col 13, NextVisitDue to col 14, UpdatedAt to col 15

4. **Review finding — MergeAsync:** Gender, FamilyType, EconomicStatus were parsed by `ValidateIntakeRequest` but not assigned in `MergeAsync`. This was fixed by adding fill-empty logic. **Apply the same fix proactively for OccupationId and EducationLevelId.**

5. **Review finding — DB indexes:** `ApplySearchFilters` adds WHERE clauses without indexes. For FK columns, PostgreSQL automatically creates an index on FK columns, so this is less of a concern here. No additional index needed.

6. **Review finding — PII redaction:** The previous review noted that demographic fields were not redacted for POCSO cases. This is a pre-existing concern. Occupation and Education Level are less sensitive than Gender/FamilyType, but consider whether they should be redacted. **Deferred concern** — same approach as Story 11.1.

7. **ValidateIntakeRequest must become async:** Unlike Story 11.1 which used synchronous enum parsing via `TryParseEnum<T>()`, this story needs async DB lookups to validate Legend FK references exist. This requires:
   - Renaming to `ValidateIntakeRequestAsync` returning `Task<ValidatedCreateCaseRequest>`
   - Updating all callers (`CreateAsync`, `MergeAsync`) to `await`
   - Accepting `CancellationToken`

### Git Intelligence Summary

Recent commits show active development across Epics 8-10 (dashboard, reports, migration). The most relevant recent changes are in Story 11.1 which established the pattern for adding socio-demographic fields to the Case entity. Key observations:

- Enum files live in `apps/api/Domain/Enums/`
- Legend entities live in `apps/api/Domain/Entities/Legends/`
- `CaseService.cs` is the primary service with validation, creation, search, export, and merge all in one file (~1320 lines)
- The prior commit pattern shows 45 files changed for Story 11.1, suggesting this story will touch a similar set of files but with FK relationships instead of enums
- EF Core migrations are created with descriptive names like `AddCaseGenderFamilyTypeEconomicStatus`

### References

- [Source: `apps/api/Domain/Entities/Case.cs`] — Entity (add OccupationId, EducationLevelId, nav properties)
- [Source: `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`] — Column/FK configuration
- [Source: `apps/api/Domain/Entities/Legends/Occupation.cs`] — Legend entity pattern
- [Source: `apps/api/Domain/Entities/Legends/EducationLevel.cs`] — Legend entity pattern
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:1009-1117`] — `ValidateIntakeRequest` (make async, add FK validation)
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:1295-1308`] — `ValidatedCreateCaseRequest` record
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:802-808`] — `ParsedSearchFilters` record
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:810-881`] — `ValidateSearchFilters` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:884-961`] — `ApplySearchFilters` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:1170-1182`] — `ToDto` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:1184-1244`] — `BuildDetailDtoAsync` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:661-724`] — `SearchAsync` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:726-784`] — `ExportAsync` method
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs:211-303`] — `MergeAsync` method
- [Source: `apps/api/Models/Cases/CaseDtos.cs`] — All Case DTOs
- [Source: `apps/api/Models/Cases/CaseSearchQuery.cs`] — Search query model
- [Source: `apps/api/Models/Cases/CaseExportRowDto.cs`] — Export row DTO
- [Source: `apps/api/Infrastructure/Cases/CaseDtoMapper.cs`] — DTO mapper
- [Source: `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`] — Excel export
- [Source: `apps/api/Infrastructure/Cases/CasePdfExporter.cs`] — PDF export
- [Source: `_bmad-output/implementation-artifacts/11-1-gender-family-type-economic-status-on-case.md`] — Previous story (pattern reference)
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 11`] — Story definition

### Review Findings

#### Patch (actionable fixes)

- [x] [Review][Patch] Missing `.Include()`/`LoadAsync()` for navigation properties in `TransitionStageAsync`, `TransferAsync`, `ListAssignedAsync` [`CaseService.cs:169-221`, `CaseService.cs:403-478`, `CaseService.cs:681-685`] — These methods load Case entities without eager-loading `Occupation`/`EducationLevel` navigation properties. `ToDto` and `BuildDetailDtoAsync` access `entity.Occupation?.Name` and `entity.EducationLevel?.Name`, which will always be null. Fix: add `.Include(c => c.Occupation).Include(c => c.EducationLevel)` or explicit `LoadAsync` calls before constructing DTOs.
- [x] [Review][Patch] `Guid.Empty` rejection message is technically incorrect [`CaseService.cs:1023,1033`] — The error message says "must be a valid UUID" but `Guid.Empty` (`00000000-0000-0000-0000-000000000000`) is a valid UUID. Message should say "must be a non-empty UUID" to accurately convey the constraint.
- [x] [Review][Patch] Export projection may silently return null for `Occupation`/`EducationLevel` names [`CaseService.cs:852`] — The export `Select` accesses `c.Occupation.Name` without `.Include()`. While EF can translate this via correlated subquery in projections, relying on implicit behavior is fragile. Either use the same two-pass approach as `SearchAsync`, or verify with a comment that EF translates the navigation property correctly.
- [x] [Review][Patch] MergeAsync audit event `draftSnapshot` omits `occupationId` and `educationLevelId` [`CaseService.cs:349-361`] — The `CaseMerged` audit event's `draftSnapshot` dictionary includes `gender`, `familyType`, `economicStatus`, `isFirstTimeOffender` but not the new FK fields. Add `occupationId` and `educationLevelId` to preserve audit completeness.
- [x] [Review][Patch] Error message format inconsistency between occupation and educationLevel validation [`CaseService.cs:1195,1208`] — Throws `"referenced occupation not found for id: {guid}"` (lowercase "occupation") vs `"referenced educationLevel not found for id: {guid}"` (camelCase "educationLevel"). Neither matches the JSON property name (`occupationId`/`educationLevelId`). Standardise to match JSON property names.

#### Deferred (pre-existing / intentional / out of scope)

- [x] [Review][Defer] TOCTOU race in legend reference validation — Race between IsActive check and SaveChangesAsync; pre-existing across the codebase.
- [x] [Review][Defer] Brittle hard-coded Excel column indices — Pre-existing pattern; global refactor needed.
- [x] [Review][Defer] Two different name-resolution strategies (search vs export) — Intentional design choice.
- [x] [Review][Defer] No support for null search filters — Feature request; not introduced by this story.
- [x] [Review][Defer] Duplicated LoadAsync blocks in CreateAsync and MergeAsync — Minor DRY concern; acceptable for now.

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

### File List

**New files:**
- `apps/api/Migrations/<timestamp>_AddCaseOccupationAndEducationLevel.cs`
- `apps/api/Migrations/<timestamp>_AddCaseOccupationAndEducationLevel.Designer.cs`

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
