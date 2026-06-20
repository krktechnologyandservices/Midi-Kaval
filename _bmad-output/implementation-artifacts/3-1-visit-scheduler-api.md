---
baseline_commit: NO_VCS
---

# Story 3.1: Visit Scheduler API

Status: done

<!-- Validated: 2026-06-16 — see 3-1-visit-scheduler-api-validation-report.md (13 fixes applied) -->

## Story

As a **field worker**,
I want today, weekly, and overdue visit lists,
so that I know what to execute (FR-8).

## Acceptance Criteria

1. **Given** I am authenticated as **SocialWorker** or **CaseWorker** (`Policies.FieldWorker`)  
   **When** I call `GET /api/v1/visits/today`, `GET /api/v1/visits/weekly`, or `GET /api/v1/visits/overdue`  
   **Then** response is **200 OK** with envelope `{ data: VisitListResultDto, meta: { requestId, totalCount } }`  
   **And** each item is time-ordered (`scheduledAtUtc` ascending; overdue uses oldest-first)  
   **And** each item includes visit fields (`id`, `scheduledAtUtc`, `status`, `isOverdue`) plus nested **`CaseSummaryDto`** (reuse existing type from `Models/Cases/CaseDtos.cs`)  
   **And** only visits where `assignee_user_id` = current user and `organisation_id` = JWT org are returned  
   **And** terminal cases (`TerminationExclusion`) are excluded from all three lists  
   **And** each item may include optional `handoffWhisper` (`HandoffWhisperDto`) when the assignee is the current user and the case has a transfer within the last 7 days (same rules as `GET /api/v1/cases/{id}` — reuse `CaseService` handoff logic; omit for supervisor case-visits list)

1b. **Given** I am **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I call `GET /api/v1/visits/today`, `GET /api/v1/visits/weekly`, `GET /api/v1/visits/overdue`, `POST /api/v1/visits/{id}/complete`, or `POST /api/v1/visits/{id}/reschedule`  
   **Then** response is **403** with `Policies.ForbiddenByRoleMessage`

2. **Given** visit list semantics (pilot UTC boundaries)  
   **When** endpoints filter rows  
   **Then** **today** = `scheduled_at_utc` date equals current UTC date and `status` ∈ {`Scheduled`, `InProgress`}  
   **And** **weekly** = `scheduled_at_utc` ∈ [start of current UTC week Monday 00:00:00, end of Sunday 23:59:59.999] and `status` ∈ {`Scheduled`, `InProgress`, `Completed`} (completed visits this week remain visible for reporting context)  
   **And** **overdue** = `scheduled_at_utc` < `DateTime.UtcNow` and `status` ∈ {`Scheduled`, `InProgress`}  
   **And** `isOverdue` on each DTO is `true` when overdue rule matches regardless of which endpoint returned the row  
   **And** a visit scheduled earlier today may appear in **both** today and overdue lists when `scheduled_at_utc < now` — intentional for Command Strip overdue styling (Story 3.2); do not dedupe across endpoints

3. **Given** I am a field worker and the visit is assigned to me with `status = Scheduled` or `InProgress`  
   **When** I `POST /api/v1/visits/{id}/complete`  
   **Then** visit `status` becomes `Completed`, `completed_at_utc` is set  
   **And** parent `cases.visit_count` increments by 1, `cases.next_visit_due_at_utc` is set to **null**, and `cases.updated_at_utc` refreshes (clears Story 2.6 registry overdue filter until coordinator schedules the next visit)  
   **And** response is **200 OK** with envelope `{ data: VisitListItemDto, meta: { requestId } }` (auto-wrapped by `ApiEnvelopeFilter` when returning bare DTO)  
   **And** `audit_events` records `visit.completed` in the **same transaction** (metadata: `{ visitId, caseId }` — no beneficiary PII)

4. **Given** I attempt to complete a visit that is already `Completed`, assigned to another worker, wrong org, or missing  
   **When** I `POST /api/v1/visits/{id}/complete`  
   **Then** **422** for already completed; **403** for wrong assignee; **404** for missing/wrong org

