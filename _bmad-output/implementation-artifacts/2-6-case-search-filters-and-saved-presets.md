---
baseline_commit: NO_VCS
---

# Story 2.6: Case Search, Filters, and Saved Presets

Status: done

<!-- Validated: 2026-06-15 — see 2-6-case-search-filters-and-saved-presets-validation-report.md (12 fixes applied) -->

## Story

As a **supervisor (Coordinator or Director)**,
I want to search and filter Cases quickly,
so that I find records without Excel (FR-6).

*Note: v1 pilot search is **web supervisor desk work** — field workers use mobile for visits; full sidebar registry IA and rich detail remain Story 2.9. Export is Story 2.7.*

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `GET /api/v1/cases/search` with optional query parameters (see **Search query contract** below)  
   **Then** response is **200 OK** with envelope `{ data: CaseSearchResultDto, meta: { requestId, totalCount } }`  
   **And** `data.items` contains cases in my organisation only, ordered by `updatedAtUtc` descending (most recently touched first)  
   **And** `meta.totalCount` is the full match count before pagination (see **Pagination envelope** in Dev Notes — controller must set `ApiMeta.TotalCount`; filter does not today)  
   **And** when `q` is non-empty, a row matches if **any** searchable column matches (**OR** within `q`): `crimeNumber`, `stNumber`, `beneficiaryName`, `beneficiaryContact`, or stored `domicile` enum string (pilot **area** full-text per FR-6 / `case-and-lifecycle.md`)  
   **And** when comparing `q` against `crimeNumber` / `stNumber`, both sides use `Trim()` + `ToUpperInvariant()`; `beneficiaryName`, `beneficiaryContact`, and `domicile` use case-insensitive substring (`ILIKE`) **without** uppercasing the entire query (multi-word names must work)  
   **And** when `q` is empty and no filter params are set, all org cases are returned (paginated)  
   **And** filter query params (AC2) combine with the `q` subset using **AND** semantics: `(org scope) AND (q-OR-block if q present) AND (each supplied filter)`

2. **Given** filter query parameters are supplied  
   **When** search runs  
   **Then** filters combine with **AND** semantics against the org-scoped base query  
   **And** supported filters for this story:
   - `currentStage` — exact `CaseStage` enum string
   - `typeOfOffence` — case-insensitive substring
   - `offenceClassification` — `Petty` | `Serious` | `Heinous`
   - `domicile` — `Urban` | `Rural` | `Coastal` | `Tribal` | `Slum` (pilot **district/area** proxy per spec)
   - `createdByUserId` — UUID; pilot **staff** filter (assigned-staff filter upgrades in Story 2.8)
   - `overdue` — `true` returns cases where `nextVisitDueAtUtc` is not null, `<` UTC now, and `currentStage != TerminationExclusion`
   **And** invalid enum values → **400** Problem Details with clear `detail`  
   **And** search is read-only — **no** audit row written

3. **Given** pagination parameters  
   **When** I pass `page` (default `1`, min `1`) and `pageSize` (default `25`, max `100`)  
   **Then** `data.items` length ≤ `pageSize`  
   **And** `meta.totalCount` reflects total matches across all pages

4. **Given** pilot volume (~10k cases assumption per NFR-8 / FR-4)  
   **When** integration tests seed **≥50** cases and coordinator searches by crime number with required btree indexes  
   **Then** a single-page search completes in **< 2 seconds** wall-clock time in the containerized integration suite (best-effort CI guardrail — not a formal p95 benchmark)  
   **And** migration adds **required** list index on `(organisation_id, updated_at_utc)` — do not rely on sequential scans at pilot scale

5. **Given** I am **SocialWorker** or **CaseWorker**  
   **When** I `GET /api/v1/cases/search`  
   **Then** **403** with `Policies.ForbiddenByRoleMessage` (field-worker registry access deferred to Story 2.8/2.9)

6. **Given** I am deactivated (`IsActive = false`)  
   **When** I call search or preset endpoints  
   **Then** **403** with `AuthService.DeactivatedMessage`

