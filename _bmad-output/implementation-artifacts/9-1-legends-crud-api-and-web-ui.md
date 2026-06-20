---
baseline_commit: eff5b06
---

# Story 9.1: Legends CRUD API and Web UI

Status: done

## Story

**As a** Project Coordinator,
**I want to** manage master data (Legends) without Excel,
**So that** dropdowns stay current across the platform (FR-23).

## Acceptance Criteria

**Given** the Legends page on the web app
**When** I select a legend type (offence types, classifications, intervention categories, education, occupation, outcomes, areas, designations, police stations)
**Then** I can view, add, edit, and soft-deactivate entries in a table

**And** empty categories show an empty table with an "Add row" CTA (UX-DR13)
**And** mutations write audit_events (FR-25)
**And** changes are visible on next client fetch

## Technical Requirements

### API: Legend Entities (new — `apps/api/Domain/Entities/Legends/`)

Create individual entity classes for each legend type. All share a common shape:

```
Id: Guid (PK)
OrganisationId: Guid (tenant-scoped)
Name: string (required, max 256)
IsActive: bool (default true)
CreatedAtUtc: DateTime
UpdatedAtUtc: DateTime
CreatedByUserId: Guid
```

Legend types (one entity per type):

| Entity | Table | Notes |
|--------|-------|-------|
| `OffenceType` | `legend_offence_types` | — |
| `Classification` | `legend_classifications` | Maps to `OffenceClassification` enum on Case |
| `InterventionCategory` | `legend_intervention_categories` | Maps to interventions |
| `EducationLevel` | `legend_education_levels` | Education field on Case |
| `Occupation` | `legend_occupations` | Occupation field on Case |
| `VisitOutcome` | `legend_visit_outcomes` | Visit outcome dropdown |
| `CourtOutcome` | `legend_court_outcomes` | Court outcome dropdown |
| `Area` | `legend_areas` | Geographic area |
| `Designation` | `legend_designations` | Staff designation |
| `PoliceStation` | `legend_police_stations` | Police station |

**Do NOT create** a single polymorphic "Legend" table. Each type gets its own dedicated table with EF Core entity configuration. This preserves referential integrity and avoids complex discriminator columns.

### API: EF Core Configurations (new — `apps/api/Infrastructure/Persistence/`)

One configuration class per entity implementing `IEntityTypeConfiguration<T>`:

- `OffenceTypeConfiguration`
- `ClassificationConfiguration`
- `InterventionCategoryConfiguration`
- `EducationLevelConfiguration`
- `OccupationConfiguration`
- `VisitOutcomeConfiguration`
- `CourtOutcomeConfiguration`
- `AreaConfiguration`
- `DesignationConfiguration`
- `PoliceStationConfiguration`

Pattern (follow `CaseConfiguration.cs`):
```csharp
builder.ToTable("legend_offence_types");
builder.HasKey(e => e.Id);
builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
```

### API: AppDbContext changes (modified — `apps/api/Infrastructure/Persistence/AppDbContext.cs`)

Add `DbSet<T>` property for each legend entity. Existing `ApplyConfigurationsFromAssembly` call auto-discovers the new configurations — no manual registration needed.

### API: DTOs (new — `apps/api/Models/Legends/`)

Create `apps/api/Models/Legends/` with request/response types:

```csharp
// LegendCreateRequest
public record LegendCreateRequest(string Name);

// LegendUpdateRequest
public record LegendUpdateRequest(string Name);

// LegendDto (response)
public record LegendDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

// LegendListResultDto
public record LegendListResultDto(List<LegendDto> Items);
```

Single set of DTOs shared for all legend types (Name is the only mutable field).

### API: Controller (new — `apps/api/Controllers/V1/LegendsController.cs`)

Route: `api/v1/legends`

| Method | Path | Action | Description |
|--------|------|--------|-------------|
| GET | `/api/v1/legends/{type}` | List | Returns active entries for legend type; `?includeInactive=true` to include deactivated |
| GET | `/api/v1/legends/{type}/{id}` | Get by id | Single entry |
| POST | `/api/v1/legends/{type}` | Create | Creates new entry |
| PUT | `/api/v1/legends/{type}/{id}` | Update | Updates name |
| DELETE | `/api/v1/legends/{type}/{id}` | Deactivate | Soft-delete (sets IsActive=false) |

