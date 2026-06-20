---
baseline_commit: NO_VCS
---

# Story 2.5: Case Merge Workflow

Status: done

<!-- Validated: 2026-06-15 — see 2-5-case-merge-workflow-validation-report.md -->

## Story

As a **Project Coordinator or Director**,
I want to merge duplicate intake into an existing Case from the duplicate match sheet,
so that history is preserved and only one active Crime/ST record remains (FR-5, UJ-3).

*Note: Merge applies to **client-side create drafts** blocked by duplicate check — no duplicate `cases` row exists yet. Field workers (`SocialWorker`, `CaseWorker`) cannot merge (API 403; Merge hidden in 2.4 UI).*

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `POST /api/v1/cases/{id}/merge` with a draft intake body (same required fields as create — see **Merge request body** below)  
   **Then** response is **200 OK** with envelope `{ data: CaseDto }` for the **target** existing case  
   **And** **no new** `cases` row is inserted  
   **And** `audit_events` records `case.merged` in the **same database transaction** as any target-case updates  
   **And** audit metadata includes at minimum: `targetCaseId`, `draftCrimeNumber`, `draftStNumber`, `actorUserId`, and a JSON snapshot of draft intake fields submitted

2. **Given** merge request draft identifiers are normalized (trim + uppercase) server-side  
   **When** merge is processed for target case `{id}`  
   **Then** at least one of draft `crimeNumber` or `stNumber` must equal the target case's stored identifier after normalization  
   **And** if draft `crimeNumber` is present and differs from target's crime number, no **other** case in the organisation may already own that crime number (**409 Conflict**)  
   **And** if draft `stNumber` is present and differs from target's ST number, no **other** case in the organisation may already own that ST number (**409 Conflict**)  
   **And** if draft identifiers do not align with the target case (neither matches) → **422** Problem Details with detail **"Draft identifiers do not match the target case."**

3. **Given** v1 fill-empty merge policy (no overwrite of established target data)  
   **When** merge succeeds  
   **Then** `case.merged` audit is **always** written (even when no case columns change)  
   **And** target case **identifiers** (`crimeNumber`, `stNumber`) and **core fields** (`beneficiaryName`, `typeOfOffence`, `offenceClassification`, `domicile`, `isFirstTimeOffender`) are **never overwritten**  
   **And** `beneficiaryAge` is set on target only when target value is `null` and draft provides a value  
   **And** `beneficiaryContact` is set on target only when target is null/empty and draft provides a non-empty value  
   **And** `updatedAtUtc` is bumped **only** when at least one fill-empty field is applied — if no fields change, audit still persists but `updatedAtUtc` stays unchanged

4. **Given** target case `{id}` does not exist in my organisation  
   **When** I call merge  
   **Then** **404** Problem Details ("Case not found.")

5. **Given** I am **SocialWorker** or **CaseWorker**  
   **When** I `POST /api/v1/cases/{id}/merge`  
   **Then** **403** with `Policies.ForbiddenByRoleMessage`

6. **Given** I am deactivated (`IsActive = false`)  
   **When** I call merge  
   **Then** **403** with `AuthService.DeactivatedMessage` (inactive handler before RBAC)

7. **Given** required draft fields are missing or invalid (same rules as create)  
   **When** I call merge  
   **Then** **400** Problem Details — mirror `CaseService` create validation messages  
   **And** no audit row or case mutation is persisted

8. **Given** duplicate match sheet is open on **web** create (`/cases/new`) and I am supervisor (`isSupervisorRole`)  
   **When** I tap **Merge** on a match row  
   **Then** an **inline confirmation** appears on that row (not a second stacked modal — project-context: modal stack >1 forbidden): copy **"Merge this intake into the existing case?"** with **Confirm merge** (primary) and **Back** (secondary)  
   **And** on **Confirm merge**, the sheet closes with `DuplicateMatchSheetResult` `{ action: 'merge', caseId, match }` — the sheet does **not** call HTTP  
   **And** `case-create.component` `handleSheetResult` receives the merge result, calls `mergeCase(caseId, buildCreateRequest())`, and owns loading/error state  
   **And** while merge is in flight, parent shows loading (Confirm was on sheet; after close, use parent `checkingDuplicate` or dedicated `merging` signal) and blocks duplicate save  
   **And** on **200**, create draft is abandoned (no `POST /cases`), web navigates to `/cases/{id}` with target `CaseDto` in router `state`, and snackbar **"Intake merged into existing case."**  
   **And** on error, parent `errorMessage` with `aria-live` via `CaseApiService.extractErrorMessage()`; user may re-open sheet via re-check if needed  
   **And** **remove** Story 2.4 stub snackbar and unused `MatSnackBar` from `duplicate-match-sheet.component.ts`

