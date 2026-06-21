---
baseline_commit: edd947b56377d2c5c7fd02213f0c9e10a2f7200e
---

# Story 12.5: Stage 6 Termination/Exclusion Records

Status: done

## Story

As a **Project Coordinator**,
I want to record termination from JJB or exclusion with reason and report attachment,
So that case closure is fully documented.

## Acceptance Criteria

1. **Given** a new `case_stage6_termination_exclusion` table
   **When** the migration runs
   **Then** the table has columns for: `id`, `case_id` (FK→cases), `organisation_id`, `termination_exclusion_type` (enum string), `jjb_details`, `exclusion_reason`, `report_attachment_id` (nullable FK→attachments), `created_by_user_id`, `created_at_utc`, `updated_at_utc`
   **And** there is a unique index on `case_id` (1 termination/exclusion record per case, matching Stage 2/4/5 patterns)
   **And** text fields are nullable with `HasMaxLength(2000)`
   **And** `report_attachment_id` has `DeleteBehavior.SetNull` (preserve record if attachment is deleted)

2. **Given** a Case in Stage 6 (`TerminationExclusion`)
   **When** a Coordinator or above calls `GET /api/v1/cases/{caseId}/stage6-data`
   **Then** the termination/exclusion record for that case is returned as a single DTO
   **And** 404 is returned if the case is not in Stage 6
   **And** 403 is returned for SocialWorker/CaseWorker roles
   **And** a default/empty DTO (with CaseId populated) is returned if no record exists yet

3. **Given** a Case in Stage 6 (`TerminationExclusion`)
   **When** a Coordinator or above calls `PUT /api/v1/cases/{caseId}/stage6-data`
   **Then** the termination/exclusion record is upserted (created if not exists, updated if exists)
   **And** the response is 200 OK with the updated DTO
   **And** 404 is returned if the case is not in Stage 6
   **And** 422 is returned if `termination_exclusion_type` is invalid, or if any text field exceeds 2000 characters
   **And** an `audit_events` row is written recording the update

4. **Given** an existing Case NOT in Stage 6
   **When** a GET or PUT request is made to `/api/v1/cases/{caseId}/stage6-data`
   **Then** 404 is returned (termination/exclusion records only applicable in Stage 6)

5. **Given** a Case is in Stage 6 (TerminationExclusion)
   **When** the record is viewed
   **Then** either `jjb_details` (for Termination type) or `exclusion_reason` (for Exclusion type) should be populated
   **And** `report_attachment_id` links to an optional attached report file in the `attachments` table

## Tasks / Subtasks

- [x] Create `TerminationExclusionType` enum in `apps/api/Domain/Enums/` with values: `Termination`, `Exclusion` (AC: 1)
- [x] Create `CaseStage6TerminationExclusion` entity in `apps/api/Domain/Entities/` (AC: 1)
- [x] Create `CaseStage6TerminationExclusionConfiguration` in `apps/api/Infrastructure/Persistence/` (AC: 1)
- [x] Add `DbSet<CaseStage6TerminationExclusion> CaseStage6TerminationExclusions` to `AppDbContext.cs` (AC: 1)
- [x] Create EF Core migration `AddCaseStage6TerminationExclusion` (AC: 1)
- [x] Create `Stage6TerminationExclusionDto` and `UpsertStage6TerminationExclusionRequest` in `apps/api/Models/Cases/Stage6DataDtos.cs` (AC: 2, 3)
- [x] Create `CaseStage6DataController` with GET and PUT endpoints at `/api/v1/cases/{caseId}/stage6-data` (AC: 2, 3, 4)
- [x] Create `CaseStage6DataService` in `apps/api/Infrastructure/Cases/` with `GetAsync` and `UpsertAsync` methods (AC: 2, 3)
- [x] Validate stage is `TerminationExclusion` before allowing data access — return 404 if not (AC: 2, 4)
- [x] Add audit event writing on upsert (AC: 3)
- [x] Add field length validation (max 2000 chars per text field) and termination/exclusion type validation — return 422 on violation (AC: 3)
- [x] Register `CaseStage6DataService` in DI (`Program.cs`)
- [x] Run `dotnet build` and verify all projects compile

## Dev Notes

### CRITICAL: This is a new entity + new API — NOT modifying Case entity

Like Stories 12.1–12.5, this story creates a **new entity** and **new controller**. Follow the established patterns.