7. **Given** saved filter presets  
   **When** I `GET /api/v1/cases/search-presets`  
   **Then** **200 OK** with `{ data: CaseSearchPresetDto[] }` for **my user** in my organisation only  
   **When** I `POST /api/v1/cases/search-presets` with `{ name, filters }`  
   **Then** **201 Created** with envelope `{ data: CaseSearchPresetDto }` (includes `id`, `name`, `filters`, `createdAtUtc`)  
   **And** `filters` JSON mirrors search query shape (camelCase keys); empty `filters` object is allowed (means no filters)  
   **And** `name` is required, trimmed, max **64** characters — empty/whitespace or too long → **400** Problem Details  
   **And** preset names are unique per user per organisation (duplicate name → **409 Conflict**)  
   **When** I `DELETE /api/v1/cases/search-presets/{id}` for my preset  
   **Then** **204 No Content**  
   **When** I delete another user's preset or unknown id  
   **Then** **404** Problem Details  
   **And** preset mutations require `CoordinatorOrAbove` (same as search)

8. **Given** I am logged in on web with `supervisorGuard` and open **Case registry** (`/cases`, `canActivate: [authGuard, supervisorGuard]`)  
   **When** the page loads  
   **Then** search input, filter controls, results table, and pagination call `GET /api/v1/cases/search` via `CaseApiService` (HttpClient + api-client types)  
   **And** I can save current filters as a named preset and reload a preset into the form  
   **And** row click navigates to existing `/cases/{id}` placeholder detail (pass `CaseSummaryDto` in router `state.summary`)  
   **And** **Create case** link to `/cases/new` remains available  
   **And** pressing **`/`** focuses the search input when focus is not already in an editable field (EXPERIENCE.md — no hijack while typing)  
   **And** `aria-live="polite"` announces result count changes (e.g. "12 cases found" or "No cases match your filters")  
   **And** zero results show neutral copy **"No cases match your filters."** (not an error state)  
   **And** errors use `CaseApiService.extractErrorMessage()` with visible `errorMessage` signal; on search error, filter form stays editable and prior results remain visible until the user changes criteria (do not lock the UI — Story 2.5 lesson)

9. **Given** OpenAPI and client contract  
   **When** this story ships  
   **Then** OpenAPI documents `GET /cases/search`, preset CRUD routes, DTOs, and pagination `meta.totalCount`  
   **And** `packages/api-client` regenerated; web `case.models.ts` re-exports new types  
   **And** README documents search + presets + pilot filter semantics (staff/overdue/district)

10. **Given** test baseline after Story 2.5 (**118** .NET: 1 unit + 117 integration; **34** web; **22** mobile)  
    **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
    **Then** all existing tests pass  
    **And** new integration tests cover: text search by crime; `q` matches domicile (area); filter by stage; filter by domicile param; createdByUserId filter; overdue filter with seeded `nextVisitDueAtUtc`; pagination totalCount asserted on `meta.totalCount`; director search 200; social worker search 403; case worker preset POST 403; deactivated search 403; deactivated preset 403; unauthenticated search 401; unauthenticated preset list 401; preset save/list/delete; invalid preset name 400; duplicate preset name 409; Swagger contains `/api/v1/cases/search` and `search-presets`  
    **And** new web tests cover: registry renders results, filter apply triggers search, preset save/load, `/` focuses search input, row navigation, zero-results copy, search error keeps form editable  
    **Verified baseline to beat:** **118** .NET, **34** web, **22** mobile

11. **Given** Stories 2.7–2.9 are not yet implemented  
    **When** this story ships  
    **Then** **no** Excel/PDF export buttons (2.7), **no** assignment transfer API (2.8), **no** full sidebar IA / rich detail / stage-edit UI (2.9)  
    **And** **no** mobile registry search screen (supervisors use web; mobile Cases tab unchanged except tests still green)

## Tasks / Subtasks