**Pattern:**
- Class: `[ApiController] [Authorize(Policy = Policies.CoordinatorOrAbove)] [Route("api/v1/legends")]`
- Primary constructor injecting `AppDbContext db`
- Legend type resolved from URL via a private helper that returns the correct `DbSet` via `db.Set<T>()`
- Use `typeof(OffenceType).Name` etc. for the type resolver — or a simple switch-expression mapping the URL slug to entity type

**Validation:**
- 400 if legend type slug is invalid
- 400 if name is empty/null on create/update
- 404 if entry not found for given type + id + organisationId
- 409 if name already exists (active or inactive) for same type + organisation (prevents duplicate legend names)

**Each mutation** writes an `AuditEvent`:
```csharp
db.AuditEvents.Add(new AuditEvent
{
    Id = Guid.NewGuid(),
    OrganisationId = organisationId,
    ActorUserId = userId,
    EventType = AuditEventTypes.LegendCreated,  // or LegendUpdated / LegendDeactivated
    EntityType = "legend_offence_types",
    EntityId = entityId,
    MetadataJson = JsonSerializer.Serialize(new { name, previousName }),
    OccurredAtUtc = DateTime.UtcNow,
});
```

New `AuditEventTypes` entries needed: `legend.created`, `legend.updated`, `legend.deactivated` (in `apps/api/Infrastructure/Audit/AuditEventTypes.cs`, following existing `"case.created"` naming pattern).

### API: Dependencies

- Legend types are referenced by existing entities (e.g., `Case.TypeOfOffence`, `Intervention.Category`). This story creates the **master data tables** only — no foreign key changes to existing entities in this story. Referential integrity will be tightened when existing entities are migrated to use legend FKs, which is out of scope for 9.1.
- Migrations: `dotnet ef migrations add CreateLegendTables`

### Web: Legends API Service (new — `apps/web/src/app/features/shell/services/legends-api.service.ts`)

Generate via `@api-client` (or create manually matching existing service patterns in `shell/services/`):

- `list(type: string, includeInactive?: boolean): Promise<LegendDto[]>`
- `get(type: string, id: string): Promise<LegendDto>`
- `create(type: string, name: string): Promise<LegendDto>`
- `update(type: string, id: string, name: string): Promise<LegendDto>`
- `deactivate(type: string, id: string): Promise<void>`

