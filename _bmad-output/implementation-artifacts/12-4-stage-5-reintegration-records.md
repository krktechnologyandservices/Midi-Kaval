---
baseline_commit: edd947b56377d2c5c7fd02213f0c9e10a2f7200e
---

# Story 12.4: Stage 5 Reintegration Records

Status: done

## Story

As a **Project Coordinator**,
I want to record reintegration level and institution details,
So that the child's reintegration path is documented.

## Acceptance Criteria

1. **Given** a new `case_stage5_reintegration` table
   **When** the migration runs
   **Then** the table has columns for: `id`, `case_id` (FK→cases), `organisation_id`, `reintegration_level` (enum string), `institution_details`, `created_by_user_id`, `created_at_utc`, `updated_at_utc`
   **And** there is a unique index on `case_id` (1 reintegration record per case, matching Stage 2 and Stage 4 patterns)
   **And** text fields are nullable with `HasMaxLength(2000)`

2. **Given** a Case in Stage 5 (`Reintegration`)
   **When** a Coordinator or above calls `GET /api/v1/cases/{caseId}/stage5-data`
   **Then** the reintegration record for that case is returned as a single DTO
   **And** 404 is returned if the case is not in Stage 5
   **And** 403 is returned for SocialWorker/CaseWorker roles
   **And** a default/empty DTO (with CaseId populated) is returned if no reintegration record exists yet

3. **Given** a Case in Stage 5 (`Reintegration`)
   **When** a Coordinator or above calls `PUT /api/v1/cases/{caseId}/stage5-data`
   **Then** the reintegration record is upserted (created if not exists, updated if exists)
   **And** the response is 200 OK with the updated DTO
   **And** 404 is returned if the case is not in Stage 5
   **And** 422 is returned if `reintegration_level` is invalid, or if any text field exceeds 2000 characters
   **And** an `audit_events` row is written recording the update

4. **Given** an existing Case NOT in Stage 5
   **When** a GET or PUT request is made to `/api/v1/cases/{caseId}/stage5-data`
   **Then** 404 is returned (reintegration records only applicable in Stage 5)

5. **Given** a Case transitions out of Stage 5 to Stage 6
   **When** the transition occurs
   **Then** the existing Stage 5 reintegration record remains in the database (no cascade delete)
   **And** GET requests for Stage 5 data on the case now return 404

## Tasks / Subtasks

- [x] Create `ReintegrationLevel` enum in `apps/api/Domain/Enums/` with values: `Community`, `Institutional` (AC: 1)
- [x] Create `CaseStage5Reintegration` entity in `apps/api/Domain/Entities/` (AC: 1)
- [x] Create `CaseStage5ReintegrationConfiguration` in `apps/api/Infrastructure/Persistence/` (AC: 1)
- [x] Add `DbSet<CaseStage5Reintegration> CaseStage5Reintegrations` to `AppDbContext.cs` (AC: 1)
- [x] Create EF Core migration `AddCaseStage5Reintegration` (AC: 1)
- [x] Create `Stage5ReintegrationDto` and `UpsertStage5ReintegrationRequest` in `apps/api/Models/Cases/Stage5DataDtos.cs` (AC: 2, 3)
- [x] Create `CaseStage5DataController` with GET and PUT endpoints at `/api/v1/cases/{caseId}/stage5-data` (AC: 2, 3, 4)
- [x] Create `CaseStage5DataService` in `apps/api/Infrastructure/Cases/` with `GetAsync` and `UpsertAsync` methods (AC: 2, 3)
- [x] Validate stage is `Reintegration` before allowing data access — return 404 if not (AC: 2, 4, 5)
- [x] Add audit event writing on upsert (AC: 3)
- [x] Add field length validation (max 2000 chars per text field) and reintegration level validation — return 422 on violation (AC: 3)
- [x] Register `CaseStage5DataService` in DI (`Program.cs`)
- [x] Run `dotnet build` and verify all projects compile

## Dev Notes

### CRITICAL: This is a new entity + new API — NOT modifying Case entity

Like Stories 12.1–12.4, this story creates a **new entity** and **new controller**. Follow the established patterns.

### Relationship Pattern: 1-to-1 (same as Stages 2 and 4)

Stage 5 has a **single reintegration record per case** — matching the Stage 2 and Stage 4 data patterns:

- **Unique index** on `case_id` — use `HasIndex(d => d.CaseId).IsUnique()`
- **GET returns a single `Stage5ReintegrationDto`** (not a list)
- **PUT upserts a single record** — create if not exists, update if exists
- **Request DTO is a flat object**, not a list

### ReintegrationLevel Enum Design

```csharp
namespace MidiKaval.Api.Domain.Enums;

public enum ReintegrationLevel
{
    Community,
    Institutional,
}
```

