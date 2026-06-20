---
baseline_commit: NO_VCS
---

# Story 2.8: Case Assignment Transfer and Handoff Whisper

Status: done

<!-- Validated: 2026-06-16 — see 2-8-case-assignment-transfer-and-handoff-whisper-validation-report.md (9 fixes applied); code review 2026-06-16 (8 patches applied) -->

## Story

As a **Social Worker receiving a transferred Case**,
I want a compressed handoff summary,
so that I start visits without reading full history (FR-7, UX-DR4, CAP-15).

*Note: This story delivers **assignment transfer API**, **per-case read authorization** for field workers, **handoff whisper on mobile case detail** (web component for tests/Epic 3 reuse), and **staff filter upgrade** on supervisor search. Coordinator transfer UI is **minimal** (detail placeholder form); full registry/detail IA remains Story 2.9. Email/push on new assignment is Epic 7 (FR-19). Staff bridge to prior handler is v1.1 (FR-28). Notes timeline behind "View full timeline" is Epic 4 (FR-13).*

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `POST /api/v1/cases/{id}/transfer` with body `{ assigneeUserId, priorActions, openItems, nextVisitPurpose }`  
   **Then** response is **200 OK** with envelope `{ data: CaseDetailDto }`  
   **And** `cases.assigned_worker_id` and `cases.assigned_at_utc` are updated to the assignee and transfer timestamp (UTC)  
   **And** a row is inserted in `case_assignments` preserving `from_worker_id` (previous assignee, null on first transfer), `to_worker_id`, the three handoff lines, `created_by_user_id`, and `created_at_utc`  
   **And** `audit_events` records `case.transferred` in the **same transaction** with metadata `{ caseId, fromWorkerId, toWorkerId }`  
   **And** all three handoff fields are required, trimmed, max **500** characters each — empty/whitespace → **400** Problem Details  
   **And** `assigneeUserId` must reference an **active** user in my organisation with role **SocialWorker** or **CaseWorker** — otherwise **422** Problem Details  
   **And** transferring to the **current** assignee → **422** ("Case is already assigned to this worker.")  
   **And** case id not found in organisation → **404** Problem Details

2. **Given** a Case exists in my organisation  
   **When** I `GET /api/v1/cases/{id}`  
   **Then** response is **200 OK** with `{ data: CaseDetailDto }` including core case summary fields (`id`, identifiers, beneficiary, stage, visit count, `assignedWorkerUserId`, `assignedAtUtc`, `nextVisitDueAtUtc`, timestamps)  
   **And** when the **authenticated user is the current assignee** and transfer is within the **7-day visibility window** (transfer day = day 1; days 1–7 visible; hidden from day 8 — see **Handoff 7-day window** in Dev Notes), `data.handoffWhisper` is populated with `{ priorActions, openItems, nextVisitPurpose, transferredAtUtc }`  
   **And** when the transfer is **older than 7 days** (day 8+), `handoffWhisper` is **null** (whisper hidden)  
   **And** when the viewer is **not** the current assignee (e.g. Coordinator viewing), `handoffWhisper` is **null** — supervisors see assignment metadata but not the whisper UX treatment  
   **And** when the Case is not found in my organisation → **404**  
   **And** read is **not** audited (same as search)

3. **Given** per-resource authorization (Epic 1.8 deferred item)  
   **When** I `GET /api/v1/cases/{id}` or `GET /api/v1/cases/assigned`  
   **Then** **Coordinator** or **Director** may read **any** case in their organisation  
   **And** **SocialWorker** or **CaseWorker** may read only cases where `assigned_worker_id` equals their user id  
   **And** field worker requesting another worker's case → **403** with `Policies.ForbiddenByRoleMessage`  
   **And** unauthenticated → **401**; deactivated → **403** with `AuthService.DeactivatedMessage`

4. **Given** I am a **field worker** with assigned cases  
   **When** I `GET /api/v1/cases/assigned?page=&pageSize=`  
   **Then** **200 OK** with `{ data: { items: CaseSummaryDto[], page, pageSize }, meta: { requestId, totalCount } }`  
   **And** items are limited to cases where `assigned_worker_id` = me, ordered by `assigned_at_utc` descending (null assignees excluded)  
   **And** **Coordinator/Director** calling this endpoint → **403** (supervisors use search)  
   **And** pagination defaults match search (`page` 1, `pageSize` 25, max 100)

