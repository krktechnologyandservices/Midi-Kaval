---
baseline_commit: NO_VCS
---

# Story 5.1: Court Sitting CRUD API

<!-- Validated: 2026-06-19 — see 5-1-court-sitting-crud-api-validation-report.md (9 fixes applied) -->

Status: done

## Story

As a **Case Worker**,
I want to record court sittings on Cases,
so that appearances are tracked (FR-15).

*Scope: **API only** — `court_sittings` schema, nested CRUD under cases, field-worker **upcoming schedule** endpoint, audit. **No** web/mobile UI (Story 5.2), **no** reminder job (Story 5.3), **no** miss escalation / Crisis Queue (Story 5.4). **Do not** conflate `CaseNoteType.Court` timeline notes with the `court_sittings` table. Assignee visibility follows **case `assigned_worker_id`** (no separate sitting assignee column in v1).*

## Acceptance Criteria

1. **Given** I can read a Case (`EnsureCanReadCase`)  
   **When** I `POST /api/v1/cases/{id}/court-sittings` with `scheduledAtUtc`, `courtName`, `purpose`, optional `status` (default `Upcoming`), optional `notes`  
   **Then** **201 Created** with envelope `{ data: CourtSittingDto, meta: { requestId } }`  
   **And** row persisted in **`court_sittings`** with org/case FKs, audit `court.sitting.created` (no notes/outcome in metadata)

2. **Given** `status=Upcoming` on create  
   **When** `scheduledAtUtc` is in the past  
   **Then** **422** (`CaseBusinessRuleException` — future date required for new Upcoming sittings)

2b. **Given** the Case `current_stage = TerminationExclusion`  
   **When** I `POST /api/v1/cases/{id}/court-sittings`  
   **Then** **422** (`CaseBusinessRuleException` — cannot add sittings to a terminal case)

2c. **Given** create body includes `status`  
   **When** value is not `Upcoming`, `Attended`, or `Postponed`  
   **Then** **400** with allowed values (no silent default to `Upcoming`)

2d. **Given** create body `status=Attended`  
   **When** `outcome` is missing or whitespace  
   **Then** **400**

2e. **Given** create body `status=Postponed`  
   **When** `scheduledAtUtc` is in the past  
   **Then** **201** allowed (backfill of postponed hearings)

3. **Given** I can read a Case  
   **When** I `GET /api/v1/cases/{id}/court-sittings`  
   **Then** **200 OK** with `{ data: { items: CourtSittingDto[] } }` ordered by `scheduledAtUtc` ascending, then `id`  
   **And** empty list returns `{ items: [] }`

4. **Given** I can read a Case  
   **When** I `GET /api/v1/cases/{id}/court-sittings/{sittingId}`  
   **Then** **200 OK** with `CourtSittingDto` or **404** if not found on case

5. **Given** I can read a Case  
   **When** I `PATCH /api/v1/cases/{id}/court-sittings/{sittingId}` with one or more fields (`status`, `scheduledAtUtc`, `courtName`, `purpose`, `notes`, `outcome`, `nextCourtAtUtc`)  
   **Then** **200 OK** with updated DTO  
   **And** audit `court.sitting.updated` (metadata: status only — no notes/outcome body)

6. **Given** `status` transitions to `Attended`  
   **When** PATCH succeeds  
   **Then** `outcome` is required (non-empty, max 2000)  
   **And** `nextCourtAtUtc` optional (if set, must be in the future)

7. **Given** `status` is `Postponed`  
   **When** PATCH includes new `scheduledAtUtc`  
   **Then** date updates and status may remain `Postponed` or caller may set `status=Upcoming` in same request  
   **And** past `scheduledAtUtc` rejected when resulting status is `Upcoming`

8. **Given** per-resource authorization (Story 2.8)  
   **When** field worker calls case nested endpoints on unassigned case  
   **Then** **403** with `Policies.ForbiddenByRoleMessage`  
   **And** supervisors can read/mutate on any org case

9. **Given** I am authenticated as **SocialWorker** or **CaseWorker** (`Policies.FieldWorker`)  
   **When** I `GET /api/v1/court-sittings/upcoming`  
   **Then** **200 OK** with `{ data: { items: CourtSittingScheduleItemDto[] }, meta: { requestId, totalCount } }`  
   **And** each item includes sitting fields, computed `isPastDue` (`true` when `status=Upcoming` and `scheduledAtUtc < now`), plus nested **`CaseSummaryDto`** via `CaseDtoMapper.ToCaseSummary(case, redactPocsoForFieldWorker: true)`  
   **And** only sittings where `cases.assigned_worker_id` = current user, same org, `status = Upcoming`, `cases.current_stage != TerminationExclusion`, ordered by `scheduledAtUtc` ascending  
   **And** includes past-due `Upcoming` rows for Command Strip overdue styling (Story 5.2)  
   **And** Coordinators/Directors receive **403** on this endpoint (use case nested list for supervisors)

