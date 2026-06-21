---
baseline_commit: edd947b56377d2c5c7fd02213f0c9e10a2f7200e
---

# Story 12.1: Stage 2 Maintain & Development Sub-Step Data

Status: done

## Story

As a **Project Coordinator**,
I want to record bio-psycho-social assessment, ICP, life skill training, PMA, and related data for Stage 2,
So that case development is tracked per the workflow.

## Acceptance Criteria

1. **Given** a new `case_stage2_data` table
   **When** the migration runs
   **Then** the table has columns for: `case_id` (FK→cases), `organisation_id`, bio-psycho-social assessment, ICP records, life skill training, parent management, group work, community program attendance, PMA status, overall progress notes, `created_by_user_id`, `created_at_utc`, `updated_at_utc`
   **And** all text/notes columns are nullable `text` with `HasMaxLength(4000)`
   **And** there is a unique index on `case_id` (one row per case)

2. **Given** a Case in Stage 2 (`MaintainAndDevelopment`)
   **When** a Coordinator or above calls `GET /api/v1/cases/{caseId}/stage2-data`
   **Then** the Stage 2 sub-step data for that case is returned (or `null` fields if not yet created)
   **And** 404 is returned if the case is not in Stage 2
   **And** 403 is returned for SocialWorker/CaseWorker roles

3. **Given** a Case in Stage 2 (`MaintainAndDevelopment`)
   **When** a Coordinator or above calls `PUT /api/v1/cases/{caseId}/stage2-data`
   **Then** the Stage 2 sub-step data is created or fully replaced (upsert pattern)
   **And** the response is 200 OK with the updated DTO
   **And** 404 is returned if the case is not in Stage 2
   **And** 422 is returned if any text field exceeds 4000 characters
   **And** an `audit_events` row is written recording the update

4. **Given** an existing Case NOT in Stage 2
   **When** a GET or PUT request is made to `/api/v1/cases/{caseId}/stage2-data`
   **Then** 404 is returned (sub-step data only applicable in Stage 2)

5. **Given** a Case transitions out of Stage 2 to Stage 3
   **When** the transition occurs
   **Then** the existing Stage 2 sub-step data row remains in the database (no cascade delete)
   **And** GET requests for Stage 2 data on the case now return 404

## Tasks / Subtasks

- [x] Create `CaseStage2Data` entity in `apps/api/Domain/Entities/` (AC: 1)
- [x] Create `CaseStage2DataConfiguration` in `apps/api/Infrastructure/Persistence/` (AC: 1)
- [x] Add `DbSet<CaseStage2Data> CaseStage2Data` to `AppDbContext.cs` (AC: 1)
- [x] Create EF Core migration `AddCaseStage2Data` (AC: 1)
- [x] Create `Stage2DataDto` and `UpsertStage2DataRequest` in a new DTOs file `apps/api/Models/Cases/Stage2DataDtos.cs` (AC: 2, 3)
- [x] Create `CaseStage2DataController` with GET and PUT endpoints at `/api/v1/cases/{caseId}/stage2-data` (AC: 2, 3, 4)
- [x] Create `CaseStage2DataService` in `apps/api/Infrastructure/Cases/` with GetAsync and UpsertAsync methods (AC: 2, 3)
- [x] Validate stage is `MaintainAndDevelopment` before allowing data access — return 404 if not (AC: 2, 4, 5)
- [x] Add audit event writing on upsert (AC: 3)
- [x] Add text length validation (max 4000 chars per field) — return 422 on violation (AC: 3)
- [x] Register `CaseStage2DataService` and controller in DI (Program.cs)
- [x] Run `dotnet build` and verify all projects compile

## Dev Notes

### CRITICAL: This is a new entity + new API — NOT modifying Case entity

Unlike Stories 11.1–11.3 which added fields to `Case.cs`, this story creates a **new entity** and **new controller**. Follow the established patterns:

### Pattern Reference — Existing Entity (CaseStageTransition)

```
apps/api/Domain/Entities/CaseStageTransition.cs  →  new: CaseStage2Data.cs
apps/api/Infrastructure/Persistence/CaseStageTransitionConfiguration.cs  →  new: CaseStage2DataConfiguration.cs
```