5. **Given** supervisor search staff filter upgrade (Story 2.6 pilot used `createdByUserId`)  
   **When** I `GET /api/v1/cases/search` with `assignedWorkerUserId` (UUID)  
   **Then** results filter on `cases.assigned_worker_id`  
   **And** when `assignedWorkerUserId` is supplied, `createdByUserId` filter is **ignored** if both are present (document precedence)  
   **And** `createdByUserId` continues to filter `cases.created_by_user_id` when `assignedWorkerUserId` is absent (backward compatible for presets)  
   **And** `CaseSearchFiltersDto` / saved presets support `assignedWorkerUserId` (camelCase JSON) for assignee filter persistence  
   **And** OpenAPI documents the new query param

6. **Given** handoff whisper presentation (FR-7, UX-DR4)  
   **When** the **receiving assignee** (field worker) opens case detail on **mobile** (`CaseDetailPlaceholder` route) within the 7-day window  
   **Then** the screen fetches `GET /api/v1/cases/{id}` via `CaseApiService` (not route params alone)  
   **And** **Handoff Whisper** renders **exactly three lines** — one per field with fixed labels (`Prior actions:`, `Open items:`, `Next visit:`); single-line with ellipsis overflow (no multi-line wrap per field) using `handoff-whisper` design tokens (`#EFF8FF` background, `3px` left border `#175CD3`) [Source: DESIGN.md]  
   **And** a **"View full timeline"** text button/link is visible below the whisper — navigates to a placeholder or shows inline "Notes timeline coming soon" (Epic 4); must not 404  
   **And** when `handoffWhisper` is null (day 8+, or not assignee), the whisper block is **not** rendered  
   **And** loading and error states follow existing case feature patterns (`extractErrorMessage`, back navigation)  
   **When** a **Coordinator** opens web case detail (`/cases/:id`, `supervisorGuard` — field workers are mobile-only per UX-DR14)  
   **Then** detail fetches `GET /api/v1/cases/{id}` via `CaseApiService`; whisper block is **not** shown (API returns null for non-assignee per AC2)  
   **And** reusable `handoff-whisper` component still exists on web for component tests and Epic 3 Command Strip reuse

7. **Given** I am a **Coordinator** on web case detail  
   **When** I use the minimal **Transfer case** form  
   **Then** I can select an assignee from `GET /api/v1/users/field-workers` (CoordinatorOrAbove; returns active SocialWorker/CaseWorker users in org as `{ id, email, role }`) and enter three handoff lines, then submit `POST .../transfer`  
   **And** on success the detail refreshes and shows updated assignee (whisper hidden for coordinator viewer per AC2)  
   **And** form disabled while submitting; errors surfaced via `errorMessage` signal

8. **Given** I am a **field worker** on mobile **Cases** tab  
   **When** the list loads  
   **Then** `GET /api/v1/cases/assigned` populates a simple list (crime/ST, stage, overdue indicator if `nextVisitDueAtUtc` past)  
   **And** row tap opens case detail with handoff whisper per AC6  
   **And** pull-to-refresh re-fetches assigned list (UX-DR10 pattern for Cases tab)

9. **Given** OpenAPI and client contract  
   **When** this story ships  
   **Then** OpenAPI documents `GET /cases/{id}`, `POST /cases/{id}/transfer`, `GET /cases/assigned`, `GET /users/field-workers`, and `assignedWorkerUserId` search param  
   **And** `packages/api-client` regenerated  
   **And** README documents transfer endpoint, field-workers list, handoff 7-day rule, and field-worker case access model