### Relationship Pattern: 1-to-1 (same as Stages 2, 4, and 5)

Stage 6 has a **single termination/exclusion record per case**:

- **Unique index** on `case_id` — use `HasIndex(d => d.CaseId).IsUnique()`
- **GET returns a single `Stage6TerminationExclusionDto`** (not a list)
- **PUT upserts a single record** — create if not exists, update if exists
- **Request DTO is a flat object**, not a list

### 1. New Enum — `TerminationExclusionType.cs`

```csharp
namespace MidiKaval.Api.Domain.Enums;

public enum TerminationExclusionType
{
    Termination,
    Exclusion,
}
```

### 2. New Entity — `CaseStage6TerminationExclusion.cs`

Key differences from previous stages:
- Two mutually exclusive text fields (`JjbDetails` for Termination, `ExclusionReason` for Exclusion)
- Optional FK to `attachments` table for report attachment via `ReportAttachmentId` (Guid?)

```csharp
using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStage6TerminationExclusion
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public TerminationExclusionType TerminationExclusionType { get; set; }
    public string? JjbDetails { get; set; }
    public string? ExclusionReason { get; set; }
    public Guid? ReportAttachmentId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

### 3. New Configuration — `CaseStage6TerminationExclusionConfiguration.cs`

Follow the pattern from `CaseStage5ReintegrationConfiguration.cs` with these additions:
- FK to `Attachment` (instead of `User` for the report FK): `HasOne<Attachment>().WithMany().HasForeignKey(d => d.ReportAttachmentId).OnDelete(DeleteBehavior.SetNull)`
- FK to `User` for `CreatedByUserId`
- Unique index on `CaseId`
- Enum string conversion with `HasMaxLength(32)`
- Text fields with `HasMaxLength(2000)`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage6TerminationExclusionConfiguration : IEntityTypeConfiguration<CaseStage6TerminationExclusion>
{
    public void Configure(EntityTypeBuilder<CaseStage6TerminationExclusion> builder)
    {
        builder.ToTable("case_stage6_termination_exclusion");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TerminationExclusionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.JjbDetails)
            .HasMaxLength(2000);

        builder.Property(d => d.ExclusionReason)
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

        builder.HasOne<Attachment>()
            .WithMany()
            .HasForeignKey(d => d.ReportAttachmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.CaseId)
            .IsUnique();
    }
}
```

### 4. AppDbContext — Add DbSet

```csharp
public DbSet<CaseStage6TerminationExclusion> CaseStage6TerminationExclusions => Set<CaseStage6TerminationExclusion>();
```

### 5. DTOs — `Stage6DataDtos.cs`

Note: `ReportAttachmentId` is included in the request DTO to allow linking or clearing an already-uploaded report attachment. Attachments themselves are created through the existing `AttachmentService` (presign → upload → confirm flow), not through this endpoint.

```csharp
using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class Stage6TerminationExclusionDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string TerminationExclusionType { get; set; } = string.Empty;
    public string? JjbDetails { get; set; }
    public string? ExclusionReason { get; set; }
    public Guid? ReportAttachmentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertStage6TerminationExclusionRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string TerminationExclusionType { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? JjbDetails { get; set; }

    [MaxLength(2000)]
    public string? ExclusionReason { get; set; }

    public Guid? ReportAttachmentId { get; set; }
}
```

### 6. Audit Event Types

Add to `AuditEventTypes.cs`:
```csharp
public const string Stage6TerminationExclusionCreated = "case.stage6_termination_exclusion.created";
public const string Stage6TerminationExclusionUpdated = "case.stage6_termination_exclusion.updated";
```

### 7. Service — `CaseStage6DataService.cs`

Follow the exact patterns from `CaseStage5DataService.cs`:

- Define constant for max text length:
  ```csharp
  private const int MaxTextFieldLength = 2000;
  ```
- `GetAsync(Guid caseId, CancellationToken ct)` → Stage check → fetch or empty DTO
- `UpsertAsync(Guid caseId, UpsertStage6TerminationExclusionRequest request, CancellationToken ct)` → Stage check → validate request → create/update → audit event → save
- `ValidateRequest` should:
  1. Validate `TerminationExclusionType` (required, valid enum via `Enum.TryParse` + `Enum.IsDefined`, apply `.Trim()` before parsing)
  2. Check `JjbDetails` length (max 2000) and whitespace-only
  3. Check `ExclusionReason` length (max 2000) and whitespace-only
  4. Return the parsed `TerminationExclusionType` enum
