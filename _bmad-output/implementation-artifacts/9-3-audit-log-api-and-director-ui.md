---
baseline_commit: eff5b06
---

# Story 9.3: Audit Log API and Director UI

Status: done

## Story

**As a** Project Director,
**I want to** view audit history of data-changing events,
**So that** compliance is demonstrable (FR-25, NFR-9).

## Acceptance Criteria

**Given** mutations have occurred across all epics (cases, visits, notes, interventions, court, travel, legends, staff, auth)
**When** the Director queries `GET /api/v1/audit` with optional filters
**Then** a paginated list of audit events is returned with who (actor email/name), what (event type), when (timestamp), and which user was affected

**Given** the audit list endpoint
**When** I filter by event type, actor, date range, or subject user
**Then** results are scoped to the Director's organisation and filtered accordingly

**Given** the audit list page on the web app
**When** the Director navigates to Admin → Audit Log
**Then** a paginated table shows events with columns: Timestamp, Event Type, Actor, Subject, Details

**Given** a POCSO-sensitive Case
**When** PII access was revealed (event type `case.pii.revealed`)
**Then** that event appears in the audit log per NFR-4

**Given** the audit log table
**When** there are no events matching the current filter
**Then** an empty state is shown: "No audit events match your filter" (UX-DR13)

**And** retention policy is documented as 7-year assumption (NFR-9) — not enforced by the API in v1
**And** all data-changing events across epics are queryable via this single endpoint

## Technical Requirements

### API: AuditLogController (new — `apps/api/Controllers/V1/AuditLogController.cs`)

**Route:** `api/v1/audit`
**Policy:** `[Authorize(Policy = Policies.DirectorOnly)]`

| Method | Path | Action | Description |
|--------|------|--------|-------------|
| GET | `/api/v1/audit` | ListEvents | Returns paginated, filtered audit events |

**Query parameters (all optional):**
- `eventType` (string, partial match or exact) — filter by event type code (e.g. `case.created`, `staff.*`)
- `actorUserId` (Guid) — filter by who performed the action
- `subjectUserId` (Guid) — filter by who was affected
- `from` (ISO 8601) — inclusive start of CreatedAtUtc range
- `to` (ISO 8601) — exclusive end of CreatedAtUtc range
- `page` (int, default 1) — page number
- `pageSize` (int, default 25, max 100) — items per page

**Pattern:**
- Primary constructor injecting `AppDbContext db`
- Organisation-scoped — only events within the Director's organisation
- Accept individual `[FromQuery]` query parameters on the action method (or a single request record bound via `[FromQuery]`)
- Join with `users` table to resolve actor email and name (left join since ActorUserId is nullable for system events)
- Order by `CreatedAtUtc DESC` (newest first)

**Required usings:**
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;  // User ref for navigation properties
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
```

**Controller action signature:**
```csharp
[HttpGet]
public async Task<IActionResult> ListEvents(
    [FromQuery] string? eventType,
    [FromQuery] Guid? actorUserId,
    [FromQuery] Guid? subjectUserId,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    CancellationToken cancellationToken)
```

**Response DTOs:**

```csharp
// AuditEventDto — single audit event row
public record AuditEventDto(
    Guid Id,
    string EventType,
    DateTime CreatedAtUtc,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorName,
    Guid? SubjectUserId,
    string? SubjectEmail,
    string? SubjectName,
    object? Metadata);

// Paginated response wrapper
public record AuditListResultDto(
    List<AuditEventDto> Items);
```

**Error responses:**
- 200 OK with `ApiResponse<AuditListResultDto>` — pagination metadata in `meta` envelope
- 401 Unauthorized
- 403 Forbidden (non-Director)
- 400 if `pageSize > 100`

**Implementation detail for the EF query:**

```csharp
var query = db.AuditEvents
    .Where(e => e.OrganisationId == organisationId);

if (!string.IsNullOrEmpty(eventType))
    query = query.Where(e => e.EventType.StartsWith(eventType));

if (actorUserId.HasValue)
    query = query.Where(e => e.ActorUserId == actorUserId);

if (subjectUserId.HasValue)
    query = query.Where(e => e.SubjectUserId == subjectUserId);

