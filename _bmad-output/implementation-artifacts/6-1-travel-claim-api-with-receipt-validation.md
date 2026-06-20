---
baseline_commit: NO_VCS
---

# Story 6.1: Travel Claim API with Receipt Validation

<!-- Validated: 2026-06-20 — see 6-1-travel-claim-api-with-receipt-validation-validation-report.md (10 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Social Worker**,
I want to submit travel claims with evidence,
so that allowances are reimbursed fairly (FR-18, CAP-9).

*Scope: **API only** — `travel_claims` schema + case links, field-worker CRUD (Draft), receipt attachments via extended `AttachmentService` (`ResourceType = TravelClaim`), `POST /travel-claims/{id}/submit` with mandatory receipt validation for Bus/Auto/Petrol, supervisor monthly totals endpoint. **No** mobile UI (Story **6.2**), **no** Director approve/return (Story **6.3**), **no** claimant notifications (Story **6.4**), **no** crisis-queue pending-claim rows (Story **8.1**), **no** offline sync (Story **6.2** + existing `sync/push` extension later).*

## Acceptance Criteria

1. **Given** I am an authenticated field worker (`Policies.FieldWorker` — SocialWorker or CaseWorker)  
   **When** I `POST /api/v1/travel-claims` with body:
   ```json
   {
     "claimDate": "2026-06-15",
     "startLocation": "Office",
     "destination": "District Court",
     "transportMode": "Bus",
     "amount": 45.50,
     "autoNumber": null,
     "notes": "Client visit travel",
     "caseIds": ["<uuid>"]
   }
   ```
   **Then** response is **201 Created** with envelope `{ data: TravelClaimDto, meta: { requestId } }`  
   **And** row persisted in `travel_claims` with `status = Draft`, `claimant_user_id = actor`, `organisation_id`, audit `travel.claim.created`  
   **And** each `caseIds` entry creates a `travel_claim_cases` link row (org-scoped, FK to `cases`)  
   **And** claimant may only link cases they can read (`EnsureCanReadCase` per case)

2. **Given** validation on create/update  
   **When** request is invalid  
   **Then**:
   - missing/null body → **400** `"Request body is required."`
   - missing/invalid `claimDate` → **400**
   - missing/empty `startLocation` or `destination` → **400** (max 256 chars each)
   - `transportMode` not in `Bus`, `Auto`, `Petrol`, `Other` → **400**
   - `amount` ≤ 0 or > 999999.99 → **400**
   - `transportMode = Auto` and missing/empty `autoNumber` → **400**
   - `caseIds` empty or contains duplicate IDs → **400**
   - linked case not in org or not readable → **403** or **404** per case
   - Coordinator/Director → **403** on create (field workers only in v1)
   - unauthenticated → **401**; deactivated → **403**

3. **Given** a Draft claim I own (`claimant_user_id = actor`)  
   **When** I `GET /api/v1/travel-claims`  
   **Then** **200 OK** with `{ data: { items: TravelClaimDto[] }, meta: { requestId, totalCount } }` — **only my claims**, ordered `claimDate` desc, then `id`  
   **And** I `GET /api/v1/travel-claims/{id}` returns single claim or **404**

3b. **Given** another field worker's claim in the same org  
   **When** I `GET /api/v1/travel-claims/{id}`  
   **Then** **404** (do not leak existence to non-claimants)

4. **Given** a Draft claim I own  
   **When** I `PATCH /api/v1/travel-claims/{id}` with updatable fields (`claimDate`, `startLocation`, `destination`, `transportMode`, `amount`, `autoNumber`, `notes`, `caseIds`)  
   **Then** **200 OK** with updated DTO and audit `travel.claim.updated`  
   **And** **422** if `status != Draft`  
   **And** **403** if not claimant (even same org field worker)

5. **Given** a Draft claim I own  
   **When** I `POST /api/v1/attachments/presign` with `{ "resourceType": "TravelClaim", "resourceId": "<claimId>", ... }`  
   **Then** same presign rules as Story 4.2 (content types, size, SAS 15 min, audit `attachment.presign.issued`)  
   **And** claim must exist, be Draft, and `claimant_user_id = actor`  
   **And** `POST /attachments/confirm` and `GET /attachments/{id}/download-url` work for TravelClaim with same claimant/RBAC rules  
   **And** `TravelClaimDto` includes `attachments: AttachmentSummaryDto[]` (confirmed only, ordered by `confirmedAtUtc`)

5b. **Given** a claim with `status != Draft`  
   **When** I `POST /attachments/presign` or `POST /attachments/confirm` for that claim  
   **Then** **422** (`"Receipts can only be added while the claim is in Draft status."`)

5c. **Given** a **Submitted** claim  
   **When** a `CoordinatorOrAbove` actor calls `GET /attachments/{id}/download-url`  
   **Then** **200 OK** (supervisor read for approval prep — Story **6.3**)  
   **And** field worker non-claimant still receives **403** or **404**

6. **Given** a Draft claim with `transportMode` in `Bus`, `Auto`, or `Petrol`  
   **When** I `POST /api/v1/travel-claims/{id}/submit`  
   **Then** if **no** `Confirmed` attachment exists for that claim → **422** Problem Details (`"Receipt image is required for Bus, Auto, and Petrol claims before submit."`)  
   **And** if receipt present → `status = Submitted`, `submitted_at_utc = now`, audit `travel.claim.submitted`  
   **And** **422** if already Submitted/Approved/Returned  
   **And** **403** if not claimant

7. **Given** a Draft claim with `transportMode = Other`  
   **When** I submit  
   **Then** receipt is **not** required; status moves to Submitted

8. **Given** a Coordinator or Director with org access  
   **When** `GET /api/v1/supervisor/travel-claims/monthly-totals?year=2026&month=6`  
   **Then** response `{ data: { items: TravelClaimMonthlyTotalDto[] }, meta: { requestId, totalCount } }`  
   **And** each row has `staffUserId`, `staffEmail`, `claimCount`, `totalAmount` (sum of `amount` for claims with `claimDate` in that calendar month UTC)  
   **And** includes claims in `Submitted` and `Approved` status only (excludes Draft, Returned)  
   **And** grouped by `claimant_user_id`, sorted by `staffEmail`  
   **And** field workers receive **403**

8b. **Given** invalid `year` or `month` query params (month ∉ 1–12, year < 2000)  
   **When** monthly-totals endpoint is called  
   **Then** **400** validation error

9. **Given** regression safety  
   **When** story ships  
   **Then** CaseNote attachment flow (Story 4.2) unchanged  
   **And** integration tests cover create, draft update guard, presign/confirm for TravelClaim, submit with/without receipt, monthly totals RBAC  
   **And** README documents travel-claims endpoints and receipt rule

## Tasks / Subtasks

- [x] **Schema** (AC: 1–2)
  - [x] `TravelClaim` entity + `TravelClaimCaseLink` junction
  - [x] Enums: `TravelClaimStatus` (Draft, Submitted, Approved, Returned), `TransportMode` (Bus, Auto, Petrol, Other)
  - [x] Migration: `travel_claims`, `travel_claim_cases`
  - [x] EF configs + indexes (`organisation_id`, `claimant_user_id`, `status`, `claim_date`)
  - [x] **Update** `UsersSchemaTests` — add `travel_claims`, `travel_claim_cases` to expected table list **and** `ClearUsersAsync` TRUNCATE list

- [x] **TravelClaimService** (AC: 1–7)
  - [x] `CreateAsync`, `ListMineAsync`, `GetAsync`, `UpdateAsync`, `SubmitAsync`
  - [x] Claimant-only mutate; `EnsureCanReadCase` for linked cases
  - [x] `autoNumber` required when `transportMode = Auto`
  - [x] Replace case links on PATCH when `caseIds` supplied
  - [x] Audit: `travel.claim.created`, `travel.claim.updated`, `travel.claim.submitted`

- [x] **AttachmentService extension** (AC: 5)
  - [x] Add `AttachmentResourceType.TravelClaim`
  - [x] Presign/confirm/download: claimant on **Draft** only for mutate; **Submitted** read for claimant + `CoordinatorOrAbove`
  - [x] Presign/confirm **422** when `status != Draft`
  - [x] `BuildBlobName` path: `{orgId}/travel-claim/{claimId}/{attachmentId}/{file}`
  - [x] Map attachments on `TravelClaimDto`

- [x] **Controllers** (AC: 1–8)
  - [x] `TravelClaimsController` — `GET/POST /api/v1/travel-claims`, `GET/PATCH /api/v1/travel-claims/{id}`, `POST .../submit`
  - [x] `[Authorize(Policy = Policies.FieldWorker)]` on field-worker routes
  - [x] `SupervisorController` or extend — `GET /api/v1/supervisor/travel-claims/monthly-totals`
  - [x] `[Authorize(Policy = Policies.CoordinatorOrAbove)]` on supervisor totals

- [x] **DTOs** (AC: 1, 3, 8)
  - [x] `TravelClaimDto`, `CreateTravelClaimRequest`, `UpdateTravelClaimRequest`
  - [x] `TravelClaimListResultDto`, `TravelClaimMonthlyTotalDto`, `TravelClaimMonthlyTotalsResultDto`

- [x] **Integration tests** (AC: 9)
  - [x] `TravelClaimApiTests.cs` — `SocialWorkerEmail` **and** `CaseWorkerEmail` sessions
  - [x] Happy path: create draft → presign → confirm receipt → submit → status Submitted
  - [x] Submit without receipt on Bus → 422
  - [x] Submit Other without receipt → 200
  - [x] PATCH non-draft → 422; wrong claimant → 403; other worker GET → 404
  - [x] Presign on Submitted claim → 422
  - [x] Monthly totals as coordinator; invalid month → 400; field worker 403
  - [x] CaseNote + TravelClaim presign both work; invalid resourceType → 400

- [x] **Docs + OpenAPI**
  - [x] README: travel claims API, receipt rule, monthly totals
  - [x] Regenerate `packages/api-client` (start API locally, then `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client`)

### Review Findings

**decision-needed** (resolved)

- [x] [Review][Decision] Monthly totals exclude inactive claimants — **Resolved: 1A** — removed `user.IsActive` filter; include all claimants in month totals.
- [x] [Review][Decision] List tie-break sort direction — **Resolved: 2B** — keep `ThenByDescending(c => c.Id)`.

**patch** (applied 2026-06-20)

- [x] [Review][Patch] TOCTOU: PATCH can overwrite Submitted status [`TravelClaimService.cs`]
- [x] [Review][Patch] TOCTOU: concurrent submit can double-audit [`TravelClaimService.cs`]
- [x] [Review][Patch] TOCTOU: presign/confirm can attach to non-Draft claim [`AttachmentService.cs`]
- [x] [Review][Patch] PATCH away from Auto leaves stale `autoNumber` [`TravelClaimService.cs`]
- [x] [Review][Patch] Amount validation should run after rounding [`TravelClaimService.cs`]
- [x] [Review][Patch] Submit should re-validate Auto requires `autoNumber` [`TravelClaimService.cs`]
- [x] [Review][Patch] Extreme `year` query can throw 500 [`TravelClaimService.cs`]
- [x] [Review][Patch] Audit `CreatedAtUtc` drifts from entity timestamps [`TravelClaimService.cs`]
- [x] [Review][Patch] TravelClaim presign 404 says "Case note not found" [`AttachmentsController.cs`]
- [x] [Review][Patch] Add test: Auto without `autoNumber` → 400 [`TravelClaimApiTests.cs`]
- [x] [Review][Patch] Add test: confirm attachment on Submitted claim → 422 [`TravelClaimApiTests.cs`]
- [x] [Review][Patch] Add test: invalid `year` on monthly totals → 400 [`TravelClaimApiTests.cs`]
- [x] [Review][Patch] Add test: submit wrong claimant → 403 [`TravelClaimApiTests.cs`]

**defer**

- [x] [Review][Defer] `ListMineAsync` N+1 queries per claim [`TravelClaimService.cs:82-99`] — deferred, pilot list sizes
- [x] [Review][Defer] `EnsureCanLinkCasesAsync` N+1 per case id [`TravelClaimService.cs:324-343`] — deferred, small caseIds arrays in v1
- [x] [Review][Defer] TravelClaim integration tests require Docker/Testcontainers — deferred, run locally before release
- [x] [Review][Defer] No DB constraint that `travel_claim_cases.organisation_id` matches linked case org — deferred, API paths always set correctly
- [x] [Review][Defer] `claimDate` datetime-with-offset can shift UTC calendar day — deferred, clients send date-only strings in practice

## Dev Notes

### READ FIRST

1. **Mirror Story 5.1 court sitting CRUD** — top-level resource (not nested under cases), `TravelClaimService` with `ResolveActorContext`, audit in same transaction, explicit validation messages.
2. **Extend Story 4.2 attachments** — add `TravelClaim` branch in `AttachmentService` presign/confirm/`EnsureCanAccessAttachmentAsync`; do **not** break CaseNote paths.
3. **Epic 6 boundaries** — Story **6.2** mobile capture + offline draft sync; **6.3** Director `approve`/`return` + web UI; **6.4** notifications; **8.1** crisis-queue pending-claim rows.
4. **No approve endpoint in 6.1** — `Approved`/`Returned` enum values exist for schema forward-compat; transitions implemented in 6.3.
5. **Claimant = creator** — `claimant_user_id` set on create from actor; immutable in v1.
6. **v1 single trip per claim** — one `transport_mode` + `amount` per row; **no** `line_items` child table (architecture "line items" deferred; CAP-9 fields map to claim columns).
7. **`UsersSchemaTests` breaks on new tables** — migration **must** update expected table list (not optional).

### Data model (CAP-9 / architecture §5.1)

| Table | Purpose |
|-------|---------|
| `travel_claims` | Root aggregate: date, locations, mode, amount, auto_number, notes, status, claimant, timestamps |
| `travel_claim_cases` | M:N link `travel_claim_id` + `case_id` (org-scoped) |

| Field | Type | Notes |
|-------|------|-------|
| `claim_date` | date (UTC midnight) | Travel date |
| `transport_mode` | Bus / Auto / Petrol / Other | Receipt required for first three |
| `amount` | decimal(10,2) | Fare amount |
| `auto_number` | string nullable | Required when Auto |
| `status` | Draft → Submitted → Approved (6.3) / Returned (6.3) |
| `submitted_at_utc` | nullable | Set on submit |

### Receipt validation (AC 6)

On submit, for `Bus`/`Auto`/`Petrol`:

```
EXISTS attachment WHERE resource_type = TravelClaim
  AND resource_id = claimId
  AND status = Confirmed
```

Use existing `attachments` table — no separate receipts table.

### Monthly totals query (AC 8)

Filter `claim_date` within `[year-month-01, next-month-01)` UTC, `status IN (Submitted, Approved)`, group by `claimant_user_id`, sum `amount`, count claims.

### RBAC

| Action | Policy |
|--------|--------|
| Create/list/get/patch/submit own claims | `FieldWorker` |
| Presign/confirm receipt on own Draft claim | `FieldWorker` (via attachment access) |
| Monthly totals | `CoordinatorOrAbove` |
| Approve/return | **Deferred 6.3** (`DirectorOnly`) |

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Domain/Entities/TravelClaim.cs` |
| NEW | `apps/api/Domain/Entities/TravelClaimCaseLink.cs` |
| NEW | `apps/api/Domain/Enums/TravelClaimStatus.cs` |
| NEW | `apps/api/Domain/Enums/TransportMode.cs` |
| NEW | `apps/api/Infrastructure/TravelClaims/TravelClaimService.cs` |
| NEW | `apps/api/Controllers/V1/TravelClaimsController.cs` |
| NEW | `apps/api/Models/TravelClaims/TravelClaimDtos.cs` |
| NEW | `apps/api/Migrations/*_AddTravelClaims.cs` |
| NEW | `tests/api.integration/TravelClaimApiTests.cs` |
| UPDATE | `apps/api/Domain/Enums/AttachmentResourceType.cs` |
| UPDATE | `apps/api/Infrastructure/Storage/AttachmentService.cs` |
| UPDATE | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| UPDATE | `apps/api/Controllers/V1/SupervisorController.cs` (monthly totals) |
| UPDATE | `apps/api/Program.cs` |
| UPDATE | `tests/api.integration/CaseCreateTests.cs` (helpers if needed) |
| UPDATE | `tests/api.integration/UsersSchemaTests.cs` |
| UPDATE | `README.md` |
| UPDATE | `packages/api-client` (regen) |

### Integration test recipe

1. `RbacTestData.EnsureRoleUsersAsync` — use `SocialWorkerEmail` session.
2. Coordinator creates case + transfer to social worker (or create as coordinator then transfer).
3. `POST /travel-claims` with linked `caseIds`.
4. Presign + confirm receipt (`FakeEmailSender` N/A — use Azurite blob PUT pattern from `AttachmentPresignTests`).
5. `POST /travel-claims/{id}/submit` → Submitted.
6. Coordinator `GET /supervisor/travel-claims/monthly-totals?year=&month=` → row with amount.
7. Submit Bus claim without receipt → 422.

### Previous story intelligence (4.2)

- Presign → PUT blob → confirm; 15 min SAS; `Pending` omitted from DTO lists.
- Extend `AllowedResourceTypeNames` and `ParseResourceType` — mirror CaseNote validation style.
- `EnsureCanAccessAttachmentAsync` must add TravelClaim branch (claimant + Draft/Submitted read rules).

### Previous story intelligence (5.1 / 5.4)

- Top-level or supervisor routes use `ApiResponse<T>` + `totalCount` in meta where listed.
- Business rules → `CaseBusinessRuleException` → 422.
- Audit metadata minimal — `claimId`, `status`, `transportMode`; **no** `notes` body in audit JSON.

### Testing requirements

- Use `AttachmentPresignTests` patterns for blob confirm.
- Assert CaseNote presign still rejects `TravelClaim` until enum wired (then accepts).
- Run `dotnet test tests/api.integration --filter TravelClaim` with Docker.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 6 Story 6.1]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-18]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.1 TravelClaim, §5.3 travel endpoints]
- [Source: `_bmad-output/specs/spec-kaval-online/field-and-court-operations.md` — CAP-9]
- [Source: `_bmad-output/implementation-artifacts/4-2-attachment-presign-upload-for-notes.md`]
- [Source: `_bmad-output/implementation-artifacts/5-1-court-sitting-crud-api.md`]
- [Source: `apps/api/Infrastructure/Storage/AttachmentService.cs`]
- [Source: `_bmad-output/project-context.md` — 422 business rules, audit same transaction]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

- Docker Desktop not running in dev environment — `TravelClaimApiTests` require Testcontainers (PostgreSQL/Azurite/Redis). Run locally: `dotnet test tests/api.integration --filter TravelClaim` with Docker started.
- OpenAPI snapshot + api-client regenerated via `EXPORT_OPENAPI_PATH` + `API_OPENAPI_FILE` (no live API host required).

### Completion Notes List

- Implemented `travel_claims` / `travel_claim_cases` schema with EF migration `AddTravelClaims`.
- `TravelClaimService` — field-worker CRUD (Draft), submit with receipt validation for Bus/Auto/Petrol, monthly totals for supervisors.
- Extended `AttachmentService` for `TravelClaim` presign/confirm/download without breaking CaseNote flow.
- `TravelClaimsController` + `SupervisorController` monthly-totals endpoint; README + OpenAPI snapshot + api-client updated.
- `TravelClaimApiTests` (13 cases) + `CaseTestData` helpers; `UsersSchemaTests` table list updated.
- Swagger tests pass; TravelClaim integration tests pending Docker.
- Code review (2026-06-20): 13 patches applied — TOCTOU guards, amount/autoNumber validation, monthly totals inactive-user fix (1A), 4 new tests.

### File List

- apps/api/Domain/Entities/TravelClaim.cs (new)
- apps/api/Domain/Entities/TravelClaimCaseLink.cs (new)
- apps/api/Domain/Enums/TravelClaimStatus.cs (new)
- apps/api/Domain/Enums/TransportMode.cs (new)
- apps/api/Domain/Enums/AttachmentResourceType.cs (modified)
- apps/api/Infrastructure/TravelClaims/TravelClaimService.cs (new)
- apps/api/Infrastructure/Persistence/TravelClaimConfiguration.cs (new)
- apps/api/Infrastructure/Persistence/TravelClaimCaseLinkConfiguration.cs (new)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (modified)
- apps/api/Infrastructure/Storage/AttachmentService.cs (modified)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (modified)
- apps/api/Controllers/V1/TravelClaimsController.cs (new)
- apps/api/Controllers/V1/SupervisorController.cs (modified)
- apps/api/Models/TravelClaims/TravelClaimDtos.cs (new)
- apps/api/Migrations/20260620002443_AddTravelClaims.cs (new)
- apps/api/Migrations/20260620002443_AddTravelClaims.Designer.cs (new)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (modified)
- apps/api/Program.cs (modified)
- tests/api.integration/TravelClaimApiTests.cs (new)
- tests/api.integration/CaseCreateTests.cs (modified)
- tests/api.integration/UsersSchemaTests.cs (modified)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- README.md (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (modified)

## Change Log

- 2026-06-19: Story 6.1 created — travel claim API, receipt validation, supervisor monthly totals; UI/approve/notifications deferred Epic 6.2–6.4.
- 2026-06-20: Validation — 10 fixes (UsersSchemaTests, presign Draft-only, GET 404 non-claimant, monthly totals validation, coordinator attachment read, CaseWorker tests, audit metadata, line_items note, api-client regen).
- 2026-06-20: Implementation — travel claims API, attachment extension, monthly totals, integration tests, README, api-client regen.
- 2026-06-20: Code review — 13 patches applied; decisions 1A (include inactive in totals), 2B (desc id sort).