- [x] **API — schema additions** (AC: 2, 4, 11)
  - [x] Migration `AddCaseSearchSupport`:
    - `cases.next_visit_due_at_utc` nullable `timestamptz` (overdue filter; Epic 3 visit scheduler will populate later; create/merge leave null)
    - Table `case_search_presets` (`id`, `organisation_id`, `user_id`, `name` max 64, `filters_json` jsonb, `created_at_utc`, `updated_at_utc`)
    - Unique index `(organisation_id, user_id, name)` on presets
    - **Required** index on `(organisation_id, updated_at_utc)` for list ordering at scale
    - Optional enhancement: `pg_trgm` GIN on `beneficiary_name` for faster ILIKE — skip if migration tooling blocks; document ILIKE limitation in README
  - [x] Update `Case` entity + `CaseConfiguration`

- [x] **API — DTOs** (AC: 1, 3, 7, 9)
  - [x] `CaseSummaryDto` — registry row fields: `id`, `crimeNumber`, `stNumber`, `beneficiaryName`, `currentStage`, `typeOfOffence`, `offenceClassification`, `domicile`, `visitCount`, `createdByUserId`, `updatedAtUtc`, `nextVisitDueAtUtc` (nullable)
  - [x] `CaseSearchResultDto` — `{ items: CaseSummaryDto[], page, pageSize }`
  - [x] `CaseSearchFiltersDto` — serializable filter object for presets (mirror query params)
  - [x] `CreateCaseSearchPresetRequest` — `{ name, filters }`
  - [x] `CaseSearchPresetDto` — `{ id, name, filters, createdAtUtc }`

- [x] **API — CaseService.SearchAsync** (AC: 1–4)
  - [x] `SearchAsync(CaseSearchQuery query)` — org scope from JWT; compose EF `IQueryable` filters; project to `CaseSummaryDto`
  - [x] Validate enums and pagination bounds; throw `CaseValidationException` for 400
  - [x] Return `(items, totalCount)` for controller to set `meta.totalCount`
  - [x] **Do not** duplicate business rules in controller

- [x] **API — preset service** (AC: 7)
  - [x] `CaseSearchPresetService` or methods on `CaseService` — list/create/delete scoped to actor user + org
  - [x] Validate preset `name` (required, trim, max 64); throw `CaseValidationException` for 400
  - [x] Serialize `filters` with `System.Text.Json` web defaults (camelCase)
  - [x] Duplicate name → `CaseConflictException` (409)

- [x] **API — CasesController routes** (AC: 1, 5, 6, 7, 9)
  - [x] `GET search` — **before** `{id:guid}` routes; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] `GET/POST search-presets`, `DELETE search-presets/{id:guid}`
  - [x] `[ProducesResponseType]` for 200/201/204/400/401/403/404/409
  - [x] **Pagination envelope (required):** search action returns `Ok(new ApiResponse<CaseSearchResultDto>(result, new ApiMeta { RequestId = requestId, TotalCount = totalCount }))` so `ApiEnvelopeFilter` skips re-wrap (`IsAlreadyWrapped`) — **do not** rely on filter alone; it only sets `RequestId` today (`ApiEnvelopeFilter.cs`)

- [x] **API — integration tests** (AC: 10)
  - [x] `tests/api.integration/CaseSearchTests.cs` — `[Collection("AuthIntegration")]`
  - [x] `tests/api.integration/CaseSearchPresetTests.cs` or combined file
  - [x] Extend `CaseTestData` — `SetNextVisitDueAsync(caseId, utc)` via DbContext scope; `SearchCasesAsync(client, token, query)` HTTP helper; `CreateSearchPresetAsync` / `DeleteSearchPresetAsync` helpers
  - [x] `SwaggerEndpointTests` — assert `/api/v1/cases/search` and `search-presets` in swagger JSON

- [x] **OpenAPI + api-client** (AC: 9, 10)
  - [x] `EXPORT_OPENAPI_PATH` export + `API_OPENAPI_FILE` + `npm run generate:api-client` + rebuild `dist`