9. **Given** duplicate match sheet is open on **mobile** `CaseCreateScreen` and `canMerge` is true  
   **When** I tap **Merge** on a row  
   **Then** same inline confirmation pattern as web (no nested `Modal`)  
   **And** on **Confirm merge**, `onMerge(match)` invokes parent handler — sheet does **not** call HTTP; parent `CaseCreateScreen` calls `mergeCase` with form values  
   **And** while merge is in flight, Confirm is disabled and shows loading label (e.g. **"Merging…"**)  
   **And** on success: sheet closes, `Alert.alert('Intake merged into existing case.')`, navigate to `CaseDetailPlaceholder` with merged case summary  
   **And** **remove** mobile stub merge message in `DuplicateMatchSheet.tsx`

10. **Given** non-supervisor roles on mobile (`canMerge: false`)  
    **When** duplicate sheet renders  
    **Then** Merge button remains hidden (unchanged from 2.4) — no merge API calls possible from UI

11. **Given** OpenAPI and client contract  
    **When** this story ships  
    **Then** OpenAPI documents `POST /api/v1/cases/{id}/merge` with request body schema and response **200**, **400**, **401**, **403**, **404**, **409**, **422**  
    **And** `packages/api-client` is regenerated (do not hand-edit `src/generated/api.ts`)  
    **And** web `case-api.service.ts` and mobile `CaseApiService.ts` expose `mergeCase(caseId, body)`  
    **And** README documents merge endpoint and fill-empty policy

12. **Given** test baseline after Story 2.4 (**103** .NET: 1 unit + 102 integration; **31** web; **21** mobile)  
    **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
    **Then** all existing tests pass  
    **And** new integration tests cover: coordinator merge matching crime 200 + audit; director merge 200; both identifiers match target 200; fill-empty `beneficiaryContact`; does not overwrite `beneficiaryName`; audit-only merge (no fill-empty) — audit written, `updatedAtUtc` unchanged; identifiers-not-matching-target 422; crime conflict 409; ST conflict 409; target 404; field worker 403; deactivated 403; unauthenticated 401; validation 400; no new case row  
    **And** `CaseTestData.MergeCaseAsync(client, token, caseId, body)` helper added alongside existing create helpers  
    **And** new web tests cover: sheet emits merge result, parent calls API with form body, success navigates, error surfaces, inline confirm (no second `MatDialog`), stub removed  
    **And** new mobile tests cover: parent merge handler, confirm UI, loading state, stub removed  
    **And** `SwaggerEndpointTests.Get_SwaggerJson_Returns200_ContainingMetaRoute` asserts swagger JSON contains `merge`  
    **Verified baseline:** **118** .NET (1 unit + 117 integration), **34** web, **22** mobile — all green (2026-06-15)

13. **Given** Stories 2.6–2.9 are not yet implemented  
    **When** this story ships  
    **Then** **no** case search/registry UI, **no** `GET /cases/{id}` rich detail API — merge navigates to existing placeholder detail only

## Tasks / Subtasks

- [x] **API — validation refactor + audit type** (AC: 1, 3, 7, 11)
  - [x] Extract `ValidateIntakeRequest(CreateCaseRequest)` (private) returning `ValidatedCreateCaseRequest` — refactor existing `CreateAsync` to use it; **do not duplicate** validation logic for merge
  - [x] Merge endpoint uses **`CreateCaseRequest`** as body type (same OpenAPI schema as create — no separate `MergeCaseRequest.cs`)
  - [x] Extend `AuditEventTypes` with `CaseMerged = "case.merged"`

