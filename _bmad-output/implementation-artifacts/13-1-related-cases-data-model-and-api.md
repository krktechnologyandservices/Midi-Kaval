---
baseline_commit: edd947b56377d2c5c7fd02213f0c9e10a2f7200e
---
# Story 13.1: Related Cases Data Model and API

Status: done

## Story

As a **Project Coordinator**,
I want to link related cases (cross-links),
So that I can see connections between siblings, co-accused, or linked children.

## Acceptance Criteria

1. **Given** a new `case_related_cases` join table
   **When** the migration runs
   **Then** the table has columns for: `id`, `case_id_a` (FK→cases), `case_id_b` (FK→cases), `relationship_type` (enum string), `created_by_user_id`, `created_at_utc`
   **And** there is a unique index on `(case_id_a, case_id_b)` and a constraint ensuring `case_id_a < case_id_b` (each pair stored once)
   **And** `case_id_a` and `case_id_b` have `DeleteBehavior.Restrict` (preserve links even if one case transitions)

2. **Given** a coordinator links two cases
   **When** `POST /api/v1/cases/{caseId}/related-cases` is called with `{ "relatedCaseId": "...", "relationshipType": "Sibling" }`
   **Then** a `case_related_cases` row is created with the lower GUID as `case_id_a` and higher as `case_id_b`
   **And** the link is bidirectional — querying from either case returns the relationship
   **And** 422 is returned if the relationship already exists
   **And** 422 is returned if `relatedCaseId` equals `caseId` (self-link)
   **And** 404 is returned if `relatedCaseId` does not exist or is in a different organisation
   **And** an `audit_events` row is written

3. **Given** an existing link between two cases
   **When** `DELETE /api/v1/cases/{caseId}/related-cases/{relatedCaseId}` is called
   **Then** the relationship row is removed
   **And** 404 is returned if the link does not exist
   **And** an `audit_events` row is written

4. **Given** a case with related cases
   **When** `GET /api/v1/cases/{caseId}/related-cases` is called
   **Then** a list of related cases is returned, each with `caseId`, `crimeNumber`, `stNumber`, `beneficiaryName`, `currentStage`, and `relationshipType`
   **And** 403 is returned for SocialWorker/CaseWorker roles

5. **Given** a case is viewed in detail
   **When** `GET /api/v1/cases/{caseId}` returns `CaseDetailDto`
   **Then** the DTO includes a `relatedCases` array with the same shape as in AC 4
   **And** 403 is enforced for SocialWorker/CaseWorker without read permission

## Tasks / Subtasks

- [ ] Create `RelationshipType` enum in `apps/api/Domain/Enums/` with values: `Sibling`, `CoAccused`, `LinkedChild` (AC: 1)
- [ ] Create `CaseRelatedCase` entity in `apps/api/Domain/Entities/` (AC: 1)
- [ ] Create `CaseRelatedCaseConfiguration` in `apps/api/Infrastructure/Persistence/` (AC: 1)
- [ ] Add `DbSet<CaseRelatedCase> CaseRelatedCases` to `AppDbContext.cs` (AC: 1)
- [ ] Create EF Core migration `AddCaseRelatedCases` (AC: 1)
- [ ] Create `RelatedCaseDto` and `LinkRelatedCaseRequest` in `apps/api/Models/Cases/CaseRelatedDtos.cs` (AC: 2, 4, 5)
- [ ] Create `CaseRelatedCasesController` with GET, POST, DELETE at `/api/v1/cases/{caseId}/related-cases` (AC: 2, 3, 4)
- [ ] Create `CaseRelatedCasesService` in `apps/api/Infrastructure/Cases/` with `GetAsync`, `LinkAsync`, `UnlinkAsync` methods (AC: 2, 3, 4)
- [ ] Add `RelatedCases` property to `CaseDetailDto` and populate in `CaseService` / `CaseDtoMapper` (AC: 5)
- [ ] Add audit event writing on link create and link delete (AC: 2, 3)
- [ ] Add `RelationshipType` validation and duplicate/self-link prevention (AC: 2)
- [ ] Register `CaseRelatedCasesService` in DI (`Program.cs`)
- [ ] Run `dotnet build` and verify all projects compile

## Dev Notes

### CRITICAL: This is NOT a stage data entity — it's a many-to-many join table

Unlike Stories 12.1–12.5, this story creates a **self-referencing many-to-many join table** between Case and itself. The relationship is bidirectional: linking Case A to Case B means both cases are mutually related.

### Bidirectional Pattern