10. **Given** test baseline after Story 2.7 (**160** .NET integration, **46** web, **22** mobile)  
    **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
    **Then** all existing tests pass  
    **And** new integration tests cover: coordinator transfer 200 + DB assignment + audit + case_assignments row; transfer unknown case 404; first transfer `from_worker_id` null; re-transfer updates assignee and records new assignment row; GET detail assignee sees whisper within 7 days; whisper null on day 8 (backdate `assigned_at_utc` via direct DB update in test); coordinator GET detail no whisper; director GET detail 200; SocialWorker assigned list 200, detail whisper, other case 403; **CaseWorker parity** — assigned list 200, detail whisper when assignee, other case 403, transfer 403; field worker unassigned case (`assigned_worker_id` null) 403; coordinator GET assigned 403; supervisor search by `assignedWorkerUserId`; **preset save/load with `assignedWorkerUserId`**; export respects `assignedWorkerUserId` via shared filters; invalid assignee role 422; same assignee 422; missing handoff field 400; deactivated 403; unauthenticated 401; Swagger paths present (including `/api/v1/users/field-workers`)  
    **And** new web tests: handoff whisper visible for assignee mock response; hidden when null; transfer form submit  
    **And** new mobile tests: assigned list render; detail whisper visible  
    **Verified baseline to beat:** **160** .NET integration, **46** web, **22** mobile

11. **Given** Stories 2.9+ are not yet implemented  
    **When** this story ships  
    **Then** **no** full sidebar IA, rich stage-edit UI, or crisis queue (2.9 / Epic 8)  
    **And** **no** email/push on assignment (Epic 7)  
    **And** **no** staff bridge / experience brief (v1.1)  
    **And** **no** notes timeline content (Epic 4) — "View full timeline" is placeholder only

## Tasks / Subtasks

- [x] **Domain — assignment model** (AC: 1, 2)
  - [x] Add `AssignedWorkerId`, `AssignedAtUtc` to `Domain/Entities/Case.cs`
  - [x] `Domain/Entities/CaseAssignment.cs` — `Id`, `CaseId`, `OrganisationId`, `FromWorkerId?`, `ToWorkerId`, `PriorActions`, `OpenItems`, `NextVisitPurpose`, `CreatedByUserId`, `CreatedAtUtc`
  - [x] `Infrastructure/Persistence/CaseAssignmentConfiguration.cs` — table `case_assignments`, snake_case, FK indexes on `(organisation_id, case_id)` and `(organisation_id, to_worker_id)`
  - [x] Update `CaseConfiguration` — nullable FK `assigned_worker_id` → `users`, index `(organisation_id, assigned_worker_id)`
  - [x] EF migration `AddCaseAssignments` — add columns + `case_assignments` table only

- [x] **API — DTOs** (AC: 1, 2, 4, 5)
  - [x] `TransferCaseRequest`, `HandoffWhisperDto`, `CaseDetailDto` (summary fields + `nextVisitDueAtUtc` + optional whisper)
  - [x] Extend `CaseSummaryDto` with nullable `assignedWorkerUserId`, `assignedAtUtc` (required for assigned list + registry)
  - [x] Extend `CaseSearchQuery` with `AssignedWorkerUserId`; add `AssignedWorkerUserId` to `CaseSearchFiltersDto` (preset JSON)
  - [x] `FieldWorkerUserDto` for field-workers list endpoint

- [x] **API — CaseService methods** (AC: 1–5)
  - [x] `TransferAsync(caseId, request)` — validate assignee role/active/org; insert assignment; update case; audit `case.transferred`
  - [x] `GetDetailAsync(caseId)` — org scope; per-resource auth; compose whisper when assignee + ≤7 days; load handoff text from **latest** `case_assignments` row where `to_worker_id == cases.assigned_worker_id` (never from `cases` columns)
  - [x] `ListAssignedAsync(page, pageSize)` — field worker only; filter `assigned_worker_id == actor`
  - [x] Update `ApplySearchFilters` — `assignedWorkerUserId` param; precedence over `createdByUserId`
  - [x] Extend `AuditEventTypes` with `CaseTransferred = "case.transferred"`