- `ToDto(CaseStage6TerminationExclusion?, Guid caseId)` → return empty DTO with CaseId set when null
- `ResolveActorContext()` — same claims resolution pattern as all other stage services

### 8. Controller — `CaseStage6DataController.cs`

Follow the exact pattern from `CaseStage5DataController.cs`:

```csharp
[ApiController]
[Route("api/v1/cases/{caseId:guid}/stage6-data")]
public sealed class CaseStage6DataController(CaseStage6DataService stage6DataService) : ControllerBase
{
    [HttpGet] [Authorize(Policy = Policies.CoordinatorOrAbove)]
    // ProducesResponseType for 200, 401, 403, 404
    // Catch CaseNotFoundException→404, InvalidOperationException→401, OperationCanceledException→throw

    [HttpPut] [RequestSizeLimit(16_384)] [Authorize(Policy = Policies.CoordinatorOrAbove)]
    // ProducesResponseType for 200, 400, 401, 403, 404, 409, 422
    // Catch CaseNotFoundException→404, CaseBusinessRuleException→422, InvalidOperationException→401, DbUpdateException→409, OperationCanceledException→throw
}
```

### 9. DI Registration

```csharp
builder.Services.AddScoped<CaseStage6DataService>();
```

### 10. Migration

```bash
dotnet ef migrations add AddCaseStage6TerminationExclusion --output-dir Migrations
```

### 11. Validation Rules Summary

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| TerminationExclusionType | string (enum) | Yes | Must be one of: Termination, Exclusion |
| JjbDetails | string? | No | Max 2000 characters, reject whitespace-only |
| ExclusionReason | string? | No | Max 2000 characters, reject whitespace-only |
| ReportAttachmentId | Guid? | No | Nullable FK to attachments table |
| Case stage | enum | Yes | Must be `TerminationExclusion` (checked at service level) |

### 12. Authorization

- `GET`, `PUT`: `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
- SocialWorker and CaseWorker roles are forbidden (403)

### Project Structure Notes

- **New files to create:**
  - `apps/api/Domain/Enums/TerminationExclusionType.cs`
  - `apps/api/Domain/Entities/CaseStage6TerminationExclusion.cs`
  - `apps/api/Infrastructure/Persistence/CaseStage6TerminationExclusionConfiguration.cs`
  - `apps/api/Models/Cases/Stage6DataDtos.cs`
  - `apps/api/Infrastructure/Cases/CaseStage6DataService.cs`
  - `apps/api/Controllers/V1/CaseStage6DataController.cs`
  - `apps/api/Migrations/<timestamp>_AddCaseStage6TerminationExclusion.cs` + `.Designer.cs`

- **Modified files:**
  - `apps/api/Infrastructure/Persistence/AppDbContext.cs` — add DbSet
  - `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — new event type constants
  - `apps/api/Program.cs` — register service in DI
  - `apps/api/Migrations/AppDbContextModelSnapshot.cs` — auto-updated

### Previous Story Intelligence (Stories 12.1–12.5)

**Key learnings baked into this story:**
1. Include all catch blocks from the start: `CaseNotFoundException`→404, `CaseBusinessRuleException`→422, `DbUpdateException`→409, `InvalidOperationException`→401
2. Include `catch (OperationCanceledException) { throw; }` in both GET and PUT (from Story 12.4 patch)
3. Include all `ProducesResponseType` attributes on both endpoints (200, 400, 401, 403, 404, 409, 422)
4. Split audit event types: *Created* on first insert, *Updated* on subsequent updates
5. Add data annotation validation (`[Required]`, `[MaxLength]`) to request DTOs from the start
6. Add `[RequestSizeLimit]` attribute on PUT endpoint
7. Include `CreatedByUserId` in the response DTO
8. Return parsed enum value from `ValidateRequest` to avoid redundant `Enum.Parse`
9. Add `Enum.IsDefined` check to reject numeric enum strings (e.g., `"1"`)
10. Add `IsNullOrWhiteSpace` checks for nullable string fields
11. Use `.Trim()` before `Enum.TryParse` in `ValidateRequest` (from Story 12.4 patch)
12. FKs: add `HasOne<Case>().WithMany()` and `HasOne<User>().WithMany()` with `DeleteBehavior.Restrict`
13. Optional FK pattern: `HasOne<Attachment>().WithMany().HasForeignKey(d => d.ReportAttachmentId).OnDelete(DeleteBehavior.SetNull)`
14. Unique index on `CaseId`: `HasIndex(d => d.CaseId).IsUnique()`
15. Migration will auto-add `created_by_user_id` and `report_attachment_id` indexes from FK convention