- [x] **API — CaseService.MergeAsync** (AC: 1–4, 7)
  - [x] `CaseService.MergeAsync(Guid targetCaseId, CreateCaseRequest request)` — load target by org; validate via `ValidateIntakeRequest`
  - [x] Enforce identifier alignment + cross-case conflict checks (AC2); 422 detail: **"Draft identifiers do not match the target case."**
  - [x] Apply fill-empty policy (AC3); bump `UpdatedAtUtc` only when fill-empty fields applied
  - [x] Single `SaveChangesAsync`: audit + optional case update in one transaction (mirror create/stage — **do not** use `AuditService.RecordAsync`)
  - [x] Throw `CaseNotFoundException`, `CaseBusinessRuleException` (422), `CaseValidationException` (400); controller maps to Problem Details

- [x] **API — CasesController endpoint** (AC: 1, 4–7, 11)
  - [x] `POST {id:guid}/merge` — body `CreateCaseRequest`; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] Return **200** + `CaseDto`; `[ProducesResponseType]` for all status codes
  - [x] Catch `DbUpdateException` unique violation → **409** (defensive)

- [x] **API — integration tests** (AC: 12)
  - [x] `tests/api.integration/CaseMergeTests.cs` — `[Collection("AuthIntegration")]`, reuse `CaseTestData` + `RbacTestData`
  - [x] Add `CaseTestData.MergeCaseAsync(client, accessToken, caseId, CreateCaseRequest)` helper
  - [x] Assert audit row `EventType == case.merged` and metadata JSON contains draft snapshot

- [x] **OpenAPI + api-client** (AC: 11, 12)
  - [x] Export: `EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json` + `API_OPENAPI_FILE` for `generate.mjs` (Windows: set env vars then `dotnet test` export test or run API export)
  - [x] `npm run generate:api-client` in `packages/api-client`; rebuild `dist` if web/mobile consume compiled output
  - [x] `SwaggerEndpointTests`: assert swagger JSON contains `merge`

- [x] **Web — wire merge** (AC: 8, 12)
  - [x] `case-api.service.ts` — `mergeCase(caseId, body: CreateCaseRequest)` via existing `HttpClient` + `CaseApiError` wrapping (mirror create/check — **do not** add raw fetch)
  - [x] `case.models.ts` — re-export `CreateCaseRequest` for merge body (no duplicate TS type)
  - [x] `duplicate-match-sheet.component` — inline confirm per row; on confirm close dialog with `{ action: 'merge', caseId, match }`; remove `merge()` stub and `MatSnackBar`
  - [x] Extend `DuplicateMatchSheetResult` union with merge action
  - [x] `case-create.component` — `handleSheetResult` merge branch: `mergeCase` + navigate + snackbar; `merging` or reuse `checkingDuplicate` signal
  - [x] Tests: `duplicate-match-sheet.component.spec.ts`, `case-create.component.spec.ts`

- [x] **Mobile — wire merge** (AC: 9, 10, 12)
  - [x] `CaseApiService.mergeCase(caseId, body)` — same `postApi` pattern as create
  - [x] `DuplicateMatchSheet.tsx` — inline confirm; `onMerge(match)` on confirm only (parent calls API); remove stub message; loading on Confirm
  - [x] `CaseCreateScreen.tsx` — merge handler with form body + navigation
  - [x] Tests: `DuplicateMatchSheet.test.tsx`, `CaseCreateScreen.test.tsx`

- [x] **Documentation** (AC: 11)
  - [x] README — merge endpoint, RBAC, fill-empty policy, no duplicate row created

### Review Findings

- [x] [Review][Patch] Web merge failure leaves Save permanently disabled [`apps/web/src/app/features/cases/create/case-create.component.ts:289`]
- [x] [Review][Patch] Add `CaseWorker_Merge_Returns403` integration test (AC5) [`tests/api.integration/CaseMergeTests.cs`]
- [x] [Review][Patch] Add `DraftCrimePointsToOtherCase_Returns409` integration test (AC12 matrix) [`tests/api.integration/CaseMergeTests.cs`]
- [x] [Review][Patch] Add `FillEmptyBeneficiaryAge_UpdatesTarget` integration test (AC3) [`tests/api.integration/CaseMergeTests.cs`]
- [x] [Review][Patch] Add web spec for merge API error recovery (save unblocked, error shown) [`apps/web/src/app/features/cases/create/case-create.component.spec.ts`]
- [x] [Review][Defer] Mobile `buildCreateBody` omits optional `beneficiaryAge`/`beneficiaryContact` — deferred, pre-existing (mobile create form has no fields for these in 2.4)