### Detailed Code Analysis

#### 1. New Entity — `CaseStage2Data.cs`

Create `apps/api/Domain/Entities/CaseStage2Data.cs`:

```csharp
using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStage2Data
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public string? BioPsychoSocialAssessment { get; set; }
    public string? IcpRecords { get; set; }
    public string? LifeSkillTraining { get; set; }
    public string? ParentManagement { get; set; }
    public string? GroupWork { get; set; }
    public string? CommunityProgramAttendance { get; set; }
    public string? PmaStatus { get; set; }
    public string? OverallProgress { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

**Important:** All data fields are nullable `string?`. The entity always exists after first creation (upsert pattern). `CaseId` has a unique index — one row per case.

#### 2. New Configuration — `CaseStage2DataConfiguration.cs`

Create `apps/api/Infrastructure/Persistence/CaseStage2DataConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage2DataConfiguration : IEntityTypeConfiguration<CaseStage2Data>
{
    public void Configure(EntityTypeBuilder<CaseStage2Data> builder)
    {
        builder.ToTable("case_stage2_data");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.BioPsychoSocialAssessment)
            .HasMaxLength(4000);

        builder.Property(d => d.IcpRecords)
            .HasMaxLength(4000);

        builder.Property(d => d.LifeSkillTraining)
            .HasMaxLength(4000);

        builder.Property(d => d.ParentManagement)
            .HasMaxLength(4000);

        builder.Property(d => d.GroupWork)
            .HasMaxLength(4000);

        builder.Property(d => d.CommunityProgramAttendance)
            .HasMaxLength(4000);

        builder.Property(d => d.PmaStatus)
            .HasMaxLength(4000);

        builder.Property(d => d.OverallProgress)
            .HasMaxLength(4000);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(d => d.CaseId)
            .IsUnique();
    }
}
```

#### 3. AppDbContext — Add DbSet

Add in `apps/api/Infrastructure/Persistence/AppDbContext.cs` after the existing DbSets:
```csharp
public DbSet<CaseStage2Data> CaseStage2Data => Set<CaseStage2Data>();
```

Also register the configuration in `OnModelCreating`:
```csharp
modelBuilder.ApplyConfiguration(new CaseStage2DataConfiguration());
```

#### 4. DTOs — `Stage2DataDtos.cs`

Create `apps/api/Models/Cases/Stage2DataDtos.cs`:

```csharp
namespace MidiKaval.Api.Models.Cases;

public sealed class Stage2DataDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string? BioPsychoSocialAssessment { get; set; }
    public string? IcpRecords { get; set; }
    public string? LifeSkillTraining { get; set; }
    public string? ParentManagement { get; set; }
    public string? GroupWork { get; set; }
    public string? CommunityProgramAttendance { get; set; }
    public string? PmaStatus { get; set; }
    public string? OverallProgress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertStage2DataRequest
{
    public string? BioPsychoSocialAssessment { get; set; }
    public string? IcpRecords { get; set; }
    public string? LifeSkillTraining { get; set; }
    public string? ParentManagement { get; set; }
    public string? GroupWork { get; set; }
    public string? CommunityProgramAttendance { get; set; }
    public string? PmaStatus { get; set; }
    public string? OverallProgress { get; set; }
}
```

#### 5. Service — `CaseStage2DataService.cs`

Create `apps/api/Infrastructure/Cases/CaseStage2DataService.cs`. Key behaviors:

- **GetAsync**: Load case → verify it's in `MaintainAndDevelopment` stage (404 if not) → load or create empty `CaseStage2Data` → map to DTO
- **UpsertAsync**: Load case → verify stage (404 if not) → validate text lengths (422 if > 4000) → upsert entity → write audit event → save → return DTO
- Use explicit `CaseBusinessRuleException` for 422 on text length violations
- Use explicit `AuditEvent` for every upsert with `EventType = "case.stage2_data.updated"`
- Authorize: `CoordinatorOrAbove` policy
- **New audit event type required**: Add `Stage2DataUpdated = "case.stage2_data.updated"` to `AuditEventTypes.cs`

Example service skeleton:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CaseStage2DataService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<Stage2DataDto> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();
        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.MaintainAndDevelopment)
            throw new CaseNotFoundException();

        var data = await db.Set<CaseStage2Data>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        return ToDto(data, caseId);
    }

    // ... UpsertAsync, validation, etc.

    private static Stage2DataDto ToDto(CaseStage2Data? data, Guid caseId)
    {
        if (data is null)
        {
            return new Stage2DataDto { CaseId = caseId };
        }

        return new Stage2DataDto
        {
            Id = data.Id,
            CaseId = data.CaseId,
            BioPsychoSocialAssessment = data.BioPsychoSocialAssessment,
            IcpRecords = data.IcpRecords,
            LifeSkillTraining = data.LifeSkillTraining,
            ParentManagement = data.ParentManagement,
            GroupWork = data.GroupWork,
            CommunityProgramAttendance = data.CommunityProgramAttendance,
            PmaStatus = data.PmaStatus,
            OverallProgress = data.OverallProgress,
            CreatedAtUtc = data.CreatedAtUtc,
            UpdatedAtUtc = data.UpdatedAtUtc,
        };
    }

    private (Guid OrganisationId, Guid ActorUserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId)
            || !Guid.TryParse(userIdClaim, out var actorUserId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return (organisationId, actorUserId);
    }
}
```