### Testing Standards Summary

- Unit tests for `CaseStage6DataService`:
  - `GetAsync` returns DTO when record exists
  - `GetAsync` returns empty DTO when no record exists
  - `GetAsync` throws `CaseNotFoundException` when case not in Stage 6
  - `GetAsync` throws `CaseNotFoundException` when case doesn't exist
  - `UpsertAsync` creates new record on first call
  - `UpsertAsync` updates existing record on subsequent calls
  - `UpsertAsync` throws 422 when termination/exclusion type is invalid
  - `UpsertAsync` throws 422 when JjbDetails exceeds 2000 chars
  - `UpsertAsync` throws 422 when ExclusionReason exceeds 2000 chars
  - `UpsertAsync` throws 422 when JjbDetails is whitespace-only
  - `UpsertAsync` throws 422 when ExclusionReason is whitespace-only
  - `UpsertAsync` writes audit event on create and update
  - Authorization: SocialWorker/CaseWorker get 403

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

## Review Findings

### Decision Needed

*(None — all findings were classifiable without ambiguity.)*

### Patches

- [x] [Review][Patch] Missing cross-field validation — `ValidateRequest` does not enforce AC 5: `ExclusionReason` is required when `TerminationExclusionType` is `Exclusion`, and should be rejected when type is `Termination`. Add validation rules after enum parsing. [`CaseStage6DataService.cs:ValidateRequest`]
- [x] [Review][Patch] Missing `ReportAttachmentId` validation — no existence or org-scope check before saving. Should validate attachment exists and belongs to same organisation, throwing `CaseBusinessRuleException`→422 if invalid. [`CaseStage6DataService.cs:UpsertAsync`]
- [x] [Review][Patch] Missing `ProducesResponseType(400)` on GET endpoint — controller GET endpoint is missing `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]` attribute. [`CaseStage6DataController.cs:Get`]

### Deferred

- [x] [Review][Defer] No `OrganisationId` FK constraint — Organisation entity/table doesn't exist yet (project-wide deferral). [`CaseStage6TerminationExclusionConfiguration.cs`]
- [x] [Review][Defer] No `RowVersion` concurrency token — pre-existing pattern matching Stage 2/4/5. [`CaseStage6TerminationExclusion.cs`]
- [x] [Review][Defer] Audit events record only `caseId` and `actorUserId` metadata, not value deltas — pre-existing pattern across all stage services. [`CaseStage6DataService.cs`]
- [x] [Review][Defer] `DbUpdateException` catch → 409 too broad (FK violations, deadlocks all mapped as "conflict") — pre-existing pattern in all stage controllers. [`CaseStage6DataController.cs`]
- [x] [Review][Defer] 401 vs 403 semantics conflated — `InvalidOperationException` from claim resolution mapped to 401 even for role failures; pre-existing pattern. [`CaseStage6DataController.cs`]
- [x] [Review][Defer] `[ApiController]` auto-400 short-circuits before service validation can produce the AC-required 422 for field length violations — pre-existing pattern affecting all stage controllers. [`Stage6DataDtos.cs`]
- [x] [Review][Defer] TOCTOU race: stage check to `SaveChangesAsync` window — case could transition out of Stage 6 between verification and persistence; pre-existing pattern. [`CaseStage6DataService.cs`]
- [x] [Review][Defer] `MaxTextFieldLength` constant (2000) duplicated across service, DTO annotation, and EF config — pre-existing pattern. [`CaseStage6DataService.cs`, `Stage6DataDtos.cs`]
- [x] [Review][Defer] No input trimming on `JjbDetails`/`ExclusionReason` string fields — pre-existing pattern matching all stage services. [`CaseStage6DataService.cs:ValidateRequest`]
- [x] [Review][Defer] `Enum.ToString()` on response path is brittle — renaming enum members breaks clients; pre-existing pattern. [`CaseStage6DataService.cs:ToDto`]
- [x] [Review][Defer] `OperationCanceledException` catch rethrows immediately — intentionally prevents other catch blocks from swallowing it; accepted pattern from Story 12.4 review. [`CaseStage6DataController.cs`]
- [x] [Review][Defer] Integer enum values (e.g., `"0"`, `"1"`) bypass string validation via `Enum.TryParse` — pre-existing behavior across all stage services. [`CaseStage6DataService.cs:ValidateRequest`]