Use the **ordered-pair convention**:
- Always store with `case_id_a < case_id_b` (compare GUIDs lexicographically)
- Query with `WHERE (case_id_a = @id OR case_id_b = @id)`
- This enforces exactly one row per unique pair and simplifies bidirectional queries

### 1. New Enum — `RelationshipType.cs`

```csharp
namespace MidiKaval.Api.Domain.Enums;

public enum RelationshipType
{
    Sibling,
    CoAccused,
    LinkedChild,
}
```

### 2. New Entity — `CaseRelatedCase.cs`

```csharp
using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseRelatedCase
{
    public Guid Id { get; set; }
    public Guid CaseIdA { get; set; }
    public Guid CaseIdB { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

Note: No `UpdatedAtUtc` — links are created and deleted, never updated. If a coordinator wants to change the relationship type, they must unlink and re-link.

### 3. New Configuration — `CaseRelatedCaseConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseRelatedCaseConfiguration : IEntityTypeConfiguration<CaseRelatedCase>
{
    public void Configure(EntityTypeBuilder<CaseRelatedCase> builder)
    {
        builder.ToTable("case_related_cases");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        // FK to cases for CaseIdA
        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseIdA)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to cases for CaseIdB
        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseIdB)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index on ordered pair
        builder.HasIndex(d => new { d.CaseIdA, d.CaseIdB })
            .IsUnique();
    }
}
```

### 4. AppDbContext — Add DbSet

```csharp
public DbSet<CaseRelatedCase> CaseRelatedCases => Set<CaseRelatedCase>();
```

### 5. DTOs — `CaseRelatedDtos.cs`

Place in `apps/api/Models/Cases/CaseRelatedDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class RelatedCaseDto
{
    public Guid CaseId { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
}

public sealed class LinkRelatedCaseRequest
{
    [Required(AllowEmptyStrings = false)]
    public Guid RelatedCaseId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string RelationshipType { get; set; } = string.Empty;
}
```

### 6. Update CaseDetailDto

Add to `CaseDetailDto` in `CaseDtos.cs`:
```csharp
public IReadOnlyList<RelatedCaseDto> RelatedCases { get; set; } = Array.Empty<RelatedCaseDto>();
```

### 7. Audit Event Types

Add to `AuditEventTypes.cs`:
```csharp
public const string CaseLinked = "case.related.created";
public const string CaseUnlinked = "case.related.deleted";
```

### 8. Service — `CaseRelatedCasesService.cs`

Create in `apps/api/Infrastructure/Cases/`:

```csharp
public sealed class CaseRelatedCasesService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<List<RelatedCaseDto>> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        // Verify case exists and belongs to organisation
        var caseExists = await db.Cases.AnyAsync(c => c.Id == caseId && c.OrganisationId == organisationId, ct);
        if (!caseExists)
            throw new CaseNotFoundException();

        var links = await db.Set<CaseRelatedCase>()
            .Where(r => r.CaseIdA == caseId || r.CaseIdB == caseId)
            .Join(
                db.Cases,
                r => r.CaseIdA == caseId ? r.CaseIdB : r.CaseIdA,
                c => c.Id,
                (r, c) => new { r.RelationshipType, c.Id, c.CrimeNumber, c.StNumber, c.BeneficiaryName, c.CurrentStage })
            .ToListAsync(ct);

        var dtos = links.Select(l => new RelatedCaseDto
        {
            CaseId = l.Id,
            CrimeNumber = l.CrimeNumber,
            StNumber = l.StNumber,
            BeneficiaryName = l.BeneficiaryName,
            CurrentStage = l.CurrentStage.ToString(),
            RelationshipType = l.RelationshipType.ToString(),
        }).ToList();

        return dtos;
    }

    public async Task<RelatedCaseDto> LinkAsync(
        Guid caseId, LinkRelatedCaseRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        // Self-link guard
        if (request.RelatedCaseId == caseId)
            throw new CaseBusinessRuleException("A case cannot be linked to itself.");

        // Validate relationship type
        var relationshipType = ValidateRelationshipType(request.RelationshipType);

        // Verify both cases exist and belong to same organisation
        var caseIds = new[] { caseId, request.RelatedCaseId };
        var existingCases = await db.Cases
            .Where(c => caseIds.Contains(c.Id) && c.OrganisationId == organisationId)
            .Select(c => new { c.Id, c.CrimeNumber, c.StNumber, c.BeneficiaryName, c.CurrentStage })
            .ToListAsync(ct);

        if (existingCases.Count != 2)
            throw new CaseNotFoundException();

        var targetCase = existingCases.First(c => c.Id == request.RelatedCaseId);

        // Normalize to ordered pair (lower GUID = CaseIdA)
        var (caseIdA, caseIdB) = OrderGuids(caseId, request.RelatedCaseId);

        // Check for existing link
        var existingLink = await db.Set<CaseRelatedCase>()
            .AnyAsync(r => r.CaseIdA == caseIdA && r.CaseIdB == caseIdB, ct);

        if (existingLink)
            throw new CaseBusinessRuleException("Cases are already linked.");

        var now = DateTime.UtcNow;
        var link = new CaseRelatedCase
        {
            Id = Guid.NewGuid(),
            CaseIdA = caseIdA,
            CaseIdB = caseIdB,
            RelationshipType = relationshipType,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
        };

        db.Add(link);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseLinked,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["relatedCaseId"] = request.RelatedCaseId.ToString("D"),
                    ["relationshipType"] = relationshipType.ToString(),
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(ct);

        return new RelatedCaseDto
        {
            CaseId = targetCase.Id,
            CrimeNumber = targetCase.CrimeNumber,
            StNumber = targetCase.StNumber,
            BeneficiaryName = targetCase.BeneficiaryName,
            CurrentStage = targetCase.CurrentStage.ToString(),
            RelationshipType = relationshipType.ToString(),
        };
    }

    public async Task UnlinkAsync(Guid caseId, Guid relatedCaseId, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var (caseIdA, caseIdB) = OrderGuids(caseId, relatedCaseId);

        var link = await db.Set<CaseRelatedCase>()
            .FirstOrDefaultAsync(r => r.CaseIdA == caseIdA && r.CaseIdB == caseIdB, ct);

        if (link is null)
            throw new CaseNotFoundException();

        db.Remove(link);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseUnlinked,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["relatedCaseId"] = relatedCaseId.ToString("D"),
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }

    private static RelationshipType ValidateRelationshipType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CaseBusinessRuleException("RelationshipType is required.");

        if (!Enum.TryParse<RelationshipType>(value.Trim(), ignoreCase: true, out var parsed))
            throw new CaseBusinessRuleException($"'{value}' is not a valid RelationshipType. Must be one of: Sibling, CoAccused, LinkedChild.");

        if (!Enum.IsDefined(parsed))
            throw new CaseBusinessRuleException($"'{value}' is not a valid named RelationshipType.");

        return parsed;
    }

    private static (Guid caseIdA, Guid caseIdB) OrderGuids(Guid a, Guid b)
    {
        // Lexicographic comparison: use CompareTo on string representation
        if (string.Compare(a.ToString("D"), b.ToString("D"), StringComparison.Ordinal) < 0)
            return (a, b);
        return (b, a);
    }

    private (Guid OrganisationId, Guid ActorUserId) ResolveActorContext()
    {
        // Same pattern as all other services
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

### 9. Controller — `CaseRelatedCasesController.cs`

```csharp
[ApiController]
[Route("api/v1/cases/{caseId:guid}/related-cases")]
public sealed class CaseRelatedCasesController(
    CaseRelatedCasesService relatedCasesService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(List<RelatedCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // returns list of related cases

    [HttpPost]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(RelatedCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    // creates a link — Catches CaseNotFoundException→404, CaseBusinessRuleException→422, DbUpdateException→409, InvalidOperationException→401, OperationCanceledException→throw

    [HttpDelete("{relatedCaseId:guid}")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    // removes a link — Catches CaseNotFoundException→404, DbUpdateException→409, InvalidOperationException→401, OperationCanceledException→throw
}
```

### 10. Update CaseDetailDto and CaseService

In `CaseDetailDto` (`CaseDtos.cs`):
```csharp
public IReadOnlyList<RelatedCaseDto> RelatedCases { get; set; } = Array.Empty<RelatedCaseDto>();
```

In `CaseService.GetCaseDetailAsync` (or wherever `CaseDetailDto` is built):
- After building the main DTO, query `case_related_cases` for related cases
- Map them using the same projection as `CaseRelatedCasesService.GetAsync`
- Assign to `RelatedCases` property

### 11. DI Registration

```csharp
builder.Services.AddScoped<CaseRelatedCasesService>();
```

### 12. Migration

```bash
dotnet ef migrations add AddCaseRelatedCases --output-dir Migrations
```

### 13. Validation Rules Summary

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| RelatedCaseId | Guid | Yes | Must not equal caseId (self-link) |
| RelationshipType | string (enum) | Yes | Must be one of: Sibling, CoAccused, LinkedChild |
| Existing link | — | — | Must not already exist for the pair |
| Case existence | — | — | Both cases must exist in same organisation |

### 14. Authorization

- `GET`, `POST`, `DELETE`: `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
- SocialWorker and CaseWorker roles are forbidden (403)

### Project Structure Notes

- **New files to create:**
  - `apps/api/Domain/Enums/RelationshipType.cs`
  - `apps/api/Domain/Entities/CaseRelatedCase.cs`
  - `apps/api/Infrastructure/Persistence/CaseRelatedCaseConfiguration.cs`
  - `apps/api/Models/Cases/CaseRelatedDtos.cs`
  - `apps/api/Infrastructure/Cases/CaseRelatedCasesService.cs`
  - `apps/api/Controllers/V1/CaseRelatedCasesController.cs`
  - `apps/api/Migrations/<timestamp>_AddCaseRelatedCases.cs` + `.Designer.cs`

- **Modified files:**
  - `apps/api/Infrastructure/Persistence/AppDbContext.cs` — add DbSet
  - `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — new event type constants
  - `apps/api/Models/Cases/CaseDtos.cs` — add `RelatedCases` to `CaseDetailDto`
  - `apps/api/Infrastructure/Cases/CaseService.cs` — populate related cases in detail
  - `apps/api/Program.cs` — register service in DI
  - `apps/api/Migrations/AppDbContextModelSnapshot.cs` — auto-updated

### Previous Story Intelligence (Epic 12 stories + earlier case patterns)

**Key learnings baked into this story:**
1. Include all catch blocks from the start: `CaseNotFoundException`→404, `CaseBusinessRuleException`→422, `DbUpdateException`→409, `InvalidOperationException`→401
2. Include `catch (OperationCanceledException) { throw; }` in all endpoints
3. Include all `ProducesResponseType` attributes on all endpoints
4. Use `.Trim()` before `Enum.TryParse` in validation
5. Add `Enum.IsDefined` check to reject numeric enum strings
6. Audit every mutation (create + delete)
7. `HasConversion<string>()` for enum storage
8. `HasOne<Case>().WithMany()` FK pattern with `DeleteBehavior.Restrict`
9. FKs to `User` for `CreatedByUserId`
10. ResolveActorContext pattern for org ID + user ID from claims
11. No concurrency token needed — links are append-only; conflicts caught by unique index → `DbUpdateException`
12. No UpdateAtUtc needed — links are immutable once created

### Testing Standards Summary

- Unit tests for `CaseRelatedCasesService`:
  - `GetAsync` returns list of related cases
  - `GetAsync` returns empty list when no links exist
  - `GetAsync` throws `CaseNotFoundException` when case doesn't exist
  - `LinkAsync` creates link with ordered pair
  - `LinkAsync` throws 422 for self-link
  - `LinkAsync` throws 422 for duplicate link
  - `LinkAsync` throws 404 when related case doesn't exist
  - `UnlinkAsync` removes existing link
  - `UnlinkAsync` throws 404 when link doesn't exist
  - `LinkAsync` writes audit event
  - `UnlinkAsync` writes audit event
  - Authorization: SocialWorker/CaseWorker get 403

## Completion Notes List

### Baseline
- Story created with baseline commit `edd947b56377d2c5c7fd02213f0c9e10a2f7200e` in `ready-for-dev` status.

### Implementation
- Created `RelationshipType` enum (`Sibling`, `CoAccused`, `LinkedChild`) in `apps/api/Domain/Enums/RelationshipType.cs`
- Created `CaseRelatedCase` entity in `apps/api/Domain/Entities/CaseRelatedCase.cs` with `Id`, `CaseIdA`, `CaseIdB`, `RelationshipType`, `CreatedByUserId`, `CreatedAtUtc`
- Created `CaseRelatedCaseConfiguration` in `apps/api/Infrastructure/Persistence/` with:
  - Table name: `case_related_cases`
  - Unique index on `(CaseIdA, CaseIdB)`
  - `HasConversion<string>()` for `RelationshipType`
  - `DeleteBehavior.Restrict` for both FK to `Case` and FK to `User`
- Added `DbSet<CaseRelatedCase> CaseRelatedCases` to `AppDbContext.cs`
- Created EF Core migration `20260621095803_AddCaseRelatedCases.cs`
- Created `RelatedCaseDto` and `LinkRelatedCaseRequest` DTOs with data annotations in `apps/api/Models/Cases/CaseRelatedDtos.cs`
- Created `CaseRelatedCasesService` in `apps/api/Infrastructure/Cases/` with:
  - `GetAsync` — bidirectional query using `case_id_a == caseId OR case_id_b == caseId` pattern
  - `LinkAsync` — ordered-pair GUID comparison, self-link prevention, duplicate detection, audit event writing
  - `UnlinkAsync` — ordered-pair lookup, audit event writing
  - `ValidateRelationshipType` — trim + TryParse + IsDefined checks
  - `ResolveActorContext` — extracts org ID and user ID from claims
- Created `CaseRelatedCasesController` in `apps/api/Controllers/V1/` with:
  - `GET /api/v1/cases/{caseId}/related-cases` — returns list of `RelatedCaseDto`
  - `POST /api/v1/cases/{caseId}/related-cases` — links cases, returns `RelatedCaseDto`
  - `DELETE /api/v1/cases/{caseId}/related-cases/{relatedCaseId}` — unlinks cases, returns 204
  - All endpoints: `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - Exception handling: `CaseNotFoundException`→404, `CaseBusinessRuleException`→422, `InvalidOperationException`→401, `DbUpdateException`→409, `OperationCanceledException`→throw
- Added `RelatedCases` property to `CaseDetailDto` in `CaseDtos.cs`
- Added `BuildRelatedCaseDtosAsync` helper in `CaseService.cs` to populate related cases in detail view
- Added audit event constants `CaseLinked` / `CaseUnlinked` in `AuditEventTypes.cs`
- Registered `CaseRelatedCasesService` in DI via `Program.cs`
- `dotnet build` succeeded (0 errors, 4 pre-existing warnings)

### Tasks Completed
- [x] Create `RelationshipType` enum
- [x] Create `CaseRelatedCase` entity
- [x] Create `CaseRelatedCaseConfiguration`
- [x] Add `DbSet<CaseRelatedCase>` to `AppDbContext.cs`
- [x] Create EF Core migration `AddCaseRelatedCases`
- [x] Create `RelatedCaseDto` and `LinkRelatedCaseRequest`
- [x] Create `CaseRelatedCasesController` with GET, POST, DELETE
- [x] Create `CaseRelatedCasesService` with `GetAsync`, `LinkAsync`, `UnlinkAsync`
- [x] Add `RelatedCases` to `CaseDetailDto` and populate in `CaseService`
- [x] Add audit event writing on link create and link delete
- [x] Add `RelationshipType` validation and duplicate/self-link prevention
- [x] Register `CaseRelatedCasesService` in DI
- [x] Run `dotnet build` and verify all projects compile

## Review Findings

### Patches Applied
- [x] [Review][Patch] `OrderGuids`: use `Guid.CompareTo` instead of string comparison [`CaseRelatedCasesService.cs:171`]
- [x] [Review][Patch] `UnlinkAsync`: add org-scope check to prevent cross-org unlink [`CaseRelatedCasesService.cs:124-130`]
- [x] [Review][Patch] `GetAsync`: add organisation filter to join to prevent cross-org data leak [`CaseRelatedCasesService.cs:28-32`]
- [x] [Review][Patch] `ValidateRelationshipType`: reject numeric enum values (`"0"`, `"1"`, etc.) [`CaseRelatedCasesService.cs:165-166`]
- [x] [Review][Patch] DB CHECK constraint `ck_case_related_cases_ordered_pair` added to migration [`20260621105452_AddCaseRelatedCases.cs:28`]

### Decisions Resolved
- [x] [Review][Decision] DB CHECK constraint for `case_id_a < case_id_b` — **Added** per user approval
- [x] [Review][Decision] `Admin` role in AC 5 — **Dismissed**: Director is the Admin-equivalent, no code change needed

### Deferred (Pre-existing, not caused by this change)
- [x] [Review][Defer] `[Required]` on non-nullable `Guid` struct — pre-existing pattern, dead metadata
- [x] [Review][Defer] LINQ ternary in EF Core join — runtime handles it, no actionable issue
- [x] [Review][Defer] `InvalidOperationException` used for auth failures (wide catch trap) — pre-existing pattern
- [x] [Review][Defer] `IHttpContextAccessor` coupling in service — pre-existing architectural pattern
- [x] [Review][Defer] Manual dictionary-based audit event construction — pre-existing pattern
- [x] [Review][Defer] Enum-as-string at API boundary — pre-existing project convention

### Dismissed (Noise / False Positive)
- Blind Hunter P1: missing `$` interpolation — **false positive** (actual code has `$`; transcription error in prompts)
- Blind Hunter: `RelatedCaseId` naming — clear in route context
- Blind Hunter: `CurrentStage.ToString()` safety — stored as string in DB
- Blind Hunter: No pagination on GetAsync — bounded by domain (cases don't have thousands of cross-links)