#### 6. Controller — `CaseStage2DataController.cs`

Create `apps/api/Controllers/V1/CaseStage2DataController.cs`:

```
GET    /api/v1/cases/{caseId}/stage2-data  → Get(caseId)
PUT    /api/v1/cases/{caseId}/stage2-data  → Upsert(caseId, request)
```

#### 7. DI Registration

In `Program.cs`, register the service:
```csharp
builder.Services.AddScoped<CaseStage2DataService>();
```

#### 8. Migration

```bash
dotnet ef migrations add AddCaseStage2Data --context AppDbContext
```

#### 9. Validation Rules Summary

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| All text fields | string? | No | Max 4000 characters |
| Case stage | enum | Yes | Must be `MaintainAndDevelopment` (checked at service level) |

#### 10. Authorization

- `GET`, `PUT`: `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
- SocialWorker and CaseWorker roles are forbidden (403)

### Project Structure Notes

- **New files to create:**
  - `apps/api/Domain/Entities/CaseStage2Data.cs`
  - `apps/api/Infrastructure/Persistence/CaseStage2DataConfiguration.cs`
  - `apps/api/Models/Cases/Stage2DataDtos.cs`
  - `apps/api/Infrastructure/Cases/CaseStage2DataService.cs`
  - `apps/api/Controllers/V1/CaseStage2DataController.cs`
  - `apps/api/Migrations/<timestamp>_AddCaseStage2Data.cs` + `.Designer.cs`

- **Modified files:**
  - `apps/api/Infrastructure/Persistence/AppDbContext.cs` — add DbSet + configuration
  - `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — new event type constant
  - `apps/api/Program.cs` — register service in DI
  - `apps/api/Migrations/AppDbContextModelSnapshot.cs` — auto-updated

### Testing Standards Summary

- Unit tests for `CaseStage2DataService`:
  - `GetAsync` returns DTO when data exists
  - `GetAsync` returns DTO with null fields when data doesn't exist yet
  - `GetAsync` throws `CaseNotFoundException` when case not in Stage 2
  - `GetAsync` throws `CaseNotFoundException` when case doesn't exist
  - `UpsertAsync` creates new row on first call (upsert)
  - `UpsertAsync` updates existing row on subsequent calls
  - `UpsertAsync` throws 422 when text exceeds 4000 chars
  - `UpsertAsync` writes audit event on upsert
  - Authorization: SocialWorker/CaseWorker get 403

- Integration tests (if Docker available):
  - Full flow: Create case → transition to Stage 2 → upsert data → GET data → verify fields round-trip
  - Stage 2 data 404 for case in Stage 1
  - Stage 2 data 404 after case transitions to Stage 3