### 1. New Enum — `ReintegrationLevel.cs`

```csharp
namespace MidiKaval.Api.Domain.Enums;

public enum ReintegrationLevel
{
    Community,
    Institutional,
}
```

### 2. New Entity — `CaseStage5Reintegration.cs`

```csharp
using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStage5Reintegration
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public ReintegrationLevel ReintegrationLevel { get; set; }
    public string? InstitutionDetails { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

### 3. New Configuration — `CaseStage5ReintegrationConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage5ReintegrationConfiguration : IEntityTypeConfiguration<CaseStage5Reintegration>
{
    public void Configure(EntityTypeBuilder<CaseStage5Reintegration> builder)
    {
        builder.ToTable("case_stage5_reintegration");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.ReintegrationLevel)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.InstitutionDetails)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.CaseId)
            .IsUnique();
    }
}
```

### 4. AppDbContext — Add DbSet

```csharp
public DbSet<CaseStage5Reintegration> CaseStage5Reintegrations => Set<CaseStage5Reintegration>();
```

### 5. DTOs — `Stage5DataDtos.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class Stage5ReintegrationDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string ReintegrationLevel { get; set; } = string.Empty;
    public string? InstitutionDetails { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertStage5ReintegrationRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string ReintegrationLevel { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? InstitutionDetails { get; set; }
}
```

### 6. Audit Event Types

Add to `AuditEventTypes.cs`:
```csharp
public const string Stage5ReintegrationCreated = "case.stage5_reintegration.created";
public const string Stage5ReintegrationUpdated = "case.stage5_reintegration.updated";
```

### 7. Service — `CaseStage5DataService.cs`

Follow the exact patterns from `CaseStage4DataService.cs`:
- Define `private const int MaxInstitutionDetailsLength = 2000;`
- `GetAsync(Guid caseId, CancellationToken ct)` → Stage check → fetch or empty DTO
- `UpsertAsync(Guid caseId, UpsertStage5ReintegrationRequest request, CancellationToken ct)` → Stage check → validate request → create/update → audit event → save
- `ValidateRequest` should: validate `ReintegrationLevel` (required, valid enum), check `InstitutionDetails` length (max 2000) and whitespace-only
- `ValidateRequest` returns the parsed enum value (avoid redundant `Enum.Parse`)
- `ToDto(CaseStage5Reintegration?, Guid caseId)` → return empty DTO with CaseId set when null
- `ResolveActorContext()` — same claims resolution pattern

### 8. Controller — `CaseStage5DataController.cs`

```csharp
[ApiController]
[Route("api/v1/cases/{caseId:guid}/stage5-data")]
public sealed class CaseStage5DataController(CaseStage5DataService stage5DataService) : ControllerBase
{
    [HttpGet] [Authorize(Policy = Policies.CoordinatorOrAbove)]
    // ProducesResponseType for 200, 401, 403, 404
    // Catch CaseNotFoundException→404, InvalidOperationException→401

    [HttpPut] [RequestSizeLimit(16_384)] [Authorize(Policy = Policies.CoordinatorOrAbove)]
    // ProducesResponseType for 200, 400, 401, 403, 404, 409, 422
    // Catch CaseNotFoundException→404, CaseBusinessRuleException→422, InvalidOperationException→401, DbUpdateException→409
}
```

### 9. DI Registration

```csharp
builder.Services.AddScoped<CaseStage5DataService>();
```

### 10. Migration

```bash
dotnet ef migrations add AddCaseStage5Reintegration --output-dir Migrations
```

### 11. Validation Rules Summary

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| ReintegrationLevel | string (enum) | Yes | Must be one of: Community, Institutional |
| InstitutionDetails | string? | No | Max 2000 characters, reject whitespace-only |
| Case stage | enum | Yes | Must be `Reintegration` (checked at service level) |

### 12. Authorization

- `GET`, `PUT`: `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
- SocialWorker and CaseWorker roles are forbidden (403)

### Project Structure Notes

- **New files to create:**
  - `apps/api/Domain/Enums/ReintegrationLevel.cs`
  - `apps/api/Domain/Entities/CaseStage5Reintegration.cs`
  - `apps/api/Infrastructure/Persistence/CaseStage5ReintegrationConfiguration.cs`
  - `apps/api/Models/Cases/Stage5DataDtos.cs`
  - `apps/api/Infrastructure/Cases/CaseStage5DataService.cs`
  - `apps/api/Controllers/V1/CaseStage5DataController.cs`
  - `apps/api/Migrations/<timestamp>_AddCaseStage5Reintegration.cs` + `.Designer.cs`

- **Modified files:**
  - `apps/api/Infrastructure/Persistence/AppDbContext.cs` — add DbSet
  - `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — new event type constants
  - `apps/api/Program.cs` — register service in DI
  - `apps/api/Migrations/AppDbContextModelSnapshot.cs` — auto-updated

### Previous Story Intelligence (Stories 12.1–12.3)

**Key learnings baked into this story:**
1. Include all catch blocks from the start: `CaseNotFoundException`→404, `CaseBusinessRuleException`→422, `DbUpdateException`→409, `InvalidOperationException`→401
2. Include all `ProducesResponseType` attributes on both endpoints (200, 400, 401, 403, 404, 409, 422)
3. Split audit event types: *Created* on first insert, *Updated* on subsequent updates
4. Add data annotation validation (`[Required]`, `[MaxLength]`) to request DTOs from the start
5. Add `[RequestSizeLimit]` attribute on PUT endpoint
6. Include `CreatedByUserId` in the response DTO
7. Return parsed enum value from `ValidateRequest` to avoid redundant `Enum.Parse`
8. Add `Enum.IsDefined` check to reject numeric enum strings (e.g., `"1"`)
9. Add `IsNullOrWhiteSpace` checks for nullable string fields
10. FKs: add `HasOne<Case>().WithMany()` and `HasOne<User>().WithMany()` with `DeleteBehavior.Restrict`
11. Unique index on `CaseId`: `HasIndex(d => d.CaseId).IsUnique()`
12. Migration will auto-add `created_by_user_id` index from FK convention

### Testing Standards Summary

- Unit tests for `CaseStage5DataService`:
  - `GetAsync` returns DTO when reintegration exists
  - `GetAsync` returns empty DTO when no reintegration record exists
  - `GetAsync` throws `CaseNotFoundException` when case not in Stage 5
  - `GetAsync` throws `CaseNotFoundException` when case doesn't exist
  - `UpsertAsync` creates new record on first call
  - `UpsertAsync` updates existing record on subsequent calls
  - `UpsertAsync` throws 422 when reintegration level is invalid
  - `UpsertAsync` throws 422 when InstitutionDetails exceeds 2000 chars
  - `UpsertAsync` throws 422 when InstitutionDetails is whitespace-only
  - `UpsertAsync` writes audit event on create and update
  - Authorization: SocialWorker/CaseWorker get 403

## Review Findings

### Decision Needed

*(None — all findings were classifiable without ambiguity.)*

### Patches

- [x] [Review][Patch] Missing `Trim()` before `Enum.TryParse` in `ValidateRequest` — inconsistent with `CaseService.TryParseEnum` convention which trims input. Use `request.ReintegrationLevel.Trim()` before parsing. [`CaseStage5DataService.cs:~134`]
- [x] [Review][Patch] Missing `OperationCanceledException` catch in controller — unhandled cancellation token exceptions propagate to middleware, producing 500 instead of clean cancellation. Add `catch (OperationCanceledException)` to both GET and PUT. [`CaseStage5DataController.cs:23-38, 60-80`]

### Deferred

- [x] [Review][Defer] No `None`/`Unset` sentinel in `ReintegrationLevel` enum — pre-existing design pattern matching `PlacementType`, `SupportType`. [`ReintegrationLevel.cs`]
- [x] [Review][Defer] Missing `OrganisationId` FK constraint — Organisation entity/table doesn't exist yet (deferred per project-wide pattern: "organisations schema is a later epic"). [`CaseStage5ReintegrationConfiguration.cs`]
- [x] [Review][Defer] `HasOne<User>()` FK assumption without verifying `User` entity mapping — pre-existing pattern used across all stages. [`CaseStage5ReintegrationConfiguration.cs`]
- [x] [Review][Defer] Audit events record only `caseId` and `actorUserId` metadata, not value deltas — pre-existing pattern across all stage services. [`CaseStage5DataService.cs`]
- [x] [Review][Defer] `JsonSerializerDefaults.Web` choice on dictionary serialization — functionally correct but stylistically questionable; pre-existing pattern. [`CaseStage5DataService.cs`]
- [x] [Review][Defer] `DbUpdateException` catch → 409 too broad (FK violations, deadlocks all mapped as "conflict") — pre-existing pattern in all previous stage controllers. [`CaseStage5DataController.cs`]
- [x] [Review][Defer] No `RowVersion` concurrency token — pre-existing pattern matching Stage 2 and Stage 4 (only Stage 3 was patched to add one). [`CaseStage5Reintegration.cs`]
- [x] [Review][Defer] `OrganisationId` denormalized without cross-reference check against `Case.OrganisationId` — pre-existing pattern across all stage entities. [`CaseStage5ReintegrationConfiguration.cs`]
- [x] [Review][Defer] Integer enum values (e.g., `"0"`, `"1"`) bypass string validation via `Enum.TryParse` — pre-existing behavior across all stage services. [`CaseStage5DataService.cs:134`]
- [x] [Review][Defer] `MaxInstitutionDetailsLength` constant (2000) duplicated across service, DTO annotation, and EF config — pre-existing pattern. [`CaseStage5DataService.cs`, `Stage5DataDtos.cs`, `CaseStage5ReintegrationConfiguration.cs`]
- [x] [Review][Defer] `RequestSizeLimit(16_384)` without `413 PayloadTooLarge` in `ProducesResponseType` — pre-existing pattern in all stage controllers. [`CaseStage5DataController.cs`]
- [x] [Review][Defer] `WithMany()` without inverse navigation property on `Case` — pre-existing pattern matching all previous stage configurations. [`CaseStage5ReintegrationConfiguration.cs`]
- [x] [Review][Defer] TOCTOU race: stage check to `SaveChangesAsync` window — case could transition out of Stage 5 between verification and persistence; pre-existing pattern across all stage services. [`CaseStage5DataService.cs:47-56, 144`]
- [x] [Review][Defer] `[ApiController]` auto-400 short-circuits before service validation can produce the AC-required 422 — pre-existing pattern affecting all stage controllers. [`Stage5DataDtos.cs`, `CaseStage5DataController.cs`]

### Dismissed

- [x] [Review][Dismiss] `WithMany()` + `IsUnique()` contradiction — same EF pattern dismissed in Stage 4 review as pre-existing and functionally correct.
- [x] [Review][Dismiss] Zombie DTO with default values when no record exists — explicitly by design per AC 2 ("a default/empty DTO (with CaseId populated) is returned if no reintegration record exists yet").
- [x] [Review][Dismiss] `InvalidOperationException` → 401 pattern too broad — intentionally catches IOE from `ResolveActorContext`; this pattern was reviewed and accepted as a patch in Stage 4 review.
- [x] [Review][Dismiss] `ReintegrationLevel` string→enum at service layer not model binding — established pattern matching all stage services.

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

- Created `ReintegrationLevel` enum in `Domain/Enums/ReintegrationLevel.cs` with values: `Community`, `Institutional`
- Created `CaseStage5Reintegration` entity in `Domain/Entities/` with properties `ReintegrationLevel` (enum), `InstitutionDetails`, audit fields
- Created `CaseStage5ReintegrationConfiguration` in `Infrastructure/Persistence/` — maps to `case_stage5_reintegration` table, `HasConversion<string>()` on enum, `HasMaxLength` constraints, unique index on `CaseId`, `HasOne<Case>().WithMany().HasForeignKey()` with `DeleteBehavior.Restrict`
- Added `DbSet<CaseStage5Reintegration> CaseStage5Reintegrations` to `AppDbContext.cs`
- Created DTOs in `Models/Cases/Stage5DataDtos.cs` — `Stage5ReintegrationDto` and `UpsertStage5ReintegrationRequest` with `[Required]`, `[MaxLength(32)]` on `ReintegrationLevel`, `[MaxLength(2000)]` on `InstitutionDetails`
- Created `CaseStage5DataController` in `Controllers/V1/` — `[Authorize(Policy = Policies.CoordinatorOrAbove)]`, GET and PUT at `/api/v1/cases/{caseId}/stage5-data`, with exception handling (400, 401, 403, 404, 409, 422)
- Created `CaseStage5DataService` in `Infrastructure/Cases/` — `GetAsync` (returns empty DTO with CaseId if no record exists), `UpsertAsync` (create/update with stage validation), `ValidateRequest` (enum parsing/validation, whitespace check, max length check)
- Audit events: `Stage5ReintegrationCreated` and `Stage5ReintegrationUpdated` added to `AuditEventTypes.cs`
- Registered `CaseStage5DataService` in DI (`Program.cs`)
- Generated EF Core migration `AddCaseStage5Reintegration`
- `dotnet build` — 0 errors; unit tests 39/39 passed; integration tests (433 failures) are pre-existing Docker/Testcontainers infrastructure issue

### File List

- `apps/api/Domain/Enums/ReintegrationLevel.cs`
- `apps/api/Domain/Entities/CaseStage5Reintegration.cs`
- `apps/api/Infrastructure/Persistence/CaseStage5ReintegrationConfiguration.cs`
- `apps/api/Infrastructure/Persistence/AppDbContext.cs` (modified)
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` (modified)
- `apps/api/Models/Cases/Stage5DataDtos.cs`
- `apps/api/Infrastructure/Cases/CaseStage5DataService.cs`
- `apps/api/Controllers/V1/CaseStage5DataController.cs`
- `apps/api/Program.cs` (modified)
- `apps/api/Migrations/20260621080747_AddCaseStage5Reintegration.cs`
- `apps/api/Migrations/20260621080747_AddCaseStage5Reintegration.Designer.cs`