## Dev Notes

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Stories 2.1–2.3 delivered create API, stage transitions, and duplicate check. **Story 2.4** wired the duplicate match sheet (Merge stubbed). **Story 2.5** delivers **`POST /api/v1/cases/{id}/merge`** and connects web/mobile Merge buttons. Story 2.6 adds search; 2.9 full registry/detail.

### Merge request body

Same fields and validation as `POST /api/v1/cases` create (Story 2.1):

| Field | Required | Notes |
|-------|----------|-------|
| `crimeNumber` | Yes | Trim; max 64; normalized uppercase server-side |
| `stNumber` | Yes | Trim; max 64; normalized uppercase server-side |
| `beneficiaryName` | Yes | Max 256 |
| `beneficiaryAge` | No | 0–120 |
| `beneficiaryContact` | No | Max 32 |
| `typeOfOffence` | Yes | Max 128 |
| `offenceClassification` | Yes | `Petty` \| `Serious` \| `Heinous` |
| `domicile` | Yes | `Urban` \| `Rural` \| `Coastal` \| `Tribal` \| `Slum` |
| `isFirstTimeOffender` | No | Default `true` |

Body represents the **in-progress create form** at merge time, not a persisted duplicate case.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `CasesController.cs` | `POST /cases`, `POST check-duplicate`, `PATCH stage` | Add `POST {id}/merge` |
| `CaseService.cs` | Create, CheckDuplicate, TransitionStage | Add `MergeAsync` |
| `AuditEventTypes.cs` | `case.created`, `case.stage.changed` | Add `case.merged` |
| `packages/api-client` | No merge types | Regenerate |
| Web duplicate sheet | Merge stub snackbar | Functional merge + inline confirm |
| Mobile duplicate sheet | Stub message + no-op `onMerge` | Functional merge + inline confirm |
| `case-create` / `CaseCreateScreen` | Open existing + cancel flows | Add merge orchestration |

**Do not break:**
- Duplicate check remains read-only (no audit on check)
- Create flow 409 re-check + sheet behavior from 2.4 patches
- `CoordinatorOrAbove` RBAC on all case mutations
- Modal stack rule on web — **no** `MatDialog.open` confirmation on top of duplicate sheet

### API contract

**Merge intake** `POST /api/v1/cases/{id}/merge`  
**Auth:** Bearer JWT; `CoordinatorOrAbove`  
**Body:** `CreateCaseRequest` (same schema as create)  
**Success:** `200 OK` — `{ data: CaseDto }` for target case  
**Errors:** 400 validation, 401, 403 RBAC/deactivated, 404 target missing, 409 identifier owned by another case, 422 draft does not match target (`"Draft identifiers do not match the target case."`)

**Audit metadata example (camelCase in JSON):**

```json
{
  "targetCaseId": "uuid",
  "draftCrimeNumber": "CR-001",
  "draftStNumber": "ST-001",
  "draftSnapshot": { "beneficiaryName": "...", "typeOfOffence": "...", ... }
}
```

### File structure (expected touch list)

```
apps/api/
├── Controllers/V1/CasesController.cs          # UPDATE — merge endpoint
├── Infrastructure/Cases/CaseService.cs        # UPDATE — MergeAsync + ValidateIntakeRequest refactor
├── Infrastructure/Audit/AuditEventTypes.cs      # UPDATE — CaseMerged
├── Models/Cases/CaseDtos.cs                   # UPDATE — document merge uses CreateCaseRequest (no new DTO file)
tests/api.integration/CaseMergeTests.cs        # NEW
tests/api.integration/CaseTestData (in CaseCreateTests.cs) # UPDATE — MergeCaseAsync helper
tests/api.integration/SwaggerEndpointTests.cs  # UPDATE — assert merge path

apps/web/src/app/features/cases/
├── services/case-api.service.ts               # UPDATE — mergeCase
├── duplicate-match-sheet/                     # UPDATE — inline confirm, remove stub
├── create/case-create.component.ts            # UPDATE — merge orchestration

apps/mobile/src/
├── services/cases/CaseApiService.ts             # UPDATE — mergeCase
├── components/DuplicateMatchSheet.tsx           # UPDATE — inline confirm, remove stub
├── screens/cases/CaseCreateScreen.tsx           # UPDATE — merge orchestration

packages/api-client/                             # REGENERATE (openapi + dist)
README.md                                        # UPDATE
```