10. **Given** validation failures (missing required fields, invalid enums, oversize strings)  
    **When** create/update  
    **Then** **400** Problem Details with allowed values where applicable

11. **Given** I am authenticated but **deactivated** (`IsActive = false`)  
    **When** I call any court sitting endpoint  
    **Then** **403** with `AuthService.DeactivatedMessage`

12. **Given** OpenAPI contract  
    **When** story ships  
    **Then** snapshot exported and `@midi-kaval/api-client` regenerated  
    **And** **no** web/mobile UI changes, **no** reminder/escalation jobs

## Tasks / Subtasks

- [x] **Schema & domain** (AC: 1)
  - [x] Enum `CourtSittingStatus` — `Upcoming`, `Attended`, `Postponed`
  - [x] Entity `CourtSitting` + EF config + migration `AddCourtSittings`
  - [x] Register `DbSet<CourtSitting>` on `AppDbContext`

- [x] **Service layer** (AC: 1–7, 10–11)
  - [x] `CourtSittingService` — create, list, get, update with `EnsureCanReadCase`
  - [x] `ListUpcomingForFieldWorkerAsync` — POCSO redaction, terminal case exclusion, `isPastDue`
  - [x] Validation: `courtName` (128), `purpose` (500), `notes` (4000), `outcome` (2000); terminal case guard on create
  - [x] Audit events without PII body fields

- [x] **API endpoints** (AC: 1–9, 11)
  - [x] `CasesController` — GET list, GET single, POST create, PATCH update under `/court-sittings`
  - [x] `CourtSittingsController` — `GET /api/v1/court-sittings/upcoming` (`Policies.FieldWorker`)
  - [x] DI registration in `Program.cs`

- [x] **Integration tests** (AC: 8–12)
  - [x] `CourtSittingsTests.cs` — RBAC, CRUD, validation, audit, ordering, upcoming filter, POCSO redaction, terminal case, deactivated 403
  - [x] Helpers on `CaseTestData` (`BuildCourtSittingRequest`, `CreateCourtSittingAsync`, `ListUpcomingCourtSittingsAsync`); update `UsersSchemaTests`

- [x] **OpenAPI + api-client** (AC: 12)
  - [x] Export snapshot via `SwaggerEndpointTests`
  - [x] `npm run generate:api-client` + `npm run build`

- [x] **Docs**
  - [x] README — court sitting endpoints + RBAC table rows

### Review Findings

- [x] [Review][Patch] Past-due upcoming test creates invalid sitting [`tests/api.integration/CourtSittingsTests.cs:356-362`]
- [x] [Review][Patch] Missing GET 404 test for unknown sittingId [`tests/api.integration/CourtSittingsTests.cs`]
- [x] [Review][Patch] Missing unassigned field worker create 403 test [`tests/api.integration/CourtSittingsTests.cs`]
- [x] [Review][Patch] Missing PATCH Upcoming + past scheduledAtUtc → 422 test (AC 7) [`tests/api.integration/CourtSittingsTests.cs`]
- [x] [Review][Patch] Missing deactivated user test on nested court-sitting endpoint (AC 11) [`tests/api.integration/CourtSittingsTests.cs`]

## Dev Notes

### READ FIRST

1. **Mirror Story 4.4 interventions** — nested routes on `CasesController`, service in `Infrastructure/Cases/`, DTOs in `Models/Cases/`, same `EnsureCanReadCase` RBAC.
2. **Separate from case notes** — `CaseNoteType.Court` is timeline logging; this story is structured court sittings (FR-15).
3. **No DELETE in v1** — create + update only (audit trail); epic "CRUD" = read + write without hard delete.
4. **Schedule assignee** — filter upcoming list via `cases.assigned_worker_id`; do **not** add `assigned_staff_user_id` on `court_sittings` unless product revises.
5. **Jobs deferred** — Story 5.3 reminder + Story 5.4 miss escalation; Story 5.2 wires UI + `CourtCountdownBanner` to upcoming API.
6. **UX-DR9 row shape** — DTO includes date, court name, status, `isPastDue` computed (`Upcoming` + `scheduledAtUtc < now`) for UI chips; no UI in this story.
7. **POCSO + terminal** — upcoming list uses `redactPocsoForFieldWorker: true`; exclude `TerminationExclusion` cases (mirror `VisitService` field-worker lists).

### Entity sketch (`court_sittings`)

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid PK | |
| `organisation_id` | uuid FK | |
| `case_id` | uuid FK | |
| `scheduled_at_utc` | timestamptz | Sitting date/time |
| `court_name` | varchar(128) | Required |
| `purpose` | varchar(500) | Required |
| `status` | enum | Default `Upcoming` |
| `notes` | varchar(4000) | Optional |
| `outcome` | varchar(2000) | Required when `Attended` |
| `next_court_at_utc` | timestamptz nullable | Optional on `Attended` |
| `created_by_user_id` | uuid | |
| `created_at_utc` / `updated_at_utc` | timestamptz | |