5. **Given** I am the assigned field worker and visit is `Scheduled` or `InProgress`  
   **When** I `POST /api/v1/visits/{id}/reschedule` with `{ scheduledAtUtc, reason }` where `reason` is non-empty trimmed text (max 500)  
   **Then** `scheduled_at_utc` updates, `last_reschedule_reason` persists, `rescheduled_at_utc` and `rescheduled_by_user_id` are set, `status` remains schedulable (`Scheduled`)  
   **And** parent `cases.next_visit_due_at_utc` syncs to the new `scheduled_at_utc`  
   **And** `audit_events` records `visit.rescheduled` in the same transaction (metadata: `{ visitId, caseId, scheduledAtUtc }` — reason stored on row, not duplicated in audit JSON)

5b. **Given** I attempt to reschedule a visit with `status = Completed`  
   **When** I `POST /api/v1/visits/{id}/reschedule`  
   **Then** **422** Problem Details; no visit/case/audit changes

6. **Given** reschedule `reason` is missing/whitespace or `scheduledAtUtc` is in the past (before `DateTime.UtcNow`)  
   **When** I `POST /api/v1/visits/{id}/reschedule`  
   **Then** **400** Problem Details; no visit/case/audit changes

7. **Given** I am **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `GET /api/v1/cases/{caseId}/visits`  
   **Then** response is **200 OK** with envelope `{ data: VisitListResultDto, meta: { requestId, totalCount } }`  
   **And** visits are time-ordered for that case in my org  
   **And** each item includes `lastRescheduleReason` when set (supervisor visibility per FR-8)  
   **And** field workers receive **403** on this endpoint

8. **Given** I am Coordinator/Director and a Case exists in my org  
   **When** I `POST /api/v1/cases/{caseId}/visits` with `{ scheduledAtUtc, assigneeUserId? }`  
   **Then** on success a `visits` row is inserted (`status = Scheduled`, assignee defaults to `cases.assigned_worker_id` when omitted)  
   **And** `cases.next_visit_due_at_utc` is set to `scheduledAtUtc`  
   **And** response is **201 Created** with envelope `{ data: VisitListItemDto, meta: { requestId } }`  
   **And** `audit_events` records `visit.scheduled` in the same transaction  
   **And** error responses follow this matrix (no partial rows):

   | Scenario | Status |
   |----------|--------|
   | Case missing or wrong org | **404** |
   | Case has no assignee and `assigneeUserId` omitted | **422** |
   | Case `current_stage = TerminationExclusion` | **422** |
   | `scheduledAtUtc` in the past | **400** |
   | `assigneeUserId` not an active field worker in org | **422** |
   | Case already has a visit with `status` ∈ {`Scheduled`, `InProgress`} | **422** (pilot: one active visit per case) |

9. **Given** I am Coordinator/Director/SocialWorker/CaseWorker but **deactivated**  
   **When** I call any visit endpoint above  
   **Then** **403** with `AuthService.DeactivatedMessage` (inactive handler — not RBAC role message)

10. **Given** I am unauthenticated  
    **When** I call any visit endpoint  
    **Then** **401** Unauthorized

11. **Given** EF Core migrations on PostgreSQL  
    **When** migration from this story is applied  
    **Then** only the **`visits`** table is added (no `visit_notes`, GPS columns, sync tables)  
    **And** snake_case columns; indexes on `(organisation_id, assignee_user_id, scheduled_at_utc)` and `(case_id, scheduled_at_utc)`

12. **Given** all existing tests from Epic 1–2 and Stories 3.2+ not implemented  
    **When** this story ships  
    **Then** **no mobile/web visit UI**, no `POST /visits/{id}/start`, no GPS/proximity/offline/sync/push (Stories 3.3–3.7, Epic 7)  
    **And** all existing tests continue to pass when the suite is run at release hardening  
    **And** new integration tests in `VisitSchedulerTests.cs` cover at minimum:
    - today / weekly / overdue filtering (including today+overdue overlap)
    - complete increments `visit_count` and clears `next_visit_due_at_utc`
    - reschedule persists reason; reschedule **422** on completed visit
    - coordinator `GET cases/{id}/visits` sees `lastRescheduleReason` with `meta.totalCount`
    - schedule **201**; schedule **400** past date; schedule **422** terminal case / duplicate active visit / invalid assignee
    - field worker **403** on supervisor case-visits list; coordinator and director **403** on `GET /visits/today`
    - wrong assignee **403**; deactivated **403**; unauthenticated **401**
    - audit rows for schedule / complete / reschedule  
    **And** `VisitTestData` helpers: `ScheduleVisitAsync(client, token, caseId, scheduledAtUtc, assigneeUserId?)`, `CompleteVisitAsync(client, token, visitId)`, `RescheduleVisitAsync(client, token, visitId, scheduledAtUtc, reason)`  
    **And** OpenAPI documents all endpoints; run `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client`

