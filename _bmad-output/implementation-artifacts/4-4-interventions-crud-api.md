---
baseline_commit: NO_VCS
---

# Story 4.4: Interventions CRUD API

Status: done

## Story

As a **Case Worker**,
I want to track interventions needed and provided,
so that support actions are accountable (FR-14, CAP-6).

*Scope: **API only** — `interventions` schema, nested CRUD under cases, audit. No web/mobile UI (Story 4.5), no overdue Hangfire job (Story 4.5), no push notifications. **Do not** conflate `CaseNoteType.Intervention` timeline notes with the `interventions` table. Category stored as `category_name` varchar until Epic 9 Legends FK.*

## Acceptance Criteria

1. **Given** I can read a Case (`EnsureCanReadCase`)  
   **When** I `POST /api/v1/cases/{id}/interventions` with body including `direction` (`Needed`|`Provided`), `categoryName`, `description`, `priority` (`High`|`Medium`|`Low`), optional `status` (default `Open`), `assignedStaffUserId`, and direction-specific dates  
   **Then** **201 Created** with envelope `{ data: InterventionDto, meta: { requestId } }`  
   **And** row persisted in **`interventions`** with org/case FKs, audit `case.intervention.created` (no description/outcome in metadata)

2. **Given** `direction=Needed` and `status=Open`  
   **When** creating  
   **Then** `dueAtUtc` required and must be in the future (**422** if past)

3. **Given** `direction=Provided`  
   **When** creating  
   **Then** `providedAtUtc` required

4. **Given** I can read a Case  
   **When** I `GET /api/v1/cases/{id}/interventions`  
   **Then** **200 OK** with `{ data: { items: InterventionDto[] } }` ordered by `createdAtUtc` ascending  
   **And** empty list returns `{ items: [] }`

5. **Given** I can read a Case  
   **When** I `GET /api/v1/cases/{id}/interventions/{interventionId}`  
   **Then** **200 OK** with `InterventionDto` or **404** if not found on case

6. **Given** I can read a Case  
   **When** I `PATCH /api/v1/cases/{id}/interventions/{interventionId}` with at least one field  
   **Then** **200 OK** with updated DTO  
   **And** audit `case.intervention.updated` with status/priority metadata only

7. **Given** per-resource authorization (Story 2.8)  
   **When** field worker calls endpoints on unassigned or other worker's case  
   **Then** **403** with `Policies.ForbiddenByRoleMessage`  
   **And** supervisors can mutate on any org case

8. **Given** validation failures (missing fields, invalid enums, inactive assignee)  
   **When** create/update  
   **Then** **400** Problem Details with allowed values where applicable

9. **Given** OpenAPI contract  
   **When** story ships  
   **Then** snapshot exported and `@midi-kaval/api-client` regenerated  
   **And** **no** web/mobile changes, **no** overdue job

## Tasks / Subtasks

- [x] **Schema & domain** (AC: 1)
  - [x] Enums: `InterventionDirection`, `InterventionPriority`, `InterventionStatus`
  - [x] Entity `Intervention` + EF config + migration `AddInterventions`
  - [x] Register `DbSet<Intervention>` on `AppDbContext`

- [x] **Service layer** (AC: 1–6, 8)
  - [x] `InterventionService` — create, list, get, update with `EnsureCanReadCase`
  - [x] Validation: category (128), description (4000), outcome (2000), assignee active in org
  - [x] Audit events without PII body fields

- [x] **API endpoints** (AC: 1–6)
  - [x] `CasesController` — GET list, GET single, POST create, PATCH update
  - [x] DI registration in `Program.cs`

- [x] **Integration tests** (AC: 7–8)
  - [x] `InterventionsTests.cs` — RBAC, CRUD, validation, audit, ordering
  - [x] Helpers on `CaseTestData`; `UsersSchemaTests` table list updated

- [x] **OpenAPI + api-client** (AC: 9)
  - [x] Export snapshot via `SwaggerEndpointTests`
  - [x] `npm run generate:api-client` + `npm run build`

### Review Findings