- [x] **API — controller routes** (AC: 1–5, 9)
  - [x] `GET {id:guid}` — `[Authorize]` only (default authenticated + active-user policy); **do not** use `CoordinatorOrAbove` — per-resource check in `GetDetailAsync`
  - [x] `POST {id:guid}/transfer` — `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] `GET assigned` — register **before** `{id:guid}`; `[Authorize(Policy = Policies.FieldWorker)]`
  - [x] **NEW** `Controllers/V1/UsersController.cs` — `GET field-workers` at `/api/v1/users/field-workers`; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] Add `CaseForbiddenException` in `CaseService.cs` (alongside `CaseNotFoundException`); map to **403** in controller
  - [x] `[ProducesResponseType]` for 200, 400, 401, 403, 404, 422

- [x] **API — integration tests** (AC: 10)
  - [x] `tests/api.integration/CaseAssignmentTests.cs` — `[Collection("AuthIntegration")]`
  - [x] Extend `CaseTestData` — `TransferCaseAsync`, `GetCaseDetailAsync`, `ListAssignedCasesAsync` helpers
  - [x] `SwaggerEndpointTests` — new paths

- [x] **OpenAPI + api-client** (AC: 9)
  - [x] Export snapshot; `npm run generate:api-client`

- [x] **Web — detail + whisper + transfer** (AC: 6, 7, 10)
  - [x] `case-api.service.ts` — `getCaseDetail`, `transferCase`
  - [x] `handoff-whisper.component` — standalone; exactly three single-line labeled rows; ellipsis overflow; timeline placeholder link
  - [x] Update `case-detail-placeholder.component` — fetch on init, whisper, coordinator transfer form
  - [x] SCSS uses semantic tokens / CSS vars aligned with DESIGN.md `handoff-whisper`
  - [x] Specs for whisper visibility and transfer submit

- [x] **Mobile — assigned list + detail whisper** (AC: 6, 8, 10)
  - [x] `CaseApiService.ts` — `getCaseDetail`, `listAssignedCases`, `transferCase` (transfer optional on mobile — coordinator uses web; omit mobile transfer UI)
  - [x] Update `CasesListScreen` — assigned cases list + navigation to detail
  - [x] Update `CaseDetailPlaceholderScreen` — API fetch + whisper block (three labeled single lines, mockup colors)
  - [x] Jest tests for list and whisper

- [x] **Documentation** (AC: 9)
  - [x] README — transfer, assigned list, handoff window, RBAC matrix

## Dev Notes

### Per-resource auth + 7-day window (READ FIRST)

1. **`GET /cases/{id}`** must use `[Authorize]` (authenticated active user), not `CoordinatorOrAbove`. Authorization lives in `CaseService.GetDetailAsync`: supervisors read any org case; field workers only when `assigned_worker_id == actorUserId`; else throw `CaseForbiddenException` → 403.
2. **7-day whisper window** (epics: visible days 1–7, hidden from day 8; transfer day = day 1):

```csharp
// UTC calendar-day math — do NOT use UtcNow.Date.AddDays(-7) on assignedAt (off-by-one on day 8)
var showWhisper = assignedAtUtc.HasValue
    && (DateTime.UtcNow.Date - assignedAtUtc.Value.Date).Days < 7;
```

3. **Web routes** (`supervisorGuard` on `/cases/:id`) — assignees are field workers (mobile-only, UX-DR14). Web builds whisper component for tests/reuse; production whisper UX is **mobile assignee**.

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Stories 2.1–2.7 delivered schema, lifecycle, duplicate prevention, merge, search/presets/registry, export. **Story 2.8** adds **worker assignment transfer with CAP-15 handoff** and **field-worker case access**. Story 2.9 delivers full web sidebar IA, rich detail, and stage edit.

### Handoff 7-day window (critical)

- Transfer day = **day 1**; whisper visible through **day 7**; **hidden from day 8** (epics AC).
- Use `(UtcNow.Date - assignedAtUtc.Date).Days < 7` — see READ FIRST (avoid `assignedAtUtc >= UtcNow.Date.AddDays(-7)`, which shows an extra day).
- On **re-transfer**, reset `assigned_at_utc` on `cases`; load whisper text from the **latest** `case_assignments` row where `to_worker_id == assigned_worker_id` (order by `created_at_utc` desc). **Never** store handoff text on `cases`.

### Per-resource authorization (READ FIRST)

Today **all** case endpoints use `CoordinatorOrAbove` except none for field workers. This story introduces **first per-resource case access**:

| Endpoint | Coordinator/Director | Field worker |
|----------|---------------------|--------------|
| `GET /cases/{id}` | Any org case | Assigned only |
| `GET /cases/assigned` | 403 | Own assignments |
| `POST /cases/{id}/transfer` | Yes | 403 |
| `GET /cases/search` | Yes | 403 (unchanged) |

Implement authorization in `CaseService` after resolving actor — throw `CaseForbiddenException` → 403 (add sealed exception class next to `CaseNotFoundException`). Do **not** rely on UI hiding.

### Schema design

```
cases
  + assigned_worker_id  uuid NULL FK users
  + assigned_at_utc     timestamptz NULL