## Tasks / Subtasks

- [x] **Domain — Visit entity + status enum** (AC: 11)
  - [x] `Domain/Entities/Visit.cs` — `Id`, `OrganisationId`, `CaseId`, `AssigneeUserId`, `ScheduledAtUtc`, `Status`, `StartedAtUtc?`, `CompletedAtUtc?`, `LastRescheduleReason?`, `RescheduledAtUtc?`, `RescheduledByUserId?`, `CreatedAtUtc`, `UpdatedAtUtc`
  - [x] `Domain/Enums/VisitStatus.cs` — `Scheduled`, `InProgress`, `Completed`, `Cancelled` (store as string in DB; only `Scheduled`/`InProgress`/`Completed` used in this story)

- [x] **Persistence — EF configuration + migration** (AC: 11)
  - [x] `Infrastructure/Persistence/VisitConfiguration.cs` — table `visits`, snake_case, `last_reschedule_reason` max 500 nullable
  - [x] Register `DbSet<Visit>` on `AppDbContext`
  - [x] Add migration `AddVisits` — **only** `visits` table
  - [x] Update `AppDbContextModelSnapshot`
  - [x] Follow `CaseAssignmentConfiguration` pilot pattern — **no FK constraints** to `cases`/`users` yet; integrity enforced in `VisitService`

- [x] **API — DTOs** (AC: 1, 3, 5, 7, 8)
  - [x] `Models/Visits/VisitDtos.cs` — `VisitListItemDto`, `VisitListResultDto`, `ScheduleVisitRequest`, `RescheduleVisitRequest`
  - [x] `VisitListItemDto` nests existing `CaseSummaryDto`; optional `HandoffWhisperDto? HandoffWhisper` for field-worker list endpoints (AC1)
  - [x] `VisitListResultDto` — `Items`, `Page`, `PageSize` (lists return all matches in pilot — `page=1`, `pageSize` = count; keep shape for Epic 8 pagination)

- [x] **Service — VisitService** (AC: 1–8)
  - [x] `Infrastructure/Visits/VisitService.cs` — scoped; resolve org + actor from `IHttpContextAccessor` (mirror `CaseService`)
  - [x] `ListTodayAsync`, `ListWeeklyAsync`, `ListOverdueAsync` — field worker assignee filter; join `cases` for summary mapping; exclude `TerminationExclusion`; attach handoff whisper when eligible (reuse case handoff helper)
  - [x] `CompleteAsync` — assignee check; increment `Case.VisitCount`; set `Case.NextVisitDueAtUtc = null`; single `SaveChangesAsync` + audit
  - [x] `RescheduleAsync` — reject `Completed` → 422; validate reason + future `scheduledAtUtc`; sync `Case.NextVisitDueAtUtc`
  - [x] `ListForCaseAsync` — coordinator org scope; include `lastRescheduleReason`; omit handoff whisper
  - [x] `ScheduleAsync` — enforce AC8 matrix (terminal case, past date, field-worker assignee, one active visit per case)
  - [x] Exceptions: `VisitNotFoundException` → 404; `VisitForbiddenException` → 403; `VisitBusinessRuleException` → 422; `VisitValidationException` → 400

- [x] **Controller — VisitsController** (AC: 1–10, 12)
  - [x] `Controllers/V1/VisitsController.cs` — route prefix `api/v1/visits`
  - [x] `GET today|weekly|overdue` — `[Authorize(Policy = Policies.FieldWorker)]`
  - [x] `POST {id}/complete`, `POST {id}/reschedule` — `[Authorize(Policy = Policies.FieldWorker)]`
  - [x] Extend `CasesController` — register `GET {id:guid}/visits` and `POST {id:guid}/visits` **before** `GET {id:guid}` (match Story 2.x route-order convention)
  - [x] `[Authorize(Policy = Policies.CoordinatorOrAbove)]` on case visit routes
  - [x] List actions return `Ok(new ApiResponse<VisitListResultDto>(result, new ApiMeta { RequestId, TotalCount }))` (mirror `CasesController.Search`)
  - [x] XML doc comments for OpenAPI