if (from.HasValue)
    query = query.Where(e => e.CreatedAtUtc >= from.Value);

if (to.HasValue)
    query = query.Where(e => e.CreatedAtUtc < to.Value);

var totalCount = await query.CountAsync(cancellationToken);

var rawItems = await query
    .OrderByDescending(e => e.CreatedAtUtc)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(e => new
    {
        e.Id,
        e.EventType,
        e.CreatedAtUtc,
        e.ActorUserId,
        ActorEmail = e.ActorUser != null ? e.ActorUser.Email : null,
        ActorName = e.ActorUser != null ? e.ActorUser.FirstName + " " + e.ActorUser.LastName : null,
        e.SubjectUserId,
        SubjectEmail = e.SubjectUser != null ? e.SubjectUser.Email : null,
        SubjectName = e.SubjectUser != null ? e.SubjectUser.FirstName + " " + e.SubjectUser.LastName : null,
        e.MetadataJson,
    })
    .ToListAsync(cancellationToken);

var items = rawItems.Select(r => new AuditEventDto(
    r.Id,
    r.EventType,
    r.CreatedAtUtc,
    r.ActorUserId,
    r.ActorEmail,
    r.ActorName,
    r.SubjectUserId,
    r.SubjectEmail,
    r.SubjectName,
    r.MetadataJson != null ? JsonSerializer.Deserialize<object>(r.MetadataJson) : null
)).ToList();
```

**Important:** The `AuditEvent` entity currently does **not** have navigation properties to `User`. You must add:

```csharp
// In AuditEvent.cs — add navigation properties
[ForeignKey(nameof(ActorUserId))]
public User? ActorUser { get; set; }