- [x] **Web — case registry** (AC: 8, 10)
  - [x] Route `path: 'cases'`, `canActivate: [authGuard, supervisorGuard]` (register **before** `cases/:id` in `app.routes.ts`)
  - [x] `features/cases/registry/case-registry.component` — standalone, signals for `query`, `filters`, `items`, `totalCount`, `page`, `loading`, `errorMessage`, `presets`
  - [x] `case-api.service.ts` — `searchCases(params)`, preset CRUD methods
  - [x] `case.models.ts` — re-export new api-client types
  - [x] `case-detail-placeholder.component.ts` — extend `CaseDetailState.summary` union to include `CaseSummaryDto`
  - [x] Link from `supervisor-home` to `/cases` ("Case registry")
  - [x] HostListener for `/` focus shortcut
  - [x] Tests: `case-registry.component.spec.ts`

- [x] **Documentation** (AC: 9)
  - [x] README — search endpoint, query params, preset CRUD, pilot semantics for staff/overdue/district

### Review Findings

- [x] [Review][Patch] Escape ILIKE wildcards in free-text search [`apps/api/Infrastructure/Cases/CaseService.cs:376`]
- [x] [Review][Patch] Add integration test: delete another user's preset returns 404 [`tests/api.integration/CaseSearchTests.cs`]
- [x] [Review][Patch] Add integration tests: invalid `offenceClassification` / `domicile` search params return 400 [`tests/api.integration/CaseSearchTests.cs`]
- [x] [Review][Patch] Add integration test: preset name longer than 64 chars returns 400 [`tests/api.integration/CaseSearchTests.cs`]
- [x] [Review][Patch] Add integration test: deactivated coordinator POST search-presets returns 403 [`tests/api.integration/CaseSearchTests.cs`]
- [x] [Review][Defer] XML doc comments on new CasesController endpoints [`apps/api/Controllers/V1/CasesController.cs`] — deferred, pre-existing (entire controller lacks `///` summaries)

## Dev Notes

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Stories 2.1–2.5 delivered create, stages, duplicate check, match sheet, and merge. **Story 2.6** delivers **`GET /api/v1/cases/search`**, **saved presets**, and a **minimal web registry page** with `/` search focus. Story 2.7 adds export; 2.8 assignment; 2.9 full sidebar registry + detail + stage edit UI.

### Pagination envelope (READ FIRST)

`ApiEnvelopeFilter` today wraps plain DTOs with `RequestId` only — it **never** sets `TotalCount` (`ApiEnvelopeFilter.cs`). For search:

```csharp
return Ok(new ApiResponse<CaseSearchResultDto>(
    result,
    new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
```

`IsAlreadyWrapped` prevents double-wrap. Integration tests must assert `meta.totalCount` on the JSON envelope, not only `data.items.length`.

### Query composition

`(organisation_id from JWT)` **AND** `(q matches any column — OR)` **AND** `(each supplied filter param — AND)`. Example: `q=Urban&domicile=Rural` returns nothing; `q=Urban` with no domicile filter matches cases whose domicile string contains "Urban" or other columns match.

### Search query contract

`GET /api/v1/cases/search`

| Param | Type | Notes |
|-------|------|-------|
| `q` | string | Optional free text; OR-match on crime, ST, name, contact, **domicile string** (area proxy) |
| `currentStage` | string | Optional `CaseStage` enum |
| `typeOfOffence` | string | Optional substring |
| `offenceClassification` | string | Optional enum |
| `domicile` | string | Optional enum — **district/area** stand-in until dedicated district field |
| `createdByUserId` | uuid | Optional — **staff** filter (pilot: created-by; Story 2.8 adds true assignee) |
| `overdue` | bool | Optional; uses `nextVisitDueAtUtc` |
| `page` | int | Default 1 |
| `pageSize` | int | Default 25, max 100 |

**Success:** `200` — `{ data: { items, page, pageSize }, meta: { requestId, totalCount } }`

### Preset filters JSON example