case_assignments (append-only history)
  id, case_id, organisation_id,
  from_worker_id (nullable),
  to_worker_id,
  prior_actions, open_items, next_visit_purpose (varchar 500),
  created_by_user_id, created_at_utc
```

- **Do not** hard-delete assignment history.
- Initial case create (2.1) leaves `assigned_worker_id` null until first transfer.
- `CaseSummaryDto` / search / assigned list: add nullable `assignedWorkerUserId`, `assignedAtUtc` in this story (required for assigned list and search filter display).

### Transfer API contract

`POST /api/v1/cases/{id}/transfer`

```json
{
  "assigneeUserId": "uuid",
  "priorActions": "Completed intake home visit; referred to vocational centre.",
  "openItems": "Pending school enrollment letter from family.",
  "nextVisitPurpose": "First follow-up home visit — verify enrollment."
}
```

**Success:** `200` — `{ data: CaseDetailDto }`

### Case detail DTO

```json
{
  "id": "uuid",
  "crimeNumber": "...",
  "stNumber": "...",
  "beneficiaryName": "...",
  "currentStage": "MaintainAndDevelopment",
  "visitCount": 2,
  "assignedWorkerUserId": "uuid",
  "assignedAtUtc": "2026-06-10T10:00:00Z",
  "nextVisitDueAtUtc": "2026-06-12T00:00:00Z",
  "createdAtUtc": "...",
  "updatedAtUtc": "...",
  "handoffWhisper": {
    "priorActions": "...",
    "openItems": "...",
    "nextVisitPurpose": "...",
    "transferredAtUtc": "2026-06-10T10:00:00Z"
  }
}
```

`handoffWhisper` omitted or null when not applicable.

### Route ordering (critical)

Register static segments before `{id:guid}`:

```
GET  /api/v1/cases/search/export     (existing)
GET  /api/v1/cases/search            (existing)
GET  /api/v1/cases/search-presets    (existing)
GET  /api/v1/cases/assigned          ← NEW (before {id})
GET  /api/v1/users/field-workers     ← NEW (`UsersController.cs`)
POST /api/v1/cases                   (existing)
GET  /api/v1/cases/{id}              ← NEW ([Authorize] only)
POST /api/v1/cases/{id}/transfer     ← NEW
PATCH /api/v1/cases/{id}/stage       (existing)
POST /api/v1/cases/{id}/merge        (existing)
```

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `Case` entity | No assignee fields | Add `AssignedWorkerId`, `AssignedAtUtc` |
| `CasesController` | No GET by id, no transfer | Add `GET {id}`, `POST {id}/transfer`, `GET assigned` |
| `UsersController` | Does not exist | **NEW** `GET field-workers` |
| `CaseService` | CRUD, search, export, merge, stage | Add transfer, detail, assigned list; search filter upgrade |
| `CaseSearchQuery` | `CreatedByUserId` only | Add `AssignedWorkerUserId` |
| `case-detail-placeholder` (web) | Router state only | API fetch + whisper + transfer form |
| `CaseDetailPlaceholderScreen` | Route params only | API fetch + whisper |
| `CasesListScreen` | Static placeholder | Assigned cases list |
| `AuditEventTypes` | Through `case.merged` | Add `case.transferred` |
| Mobile `CaseApiService` | create, duplicate, merge | get detail, list assigned |

**Do not break:**
- Search, export, presets, create, merge, stage flows and tests
- `CoordinatorOrAbove` on supervisor-only endpoints
- Registry export buttons and filter semantics (except additive `assignedWorkerUserId`)
- Placeholder routes `/cases/:id` — enhance, do not remove

### Previous story intelligence (2.7)

- **Shared filters** in `ApplySearchFilters` — add assignee filter in same method, do not fork
- **Test baseline:** **160** .NET integration, **46** web, **22** mobile
- **File download** endpoints unaffected
- Export columns deferred assignee — can add `Assigned Worker` column optionally; not required for 2.8 AC

### Previous story intelligence (2.6)

- **`CaseSummaryDto`** for list rows — extend with `assignedWorkerUserId`, `assignedAtUtc` if useful
- **Field worker search 403** — unchanged; assigned list is separate endpoint
- **Pagination envelope** — `ApiMeta.TotalCount` on assigned list
- **ILIKE / filter patterns** — follow same validation exceptions

### Previous story intelligence (2.1)

- **Audit in same transaction** as case mutation — mirror `CreateAsync` / `TransitionStageAsync` pattern (direct `db.AuditEvents.Add`, single `SaveChangesAsync`)
- **`assigned_worker_id` was explicitly deferred** to this story

### Architecture compliance

- Case aggregate children include **assignments** [Source: architecture.md §5.1]
- **Policy-based authorization** + server-side per-resource checks [Source: project-context.md]
- **Business logic in `CaseService`** — not controller [Source: project-context.md]
- **Handoff whisper ≤7 days** domain rule [Source: project-context.md]
- **No hand-edit** `packages/api-client` generated files
- **UUID** identifiers, **ISO 8601 UTC**, **Problem Details** errors

### UX compliance

- **Handoff Whisper** — max 3 lines + "View full timeline" [Source: UX-DR4, EXPERIENCE.md]
- **Design tokens** — `handoff-whisper`: `#EFF8FF` bg, `3px solid #175CD3` left border [Source: DESIGN.md]
- **Command strip mockup** shows inline whisper on expanded visit card [Source: mockups/command-strip-today.html] — Epic 3 will consume same DTO; build reusable whisper presentation
- **Operational tone** — no gamification [Source: EXPERIENCE.md]
- **Mobile Cases tab** pull-to-refresh [Source: UX-DR10]