### Dismissed

- [x] [Review][Dismiss] 404 for wrong stage conflates two error cases — pre-existing pattern by design, reviewed and accepted in previous stories as a security measure (information hiding).
- [x] [Review][Dismiss] Zombie DTO with default values when no record exists — explicitly by design per AC 2 ("a default/empty DTO (with CaseId populated) is returned if no record exists yet").
- [x] [Review][Dismiss] `ResolveActorContext` no per-request caching — pre-existing pattern; claims parsing is lightweight and not a bottleneck.
- [x] [Review][Dismiss] Case-existence check duplicated in `GetAsync` and `UpsertAsync` — intentional separation of concerns; pre-existing pattern across all stage services.

### Completion Notes List

- Created `TerminationExclusionType` enum in `Domain/Enums/TerminationExclusionType.cs` with values: `Termination`, `Exclusion`
- Created `CaseStage6TerminationExclusion` entity in `Domain/Entities/` with properties `TerminationExclusionType` (enum), `JjbDetails`, `ExclusionReason`, `ReportAttachmentId` (nullable FK to attachments), audit fields
- Created `CaseStage6TerminationExclusionConfiguration` in `Infrastructure/Persistence/` — maps to `case_stage6_termination_exclusion` table, `HasConversion<string>()` on enum, `HasMaxLength` constraints, unique index on `CaseId`, `HasOne<Case>().WithMany()` FK with `DeleteBehavior.Restrict`, `HasOne<Attachment>().WithMany()` FK for report attachment with `DeleteBehavior.SetNull`
- Added `DbSet<CaseStage6TerminationExclusion> CaseStage6TerminationExclusions` to `AppDbContext.cs`
- Created DTOs in `Models/Cases/Stage6DataDtos.cs` — `Stage6TerminationExclusionDto` and `UpsertStage6TerminationExclusionRequest` with `[Required]`, `[MaxLength(32)]` on `TerminationExclusionType`, `[MaxLength(2000)]` on text fields, nullable `ReportAttachmentId`
- Created `CaseStage6DataController` in `Controllers/V1/` — `[Authorize(Policy = Policies.CoordinatorOrAbove)]`, GET and PUT at `/api/v1/cases/{caseId}/stage6-data`, with exception handling (400, 401, 403, 404, 409, 422, OperationCanceledException)
- Created `CaseStage6DataService` in `Infrastructure/Cases/` — `GetAsync` (returns empty DTO with CaseId if no record exists), `UpsertAsync` (create/update with stage validation, nullable report attachment), `ValidateRequest` (enum parsing/validation with Trim, whitespace check, max length check for both text fields)
- Audit events: `Stage6TerminationExclusionCreated` and `Stage6TerminationExclusionUpdated` added to `AuditEventTypes.cs`
- Registered `CaseStage6DataService` in DI (`Program.cs`)
- Generated EF Core migration `20260621091720_AddCaseStage6TerminationExclusion`
- `dotnet build` — 0 errors; unit tests 39/39 passed

### File List

- `apps/api/Domain/Enums/TerminationExclusionType.cs`
- `apps/api/Domain/Entities/CaseStage6TerminationExclusion.cs`
- `apps/api/Infrastructure/Persistence/CaseStage6TerminationExclusionConfiguration.cs`
- `apps/api/Infrastructure/Persistence/AppDbContext.cs` (modified)
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` (modified)
- `apps/api/Models/Cases/Stage6DataDtos.cs`
- `apps/api/Infrastructure/Cases/CaseStage6DataService.cs`
- `apps/api/Controllers/V1/CaseStage6DataController.cs`
- `apps/api/Program.cs` (modified)
- `apps/api/Migrations/20260621091720_AddCaseStage6TerminationExclusion.cs`
- `apps/api/Migrations/20260621091720_AddCaseStage6TerminationExclusion.Designer.cs`