```json
{
  "q": "CR-2024",
  "currentStage": "Rehabilitation",
  "domicile": "Urban",
  "overdue": true
}
```

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `CasesController.cs` | create, check-duplicate, stage, merge | Add `GET search`, preset routes |
| `CaseService.cs` | Create, CheckDuplicate, TransitionStage, Merge | Add `SearchAsync` + preset methods |
| `CaseDto` | Minimal summary (5 fields + dates) | Add **`CaseSummaryDto`** for registry rows (do not break existing `CaseDto` consumers) |
| `cases` table | No `next_visit_due_at_utc` | Migration adds column + indexes |
| Web routes | `/cases/new`, `/cases/:id` placeholder | Add `/cases` registry |
| `supervisor-home` | Link to new case only | Add registry link |
| Mobile | `CasesListScreen` placeholder | **No change** (supervisor web-only for search) |
| `ApiEnvelopeFilter` | Sets `RequestId` only | Search returns pre-wrapped `ApiResponse<>` with `TotalCount` |

**Do not break:**
- Create, merge, duplicate-check, stage transition flows and tests
- `CoordinatorOrAbove` RBAC pattern on case endpoints
- Normalization rules on crime/ST for duplicate check — search `q` should uppercase identifier-like tokens consistently
- Placeholder detail route `/cases/:id` — registry rows navigate there with `state`
- Modal stack rule — registry uses inline filters, not stacked dialogs

### API route ordering (critical)

Register static segments before parameterized routes:

```
GET  /api/v1/cases/search
GET  /api/v1/cases/search-presets
POST /api/v1/cases/search-presets
DELETE /api/v1/cases/search-presets/{id}
POST /api/v1/cases/check-duplicate   (existing)
POST /api/v1/cases                    (existing)
PATCH /api/v1/cases/{id}/stage        (existing)
POST /api/v1/cases/{id}/merge         (existing)
```

Angular routes — register `cases` before `cases/:id`:

```typescript
{ path: 'cases', ... registry ... },
{ path: 'cases/new', ... },
{ path: 'cases/:id', ... detail ... },
```

### File structure (expected touch list)

```
apps/api/
├── Domain/Entities/Case.cs                          # UPDATE — NextVisitDueAtUtc
├── Domain/Entities/CaseSearchPreset.cs              # NEW
├── Infrastructure/Persistence/CaseConfiguration.cs  # UPDATE
├── Infrastructure/Persistence/CaseSearchPresetConfiguration.cs # NEW
├── Infrastructure/Persistence/Migrations/           # NEW migration
├── Infrastructure/Cases/CaseService.cs                # UPDATE — SearchAsync
├── Infrastructure/Cases/CaseSearchPresetService.cs  # NEW (or merge into CaseService)
├── Models/Cases/CaseDtos.cs                         # UPDATE — search + preset DTOs
├── Controllers/V1/CasesController.cs                # UPDATE — search + presets
tests/api.integration/CaseSearchTests.cs             # NEW
tests/api.integration/CaseSearchPresetTests.cs       # NEW (or combined)
tests/api.integration/CaseCreateTests.cs             # UPDATE — SearchCasesAsync, preset helpers
tests/api.integration/SwaggerEndpointTests.cs        # UPDATE

apps/web/src/app/
├── app.routes.ts                                    # UPDATE — /cases registry route order + supervisorGuard
├── features/home/supervisor-home.component.html     # UPDATE — registry link
├── features/cases/registry/                         # NEW — case-registry component
├── features/cases/detail/case-detail-placeholder.component.ts # UPDATE — CaseSummaryDto in state union
├── features/cases/services/case-api.service.ts      # UPDATE
├── features/cases/models/case.models.ts             # UPDATE

packages/api-client/                                 # REGENERATE
README.md                                            # UPDATE
```

### Previous story intelligence (2.5)