- [x] **Audit** (AC: 3, 5, 8)
  - [x] Extend `AuditEventTypes` — `VisitScheduled = "visit.scheduled"`, `VisitCompleted = "visit.completed"`, `VisitRescheduled = "visit.rescheduled"`
  - [x] Direct `db.AuditEvents.Add` inside same `SaveChangesAsync` as domain writes (do **not** use `AuditService.RecordAsync`)

- [x] **Tests — API integration** (AC: 12)
  - [x] `tests/api.integration/VisitSchedulerTests.cs` — `[Collection("AuthIntegration")]`
  - [x] Add `VisitTestData` (or extend `CaseTestData`) with pinned helper signatures from AC12
  - [x] Full matrix per AC12 (overlap, schedule 400/422, complete clears due date, director 403, etc.)
  - [x] Regenerate OpenAPI snapshot if project uses committed snapshot (follow Story 2.x pattern)

- [x] **Docs** (AC: 12)
  - [x] Update `README.md` visit scheduler section (endpoints, UTC week boundary, pilot schedule path via coordinator POST)

## Dev Notes

### Epic 3 context and scope boundaries

| In scope (3.1) | Deferred |
|----------------|----------|
| `visits` table + list/complete/reschedule/schedule APIs | `POST /visits/{id}/start` (Story 3.3) |
| Field-worker Command Strip data source (`GET /visits/today`) | Mobile UI (Story 3.2) |
| `visit_count` increment on complete | Visit notes on complete (Story 3.3) |
| Reschedule reason for supervisors | GPS, proximity, offline sync (3.4–3.7) |
| Populate/clear `cases.next_visit_due_at_utc` on schedule/reschedule/complete | Push notifications (Epic 7) |
| Optional `handoffWhisper` on field-worker visit lists (Story 3.2 prep) | POCSO initials-only list DTOs (Story 3.8 — no `sensitivity_level` column yet) |

**Critical:** Do **not** compose Command Strip from `GET /cases/assigned` — project-context forbids client-composed visit queues. Dedicated visit endpoints only.

### Existing code to reuse (do not reinvent)

- **`CaseSummaryDto`** — already used by registry search; map from `Case` entity in visit queries
- **`Policies.FieldWorker`** — SocialWorker + CaseWorker (see `AuthServiceCollectionExtensions.cs`)
- **`CaseService` patterns** — org scoping via JWT `organisation_id`; `CaseForbiddenException` / `CaseNotFoundException` naming; transaction + audit pattern from `TransferAsync` / `TransitionStageAsync`
- **`cases.next_visit_due_at_utc`** — nullable column added in Story 2.6; overdue case **search** filter uses `next_visit_due_at_utc < now` (`CaseService.SearchAsync`) — set on schedule/reschedule, **clear to null on complete** so registry overdue filter does not stay stale
- **`HandoffWhisperDto` / handoff logic** — reuse from `CaseService.GetDetailAsync` handoff rules (≤7 days, assignee-only) for optional whisper on field-worker visit list items
- **`cases.visit_count`** — integer default 0; increment atomically on complete

### Visit scheduling data path (pilot)

Epics do not define automatic visit generation. **Coordinator schedule endpoint (AC 8) is required** so integration tests and Story 3.2 have data. Field workers do not create visits.

Typical flow:
1. Coordinator transfers case to field worker (Story 2.8) → `assigned_worker_id` set
2. Coordinator `POST /cases/{id}/visits` with `scheduledAtUtc` → visit row + `next_visit_due_at_utc`
3. Field worker `GET /visits/today` → Command Strip payload in 3.2

### API contract sketches

**`GET /api/v1/visits/today`** (FieldWorker)

```json
{
  "data": {
    "items": [
      {
        "id": "uuid",
        "scheduledAtUtc": "2026-06-16T09:00:00Z",
        "status": "Scheduled",
        "isOverdue": false,
        "lastRescheduleReason": null,
        "handoffWhisper": null,
        "case": { /* CaseSummaryDto */ }
      }
    ],
    "page": 1,
    "pageSize": 1
  },
  "meta": { "requestId": "...", "totalCount": 1 }
}
```

**`POST /api/v1/visits/{id}/reschedule`**

```json
{ "scheduledAtUtc": "2026-06-17T10:00:00Z", "reason": "Beneficiary unavailable — family function" }
```

**`POST /api/v1/cases/{caseId}/visits`** (CoordinatorOrAbove)

```json
{ "scheduledAtUtc": "2026-06-16T09:00:00Z", "assigneeUserId": "optional-uuid" }
```