### Previous story intelligence (2.4)

- **Merge is stubbed** — web `duplicate-match-sheet.component.ts` `merge()` snackbar; mobile `DuplicateMatchSheet.tsx` stub message — **replace entirely**
- **Parent-owned API pattern:** extend `DuplicateMatchSheetResult` with `{ action: 'merge'; caseId: string; match: CaseDuplicateMatchDto }`; sheet closes on confirm; `case-create.component` `handleSheetResult` calls `mergeCase(buildCreateRequest())`
- **`handleSheetResult` today** only handles `open` — add `merge` branch
- **`canMerge`** uses `isSupervisorRole()` (Coordinator + Director) — matches API policy
- **`DuplicateCheckOutcome`** tri-state on create — merge path is separate; never call `createCase` after merge
- **Web HTTP:** `case-api.service.ts` uses `HttpClient` + types from `@midi-kaval/api-client` via `case.models.ts` re-exports (Story 2.4 pattern — not raw fetch)
- **`CaseApiError`** + `extractErrorMessage()` — reuse for merge
- **Placeholder detail** already exists — reuse for post-merge navigation
- **Test baseline:** 103 .NET, 31 web, 21 mobile — maintain green
- **Mobile coordinator E2E** still unreachable (`WebOnlyScreen`) — component tests with mocks only

### Previous story intelligence (2.1–2.3)

- **`ValidateRequest` is private** in `CaseService` — extract `ValidateIntakeRequest(CreateCaseRequest)` before adding `MergeAsync`
- **Normalization:** server `Trim()` + `ToUpperInvariant()` on crime/ST — clients send trimmed raw input
- **Single transaction audit** — `db.AuditEvents.Add` + `SaveChangesAsync` once; never split
- **Integration tests:** `[Collection("AuthIntegration")]`, `CaseTestData.BuildValidRequest()`, `CaseTestData.CreateCaseAsync`
- **Director test user:** `AuthTestData.Email` (`director@pilot.example`) per Story 2.1
- **Route ordering:** static `check-duplicate` before `{id}` — `merge` is `{id}/merge` sub-route (no conflict)
- **409 on create** when unique index violated — merge must pre-check cross-case conflicts before update

### OpenAPI regeneration (Windows)

```text
set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json
set API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json
dotnet test tests/api.integration --filter "FullyQualifiedName~SwaggerEndpointTests.Export_Swagger_WhenRequested"
cd packages/api-client && npm run generate:api-client
```

Rebuild `packages/api-client/dist` if consumers resolve compiled output (Story 2.4 required dist rebuild).

### Architecture compliance

- **Business logic in `CaseService`** — not controller, not Angular/RN clients [Source: `project-context.md`]
- **HTTP types from api-client** — re-export schemas in `case.models.ts`; web uses `HttpClient` in `CaseApiService` (Story 2.4 brownfield pattern)
- **RFC 7807 Problem Details** + envelope on success
- **Audit every mutation** in same transaction
- **Angular:** standalone components, signals for confirm/loading state
- **No modal stack >1** on web — inline confirm in duplicate sheet [Source: `project-context.md`]

### UX compliance

- Duplicate sheet remains **blocking**; tone is review not error (UX-DR12) [Source: `DESIGN.md` duplicate-match-sheet]
- Merge is **primary** per-row action for supervisors; Cancel remains sheet-level
- Flow 3 climax: single active Crime/ST record [Source: `EXPERIENCE.md`]
- Success copy: **"Intake merged into existing case."** — factual, non-celebratory

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 103 existing + new merge integration tests |
| Web | `npm run test:web` | 31 existing + merge/confirm specs |
| Mobile | `npm run test:mobile` | 21 existing + merge specs |

**Integration test matrix (minimum):**