### Previous Story Intelligence (Epic 11)

**Key learnings from Stories 11.1–11.3:**

1. **CRITICAL — Controller catch blocks**: The Create endpoint in CasesController was missing a `CaseBusinessRuleException` catch (discovered in code review of 11.3). For this story, ensure the controller catches `CaseBusinessRuleException` → `UnprocessableProblem()` (422). Reference: Merge endpoint pattern.

2. **CRITICAL — ProducesResponseType attributes**: Include 422 `ProducesResponseType` on the PUT endpoint from the start. The Create endpoint was patched retroactively.

3. **Audit event pattern**: Every mutation writes `AuditEvent` with `EventType`, `MetadataJson` (dictionary with relevant details), and `CreatedAtUtc`. Follow the existing pattern from `CaseService.MergeAsync`.

4. **DI registration pattern**: Services are registered in `Program.cs` via `builder.Services.AddScoped<T>()`.

5. **CaseNotFoundException**: Already exists as an empty `Exception` subclass in `CaseService.cs` — reusable.

### Review Findings

- [x] [Review][Patch] Missing FK constraint on `case_id` → `cases.id` — Added `HasOne<Case>().WithMany().HasForeignKey(d => d.CaseId).OnDelete(DeleteBehavior.Restrict)` in configuration; regenerated migration.
- [x] [Review][Patch] Missing FK constraint on `created_by_user_id` → `users.id` — Added `HasOne<User>().WithMany().HasForeignKey(d => d.CreatedByUserId).OnDelete(DeleteBehavior.Restrict)` in configuration; regenerated migration.
- [x] [Review][Patch] No `DbUpdateException` handler in controller PUT — Added `catch (DbUpdateException)` returning 409 Conflict in `CaseStage2DataController.Upsert`.
- [x] [Review][Patch] `InvalidOperationException` not caught in GET endpoint — Added `catch (InvalidOperationException)` → 401 Unauthorized in `CaseStage2DataController.Get`.
- [x] [Review][Patch] Audit event type conflates create vs update — Added `Stage2DataCreated = "case.stage2_data.created"`; service uses `Stage2DataCreated` on insert and `Stage2DataUpdated` on update.
- [x] [Review][Patch] No `[RequestSizeLimit]` on PUT endpoint — Added `[RequestSizeLimit(32_768)]` to the PUT endpoint.
- [x] [Review][Patch] `CreatedByUserId` omitted from `Stage2DataDto` response — Added `CreatedByUserId` to `Stage2DataDto` and mapped in `ToDto`.

- [x] [Review][Defer] Concurrency unsafety — race condition on upsert — Read-then-write pattern with no row version. Pre-existing pattern across the codebase.
- [x] [Review][Defer] `ValidateFieldLengths` repetitive maintenance liability — 8 fields checked with copy-paste code. Pre-existing pattern.
- [x] [Review][Defer] No input sanitization on text fields — All text fields accept arbitrary string content. Cross-cutting concern affecting entire API.

#### Cross-Story Review (2026-06-21)

_Note: The following findings originate from Epic 11 / prior-story changes that share the same uncommitted working tree. They are logged here for visibility; their primary story is 11.2 / 11.3._

- ~~[ ] [Review][Patch] Dead `.Include()` calls in `SearchCasesAsync` (`apps/api/Infrastructure/Cases/CaseService.cs`) — **Dismissed** — The `.Include()` calls are in `ListMineAsync` before `.ToListAsync()`, and the in-memory `CaseDtoMapper.ToCaseSummary` accesses `entity.Occupation?.Name` and `entity.EducationLevel?.Name`, which requires the navigation properties to be loaded.~~
- [x] [Review][Patch] `HasDefaultValue(false)` duplicates C# field initializer (`apps/api/Infrastructure/Persistence/CaseConfiguration.cs`) — Removed `.HasDefaultValue(false)`. The C# `= false` initializer on `Case.FamilyHistoryOfCrime` is sufficient.