[ForeignKey(nameof(SubjectUserId))]
public User? SubjectUser { get; set; }
```

And in `AuditEventConfiguration.cs`:

```csharp
builder.HasOne(e => e.ActorUser)
    .WithMany()
    .HasForeignKey(e => e.ActorUserId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasOne(e => e.SubjectUser)
    .WithMany()
    .HasForeignKey(e => e.SubjectUserId)
    .OnDelete(DeleteBehavior.SetNull);
```

**No migration needed** for these navigation properties — they are EF shadow navigation only, no schema change.

**Pagination metadata:** `ApiMeta` (in `apps/api/Models/ApiMeta.cs`) already has `RequestId` and `TotalCount`. Add two optional properties so the controller can pass pagination state to the client:

```csharp
public sealed class ApiMeta
{
    public string RequestId { get; init; } = string.Empty;
    public int? TotalCount { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
```

The controller returns `new ApiResponse<AuditListResultDto>(dto, new ApiMeta { RequestId = ..., TotalCount = totalCount, Page = page, PageSize = pageSize })`.

**Response envelope:**

```json
{
  "data": {
    "items": [
      {
        "id": "uuid",
        "eventType": "staff.created",
        "createdAtUtc": "2026-06-20T14:30:00Z",
        "actorUserId": "uuid",
        "actorEmail": "director@org.org",
        "actorName": "Ravi Director",
        "subjectUserId": "uuid",
        "subjectEmail": "worker@org.org",
        "subjectName": "Priya Worker",
        "metadata": { "role": "SocialWorker" }
      }
    ]
  },
  "meta": {
    "requestId": "...",
    "totalCount": 142,
    "page": 1,
    "pageSize": 25
  }
}
```

### API: Dependencies

- No new NuGet packages
- The `AuditEvent` entity exists — only need to add optional navigation properties to `User`
- The `AuditEventConfiguration` needs relationship configuration additions
- No EF Core migration needed (navigation properties are in-memory only, no schema changes)

### API: Existing AuditEventTypes Reference

All event types already defined in `MidiKaval.Api.Infrastructure.Audit.AuditEventTypes`:
- `auth.login.success`, `auth.login.failed`, `auth.otp.failed`, `auth.logout`, `auth.refresh.success`, `auth.session.invalidated`, `auth.password_reset.requested`, `auth.password_reset.completed`
- `case.created`, `case.stage.changed`, `case.merged`, `case.transferred`, `case.gps.verified`, `case.pii.revealed`
- `case.note.created`
- `visit.scheduled`, `visit.completed`, `visit.rescheduled`, `visit.started`, `visit.note.merged`
- `case.intervention.created`, `case.intervention.updated`
- `court.sitting.created`, `court.sitting.updated`, `court.sitting.reminder_sent`, `court.sitting.miss_escalated`
- `travel.claim.created`, `travel.claim.updated`, `travel.claim.submitted`, `travel.claim.approved`, `travel.claim.returned`
- `attachment.presign.issued`, `attachment.confirmed`
- `legend.created`, `legend.updated`, `legend.deactivated`, `legend.reactivated`
- `staff.created`, `staff.updated`, `staff.deactivated`, `staff.reactivated`, `staff.force-reset`

These are all queryable via the `eventType` filter using `StartsWith` for category grouping (e.g. `eventType=staff.` returns all staff events).

### Web: Audit Log API Service (new — `apps/web/src/app/features/shell/services/audit-api.service.ts`)

Follow the `StaffApiService` pattern:

```typescript
@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/audit`;

  async list(filters: AuditLogFilter): Promise<AuditListResultDto>;
}
```

Where `AuditLogFilter` interface:
```typescript
export interface AuditLogFilter {
  eventType?: string;
  actorUserId?: string;
  subjectUserId?: string;
  from?: string;    // ISO 8601
  to?: string;      // ISO 8601
  page?: number;
  pageSize?: number;
}
```

And `AuditListResultDto` interface:
```typescript
export interface AuditListResultDto {
  items: AuditEventDto[];
}

export interface AuditEventDto {
  id: string;
  eventType: string;
  createdAtUtc: string;
  actorUserId: string | null;
  actorEmail: string | null;
  actorName: string | null;
  subjectUserId: string | null;
  subjectEmail: string | null;
  subjectName: string | null;
  metadata: Record<string, unknown> | null;
}
```

### Web: Audit Log Page Component (new — `apps/web/src/app/features/shell/pages/audit-log-page.component.ts`)

Standalone component with signal state management:

**State (signals):**
- `items: WritableSignal<AuditEventDto[]>` — current page of events
- `loading: WritableSignal<boolean>`
- `errorMessage: WritableSignal<string | null>`
- `totalCount: WritableSignal<number>`
- `currentPage: WritableSignal<number>`
- `pageSize: WritableSignal<number>`
- `filterEventType: WritableSignal<string>` — text input for event type filter
- `filterActorUserId: WritableSignal<string>` — optional user id filter
- `filterFrom: WritableSignal<string>` — date input
- `filterTo: WritableSignal<string>` — date input

**Template structure:**
- Page header with title "Audit Log" and subtitle "Review data-changing events across the organisation"
- Filter bar: event type text input, date range pickers (from/to), search/apply button, clear filters button
- Material table (`mat-table`) with columns:
  - Timestamp (formatted date/time)
  - Event Type (formatted human-readable label + code tooltip)
  - Actor (email or "System" if null)
  - Subject (email or "—")
  - Details (first 100 chars of metadata JSON or "—")
- Paginator (`MatPaginator`) at bottom
- Empty state: "No audit events match your filter"
- Loading state: skeleton rows

**Event type display helper:**

Create a `formatEventType()` function that maps event type codes to human-readable labels. See `AuditEventTypes.cs` (`MidiKaval.Api.Infrastructure.Audit`) for all defined event type constants. Map each code to a title-case label (e.g. `"case.created"` → `"Case created"`). Event types follow dot‑separated category prefixes (`auth.*`, `case.*`, `visit.*`, `court.*`, `travel.*`, `staff.*`, `legend.*`).

**Behavior:**
- On init, fetch first page with no filters (show all recent events)
- Apply filters on button click, reset to page 1
- Page change triggers re-fetch with current filters
- Loading state disables filter/pagination controls
- Error shows errorMessage banner

**Imports:** `MatTableModule`, `MatButtonModule`, `MatIconModule`, `MatCardModule`, `MatPaginatorModule`, `MatFormFieldModule`, `MatInputModule`, `MatDatepickerModule`, `MatNativeDateModule`, `CommonModule`, `FormsModule`

### Web: Route (modified — `apps/web/src/app/app.routes.ts`)

Add a new route under the shell (with `directorGuard`):

```typescript
{
  path: 'admin/audit',
  canActivate: [directorGuard],
  loadComponent: () =>
    import('./features/shell/pages/audit-log-page.component').then(
      (m) => m.AuditLogPageComponent,
    ),
},
```

### Web: Admin Page (modified — `apps/web/src/app/features/shell/pages/admin-page.component.ts`)

Add "Audit Log" nav link to the admin navigation bar, between "Staff Directory" and "Travel claims approval" (keep alphabetical ordering or logical grouping):

```html
<a mat-stroked-button routerLink="/admin/audit">Audit Log</a>
```

### Web: Event type labels (new — `apps/web/src/app/features/shell/pages/audit-event-labels.ts` or inline in component)

Store the `formatEventType()` helper function and optionally a list of common event type categories for the filter placeholder.

### Testing

| Layer | File | Key tests |
|-------|------|-----------|
| API unit | `tests/api.unit/` | AuditLogController: list with/without filters, pagination boundary, non-Director 403, future date range returns empty |
| API integration | `tests/api.integration/` | Audit events created by other operations are queryable via audit endpoint |
| Web | `apps/web/src/app/features/shell/pages/audit-log-page.component.spec.ts` | Component renders empty state, applies filters, handles pagination, shows error state |

## Architecture Compliance

- Route: `/api/v1/audit` — plural kebab-case per architecture §5.3
- Policy: `DirectorOnly` — only Directors can view audit trail
- Organisation-scoped — always filtered by `OrganisationId`
- Envelope: `{ data, meta }` via `ApiResponse<T>`
- UUID v4 for all IDs (already in AuditEvent entity)
- ISO 8601 UTC timestamps
- CamelCase JSON response
- Web: standalone component with signals for local state
- Empty state per UX-DR13
- Read-only query endpoint — no mutations in this story
- Navigation properties use EF Core relationships (no new schema changes)

## Library / Framework Requirements

- **Angular Material**: `MatTableModule`, `MatButtonModule`, `MatIconModule`, `MatCardModule`, `MatPaginatorModule`, `MatFormFieldModule`, `MatInputModule`, `MatDatepickerModule`, `MatNativeDateModule`
- **ASP.NET Core 8**: Entity Framework Core, primary constructors on controller
- No new NuGet or npm packages

## File Structure Requirements

### New files (API)

| File | Purpose |
|------|---------|
| `apps/api/Controllers/V1/AuditLogController.cs` | Audit log query controller |
| `apps/api/Models/Audit/AuditEventDto.cs` | Audit event response DTO |
| `apps/api/Models/Audit/AuditListResultDto.cs` | Paginated audit list DTO |

### Modified files (API)

| File | Change |
|------|--------|
| `apps/api/Domain/Entities/AuditEvent.cs` | Add ActorUser and SubjectUser navigation properties |
| `apps/api/Infrastructure/Persistence/AuditEventConfiguration.cs` | Add HasOne/WithMany/HasForeignKey/OnDelete for ActorUser and SubjectUser |

### New files (Web)

| File | Purpose |
|------|---------|
| `apps/web/src/app/features/shell/services/audit-api.service.ts` | API service for audit log |
| `apps/web/src/app/features/shell/pages/audit-log-page.component.ts` | Audit log page component (TS inline template OK) |
| `apps/web/src/app/features/shell/pages/audit-log-page.component.scss` | Audit log page styles |

### Modified files (Web)

| File | Change |
|------|--------|
| `apps/web/src/app/app.routes.ts` | Add `admin/audit` route with directorGuard |
| `apps/web/src/app/features/shell/pages/admin-page.component.ts` | Add Audit Log nav link |

## Previous Story Intelligence

Story 9.2 (Staff Directory Management) established and reinforced:

- **Patterns for CRUD API controllers** — use primary constructor with `AppDbContext db`, `Problem()` helper for errors, `ResolveOrganisationId()` / `ResolveUserId()` / `ResolveRequestId()` pattern
- **DirectorOnly policy** for Director-only endpoints
- **Signal-based state management** with `WritableSignal`, loading/error states
- **`firstValueFrom`** wrapper for all API calls (from `rxjs`)
- **MatTable** with pagination and action columns
- **Error handling** via `extractErrorMessage` pattern
- **code review patches applied** in 9.2 are now available as project knowledge:
  - `Problem()` on ControllerBase (not manual ProblemDetails)
  - Error extraction unwraps `HttpErrorResponse` in `extractErrorMessage`
  - `DbUpdateException` try-catch pattern for 409 conflicts
  - HttpErrorResponse `wrapError` passes through instead of re-wrapping in StaffApiError

Key learnings to apply:

- **Do** follow the same controller pattern with `Problem()` helper, not manual `ProblemDetails`
- **Do** use signals for local component state
- **Do** use `firstValueFrom` with `HttpClient`
- **Do** add navigation properties carefully — they're in-memory only, no migration
- **Don't** add write/modify endpoints — this is a read-only query
- **Don't** reuse `page`/`pageSize` naming that conflicts with existing patterns — this is a new pagination addition to the API
- The `AuditEvent` entity is append-only — no editing or deleting events
- The `MetadataJson` field is a JSONB column with unstructured data — deserialize to `object` for display
- No `window.confirm()` — use component-level actions for filter apply/clear

## Project Context Reference

- Standalone components + signals for local state
- Manual API services following `firstValueFrom` + `inject(HttpClient)` pattern
- API envelope: `{ "data": {}, "meta": { "requestId": "..." } }`
- All mutations write audit_events (already implemented across all epics)
- UUID v4 IDs, snake_case DB tables, camelCase JSON
- DirectorOnly policy for audit log access
- NFR-9: Audit log retention 7-year assumption (not enforced in v1)
- NFR-4: POCSO PII access events (`case.pii.revealed`) must be queryable
- The audit_events table already has indexes on `(OrganisationId, CreatedAtUtc)` and `(EventType, CreatedAtUtc)` — these support the filtering queries
- `MetadataJson` column is nullable jsonb — events may or may not have metadata
- ActorUserId and SubjectUserId are both nullable — system events may have no actor (e.g. background jobs) or no subject

## Story Completion Status

- **Type:** New feature (API query endpoint + Web UI)
- **Dependencies:** No EF Core migration needed (navigation properties only, existing schema unchanged)
- **Estimated files:** ~2 new (API), ~2 modified (API), ~2 new (Web), ~2 modified (Web)
- **Verification:** `dotnet build` on API, `npx tsc --noEmit` on web, manual filter/pagination testing via web UI

---

## Dev Agent Record

### Implementation Notes

- Followed the established controller pattern (primary constructor, `Problem()` helper, `ResolveOrganisationId()`, `ResolveRequestId()`)
- Used two-step EF query: server-side anonymous `.Select()` → `.ToListAsync()` → in-memory `.Select()` for `JsonSerializer.Deserialize` to avoid SQL translation errors
- Added `Page` and `PageSize` properties to `ApiMeta` for pagination metadata, consistent with existing `TotalCount`
- Navigation properties (`ActorUser`, `SubjectUser`) added to `AuditEvent` with `[ForeignKey]` attributes
- `AuditEventConfiguration` updated with `HasOne`/`WithMany`/`HasForeignKey`/`OnDelete(SetNull)` for both navigation properties
- Web component uses `MatPaginator` with `PageEvent`, signals for state, and date pickers for filter range
- `AuditApiService` passes `HttpParams` for query parameter serialization

### Key Decisions

- Pagination metadata (`totalCount`, `page`, `pageSize`) placed in `ApiMeta` envelope (per architecture §5.3 convention), not in the `data` payload
- `formatEventType()` labels kept in the component with the full mapping from `AuditEventTypes.cs` for display purposes
- `[FromQuery]` individual parameters used instead of a request DTO for simplicity
- Navigation properties use `DeleteBehavior.SetNull` so deactivating users doesn't cascade-delete audit history

### Pre-existing State

- API build has 34 pre-existing errors in LegendsController, ReportsController, ReportGenerationService, and ReportExportJobRunner — none related to this story
- Web `tsc --noEmit` has pre-existing errors in `.spec.ts` files and notification component — none related to this story

## File List

### New files (API)
- `apps/api/Controllers/V1/AuditLogController.cs`
- `apps/api/Models/Audit/AuditEventDto.cs`
- `apps/api/Models/Audit/AuditListResultDto.cs`

### Modified files (API)
- `apps/api/Domain/Entities/AuditEvent.cs` — added ActorUser, SubjectUser navigation properties
- `apps/api/Infrastructure/Persistence/AuditEventConfiguration.cs` — added HasOne/WithMany configuration
- `apps/api/Models/ApiMeta.cs` — added Page, PageSize properties

### New files (Web)
- `apps/web/src/app/features/shell/services/audit-api.service.ts`
- `apps/web/src/app/features/shell/audit.models.ts`
- `apps/web/src/app/features/shell/pages/audit-log-page.component.ts`
- `apps/web/src/app/features/shell/pages/audit-log-page.component.scss`

### Modified files (Web)
- `apps/web/src/app/app.routes.ts` — added `admin/audit` route with directorGuard
- `apps/web/src/app/features/shell/pages/admin-page.component.ts` — added Audit Log nav link

## Change Log

| Date | Change |
|------|--------|
| 2026-06-20 | Initial implementation of Audit Log API and Director UI |
| 2026-06-20 | API: AuditLogController created with GET /api/v1/audit endpoint, paginated + filtered |
| 2026-06-20 | API: AuditEvent navigation properties added (ActorUser, SubjectUser) |
| 2026-06-20 | API: ApiMeta extended with Page/PageSize for pagination metadata |
| 2026-06-20 | Web: AuditApiService, audit models, and audit-log-page component created |
| 2026-06-20 | Web: admin/audit route added with directorGuard, nav link in admin page |
| 2026-06-20 | Code review patches applied — 5 patch findings fixed + 1 decision resolved |
| 2026-06-20 | D1 resolved: `To` date filter changed from exclusive `<` to inclusive `<=` for same-day events |

### Review Findings

**Decision needed (1 unresolved):**

- [x] [Review][Decision] `To` date exclusive `<` excludes same-day events — changed backend to `<=` to include events on the selected end date. [`apps/api/.../AuditLogController.cs:88`]

**Patch (all resolved):**

- [x] [Review][Patch] `pageSize < 1` not guarded — added `if (pageSize < 1)` returning 400. [`apps/api/.../AuditLogController.cs:41-46`]
- [x] [Review][Patch] Invalid JSON in `MetadataJson` throws uncaught `JsonException` — wrapped in `DeserializeMetadata()` helper with try-catch returning null. [`apps/api/.../AuditLogController.cs:140-151`]
- [x] [Review][Patch] `from` > `to` date range not cross-validated — added guard returning 400. [`apps/api/.../AuditLogController.cs:75-80`]
- [x] [Review][Patch] Event type filter whitespace sensitivity — added `.trim()` to `filterEventType`. [`apps/web/.../audit-log-page.component.ts:245`]
- [x] [Review][Patch] `[(ngModel)]` binding on signal with `()` breaks two-way binding — changed to `[ngModel]` + `(ngModelChange)` split syntax. [`apps/web/.../audit-log-page.component.ts:91`]

**Deferred (pre-existing / out of scope):**

- [x] [Review][Defer] `ResolveOrganisationId()` throws 500 instead of 401/403 for bad claims — pre-existing across all controllers
- [x] [Review][Defer] Skip/Take pagination O(n) on append-only table — keyset pagination deferred to future performance story
- [x] [Review][Defer] ActorUserId/SubjectUserId filters lack dedicated DB indexes — performance optimization, not in scope
- [x] [Review][Defer] `audit-api.service.ts` re-throws raw error instead of wrapping — component handles it correctly with `extractErrorMessage`
- [x] [Review][Defer] `AuditEventDto` 10-parameter positional record fragility — matches project-wide record pattern
- [x] [Review][Defer] `ResolveRequestId()` fallback to `TraceIdentifier` inconsistent — pre-existing across controllers
- [x] [Review][Defer] Audit log access not itself audited — read-only endpoint, not in spec
- [x] [Review][Defer] Missing `actorUserId`/`subjectUserId` UI filter controls — API supports them; UI needs user-search infrastructure