- **`ValidateIntakeRequest`** shared — search does not use create validation
- **Single-transaction audit** on mutations — search/presets are reads except preset CRUD (no case audit on preset save)
- **Integration tests:** `[Collection("AuthIntegration")]`, `CaseTestData` helpers, `RbacTestData`, `AuthTestData.Email` for director
- **Web HTTP:** `case-api.service.ts` + `case.models.ts` re-exports — not raw fetch
- **`CaseApiError`** + `extractErrorMessage()` — reuse on registry
- **Test baseline:** **118** .NET, **34** web, **22** mobile — maintain green
- **Code review patches (2.5):** merge error unblocks save — registry must not lock filters/results on search API error; keep form editable, show `errorMessage`, retain prior results until criteria change

### Previous story intelligence (2.4–2.1)

- **Normalization:** server `Trim()` + `ToUpperInvariant()` on crime/ST for identifier lookups
- **Placeholder detail** at `/cases/:id` — reuse for registry row open
- **`supervisorGuard`** on web case routes — registry uses same guard
- **TerminationExclusion** cases appear in search results (do not filter out unless UX spec says otherwise)
- **Director + Coordinator** test users via `RbacTestData` / `AuthTestData`

### Pilot filter semantics (document in README)

| Spec term | Story 2.6 implementation | Future story |
|-----------|-------------------------|--------------|
| district / area | `q` matches domicile string + `domicile` filter param | Dedicated district field if added later |
| assigned staff | `createdByUserId` filter | Story 2.8 true assignee |
| overdue | `nextVisitDueAtUtc < now` | Epic 3 visit scheduler populates due dates |

### Architecture compliance

- **Business logic in `CaseService`** — not controller, not Angular [Source: `project-context.md`]
- **HTTP types from api-client** — re-export in `case.models.ts`
- **RFC 7807 Problem Details** + envelope on success
- **Pagination** per `architecture.md` §5.3 — `page`, `pageSize`, `meta.totalCount`
- **Angular:** standalone components, signals; Material table or card list for results
- **No modal stack >1** — filters inline or expansion panel, not dialog-on-dialog

### UX compliance

- Registry search: operational tone, no gamification [Source: `DESIGN.md`]
- **`/` focuses registry search** on web [Source: `EXPERIENCE.md`]
- WCAG: `aria-live` for result count; keyboard shortcut does not steal focus from inputs
- Row click → case detail (placeholder until 2.9)

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 118 existing + search/preset tests |
| Web | `npm run test:web` | 34 existing + registry specs |
| Mobile | `npm run test:mobile` | 22 unchanged |

**During development:**

```bash
npm run test:api:cases    # case integration tests only
npm run test:web
```

**Integration test matrix (minimum):**

| Scenario | Status |
|----------|--------|
| Search by crime substring | 200 + match |
| `q` matches domicile (area full-text) | 200 + match |
| Filter currentStage | 200 + filtered |
| Filter domicile param | 200 |
| Filter createdByUserId | 200 |
| Overdue true with due date in past | 200 + includes case |
| Overdue false / null due date excluded | 200 |
| Pagination page 2 + `meta.totalCount` | 200 + stable total |
| Director search | 200 |
| Social worker search | 403 |
| Case worker preset POST | 403 |
| Deactivated search / preset | 403 |
| Unauthenticated search / preset list | 401 |
| Invalid preset name | 400 |
| Save preset + list + delete | 201/200/204 |
| Duplicate preset name | 409 |
| Swagger `/api/v1/cases/search` + `search-presets` | pass |
| Perf: 50+ seeded cases, crime search <2s | pass (best-effort) |

### Scope boundaries

| In scope (2.6) | Out of scope |
|----------------|--------------|
| `GET /cases/search` API + indexes | Excel/PDF export (2.7) |
| Preset CRUD per user | Assignment transfer API (2.8) |
| Web `/cases` registry + `/` shortcut | Full sidebar IA (2.9) |
| `next_visit_due_at_utc` column (nullable) | Visit scheduler writing due dates (Epic 3) |
| OpenAPI + api-client regen | Mobile registry search UI |
| `CaseSummaryDto` for list rows | `GET /cases/{id}` rich detail API (2.9) |
| Link from supervisor home | Stage edit UI on detail (2.9) |

### Definition of Done

