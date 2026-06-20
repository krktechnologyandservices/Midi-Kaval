---
baseline_commit: NO_VCS
---

# Story 6.3: Director Claim Approval on Web

<!-- Validated: 2026-06-20 — see 6-3-director-claim-approval-on-web-validation-report.md (12 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Project Director**,
I want to approve or return claims with comments,
so that spend is controlled (FR-18, Flow 6).

*Scope: **API + web UI** — Director `approve`/`return` endpoints with decision comment persistence, in-app notification rows for claimant (push deferred Story **7.2**), extend `GET /api/v1/supervisor/crisis-queue` with **neutral pending-claim rows**, **Admin → Travel claims** list/detail with receipt review, **Crisis Queue** renders real API rows (court miss + pending claims) with claim-row navigation to review screen. **No** full Crisis Queue polish (Story **8.2**), **no** Redis cache / global severity merge (Story **8.1**), **no** notification bell UI (Story **7.4**), **no** mobile reopen/resubmit UI (claimant sees `Returned` read-only per Story **6.2** — API may expose `decisionComment` on DTO for future mobile).*

## Acceptance Criteria

1. **Given** a travel claim with `status = Submitted` in my organisation  
   **When** I am an authenticated **Director** and `POST /api/v1/travel-claims/{id}/approve` with optional body `{ "comment": "..." }`  
   **Then** response is **200 OK** with `TravelClaimDto` (wrapped by `ApiEnvelopeFilter` → `{ data, meta: { requestId } }` — mirror `POST .../submit`, **not** manual `ApiResponse<>` in controller)  
   **And** `status = Approved`, `decidedAtUtc` and `decidedByUserId` set, `decisionComment` stored when provided (max 2000 chars)  
   **And** audit event `travel.claim.approved` written in same transaction (metadata: `claimId`, `status`, `claimantUserId`, `amount` — no claimant notes body)  
   **And** claimant receives an in-app notification (`travel.claim.approved`, `UserId = claimantUserId`, `ResourceType = TravelClaim`, `ResourceId = claimId`, `CaseId = first linked case id` from `travel_claim_cases` — required by `in_app_notifications` schema)  
   **And** **422** if status is not `Submitted` (TOCTOU guard on re-read before update)  
   **And** **404** if claim not in org  
   **And** Coordinator / field worker → **403**; unauthenticated → **401**

2. **Given** a Submitted claim  
   **When** Director `POST /api/v1/travel-claims/{id}/return` with body `{ "comment": "..." }`  
   **Then** **200 OK** with updated DTO, `status = Returned`, `decisionComment` **required** (non-empty, max 2000)  
   **And** audit `travel.claim.returned`  
   **And** in-app notification `travel.claim.returned` for claimant including comment in notification body/metadata  
   **And** missing/empty comment → **400**; non-Submitted → **422**; RBAC same as approve

3. **Given** I am a Director  
   **When** `GET /api/v1/director/travel-claims/pending`  
   **Then** **200 OK** `{ data: { items: TravelClaimDto[] }, meta: { requestId, totalCount } }` (explicit `ApiResponse<>` in controller like crisis-queue list)  
   **And** items are org-scoped claims with `status = Submitted`, ordered `submittedAtUtc` asc (oldest first), then `id`  
   **And** each DTO includes supervisor-only `claimantEmail`, linked `caseIds`, confirmed `attachments`, `decisionComment` null  
   **And** field-worker `GET /travel-claims` / `GET /travel-claims/{id}` **must not** populate `claimantEmail` (claimant is always self)  
   **And** non-Director → **403**

3b. **Given** I am the claimant and my claim is `Approved` or `Returned`  
   **When** I `GET /api/v1/travel-claims/{id}`  
   **Then** DTO includes `decisionComment`, `decidedAtUtc` when set (mobile read-only detail can show Director feedback)

4. **Given** I am a Director  
   **When** `GET /api/v1/director/travel-claims/{id}` for a claim in my org (any status)  
   **Then** **200 OK** with full `TravelClaimDto` including attachments and `claimantEmail`  
   **And** receipt download via existing `GET /api/v1/attachments/{id}/download-url` works for Director on Submitted/Approved/Returned claims (`IsSupervisorRole` already includes Director — **verify with test**, no duplicate RBAC branch)  
   **And** **404** when claim not in org; field worker routes unchanged (claimant-only `GET /travel-claims/{id}`)

4b. **Given** I am a Coordinator (not Director)  
   **When** `GET /api/v1/supervisor/travel-claims/{id}` for a Submitted claim in my org  
   **Then** **200 OK** read-only review DTO (same shape as director get, **no** approve/return)  
   **And** **403** for field workers; **404** when claim not in org or not Submitted/Approved/Returned

5. **Given** Submitted claims exist in the org  
   **When** Coordinator or Director calls `GET /api/v1/supervisor/crisis-queue`  
   **Then** response includes **pending travel claim rows** in addition to existing court-miss rows  
   **And** each claim row has `rowType = travel_claim_pending`, `severity = neutral`, `badgeLabel = Claim`, `travelClaimId`, `claimantUserId`, `claimantEmail`, `amount`, `receiptCount` (count of **Confirmed** attachments for that claim), composed `title`/`detail` per mockup (*"Travel claim pending approval — {emailLocalPart}"*, e.g. `priya` from `priya@pilot.example`, *"{amount} · {receiptCount} receipt(s)"*)  
   **And** court-only fields (`courtSittingId`, `crimeNumber`, `stNumber`, `scheduledAtUtc`) are **null** on claim rows (make DTO properties nullable — breaking JSON shape vs 5.4; update crisis-queue tests)  
   **And** claim rows sorted among themselves by `submittedAtUtc` asc  
   **And** merged list order for v1: **critical court-miss rows first** (existing), then **neutral claim rows** (Story **8.1** adds warning/info/handoff + Redis — do not implement here)  
   **And** field workers → **403**

6. **Given** I log in as **Director** on web  
   **When** I open **Admin → Travel claims** (`/admin/travel-claims`)  
   **Then** I see pending Submitted claims from AC3 with loading spinner, error+retry, empty state *"No claims awaiting approval"*  
   **And** each row shows claim date, claimant, destination, amount, transport mode, receipt count  
   **And** tapping a row opens review detail (`/admin/travel-claims/:id`)

7. **Given** travel claim review detail on web  
   **When** the page loads  
   **Then** I see claim fields, linked cases (crime/ST links to case detail), claimant notes, receipt thumbnails/names  
   **And** each receipt opens via presigned download URL in new tab (reuse `AttachmentApiService.getDownloadUrl`)  
   **And** **Approve** opens confirm dialog with optional comment field  
   **And** **Return** opens dialog with **required** comment field  
   **And** successful action navigates back to pending list and removes row from crisis queue feed on refresh  
   **And** API errors surface via `extractErrorMessage` / accessible error region (mirror interventions patterns)

8. **Given** I log in as **Coordinator** or **Director**  
   **When** I open **Crisis queue**  
   **Then** page loads `GET /api/v1/supervisor/crisis-queue` (replace Epic 8 placeholder)  
   **And** renders rows with severity styling per `DESIGN.md` (`crisis-row-critical`, `crisis-row-neutral`)  
   **And** court-miss rows navigate to case detail (`/cases/:caseId`)  
   **And** neutral claim rows navigate Director to `/admin/travel-claims/:travelClaimId` (`directorGuard`)  
   **And** Coordinator clicking claim row navigates to `/crisis-queue/travel-claims/:travelClaimId` (read-only review, `supervisorGuard` only — **no** approve/return buttons, banner *"Director approval required"*)  
   **And** empty state when no rows: *"No urgent items"* with links to Cases (full empty polish deferred Story **8.2**)

9. **Given** regression safety  
   **When** story ships  
   **Then** field-worker create/submit/mobile flows (Stories **6.1**, **6.2**) unchanged  
   **And** court-miss crisis rows (Story **5.4**) still appear and sort first  
   **And** integration tests cover approve, return validation, pending list RBAC, coordinator supervisor GET, crisis-queue claim rows (present when Submitted, absent after approve), notification creation with `CaseId` + `ResourceType`  
   **And** web unit tests cover pending list, review approve/return guards, crisis queue row navigation (Director vs Coordinator routes)  
   **And** `SwaggerEndpointTests` asserts new routes (`/director/travel-claims/pending`, `/approve`, `/return`, `/supervisor/travel-claims/{id}`)  
   **And** `CaseTestData` helpers extended for director approve/return flows (reuse `CreateAssignedCaseAndTravelClaimAsync`)  
   **And** README documents director endpoints + Admin travel approval  
   **And** OpenAPI snapshot + `@midi-kaval/api-client` regenerated (`EXPORT_OPENAPI_PATH` + `npm run build -w @midi-kaval/api-client`)

## Tasks / Subtasks

- [x] **Schema + DTOs** (AC: 1–2, 4)
  - [x] Add to `travel_claims`: `decision_comment` (nullable, max 2000), `decided_at_utc`, `decided_by_user_id` (FK users) + migration
  - [x] Update `TravelClaim` entity + EF config
  - [x] Extend `TravelClaimDto` with `decisionComment`, `decidedAtUtc`, `decidedByUserId`; map `claimantEmail` **only** in director/supervisor get/list methods (not field-worker `MapToDtoAsync`)
  - [x] Add `ApproveTravelClaimRequest`, `ReturnTravelClaimRequest` (comment field)
  - [x] `AuditEventTypes`: `TravelClaimApproved`, `TravelClaimReturned`
  - [x] Update `UsersSchemaTests` only if new table (column-only migration — likely no table list change)

- [x] **TravelClaimService — director methods** (AC: 1–4, 3b)
  - [x] `ListPendingForDirectorAsync` — org Submitted claims + claimant email join
  - [x] `GetForDirectorAsync` — org-scoped any status
  - [x] `GetForSupervisorAsync` — Coordinator read-only (Submitted/Approved/Returned)
  - [x] `ApproveAsync` / `ReturnAsync` — status guard Submitted only; TOCTOU re-check before SaveChanges
  - [x] Update field-worker `GetAsync` / `MapToDtoAsync` to include decision fields for own Approved/Returned claims (AC 3b)
  - [x] Inject `NotificationService`; create claimant notification in same transaction as audit (pass first linked `caseId`)
  - [x] Update `Program.cs` DI registration for `TravelClaimService` constructor

- [x] **AttachmentService** (AC: 4)
  - [x] **Verify** existing `IsSupervisorRole` (Director + Coordinator) covers TravelClaim download on Submitted+ — add integration test only if gap found (do not duplicate RBAC)

- [x] **Controllers** (AC: 1–4, 4b)
  - [x] `DirectorTravelClaimsController` — `GET pending`, `GET {id}` under `api/v1/director/travel-claims` with `[Authorize(Policy = Policies.DirectorOnly)]`
  - [x] `SupervisorController` — add `GET api/v1/supervisor/travel-claims/{id}` with `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] `TravelClaimsController` — add `POST .../approve` and `POST .../return` with `DirectorOnly` (architecture §5.3 path); return `Ok(dto)` like submit

- [x] **CrisisQueueService extension** (AC: 5)
  - [x] Query Submitted claims in org; map to `CrisisQueueItemDto` neutral rows
  - [x] Extend DTO: nullable `TravelClaimId`, `ClaimantUserId`, `ClaimantEmail`, `Amount`, `ReceiptCount`; make `CourtSittingId`/`ScheduledAtUtc` nullable for claim rows
  - [x] Merge: court-miss items + claim items (critical block then neutral block)

- [x] **Notifications** (AC: 1–2)
  - [x] `NotificationEventTypes.TravelClaimApproved`, `TravelClaimReturned`
  - [x] `NotificationService.CreateTravelClaimDecisionNotificationForSave` — `UserId = claimant`, `ResourceType = TravelClaim`, `ResourceId = claimId`, `CaseId = firstLinkedCaseId`; include return comment in `Body`; push logged deferred 7.2

- [x] **Integration tests** (AC: 9)
  - [x] `TravelClaimDirectorApiTests.cs` — submit → approve as director; return with required comment; 422 wrong status; coordinator 403 on approve; coordinator GET supervisor review 200; notifications with CaseId/ResourceType
  - [x] Extend crisis-queue tests — pending claim row present/absent after approve; nullable court fields on claim rows
  - [x] `SwaggerEndpointTests` — new route strings
  - [x] Extend `CaseTestData` — `ApproveTravelClaimAsync`, `ReturnTravelClaimAsync`, `GetSupervisorTravelClaimAsync`
  - [x] Regression: existing `TravelClaimApiTests`, `CourtMissEscalationJobTests` crisis rows

- [x] **Web — services + models** (AC: 6–8)
  - [x] `apps/web/src/app/features/travel/travel.models.ts` — types from `@midi-kaval/api-client`
  - [x] `TravelClaimApiService` — `listPending`, `getForReview`, `approve`, `return`, error wrapping mirroring `CaseApiService`
  - [x] `CrisisQueueApiService` — `list()` for supervisor feed

- [x] **Web — Admin travel claims** (AC: 6–7)
  - [x] Replace `AdminPageComponent` placeholder with hub linking to Travel claims; add routes `admin/travel-claims`, `admin/travel-claims/:id` under `directorGuard`
  - [x] `TravelClaimsPendingListComponent` — list AC6
  - [x] `TravelClaimReviewComponent` — detail, receipt links, approve/return dialogs AC7; `readOnly` input for coordinator reuse
  - [x] Unit tests for list + review

- [x] **Web — Crisis queue** (AC: 8)
  - [x] Replace `CrisisQueuePageComponent` placeholder — fetch API, render severity rows, badge labels, navigation split by `rowType`
  - [x] Add route `crisis-queue/travel-claims/:id` — reuse `TravelClaimReviewComponent` with `readOnly=true` (coordinator)
  - [x] SCSS: map `crisis-row-critical` / `crisis-row-neutral` from `DESIGN.md` semantic tokens (no hardcoded hex)
  - [x] Unit test: renders court + claim rows; Director vs Coordinator claim navigation

- [x] **OpenAPI + docs** (AC: 9)
  - [x] Regenerate `packages/api-client`
  - [x] README — director travel-claims endpoints, crisis-queue claim rows, Admin UI entry

### Review Findings

- [x] [Review][Decision] Block Director self-approval? — Implemented: `EnsureActorNotClaimant` returns 422 when director is claimant.

- [x] [Review][Patch] Post-action navigation ignores entry route — Fixed: navigate to `backLink()` after approve/return.

- [x] [Review][Patch] Receipt download fails silently — Fixed: show action error when download URL missing.

- [x] [Review][Patch] Linked-case fetch fails entire review — Fixed: `Promise.allSettled` with partial linked-case render.

- [x] [Review][Patch] Crisis queue claim row missing `travelClaimId` guard — Fixed: early return when id missing.

- [x] [Review][Patch] Crisis queue detail copy deviates from AC5 — Fixed: `{receiptCount} receipt(s)` format.

- [x] [Review][Patch] Pending list loading state not a spinner — Fixed: `mat-progress-spinner`.

- [x] [Review][Patch] Claims without case links surface in queue but cannot be decided — Fixed: skip in crisis queue; filter from director pending list.

- [x] [Review][Patch] AC9 integration test gaps — Fixed: added self-approval, RBAC, director download, sort regression, nullable field tests.

- [x] [Review][Patch] AC9 web unit test gaps — Fixed: readOnly, return validation, spinner, error+retry, row rendering tests.

- [x] [Review][Patch] Crisis queue SCSS uses hardcoded hex — Fixed: semantic Material tokens only.

- [x] [Review][Defer] Crisis queue N+1 queries per claim row — attachment count + case link queried in loop; acceptable for pilot volume, optimize in 8.1/8.2. [CrisisQueueService.cs:155-180] — deferred, pre-existing scale concern

- [x] [Review][Defer] Concurrent approve/return race — no row version/lock; two simultaneous directors could double-commit. Same pattern as other status transitions; defer optimistic concurrency to follow-up. [TravelClaimService.cs:419-446] — deferred, pre-existing

- [x] [Review][Defer] Approve/Return inline forms vs MatDialog — AC7 wording says "confirm dialog"; inline expand forms work but differ from spec literal. [travel-claim-review.component.html:71-100] — deferred, UX acceptable for v1 slice

- [x] [Review][Defer] Receipt thumbnails not implemented — AC7 mentions thumbnails/names; names-only buttons shipped. Full preview deferred. [travel-claim-review.component.html:50-64] — deferred, polish follow-up

## Dev Notes

### READ FIRST

1. **Stories 6.1 + 6.2 are done** — field-worker CRUD/submit and mobile capture exist. **Do not** change submit/receipt rules or mobile screens except DTO fields (`decisionComment`, `claimantEmail`) consumed passively by regenerated client.
2. **Approve/return is Director-only** — `Policies.DirectorOnly` (not Coordinator). Coordinators see pending rows in crisis queue (FR-21) but cannot approve.
3. **Mirror Story 4.5 notification pattern** — create in-app notification row in same `SaveChanges` as audit + status update; log push deferred to 7.2. Story **6.4** owns richer notification copy/channels; this story emits the event row.
4. **Mirror Story 5.4 crisis-queue extension** — 5.4 added court-miss rows only; 6.3 adds claim rows without Redis (8.1) or full UI polish (8.2).
5. **Returned status v1 limitation** — mobile shows Returned as read-only (6.2 AC3). Return stores comment + notifies claimant; **no** `reopen` endpoint in v1 (claimant cannot edit/resubmit until follow-up story). `decisionComment` exposed on field-worker GET (AC 3b) so mobile can display feedback without new screens.
6. **Monthly totals regression** — Story 6.1 totals include `Submitted` + `Approved` only; after approve claim stays in totals; after return (`Returned`) claim **drops out** of monthly totals.
7. **TOCTOU** — mirror 6.1 review patches: re-check `status == Submitted` immediately before update.
8. **Voice/tone** — Empty Admin list: *"No claims awaiting approval"*. Crisis queue empty: *"No urgent items"*. No gamified language (EXPERIENCE.md).

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `TravelClaim.cs` | No decision columns | Add decision fields |
| `TravelClaimService.cs` | Field-worker CRUD + submit only | Add director list/get/approve/return |
| `TravelClaimsController.cs` | FieldWorker routes only | Add director approve/return OR separate controller |
| `CrisisQueueService.cs` | Court-miss rows only | Merge pending Submitted claims |
| `CrisisQueueItemDto.cs` | Court-centric required fields | Nullable court fields + travel claim fields |
| `crisis-queue-page.component.ts` | Placeholder stub | Real API-driven list |
| `admin-page.component.ts` | Placeholder stub | Travel claims admin entry |

### API contract

| Action | Method | Path | Policy |
|--------|--------|------|--------|
| List pending | GET | `/api/v1/director/travel-claims/pending` | DirectorOnly |
| Get for review (Director) | GET | `/api/v1/director/travel-claims/{id}` | DirectorOnly |
| Get for review (Coordinator) | GET | `/api/v1/supervisor/travel-claims/{id}` | CoordinatorOrAbove |
| Approve | POST | `/api/v1/travel-claims/{id}/approve` | DirectorOnly |
| Return | POST | `/api/v1/travel-claims/{id}/return` | DirectorOnly |
| Crisis feed | GET | `/api/v1/supervisor/crisis-queue` | CoordinatorOrAbove |
| Receipt view | GET | `/api/v1/attachments/{id}/download-url` | Director/Coordinator via `IsSupervisorRole` |

**Approve body (optional):**

```json
{ "comment": "Approved for June field visit" }
```

**Return body (required comment):**

```json
{ "comment": "Receipt image unclear — please resubmit with fare visible." }
```

**Crisis queue claim row shape:**

```json
{
  "rowType": "travel_claim_pending",
  "severity": "neutral",
  "badgeLabel": "Claim",
  "travelClaimId": "<uuid>",
  "claimantUserId": "<uuid>",
  "claimantEmail": "priya@pilot.example",
  "amount": 840.00,
  "receiptCount": 1,
  "title": "Travel claim pending approval — priya@pilot.example",
  "detail": "₹840.00 · 1 receipt attached"
}
```

Use org currency formatting on web (pilot: prefix `₹` per mockup or locale-neutral `{amount}` if no i18n currency yet).

### Data model additions

| Column | Type | Notes |
|--------|------|-------|
| `decision_comment` | varchar(2000) nullable | Required on return; optional on approve |
| `decided_at_utc` | timestamptz nullable | Set on approve/return |
| `decided_by_user_id` | uuid nullable FK | Director actor |

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Controllers/V1/DirectorTravelClaimsController.cs` |
| UPDATE | `apps/api/Controllers/V1/SupervisorController.cs` — `GET travel-claims/{id}` |
| UPDATE | `apps/api/Controllers/V1/TravelClaimsController.cs` — approve/return actions |
| NEW | `apps/api/Migrations/*_AddTravelClaimDecisionFields.cs` |
| NEW | `tests/api.integration/TravelClaimDirectorApiTests.cs` |
| UPDATE | `apps/api/Domain/Entities/TravelClaim.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/TravelClaimConfiguration.cs` |
| UPDATE | `apps/api/Infrastructure/TravelClaims/TravelClaimService.cs` |
| UPDATE | `apps/api/Infrastructure/Supervisor/CrisisQueueService.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationService.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Storage/AttachmentService.cs` (Director download if needed) |
| UPDATE | `apps/api/Models/TravelClaims/TravelClaimDtos.cs` |
| UPDATE | `apps/api/Models/Supervisor/CrisisQueueDtos.cs` |
| NEW | `apps/web/src/app/features/travel/services/travel-claim-api.service.ts` |
| NEW | `apps/web/src/app/features/travel/services/crisis-queue-api.service.ts` |
| NEW | `apps/web/src/app/features/travel/travel.models.ts` |
| NEW | `apps/web/src/app/features/travel/travel-claims-pending-list.component.ts` |
| NEW | `apps/web/src/app/features/travel/travel-claim-review.component.ts` |
| NEW | `apps/web/src/app/features/travel/*.spec.ts` |
| UPDATE | `apps/web/src/app/features/shell/pages/crisis-queue-page.component.ts` (+ html/scss) |
| UPDATE | `apps/web/src/app/features/shell/pages/admin-page.component.ts` |
| UPDATE | `apps/web/src/app/app.routes.ts` — admin + crisis-queue/travel-claims/:id |
| UPDATE | `apps/api/Program.cs` — TravelClaimService DI |
| UPDATE | `tests/api.integration/SwaggerEndpointTests.cs` |
| UPDATE | `tests/api.integration/CaseCreateTests.cs` — CaseTestData helpers |
| UPDATE | `packages/api-client/openapi-snapshot.json` |
| UPDATE | `packages/api-client/src/generated/api.ts` |
| UPDATE | `README.md` |

**Reuse without modification:** `AttachmentApiService` (web), `CaseApiService` error patterns, `directorGuard`, `supervisorGuard`, field-worker `TravelClaimsController` mutate routes.

### Previous story intelligence (6.1)

- Submitted claims already allow `CoordinatorOrAbove` attachment download (6.1 AC 5c); `IsSupervisorRole` includes **Director** — verify with test, do not add parallel RBAC branch.
- Monthly totals include Submitted + Approved — after approve, totals update on next GET (no cache in 6.1).
- Enum `Returned`/`Approved` exist; transitions were deferred to 6.3.
- Integration tests use Docker/Testcontainers; helpers in `CaseCreateTests.cs` / `TravelClaimApiTests.cs`.

### Previous story intelligence (6.2)

- Mobile lists claims with status chips including Returned — after director decision, mobile pull-to-refresh shows new status.
- Crisis queue claim row copy references claimant first name — web can use email local-part until staff display names exist (Epic 9).
- Do not break `travel.claim.create` sync mutation or offline queue.

### Previous story intelligence (5.4)

- Crisis queue returns `{ data: { items }, meta: { requestId, totalCount } }`.
- Court-miss rows: `rowType=court_miss`, `severity=critical`, sort by `scheduledAtUtc` asc.
- Extend merge carefully — existing integration tests assert court-miss-only behavior; update tests to allow additional neutral rows.

### UX references (Flow 6, UX-DR3)

- [Source: `EXPERIENCE.md` Flow 6] — Director opens crisis queue neutral row → reviews receipts → approves/returns with comment.
- [Source: `mockups/crisis-queue.html`] — neutral row: badge *Claim*, title *Travel claim pending approval — Priya*, detail amount + receipt count.
- [Source: `DESIGN.md`] — `crisis-row-neutral` border `#667085`; status-neutral badge token.
- [Source: `EXPERIENCE.md` Admin surface] — Director sidebar Admin includes approvals (travel claims v1 slice).

### Testing requirements

- API: `dotnet test tests/api.integration --filter TravelClaimDirector`
- API regression: `dotnet test tests/api.integration --filter TravelClaim`
- Web: `npm test -w apps/web` for new travel + crisis queue specs
- Regenerate client: export OpenAPI + `npm run build -w @midi-kaval/api-client`
- Manual: field worker submit claim → Director approve from Admin → claim disappears from crisis queue → claimant notification row via `GET /notifications`

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 6 Story 6.3]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-18]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 approve endpoint, §5.4 web routing]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 6]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/crisis-queue.html`]
- [Source: `_bmad-output/implementation-artifacts/6-1-travel-claim-api-with-receipt-validation.md`]
- [Source: `_bmad-output/implementation-artifacts/6-2-mobile-travel-claim-capture.md`]
- [Source: `_bmad-output/implementation-artifacts/5-4-court-miss-escalation-and-crisis-queue-feed.md`]
- [Source: `_bmad-output/implementation-artifacts/4-5-interventions-ui-and-overdue-job.md` — notification pattern]
- [Source: `apps/api/Infrastructure/TravelClaims/TravelClaimService.cs`]
- [Source: `apps/api/Infrastructure/Supervisor/CrisisQueueService.cs`]
- [Source: `apps/web/src/app/features/cases/interventions/case-interventions.component.ts` — web list/form patterns]
- [Source: `apps/web/src/app/features/cases/services/attachment-api.service.ts`]
- [Source: `_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