Index: `(organisation_id, case_id, scheduled_at_utc)`; `(organisation_id, status, scheduled_at_utc)` for upcoming query.

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Domain/Enums/CourtSittingStatus.cs` |
| NEW | `apps/api/Domain/Entities/CourtSitting.cs` |
| NEW | `apps/api/Infrastructure/Persistence/CourtSittingConfiguration.cs` |
| NEW | `apps/api/Infrastructure/Cases/CourtSittingService.cs` |
| NEW | `apps/api/Models/Cases/CourtSittingDtos.cs` |
| NEW | `apps/api/Controllers/V1/CourtSittingsController.cs` |
| NEW | `apps/api/Migrations/*_AddCourtSittings.cs` |
| NEW | `tests/api.integration/CourtSittingsTests.cs` |
| UPDATE | `AppDbContext.cs`, `AuditEventTypes.cs` (`CourtSittingCreated = "court.sitting.created"`, `CourtSittingUpdated = "court.sitting.updated"`), `CasesController.cs`, `Program.cs`, `CaseCreateTests.cs`, `UsersSchemaTests.cs`, `README.md`, `packages/api-client/` |

### Previous story intelligence (Epic 4.5)

- Integration tests use Testcontainers; invoke services directly when background jobs deferred.
- `UsersSchemaTests` must list every new table in expected list **and** TRUNCATE chain.
- Invalid enum on create must **400**, not silent default (`ParseRequiredOrDefaultStatus` pattern from interventions review).
- OpenAPI snapshot + api-client regen is mandatory gate; run `SwaggerEndpointTests` after controller changes.
- README RBAC table + narrative section per feature area (see interventions + notifications docs).

### Testing requirements

- Follow `InterventionsTests.cs` patterns: coordinator creates case + transfer, field worker mutates assigned case.
- Upcoming: two workers — only assignee sees their case's sittings; POCSO case shows initials only on upcoming list.
- Terminal case create → 422; invalid status on create → 400.
- Attended without outcome → 400 on PATCH and on POST when status=Attended.
- Unassigned field worker GET list → 403; director GET upcoming → 403; deactivated user → 403.
- Audit metadata excludes `notes`/`outcome` text.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 5, Story 5.1]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-15]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 API Court endpoints, §5.6 jobs (defer)]
- [Source: `_bmad-output/implementation-artifacts/4-4-interventions-crud-api.md` — API patterns]
- [Source: `_bmad-output/implementation-artifacts/3-1-visit-scheduler-api.md` — field-worker list + `CaseSummaryDto` nesting]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

- API + integration test projects build successfully.
- Integration tests require Docker/Testcontainers (not available in this session).
- OpenAPI exported via `EXPORT_OPENAPI_PATH`; api-client regenerated with `API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json`.

### Completion Notes List

- Implemented `court_sittings` schema, `CourtSittingService`, nested CRUD on `CasesController`, and `GET /api/v1/court-sittings/upcoming` for field workers.
- Validation covers status matrix, terminal case guard, Attended outcome, POCSO redaction on upcoming list, `isPastDue` + `meta.totalCount`.
- Audit events `court.sitting.created` / `court.sitting.updated` exclude notes/outcome body.
- 14 integration tests in `CourtSittingsTests.cs`; README + OpenAPI/api-client updated.

### File List

- apps/api/Domain/Enums/CourtSittingStatus.cs (new)
- apps/api/Domain/Entities/CourtSitting.cs (new)
- apps/api/Infrastructure/Persistence/CourtSittingConfiguration.cs (new)
- apps/api/Infrastructure/Cases/CourtSittingService.cs (new)
- apps/api/Models/Cases/CourtSittingDtos.cs (new)
- apps/api/Controllers/V1/CourtSittingsController.cs (new)
- apps/api/Migrations/20260619120253_AddCourtSittings.cs (new)
- apps/api/Migrations/20260619120253_AddCourtSittings.Designer.cs (new)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (modified)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (modified)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (modified)
- apps/api/Controllers/V1/CasesController.cs (modified)
- apps/api/Program.cs (modified)
- tests/api.integration/CourtSittingsTests.cs (new)
- tests/api.integration/CaseCreateTests.cs (modified)
- tests/api.integration/UsersSchemaTests.cs (modified)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- README.md (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)

## Change Log

- 2026-06-19: Story 5.1 created — court sitting CRUD API + upcoming schedule endpoint (FR-15).
- 2026-06-19: Validation — 9 fixes (POCSO redaction, terminal case guards, create status matrix, deactivated 403, isPastDue/totalCount).
- 2026-06-19: Implementation complete — API, tests, OpenAPI/api-client, README; status → review.
- 2026-06-19: Code review — 5 patch findings fixed (test gaps + past-due backdate); status → done.