### Staff assignee picker (pilot)

Epic 9 Story 9.2 delivers full staff directory CRUD. This story adds minimal **`GET /api/v1/users/field-workers`** (CoordinatorOrAbove) — query `users` where `organisation_id` matches, `is_active`, role in (`SocialWorker`, `CaseWorker`); return `{ id, email, role }` sorted by email. No create/update/delete.

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 160 existing + assignment tests |
| Web | `npm run test:web` | 46 existing + whisper/transfer specs |
| Mobile | `npm run test:mobile` | 22 existing + assigned/whisper specs |

**During development:**

```bash
npm run test:api:cases
npm run test:web
npm run test:mobile
```

**Integration test matrix (minimum):**

| Scenario | Status |
|----------|--------|
| Coordinator transfer 200 + case + assignment + audit | 200 |
| GET detail assignee day 1 — whisper present | 200 |
| GET detail assignee day 8 — whisper null | 200 |
| GET detail coordinator — whisper null | 200 |
| Field worker other case GET | 403 |
| GET assigned list field worker | 200 |
| Coordinator GET assigned | 403 |
| Search `assignedWorkerUserId` | 200 |
| Export with `assignedWorkerUserId` | 200 + filtered rows |
| Transfer unknown case | 404 |
| Field worker POST transfer | 403 |
| Director GET detail | 200 |
| Assignee inactive / wrong role | 422 |
| Same assignee transfer | 422 |
| Missing handoff line | 400 |
| Deactivated transfer | 403 |
| Unauthenticated | 401 |
| CaseWorker assigned list + detail + 403 parity | 200 / 403 |
| Preset with `assignedWorkerUserId` save/load | 200 |
| Swagger `/api/v1/users/field-workers` | pass |

### Scope boundaries

| In scope (2.8) | Out of scope |
|----------------|--------------|
| Transfer API + assignment schema | Email/push assignment notification (Epic 7) |
| Handoff whisper on mobile detail (+ web component for tests) | Full notes timeline (Epic 4) |
| Field worker assigned list + detail read | Field worker search/registry (2.9) |
| `assignedWorkerUserId` search + export filter | Staff bridge / FR-28 (v1.1) |
| `GET /users/field-workers` picker | Full staff directory CRUD (Epic 9.2) |
| Minimal coordinator transfer form (web) | Full sidebar IA (2.9) |
| Per-resource case read auth | Crisis queue handoff rows (Epic 8) |