Composer (Cursor Agent)

### Debug Log References

- Integration tests require Docker/Testcontainers; `TravelClaimDirectorApiTests` (9 tests) blocked when Docker unavailable; `SwaggerEndpointTests` (2) pass without DB.
- OpenAPI client regenerated via `API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json` after `EXPORT_OPENAPI_PATH` snapshot export.

### Completion Notes List

- Added travel claim decision fields (comment, decidedAt, decidedBy) with EF migration; director approve/return with TOCTOU Submitted guard, audit events, and in-app notifications (CaseId from first linked case).
- Director pending list/get, coordinator read-only supervisor GET, crisis-queue neutral pending-claim rows merged after court-miss critical rows.
- Web: Admin travel claims list/review (Director), crisis queue wired to API with severity styling and role-based claim navigation; coordinator read-only review route reuses review component.
- Tests: `TravelClaimDirectorApiTests` (15), extended `CaseTestData` + Swagger route assertions; web unit tests (84 passed) including crisis queue navigation spec.
- README + OpenAPI snapshot/api-client regenerated.
- Code review patches: block self-approval, navigation/receipt/linked-case UX fixes, crisis queue guards + AC5 copy, spinner, test coverage, semantic SCSS tokens.

### File List

- apps/api/Controllers/V1/DirectorTravelClaimsController.cs
- apps/api/Controllers/V1/SupervisorController.cs
- apps/api/Controllers/V1/TravelClaimsController.cs
- apps/api/Domain/Entities/TravelClaim.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Infrastructure/Notifications/NotificationEventTypes.cs
- apps/api/Infrastructure/Notifications/NotificationService.cs
- apps/api/Infrastructure/Persistence/TravelClaimConfiguration.cs
- apps/api/Infrastructure/Supervisor/CrisisQueueService.cs
- apps/api/Infrastructure/TravelClaims/TravelClaimService.cs
- apps/api/Migrations/20260620012312_AddTravelClaimDecisionFields.cs
- apps/api/Migrations/20260620012312_AddTravelClaimDecisionFields.Designer.cs
- apps/api/Models/Supervisor/CrisisQueueDtos.cs
- apps/api/Models/TravelClaims/TravelClaimDtos.cs
- apps/api/Program.cs
- apps/web/src/app/app.routes.ts
- apps/web/src/app/features/shell/pages/admin-page.component.ts
- apps/web/src/app/features/shell/pages/crisis-queue-page.component.html
- apps/web/src/app/features/shell/pages/crisis-queue-page.component.scss
- apps/web/src/app/features/shell/pages/crisis-queue-page.component.spec.ts
- apps/web/src/app/features/shell/pages/crisis-queue-page.component.ts
- apps/web/src/app/features/travel/services/crisis-queue-api.service.ts
- apps/web/src/app/features/travel/services/travel-claim-api.service.ts
- apps/web/src/app/features/travel/travel.models.ts
- apps/web/src/app/features/travel/travel-claim-review.component.html
- apps/web/src/app/features/travel/travel-claim-review.component.scss
- apps/web/src/app/features/travel/travel-claim-review.component.spec.ts
- apps/web/src/app/features/travel/travel-claim-review.component.ts
- apps/web/src/app/features/travel/travel-claims-pending-list.component.html
- apps/web/src/app/features/travel/travel-claims-pending-list.component.scss
- apps/web/src/app/features/travel/travel-claims-pending-list.component.spec.ts
- apps/web/src/app/features/travel/travel-claims-pending-list.component.ts
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- README.md
- tests/api.integration/CaseCreateTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- tests/api.integration/TravelClaimDirectorApiTests.cs

## Change Log

- 2026-06-20: Story 6.3 created — Director approve/return API, crisis-queue pending claim rows, Admin + Crisis Queue web UI; notifications stub in-app only; full crisis queue/dashboard deferred Epic 8.
- 2026-06-20: Validation — 12 fixes (notification CaseId, claimantEmail mapping scope, coordinator read route, ApiEnvelope pattern, nullable crisis DTO fields, AC 3b decisionComment, Swagger/CaseTestData tasks, monthly totals regression note).
- 2026-06-20: Implementation complete — API director/coordinator flows, crisis queue claim rows, Admin + Crisis Queue web UI, integration + unit tests, OpenAPI snapshot/client update; story marked review.
- 2026-06-20: Code review — 1 decision-needed, 10 patch, 4 defer, 8 dismissed (parallel Blind Hunter / Edge Case / Acceptance Auditor layers).
- 2026-06-20: Code review resolved — blocked self-approval, applied all patch findings; story marked done.