| Scenario | Status |
|----------|--------|
| Coordinator merge matching crime | 200 + audit |
| Director merge | 200 + audit |
| Both identifiers match target | 200 + audit |
| Fill-empty `beneficiaryContact` | 200 + field updated |
| Audit-only merge (no fill-empty) | 200 + audit; `updatedAtUtc` unchanged |
| Does not overwrite `beneficiaryName` | 200 + name unchanged |
| Draft crime matches, ST points to other case | 409 |
| Draft identifiers don't match target | 422 |
| Unknown target id | 404 |
| Social worker | 403 |
| Unauthenticated | 401 |
| Missing `beneficiaryName` | 400 |
| No new `cases` row count increase | assert DB |

### Scope boundaries

| In scope (2.5) | Out of scope |
|----------------|--------------|
| `POST /cases/{id}/merge` API + audit | Full case detail API (2.9) |
| Web/mobile merge from duplicate sheet | Merge from registry or search (2.6+) |
| Fill-empty optional field policy | Deep field-level merge / conflict resolution UI |
| OpenAPI + api-client regen | Playwright E2E |
| Inline confirm (no stacked modal) | Opening merge to field workers |
| Navigate to placeholder detail | Rich timeline / notes merge |

### Definition of Done

- [x] `ValidateIntakeRequest` shared; merge uses `CreateCaseRequest` body (no duplicate DTO)
- [x] Merge API returns 200, writes `case.merged` audit always; `updatedAtUtc` only on fill-empty
- [x] Fill-empty policy enforced; 409/422 conflict rules covered by tests
- [x] Web + mobile Merge buttons call API; stubs removed; inline confirm works
- [x] api-client regenerated; README updated
- [x] 103+ .NET, 31+ web, 21+ mobile tests green

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.5, FR-5]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-5, UJ-3]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 Cases `POST /cases/{id}/merge`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — duplicate-match-sheet]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 3]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — CAP-3 duplicate prevention]
- [Source: `_bmad-output/project-context.md` — audit transaction, api-client, modal stack]
- [Source: `_bmad-output/implementation-artifacts/2-4-duplicate-match-sheet-on-web-and-mobile-create.md` — sheet UX, stubs, test baseline]
- [Source: `_bmad-output/implementation-artifacts/2-1-case-aggregate-schema-and-create-api.md` — create validation, Case entity]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — patterns to extend]
- [Source: `apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.ts` — stub to replace]
- [Source: `apps/mobile/src/components/DuplicateMatchSheet.tsx` — stub to replace]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created
- Validated 2026-06-15 — 8 validation fixes applied (see validation report)
- Implementation complete 2026-06-15 — `POST /api/v1/cases/{id}/merge`, fill-empty policy, `case.merged` audit, web/mobile inline confirm merge wired; Story 2.4 stubs removed
- OpenAPI snapshot + `@midi-kaval/api-client` regenerated (`openapi-snapshot.json`, `src/generated/api.ts`, `dist/`)
- Integration audit assertion uses `OrderByDescending(CreatedAtUtc).FirstAsync` (shared DB may have multiple `case.merged` rows)
- Tests green: **118** .NET (1 unit + 117 integration), **34** web, **22** mobile
- Code review 2026-06-15 — 5 patches applied (merge error save unblock, 3 integration tests, web error spec); 1 defer (mobile optional fields)

### File List

- apps/api/Controllers/V1/CasesController.cs (modified)
- apps/api/Infrastructure/Cases/CaseService.cs (modified)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (modified)
- tests/api.integration/CaseMergeTests.cs (new)
- tests/api.integration/CaseCreateTests.cs (modified — MergeCaseAsync, CreateAuthorizedMergePost)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- apps/web/src/app/features/cases/services/case-api.service.ts (modified)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.ts (modified)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.html (modified)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.spec.ts (modified)
- apps/web/src/app/features/cases/create/case-create.component.ts (modified)
- apps/web/src/app/features/cases/create/case-create.component.spec.ts (modified)
- apps/mobile/src/services/cases/CaseApiService.ts (modified)
- apps/mobile/src/components/DuplicateMatchSheet.tsx (modified)
- apps/mobile/src/screens/cases/CaseCreateScreen.tsx (modified)
- apps/mobile/__tests__/DuplicateMatchSheet.test.tsx (modified)
- apps/mobile/__tests__/CaseCreateScreen.test.tsx (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (regenerated)
- packages/api-client/dist/* (rebuilt)
- README.md (modified — merge endpoint docs)
- package.json (modified — convenience test scripts)