- [x] [Review][Defer] No database CHECK constraint on recidivism counts — Only application-layer validation; no `CHECK (recidivism_before_count >= 0)` in migration or config. Pre-existing pattern across the codebase.
- [x] [Review][Defer] Search filters accept inactive legend IDs but intake rejects them — `ApplySearchFilters` doesn't filter by `IsActive`, while `ValidateIntakeRequestAsync` checks `o.IsActive` and `el.IsActive`. Behavioral inconsistency.
- [x] [Review][Defer] Duplicate DTO-mapping surface area across 5+ locations — Same new fields mapped in `CaseDtoMapper`, `ToDto`, `BuildDetailDtoAsync`, `SearchCasesAsync` projection, and `ExportAsync` projection. Pre-existing pattern.

### References

- [Source: `apps/api/Domain/Entities/CaseStageTransition.cs`] — Pattern for new entity
- [Source: `apps/api/Infrastructure/Persistence/CaseStageTransitionConfiguration.cs`] — Pattern for configuration
- [Source: `apps/api/Infrastructure/Persistence/AppDbContext.cs`] — Add DbSet
- [Source: `apps/api/Controllers/V1/CasesController.cs`] — Controller pattern (specifically Merge endpoint for audit + 422 pattern)
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs`] — Service pattern with auth, audit, exception handling
- [Source: `apps/api/Domain/Enums/CaseStage.cs`] — Stage enum with `MaintainAndDevelopment`
- [Source: `apps/api/Infrastructure/Cases/CaseStageTransitionRules.cs`] — Stage flow rules
- [Source: `apps/api/Infrastructure/Audit/AuditEventTypes.cs`] — Add new event type constant
- [Source: `apps/api/Domain/Entities/AuditEvent.cs`] — Audit event entity properties
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 12`] — Story definition
- [Source: `_bmad-output/implementation-artifacts/2-2-six-stage-lifecycle-transitions.md`] — Previous stage implementation (case_stages table, deferred sub-step matrix)

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

- ✅ Created `CaseStage2Data` entity with 8 text fields (bio-psycho-social, ICP, life skills, parent management, group work, community program, PMA, overall progress)
- ✅ Created `CaseStage2DataConfiguration` with unique index on `CaseId` and `HasMaxLength(4000)` on all text fields
- ✅ Added `DbSet<CaseStage2Data>` to `AppDbContext`
- ✅ Added `Stage2DataUpdated = "case.stage2_data.updated"` to `AuditEventTypes.cs`
- ✅ Created `Stage2DataDto` and `UpsertStage2DataRequest` DTOs
- ✅ Created `CaseStage2DataService` with `GetAsync` and `UpsertAsync` — stage gating (404), text length validation (422), audit event writing
- ✅ Created `CaseStage2DataController` with GET and PUT at `/api/v1/cases/{caseId}/stage2-data` — catches `CaseNotFoundException` (404), `CaseBusinessRuleException` (422)
- ✅ Registered `CaseStage2DataService` in `Program.cs` DI
- ✅ Created EF Core migration `20260621020247_AddCaseStage2Data` (regenerated as `20260621041034_AddCaseStage2Data` with FK constraints)
- ✅ Build succeeded with 0 errors; 39/39 unit tests pass
- ✅ Applied all 7 code review patches: FK constraints, DbUpdateException handler, InvalidOperationException handler, split audit event types, RequestSizeLimit, CreatedByUserId in DTO

### File List

**New files:**
- `apps/api/Domain/Entities/CaseStage2Data.cs`
- `apps/api/Infrastructure/Persistence/CaseStage2DataConfiguration.cs`
- `apps/api/Models/Cases/Stage2DataDtos.cs`
- `apps/api/Infrastructure/Cases/CaseStage2DataService.cs`
- `apps/api/Controllers/V1/CaseStage2DataController.cs`
- `apps/api/Migrations/20260621041034_AddCaseStage2Data.cs`
- `apps/api/Migrations/20260621041034_AddCaseStage2Data.Designer.cs`

**Modified files:**
- `apps/api/Infrastructure/Persistence/AppDbContext.cs`
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs`
- `apps/api/Program.cs`
- `apps/api/Migrations/AppDbContextModelSnapshot.cs`