### Files to create / update

```
apps/api/
├── Domain/Entities/Visit.cs                         # NEW
├── Domain/Enums/VisitStatus.cs                      # NEW
├── Infrastructure/Persistence/VisitConfiguration.cs
├── Infrastructure/Visits/VisitService.cs            # NEW
├── Infrastructure/Visits/VisitExceptions.cs       # NEW (or shared pattern)
├── Models/Visits/VisitDtos.cs                       # NEW
├── Controllers/V1/VisitsController.cs               # NEW
├── Controllers/V1/CasesController.cs                  # UPDATE — case visits routes
├── Infrastructure/Persistence/AppDbContext.cs       # UPDATE — DbSet
├── Infrastructure/Audit/AuditEventTypes.cs          # UPDATE
├── Program.cs or extension                            # UPDATE — register VisitService
└── Migrations/*AddVisits*                             # NEW

tests/api.integration/
├── VisitSchedulerTests.cs                           # NEW
└── CaseTestData.cs (or VisitTestData.cs)              # UPDATE — helpers

packages/api-client/                                   # REGENERATE — never hand-edit
README.md                                              # UPDATE
```

### List filter overlap (today + overdue)

A visit scheduled for today at 09:00 UTC still appears in `GET /visits/today` at 14:00 UTC **and** in `GET /visits/overdue` because overdue uses `scheduled_at_utc < now`. This is **intentional** — Story 3.2 Command Strip uses `isOverdue` for card styling (`command-strip-today.html` overdue border). Do not dedupe across endpoints.

### POCSO / list DTO privacy

`CaseSummaryDto.beneficiaryName` returns full name in visit lists for this story. `cases.sensitivity_level` does not exist yet — initials-only list rules apply in Story 3.8 when the column is added. Do not block 3.1 on POCSO.

### Regression traps

- Register `GET/POST {id:guid}/visits` on `CasesController` **before** `GET {id:guid}` to match established route-order convention
- Do **not** break existing case search/export/transfer/stage endpoints
- Do **not** add `[AllowAnonymous]` on visit mutations
- Do **not** return full beneficiary PII in audit metadata
- Field worker `GET /cases/{id}` (Story 2.8) must remain unchanged — visits are separate resource
- `GET /visits/today` is **field-worker only** — coordinators use crisis queue (Epic 8), not this endpoint
- Complete from `Scheduled` is allowed in 3.1 (start flow adds `InProgress` in 3.3 — service should accept both states for complete)

### Testing standards

- Integration tests use `AuthWebApplicationFactory` + `RbacTestData.EnsureRoleUsersAsync` (see `CaseStageTransitionTests.cs`)
- Assert DB state via scoped `AppDbContext` after HTTP calls
- Assert audit `EventType` + metadata JSON contains ids (not PII)
- Per current project policy: **author tests; running full suite may be deferred to release hardening** unless dev-story explicitly runs them

### Previous story intelligence (Epic 2)

- **Story 2.6** added `next_visit_due_at_utc` with comment "Epic 3 populates" — this story owns writes
- **Story 2.8** established assignee on `cases.assigned_worker_id` + handoff whisper on detail — optional whisper on visit list items reuses same rules (3.2 consumer)
- **Story 2.9** completed web shell — no web work in 3.1
- **Code review pattern** — use domain-specific error signals (`stageErrorMessage` style on web); API uses separate 400/422 exceptions
- **`case_assignments` lacks FK constraints** — match that pattern for `visits` table [Source: `deferred-work.md`]

### Project structure notes