- [x] Search API returns paginated results with `meta.totalCount` on envelope; wall-clock <2s on seeded integration test (≥50 cases)
- [x] All FR-6 search/filter dimensions implemented per pilot semantics table (or documented deferral none — overdue uses new column)
- [x] Presets save/load/delete per user
- [x] Web registry page with `/` shortcut and preset UX
- [x] api-client regenerated; README updated
- [x] 118+ .NET, 34+ web, 22 mobile tests green

### OpenAPI regeneration (Windows)

```text
set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json
dotnet test tests/api.integration --filter "FullyQualifiedName~Export_Swagger_WhenRequested"
set API_OPENAPI_FILE=c:\Users\Admin\source\repos\Midi-Kaval\packages\api-client\openapi-snapshot.json
npm run generate:api-client
npm run build -w @midi-kaval/api-client
```

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.6, FR-6]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-6, NFR-8]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 `GET /cases/search`, pagination]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — `/` registry search]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — Search and filtering]
- [Source: `_bmad-output/project-context.md` — api-client, RBAC, testing]
- [Source: `_bmad-output/implementation-artifacts/2-5-case-merge-workflow.md` — patterns, test baseline]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — extend patterns]
- [Source: `apps/api/Infrastructure/ApiEnvelopeFilter.cs` — pre-wrap pattern for TotalCount]
- [Source: `apps/api/Models/ApiMeta.cs` — `TotalCount` property]

## Dev Agent Record

### Agent Model Used

claude-opus-4-6

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created
- Validated 2026-06-15 — 12 validation fixes applied (see validation report)
- Implemented `GET /api/v1/cases/search` with org-scoped EF filters, `meta.totalCount` pre-wrapped envelope, and `(organisation_id, updated_at_utc)` index
- Added `case_search_presets` table + `CaseSearchPresetService` for per-user preset CRUD
- Added web `/cases` registry with filters, presets, `/` search focus, aria-live result count, and error-resilient search UX
- Regenerated `packages/api-client` from OpenAPI snapshot
- Test results: **140** .NET (1 unit + 139 integration), **42** web, **22** mobile — all green
- Code review 2026-06-15: 5 patch findings applied (ILIKE escape, 4 integration test gaps); 1 deferred (XML docs)
- Post-review .NET integration: **146** total (28 case-search tests)

### File List

- apps/api/Domain/Entities/Case.cs
- apps/api/Domain/Entities/CaseSearchPreset.cs
- apps/api/Infrastructure/Persistence/CaseConfiguration.cs
- apps/api/Infrastructure/Persistence/CaseSearchPresetConfiguration.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Migrations/20260615161135_AddCaseSearchSupport.cs
- apps/api/Migrations/20260615161135_AddCaseSearchSupport.Designer.cs
- apps/api/Migrations/AppDbContextModelSnapshot.cs
- apps/api/Infrastructure/Cases/CaseService.cs
- apps/api/Infrastructure/Cases/CaseSearchPresetService.cs
- apps/api/Models/Cases/CaseDtos.cs
- apps/api/Models/Cases/CaseSearchQuery.cs
- apps/api/Controllers/V1/CasesController.cs
- apps/api/Program.cs
- tests/api.integration/CaseSearchTests.cs
- tests/api.integration/CaseCreateTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- tests/api.integration/UsersSchemaTests.cs
- apps/web/src/app/app.routes.ts
- apps/web/src/app/features/home/supervisor-home.component.html
- apps/web/src/app/features/cases/registry/case-registry.component.ts
- apps/web/src/app/features/cases/registry/case-registry.component.html
- apps/web/src/app/features/cases/registry/case-registry.component.scss
- apps/web/src/app/features/cases/registry/case-registry.component.spec.ts
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.ts
- apps/web/src/app/features/cases/services/case-api.service.ts
- apps/web/src/app/features/cases/models/case.models.ts
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- README.md

### Change Log

- 2026-06-15: Story 2.6 — case search API, saved presets, web registry page, OpenAPI/api-client regen
- 2026-06-15: Code review patches — ILIKE wildcard escape, expanded integration test coverage