- [x] [Review][Patch] Invalid `status` on create silently defaults to `Open` [`InterventionService.cs:41`] — `ParseStatus(request.Status) ?? Open` swallows bad values; AC8 requires **400** with allowed values (PATCH already rejects invalid status)
- [x] [Review][Patch] README missing interventions endpoints and RBAC table rows [`README.md`] — Story 4.1 documented notes; interventions CRUD should be listed for API consumers
- [x] [Review][Patch] No test for inactive `assignedStaffUserId` [`InterventionsTests.cs`] — validation exists in service (AC8) but untested
- [x] [Review][Patch] No test for unassigned field worker `GET` list **403** [`InterventionsTests.cs`] — POST 403 covered; parity gap vs `CaseNotesTests.FieldWorker_UnassignedCase_Returns403`
- [x] [Review][Defer] `category_name` varchar instead of Legends FK [`Intervention.cs`] — deferred, story documents Epic 9 migration; matches offence-type pre-legends pattern from Story 2.1

## Dev Notes

### READ FIRST

1. **Separate from case notes** — `CaseNoteType.Intervention` is timeline logging; this story is structured interventions (CAP-6).
2. **Legends interim** — `category_name` varchar(128) until Epic 9 `legend_intervention_categories` FK.
3. **Overdue job deferred** — Story 4.5 adds daily job + push; this API exposes `dueAtUtc` + `status=Open` for that job.
4. **RBAC** — same `EnsureCanReadCase` as notes/attachments.
5. **No DELETE** in v1 — create + update only.

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Domain/Enums/Intervention*.cs` |
| NEW | `apps/api/Domain/Entities/Intervention.cs` |
| NEW | `apps/api/Infrastructure/Persistence/InterventionConfiguration.cs` |
| NEW | `apps/api/Infrastructure/Cases/InterventionService.cs` |
| NEW | `apps/api/Models/Cases/InterventionDtos.cs` |
| NEW | `apps/api/Migrations/*_AddInterventions.cs` |
| NEW | `tests/api.integration/InterventionsTests.cs` |
| UPDATE | `AppDbContext.cs`, `AuditEventTypes.cs`, `CasesController.cs`, `Program.cs`, `CaseCreateTests.cs`, `UsersSchemaTests.cs`, `packages/api-client/` |

## Dev Agent Record

### Agent Model Used

Composer

### Completion Notes List

- Added `interventions` table with direction Needed/Provided, categoryName (legends interim), priority, status, due/provided dates, outcome, assignee.
- `InterventionService` + nested routes on `CasesController`; audit `case.intervention.created` / `case.intervention.updated`.
- 11 integration tests in `InterventionsTests.cs`; OpenAPI snapshot + api-client regenerated.
- **Tests:** 281/283 integration pass; 2 pre-existing `VisitGroupingTests` failures (POCSO seed pollution) — unrelated.
- **Code review (2026-06-19):** 4 patches — invalid status on create returns 400, README interventions docs, inactive assignee + unassigned GET 403 tests. 14/14 `InterventionsTests` pass.

### File List

- apps/api/Domain/Enums/InterventionDirection.cs
- apps/api/Domain/Enums/InterventionPriority.cs
- apps/api/Domain/Enums/InterventionStatus.cs
- apps/api/Domain/Entities/Intervention.cs
- apps/api/Infrastructure/Persistence/InterventionConfiguration.cs
- apps/api/Infrastructure/Cases/InterventionService.cs
- apps/api/Models/Cases/InterventionDtos.cs
- apps/api/Migrations/*_AddInterventions.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Controllers/V1/CasesController.cs
- apps/api/Program.cs
- tests/api.integration/InterventionsTests.cs
- tests/api.integration/CaseCreateTests.cs
- tests/api.integration/UsersSchemaTests.cs
- README.md
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- packages/api-client/dist/

## Change Log

- 2026-06-19: Story 4.4 — Interventions CRUD API (FR-14). Schema, service, endpoints, tests, api-client regen.
- 2026-06-19: Code review — 4 patch findings fixed; 1 deferred (Legends FK to Epic 9).