- Controllers stay thin; all visit rules in `VisitService`
- JSON camelCase; DB snake_case via EF configuration
- Register service in DI alongside `CaseService`
- OpenAPI → `npm run generate:api-client` from repo root after API builds

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.1, Epic 3]
- [Source: `_bmad-output/planning-artifacts/architecture.md` §5.1, §5.3 — visits aggregate, dedicated endpoints]
- [Source: `_bmad-output/specs/spec-kaval-online/field-and-court-operations.md` — CAP-4 visit scheduling]
- [Source: `_bmad-output/specs/spec-kaval-online/SPEC.md` — CAP-4 success criteria]
- [Source: `_bmad-output/project-context.md` — dedicated `/visits/today`, no client-composed queues]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — org scope, audit, search overdue filter]
- [Source: `apps/api/Models/Cases/CaseDtos.cs` — `CaseSummaryDto` reuse]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/command-strip-today.html` — downstream consumer shape (3.2)]
- [Source: `_bmad-output/implementation-artifacts/2-6-case-search-filters-and-saved-presets.md` — `next_visit_due_at_utc` column]
- [Source: `_bmad-output/implementation-artifacts/2-8-case-assignment-transfer-and-handoff-whisper.md` — assignee + handoff patterns]

## Dev Agent Record

### Agent Model Used

Auto (Cursor)

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created
- Validated 2026-06-16 — 13 validation fixes applied (see validation report)
- Implemented `visits` table + VisitService with today/weekly/overdue lists, schedule/complete/reschedule, coordinator case-visits history
- 13 integration tests in `VisitSchedulerTests.cs`; OpenAPI snapshot + api-client regenerated
- Updated `UsersSchemaTests` expected table list for `visits` migration

### File List

- apps/api/Domain/Entities/Visit.cs
- apps/api/Domain/Enums/VisitStatus.cs
- apps/api/Infrastructure/Persistence/VisitConfiguration.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Infrastructure/Visits/VisitService.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Models/Visits/VisitDtos.cs
- apps/api/Controllers/V1/VisitsController.cs
- apps/api/Controllers/V1/CasesController.cs
- apps/api/Program.cs
- apps/api/Migrations/*AddVisits*
- tests/api.integration/VisitSchedulerTests.cs
- tests/api.integration/VisitTestData.cs
- tests/api.integration/UsersSchemaTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- README.md

### Review Findings

- [x] [Review][Patch] Add weekly list filtering test — AC12 requires today/weekly/overdue coverage; no `GET /visits/weekly` test exists [tests/api.integration/VisitSchedulerTests.cs]
- [x] [Review][Patch] Add `visit.scheduled` audit integration test — AC12 requires audit rows for schedule/complete/reschedule; only `visit.completed` is asserted today [tests/api.integration/VisitSchedulerTests.cs:129]
- [x] [Review][Patch] Add schedule 422 tests for TerminationExclusion case and invalid assignee — AC8/AC12 error matrix implemented in `VisitService` but untested [tests/api.integration/VisitSchedulerTests.cs]
- [x] [Review][Patch] Add TerminationExclusion exclusion from field-worker list test — AC1 excludes terminal cases in `ListForFieldWorkerAsync` but no integration test [tests/api.integration/VisitSchedulerTests.cs]
- [x] [Review][Patch] Add complete-already-completed 422 test — AC4 requires 422 for completed visits; reschedule counterpart exists, complete does not [tests/api.integration/VisitSchedulerTests.cs]
- [x] [Review][Defer] Transfer leaves stale visit assignee — case reassignment does not update or cancel active visits; new assignee blocked from scheduling [apps/api/Infrastructure/Visits/VisitService.cs:138] — deferred, follow-up when transfer+visit lifecycle is specified
- [x] [Review][Defer] Concurrent schedule/complete without optimistic concurrency — `AnyAsync` + insert and `visit_count += 1` can race under parallel requests [apps/api/Infrastructure/Visits/VisitService.cs:138] — deferred, pilot scale; add row version or partial unique index in hardening pass
- [x] [Review][Defer] Deactivated assignee cannot clear active visit — all visit mutations require active FieldWorker policy [apps/api/Infrastructure/Visits/VisitService.cs:191] — deferred, admin/ops path not in 3.1 scope
- [x] [Review][Defer] TerminationExclusion hides visit from lists but not mutations — complete/reschedule still allowed via direct POST if visit ID known [apps/api/Infrastructure/Visits/VisitService.cs:355] — deferred, terminal-case visit lifecycle not specified in AC
- [x] [Review][Defer] No DB partial unique index for one active visit per case — enforcement is app-layer only [apps/api/Migrations/20260616134617_AddVisits.cs:37] — deferred, matches pilot no-FK pattern from Story 2.x
- [x] [Review][Defer] InProgress reschedule leaves `StartedAtUtc` set — status reset to Scheduled without clearing start timestamp [apps/api/Infrastructure/Visits/VisitService.cs:315] — deferred, InProgress not used until Story 3.3
- [x] [Review][Defer] N+1 handoff queries on field-worker list endpoints — `BuildHandoffWhisperAsync` per row [apps/api/Infrastructure/Visits/VisitService.cs:358] — deferred, acceptable pilot list sizes