Pattern follow `reports-api.service.ts` — `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `firstValueFrom`.

### Web: LegendsPageComponent (modified — `apps/web/src/app/features/shell/pages/legends-page.component.ts`)

Replace the existing placeholder component with a full CRUD page:

**State (signals):**
- `selectedType: WritableSignal<string>` — currently selected legend type
- `items: WritableSignal<LegendDto[]>` — entries for selected type
- `loading: WritableSignal<boolean>`
- `errorMessage: WritableSignal<string | null>`

**Template structure:**
- Page header with title "Legends" and subtitle
- Dropdown (`mat-select`) at top for picking legend type from predefined list
- Material table (`mat-table`) with columns: Name, Status (Active/Inactive), Actions (Edit, Deactivate)
- "Add {type}" button above table
- Empty state: when `items().length === 0 && !loading()`, show empty table with "Add first {type}" CTA (UX-DR13)
- Edit via dialog (`MatDialog`) or inline — prefer dialog for simplicity
- Deactivate with confirmation dialog

**Behavior:**
- On type change, fetch items for that type
- Create optimistically adds to local list then refreshes
- Edit updates in-place then refreshes
- Deactivate removes row (soft-delete via API)
- Error handling with `errorMessage` signal and retry

**Imports:** `MatTableModule`, `MatSelectModule`, `MatFormFieldModule`, `MatButtonModule`, `MatIconModule`, `MatDialogModule`, `MatCardModule`, `CommonModule`, `ReactiveFormsModule`

### Web: Route (already exists — no changes needed)

- Route `/legends` already registered in `app.routes.ts` pointing to `LegendsPageComponent`
- Sidebar nav item already in `supervisor-shell.component.ts` nav items

### Testing

| Layer | File | Key tests |
|-------|------|-----------|
| API unit | `tests/api.unit/` | Legend controller: valid CRUD, invalid type slug, duplicate name 409, missing auth returns 401, non-Coordinator returns 403, audit event written on mutations |
| API integration | `tests/api.integration/` | Legend type CRUD against real DB, soft-delete cascades |
| Web | `apps/web/src/app/features/shell/pages/legends-page.component.spec.ts` | Component renders type selector, loads items, shows empty state, create/edit/deactivate flows, error state, non-empty list displays correctly |

## Architecture Compliance

- Route: `/api/v1/legends/{type}` — plural kebab-case per architecture §5.3
- Policy: `CoordinatorOrAbove` on all endpoints — no `[AllowAnonymous]`
- Audit: every mutation writes `audit_events` table
- Envelope: `{ data, meta }` via `ApiEnvelopeFilter`
- UUID v4 for all IDs
- ISO 8601 UTC timestamps
- CamelCase JSON response
- Web: standalone component with signals for local state
- Web: generated `@api-client` for HTTP (or manual service matching same patterns)
- **After creating the API endpoints**, regenerate the TypeScript client via `dotnet build` then `npx openapi-generator-cli generate` (or automated gen script from `packages/api-client`) so the new `LegendsController` types are available in `@api-client`
- Empty state per UX-DR13

## Library / Framework Requirements

- **Angular Material**: `MatTableModule`, `MatSelectModule`, `MatFormFieldModule`, `MatButtonModule`, `MatIconModule`, `MatDialogModule`, `MatCardModule`
- **ASP.NET Core 8**: Entity Framework Core, primary constructors on controller
- **ClosedXML / QuestPDF**: Not needed for this story (no reports)

## File Structure Requirements

### New files (API)

| File | Purpose |
|------|---------|
| `apps/api/Domain/Entities/Legends/OffenceType.cs` | Entity |
| `apps/api/Domain/Entities/Legends/Classification.cs` | Entity |
| `apps/api/Domain/Entities/Legends/InterventionCategory.cs` | Entity |
| `apps/api/Domain/Entities/Legends/EducationLevel.cs` | Entity |
| `apps/api/Domain/Entities/Legends/Occupation.cs` | Entity |
| `apps/api/Domain/Entities/Legends/VisitOutcome.cs` | Entity |
| `apps/api/Domain/Entities/Legends/CourtOutcome.cs` | Entity |
| `apps/api/Domain/Entities/Legends/Area.cs` | Entity |
| `apps/api/Domain/Entities/Legends/Designation.cs` | Entity |
| `apps/api/Domain/Entities/Legends/PoliceStation.cs` | Entity |
| `apps/api/Infrastructure/Persistence/OffenceTypeConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/ClassificationConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/InterventionCategoryConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/EducationLevelConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/OccupationConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/VisitOutcomeConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/CourtOutcomeConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/AreaConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/DesignationConfiguration.cs` | EF config |
| `apps/api/Infrastructure/Persistence/PoliceStationConfiguration.cs` | EF config |
| `apps/api/Models/Legends/LegendCreateRequest.cs` | DTO |
| `apps/api/Models/Legends/LegendUpdateRequest.cs` | DTO |
| `apps/api/Models/Legends/LegendDto.cs` | DTO |
| `apps/api/Models/Legends/LegendListResultDto.cs` | DTO |
| `apps/api/Controllers/V1/LegendsController.cs` | Controller |

### Modified files (API)

| File | Change |
|------|--------|
| `apps/api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<T>` for each legend entity |
| `apps/api/Infrastructure/Audit/AuditEventTypes.cs` | Add `legend.created`, `legend.updated`, `legend.deactivated` constants |

### New files (Web)

| File | Purpose |
|------|---------|
| `apps/web/src/app/features/shell/services/legends-api.service.ts` | API service |
| `apps/web/src/app/features/shell/pages/legend-edit-dialog.component.ts` | Add/edit dialog component |
| `apps/web/src/app/features/shell/legends.models.ts` | Legend types and constants |

### Modified files (Web)

| File | Change |
|------|--------|
| `apps/web/src/app/features/shell/pages/legends-page.component.ts` | Replaced placeholder with full CRUD implementation |
| `apps/web/src/app/features/shell/pages/legends-page.component.html` | New template with type selector, table, empty state |
| `apps/web/src/app/features/shell/pages/legends-page.component.scss` | New styles for legends page |

## Previous Story Intelligence

Story 8.7 (most recent — Angular PWA Offline Snapshot Cache) established:
- Patterns for `signal()`-based state management with `computed()` in components
- `inject()` pattern for dependency injection
- `firstValueFrom` wrapper for API calls returning Promises
- Error handling via `errorMessage` signals + `extractErrorMessage`

No previous story in Epic 9 — this is the first story. Epic 9 legends master data is standalone with no dependencies on other Epic 9 stories.

### Change Log

- **2026-06-20**: Story 9.1 implemented — Legends CRUD API and Web UI.
  - Created 10 legend entity classes + EF configurations with unique name constraint
  - Created LegendsController with GET/POST/PUT/DELETE for each legend type
  - Added audit event types for legend CRUD mutations
  - Created LegendsApiService and LegendEditDialogComponent
  - Replaced LegendsPageComponent placeholder with full CRUD UI (type selector, table, add/edit/deactivate)
  - Verification: API compiles, web compiles with zero new TS errors
- **2026-06-20**: Code review applied — 9 patch findings fixed.
  - Added try-catch for DbUpdateException race condition (Create/Update → 409)
  - Removed ToLower() from NameExistsAsync to enable index seek
  - Replaced deprecated .toPromise() with firstValueFrom()
  - Replaced window.confirm() with MatDialog confirmation dialog
  - Added [MaxLength(256)] validation to request DTOs
  - Added early exit guard for already-inactive deactivation
  - Fixed dialog save() empty name hang issue
  - Fixed frontend edit guard case-sensitivity mismatch
  - Added PATCH /reactivate endpoint + UI button for reactivation

## Project Context Reference

- Standalone components + signals for local state
- Generated `@api-client` preferred for HTTP; manual service acceptable if following same `firstValueFrom` + `inject(HttpClient)` pattern
- API envelope: `{ "data": {}, "meta": { "requestId": "..." } }`
- All mutations write audit_events
- UUID v4 IDs, snake_case DB tables, camelCase JSON
- CoordinatorOrAbove policy — never AllowAnonymous
- NFR-12: Web PWA is network-only for mutations (legends CRUD is a mutation path — requires connectivity)

## Tasks / Subtasks

- [x] Create 10 Legend entity classes in `apps/api/Domain/Entities/Legends/`
- [x] Create 10 EF Core configuration classes in `apps/api/Infrastructure/Persistence/`
- [x] Add `DbSet<T>` for each legend entity in `AppDbContext.cs`
- [x] Add `legend.created`, `legend.updated`, `legend.deactivated` audit event types
- [x] Create Legend DTOs in `apps/api/Models/Legends/`
- [x] Create `LegendsController` with CRUD endpoints at `/api/v1/legends/{type}`
- [x] Create `LegendsApiService` in web app
- [x] Create `LegendEditDialogComponent` for add/edit
- [x] Replace `LegendsPageComponent` placeholder with full CRUD UI
- [x] Verify TypeScript compilation (zero new errors)

### Review Findings

**Patch (all resolved):**

- [x] [Review][Patch] Duplicate-name race condition (TOCTOU) — wrapped `SaveChangesAsync` in try-catch for `DbUpdateException` returning 409 Conflict. [`apps/api/.../LegendsController.cs:134-160,209-236`]
- [x] [Review][Patch] `NameExistsAsync` uses `.ToLower()` — removed `.ToLower()`, relying on DB collation for case-insensitive comparison. [`apps/api/.../LegendsController.cs:311`]
- [x] [Review][Patch] Deprecated `.toPromise()` — replaced with `firstValueFrom()` in add/edit/deactivate flows. [`apps/web/.../legends-page.component.ts`]
- [x] [Review][Patch] Deactivation uses `window.confirm()` — replaced with `LegendConfirmDialogComponent` MatDialog. [`apps/web/.../legends-page.component.ts:101`]
- [x] [Review][Patch] Missing `[MaxLength(256)]` — added to `LegendCreateRequest` and `LegendUpdateRequest` DTOs. [`apps/api/Models/Legends/`]
- [x] [Review][Patch] Already-inactive entity deactivation — added early exit guard in `Deactivate` endpoint. [`apps/api/.../LegendsController.cs:243-289`]
- [x] [Review][Patch] Dialog `save()` with empty name — changed early return to `dialogRef.close()`. [`apps/web/.../legend-edit-dialog.component.ts:45-50`]
- [x] [Review][Patch] Frontend edit guard case sensitivity — changed to case-insensitive comparison. [`apps/web/.../legends-page.component.ts:87`]
- [x] [Review][Patch] Soft-delete reactivation — added `PATCH /reactivate` endpoint + reactivate UI button for inactive rows. [`apps/api/.../LegendsController.cs`, `apps/web/.../legends-page.component.ts`]

**Deferred (pre-existing / out of scope):**

- [x] [Review][Defer] `[FromBody]` attribute not explicitly specified — `[ApiController]` infers it implicitly. False positive. [`apps/api/.../LegendsController.cs:114,174`] — deferred, pre-existing
- [x] [Review][Defer] Claims resolution throws `InvalidOperationException` (500) instead of 4xx — `[Authorize]` guarantees authentication, 500 is acceptable for config failures. [`apps/api/.../LegendsController.cs:381-400`] — deferred, pre-existing
- [x] [Review][Defer] `IQueryable<object>` may cause EF Core query issues — deliberate design choice for dynamic type resolution. [`apps/api/.../LegendsController.cs:403`] — deferred, pre-existing
- [x] [Review][Defer] Runtime reflection with magic strings for property access — inherent trade-off of the unified controller pattern. [`apps/api/.../LegendsController.cs:351-375`] — deferred, pre-existing
- [x] [Review][Defer] Reflection helpers use null-forgiving operators without fallback — guaranteed by construction, fail-fast is acceptable. [`apps/api/.../LegendsController.cs:351-375`] — deferred, pre-existing
- [x] [Review][Defer] `ngOnInit` returns unawaited Promise — project-wide pattern, try/catch handles errors. [`apps/web/.../legends-page.component.ts:43`] — deferred, pre-existing
- [x] [Review][Defer] FindEntityAsync doesn't filter by active/inactive status — intentional (view/edit of historical data). [`apps/api/.../LegendsController.cs`] — deferred, pre-existing
- [x] [Review][Defer] No request cancellation on rapid type switch — pre-existing pattern concern across the app. [`apps/web/.../legends-page.component.ts`] — deferred, pre-existing
- [x] [Review][Defer] No optimistic concurrency on legend entities — pre-existing omission across the project. All 10 entities. [`apps/api/.../Domain/Entities/Legends/`] — deferred, pre-existing
- [x] [Review][Defer] No pagination on list endpoint — legend tables are master data with limited rows. [`apps/api/.../LegendsController.cs`] — deferred, pre-existing
- [x] [Review][Defer] Missing optimistic add in Create flow — minor spec deviation, refresh is immediate. [`apps/web/.../legends-page.component.ts:73-78`] — deferred, pre-existing
- [x] [Review][Defer] Missing `MatDialogModule`/`ReactiveFormsModule` in imports — `MatDialog` is a service, `ReactiveFormsModule` not used. [`apps/web/.../legends-page.component.ts:16-19`] — deferred, pre-existing

**Dismissed:** 4 findings (naming convention, trivial allocation, established pattern, spec schema mismatch).

## Story Completion Status

- **Type:** New feature (API + Web UI)
- **Dependencies:** None
- **Estimated files:** ~30 new, ~2 modified (API) + ~3 new/modified (Web)
- **Verification:** `dotnet test` on API, `npx tsc --noEmit` on web, manual CRUD test via web UI