### Definition of Done

- [ ] Transfer persists assignment + handoff history + audit
- [ ] Assignee sees whisper ≤7 days on **mobile** case detail; web `handoff-whisper` component covered by unit tests (coordinator detail does not show whisper per AC2)
- [ ] Field workers can list and open only assigned cases
- [ ] Search supports `assignedWorkerUserId`
- [ ] OpenAPI + api-client + README updated
- [ ] All test suites green above baseline

### OpenAPI regeneration (Windows)

```text
set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json
dotnet test tests/api.integration --filter "FullyQualifiedName~Assignment_Swagger_WhenRequested"
set API_OPENAPI_FILE=c:\Users\Admin\source\repos\Midi-Kaval\packages\api-client\openapi-snapshot.json
npm run generate:api-client
npm run build -w @midi-kaval/api-client
```

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.8, FR-7]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-7]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — Handoff CAP-15]
- [Source: `_bmad-output/specs/spec-kaval-online/SPEC.md` — CAP-15]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — handoff-whisper tokens]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Handoff Whisper pattern]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/command-strip-today.html`]
- [Source: `_bmad-output/project-context.md` — RBAC, audit, handoff rule]
- [Source: `_bmad-output/implementation-artifacts/2-7-case-export-to-excel-and-pdf.md` — test baseline, scope boundaries]
- [Source: `_bmad-output/implementation-artifacts/2-6-case-search-filters-and-saved-presets.md` — staff filter upgrade]
- [Source: `_bmad-output/implementation-artifacts/2-1-case-aggregate-schema-and-create-api.md` — deferred assigned_worker_id]
- [Source: `apps/api/Domain/Entities/Case.cs` — current entity]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — extend patterns]
- [Source: `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.ts`]
- [Source: `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx`]
- [Source: `apps/web/src/app/app.routes.ts` — `supervisorGuard` on `/cases/:id`]
- [Source: `apps/web/src/app/core/auth/supervisor.guard.ts` — mobile-only roles → `/mobile-only`]

### Review Findings

- [x] [Review][Patch] 7-day whisper window off-by-one — `UtcNow.Date.AddDays(-7)` shows whisper on day 8; fixed to `(UtcNow.Date - assignedAtUtc.Date).Days < 7`
- [x] [Review][Patch] `GET /cases/{id}` must not use `CoordinatorOrAbove` — field workers need access; use `[Authorize]` + `CaseForbiddenException` in service
- [x] [Review][Patch] `CaseForbiddenException` does not exist in codebase — add sealed exception next to `CaseNotFoundException`
- [x] [Review][Patch] AC6 implied field-worker web detail — `supervisorGuard` blocks mobile-only roles on `/cases/:id`; split AC6 mobile assignee vs web coordinator
- [x] [Review][Patch] Assignee picker ambiguous — standardized on `GET /api/v1/users/field-workers`
- [x] [Review][Patch] Transfer 404 not in AC — added unknown case 404
- [x] [Review][Patch] Export should inherit `assignedWorkerUserId` via shared `ApplySearchFilters` — added test matrix row
- [x] [Review][Patch] `CaseSummaryDto` assignee fields marked optional — required for assigned list
- [x] [Review][Patch] Integration matrix gaps — director GET detail, field worker transfer 403, unassigned case 403

- [x] [Review][Defer] Transfer on `TerminationExclusion` cases — pilot allows transfer at any stage; no stage gate in AC1 unless product adds later
- [x] [Review][Patch] Definition of Done contradicts AC6 — DoD updated: mobile assignee whisper; web component tests only
- [x] [Review][Patch] AC9 OpenAPI path list incomplete — added `GET /users/field-workers`
- [x] [Review][Patch] `CaseDetailDto` missing `nextVisitDueAtUtc` — added to AC2 and JSON example
- [x] [Review][Patch] `CaseSearchFiltersDto` missing `assignedWorkerUserId` — added to AC5, tasks, and preset test
- [x] [Review][Patch] `UsersController` does not exist — pinned to new `Controllers/V1/UsersController.cs`
- [x] [Review][Patch] AC6 "max 3 lines" ambiguity — exactly three single-line labeled rows with ellipsis
- [x] [Review][Patch] CaseWorker test parity — explicit CaseWorker paths in AC10 and test matrix
- [x] [Review][Patch] Whisper text source rule — `GetDetailAsync` loads from latest `case_assignments` row only
- [x] [Review][Defer] Mobile pull-to-refresh first use — AC8 requires RefreshControl on Cases tab; no existing mobile pull-to-refresh pattern in repo [`apps/mobile/src/screens/cases/CasesListScreen.tsx`] — deferred, acceptable greenfield within story

- [x] [Review][Patch] Web detail shows assignee UUID instead of email — `assignedWorkerLabel()` returns `assignedWorkerUserId` raw; `fieldWorkers` signal already loaded for transfer picker [`case-detail-placeholder.component.ts:67-70`]
- [x] [Review][Patch] Field workers load failure leaves empty assignee picker with no error — `loadFieldWorkers()` swallows errors and sets `[]`; coordinator cannot transfer with no feedback [`case-detail-placeholder.component.ts:100-106`]
- [x] [Review][Defer] Web registry assignee filter and preset round-trip for `assignedWorkerUserId` — `buildFiltersDto` / `applyFiltersFromDto` omit assignee; no registry UI (same gap as `createdByUserId` from 2.6); API + integration preset tests satisfy AC5 [`case-registry.component.ts:240-257`] — deferred, Story 2.9 full registry IA
- [x] [Review][Defer] `case_assignments` table lacks FK constraints to `cases` / `users` — migration creates PK + indexes only; integrity relies on `CaseService` [`CaseAssignmentConfiguration.cs`, `20260615204455_AddCaseAssignments.cs`] — deferred, pilot pattern aligned with 2-1 org FK deferral

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Implemented assignment transfer API with `case_assignments` history, per-resource case read, field-worker assigned list, and 7-day handoff whisper window.
- Added `UsersController` field-workers endpoint; extended search/export/presets with `assignedWorkerUserId`.
- Web: `handoff-whisper` component, detail fetch + coordinator transfer form. Mobile: assigned list with pull-to-refresh, detail API fetch + whisper.
- Tests: **181** .NET integration (+21), **52** web (+6), **24** mobile (+2). OpenAPI snapshot and `@midi-kaval/api-client` regenerated.

### File List

- `apps/api/Domain/Entities/Case.cs`
- `apps/api/Domain/Entities/CaseAssignment.cs`
- `apps/api/Infrastructure/Persistence/CaseAssignmentConfiguration.cs`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`
- `apps/api/Infrastructure/Persistence/AppDbContext.cs`
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs`
- `apps/api/Infrastructure/Cases/CaseService.cs`
- `apps/api/Infrastructure/Users/UserQueryService.cs`
- `apps/api/Models/Cases/CaseDtos.cs`
- `apps/api/Models/Cases/CaseSearchQuery.cs`
- `apps/api/Controllers/V1/CasesController.cs`
- `apps/api/Controllers/V1/UsersController.cs`
- `apps/api/Program.cs`
- `apps/api/Migrations/20260615204455_AddCaseAssignments.cs`
- `apps/api/Migrations/20260615204455_AddCaseAssignments.Designer.cs`
- `tests/api.integration/CaseAssignmentTests.cs`
- `tests/api.integration/CaseCreateTests.cs` (CaseTestData helpers)
- `tests/api.integration/SwaggerEndpointTests.cs`
- `tests/api.integration/UsersSchemaTests.cs`
- `packages/api-client/openapi-snapshot.json`
- `packages/api-client/src/generated/api.ts`
- `apps/web/src/app/features/cases/handoff-whisper/*`
- `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.*`
- `apps/web/src/app/features/cases/services/case-api.service.ts`
- `apps/web/src/app/features/cases/models/case.models.ts`
- `apps/mobile/src/services/cases/CaseApiService.ts`
- `apps/mobile/src/services/cases/case.models.ts`
- `apps/mobile/src/services/auth/AuthSessionService.ts`
- `apps/mobile/src/screens/cases/CasesListScreen.tsx`
- `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx`
- `apps/mobile/__tests__/CasesListScreen.test.tsx`
- `apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx`
- `README.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Story Completion Status

- Status: done
- Completion note: Story 2.8 implementation complete — all ACs covered; code review patches applied.
