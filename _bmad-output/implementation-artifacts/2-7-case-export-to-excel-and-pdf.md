---
baseline_commit: NO_VCS
---

# Story 2.7: Case Export to Excel and PDF

Status: done

<!-- Validated: 2026-06-16 — see 2-7-case-export-to-excel-and-pdf-validation-report.md (8 fixes applied) -->

## Story

As a **Coordinator or Director**,
I want to export filtered Cases to Excel or PDF,
so that I share read-only reports without operational Excel sync (FR-6).

*Note: Export applies to **all rows matching the current registry filters** (not only the visible page). Async job queue + blob storage for very large exports is Epic 8 (`POST /reports/{type}/export`); this story delivers **synchronous** pilot exports under a row cap. Mobile has no export UI.*

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `GET /api/v1/cases/search/export` with `format` = `xlsx` or `pdf` and the **same optional query parameters** as `GET /api/v1/cases/search` (see Story 2.6 **Search query contract**)  
   **Then** response is **200 OK** with `Content-Type` `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (xlsx) or `application/pdf` (pdf)  
   **And** `Content-Disposition: attachment` with filename `cases-export-{yyyyMMdd-HHmmss}Z.{xlsx|pdf}` using **UTC** clock  
   **And** the file contains **every case row matching the filter** in my organisation (not paginated to `page`/`pageSize` — export ignores pagination params)  
   **And** columns match the registry export set (see **Export columns** below)  
   **And** filter semantics are **identical** to `CaseService.SearchAsync` (reuse shared query composition — do not duplicate filter logic)  
   **And** export is a read-only snapshot — **no** case row mutations and **no** Excel write-back sync channel

2. **Given** the filtered result count exceeds the pilot export cap (`CaseExportOptions.MaxRows`, default **5000**)  
   **When** I request export  
   **Then** **422 Unprocessable Entity** Problem Details with `detail` instructing the user to narrow filters (e.g. "Export limited to 5000 cases; refine filters and try again.")  
   **And** no file is generated

3. **Given** `format` is missing, not `xlsx`/`pdf`, or invalid  
   **When** I request export  
   **Then** **400** Problem Details with clear `detail`

4. **Given** I am **SocialWorker** or **CaseWorker**  
   **When** I call export  
   **Then** **403** with `Policies.ForbiddenByRoleMessage`

5. **Given** I am deactivated (`IsActive = false`)  
   **When** I call export  
   **Then** **403** with `AuthService.DeactivatedMessage`

6. **Given** I am on web **Case registry** (`/cases`) with active filters  
   **When** I click **Export Excel** or **Export PDF**  
   **Then** the browser downloads a file via `CaseApiService.exportCases(format, params)` using the **current registry filter state** (same params as the last search, not an empty filter set)  
   **And** both export buttons are **disabled** while an export request is in flight  
   **And** buttons are disabled when `totalCount === 0` (nothing to export)  
   **And** errors use `CaseApiService.extractErrorMessage()` with visible `errorMessage` signal; filter form stays editable (Story 2.6 pattern)  
   **And** when export returns **422** (over row cap), show the Problem Details `detail` in `errorMessage` — do not disable the filter form  
   **And** export does not clear the results table

7. **Given** OpenAPI and client contract  
   **When** this story ships  
   **Then** OpenAPI documents `GET /cases/search/export` with `format` query param and shared search filter params  
   **And** `packages/api-client` regenerated (export may use `HttpClient` blob response — document if not in generated client)  
   **And** README documents export endpoint, column set, row cap, and read-only snapshot semantics

8. **Given** test baseline after Story 2.6 (**146** .NET: 1 unit + 145 integration; **42** web; **22** mobile)  
   **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
   **Then** all existing tests pass  
   **And** new integration tests cover: coordinator xlsx export 200 + correct content-type + non-empty body; pdf export 200 + `application/pdf`; export rows match search filter (subset assertion); export ignores pagination (seed > pageSize, export contains all matches); over-cap 422 (set `CaseExport__MaxRows=2` via env + seed 3 cases); invalid format 400; invalid search filter enum on export 400; director export 200; social worker 403; deactivated 403; unauthenticated 401; Swagger contains `/api/v1/cases/search/export`  
   **And** new web tests cover: export buttons disabled when totalCount 0; export triggers API with current filters; buttons disabled while exporting  
   **Verified baseline to beat:** **146** .NET, **42** web, **22** mobile

9. **Given** Stories 2.8–2.9 are not yet implemented  
   **When** this story ships  
   **Then** **no** assignment transfer / handoff whisper API (2.8), **no** full sidebar IA / rich detail / stage-edit UI (2.9)  
   **And** **no** async blob job queue for case export (Epic 8 reports pattern)  
   **And** **no** mobile export UI

## Tasks / Subtasks

- [x] **API — NuGet packages** (AC: 1, 7)
  - [x] Add **ClosedXML** `0.104.2` (Excel) and **QuestPDF** `2024.12.3` (PDF) to `apps/api/MidiKaval.Api.csproj` — .NET 8 compatible; adjust patch version only if restore fails
  - [x] QuestPDF: set `QuestPDF.Settings.License = LicenseType.Community` once at startup (`Program.cs`)
  - [x] Add `CaseExport` config section (`MaxRows` default 5000) in `appsettings.json` + `appsettings.Development.json`; bind `CaseExportOptions` in `Program.cs`

- [x] **API — refactor search query composition** (AC: 1)
  - [x] Extract from `SearchAsync`: (a) enum/pagination **validation**, (b) `ApplySearchFilters(IQueryable<Case> cases, CaseSearchQuery query)` returning filtered `IQueryable<Case>` — used by both `SearchAsync` and `ExportAsync`
  - [x] **Do not** fork filter logic between search and export; refactoring `SearchAsync` must keep all existing search integration tests green

- [x] **API — export service** (AC: 1–3)
  - [x] `CaseService.ExportAsync(CaseSearchQuery query, string format)` → `(byte[] content, string contentType, string fileName)`
  - [x] Count matches first via shared filters; throw `CaseBusinessRuleException` (422) when `totalCount > MaxRows`
  - [x] Throw `CaseValidationException` (400) for bad `format` or invalid filter enums (same rules as search)
  - [x] Materialize all matching rows ordered by `updatedAtUtc` descending; project to export row DTO (same fields as **Export columns**)
  - [x] `CaseExcelExporter` / `CasePdfExporter` in `Infrastructure/Cases/` (or methods on export service) — keep controllers thin

- [x] **API — controller route** (AC: 1, 4, 5, 7)
  - [x] `GET search/export` on `CasesController` — signature: `Export([FromQuery] string format, [FromQuery] CaseSearchQuery query, ...)` — `format` is **not** on `CaseSearchQuery`
  - [x] Register **before** `{id:guid}` routes; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] Return `File(content, contentType, fileName)` — **not** JSON envelope (`FileResult` bypasses `ApiEnvelopeFilter`)
  - [x] Catch `CaseValidationException` → 400, `CaseBusinessRuleException` → 422 (same helpers as stage transition)
  - [x] `[ProducesResponseType]` for 200 (file), 400, 401, 403, 422

- [x] **API — integration tests** (AC: 8)
  - [x] `tests/api.integration/CaseExportTests.cs` — `[Collection("AuthIntegration")]`
  - [x] Extend `CaseTestData` — `ExportCasesAsync(client, token, format, query)` helper
  - [x] `SwaggerEndpointTests` — assert `/api/v1/cases/search/export` in swagger JSON

- [x] **OpenAPI + api-client** (AC: 7, 8)
  - [x] Export OpenAPI snapshot; regen client if applicable
  - [x] Web export likely uses `HttpClient` `responseType: 'blob'` — document in README if not generated as typed operation

- [x] **Web — registry export buttons** (AC: 6, 8)
  - [x] `case-registry.component` toolbar — **Export Excel** / **Export PDF** buttons
  - [x] `case-api.service.ts` — `exportCases(format, params)` builds query string from current filters, downloads blob (`responseType: 'blob'`), triggers browser save (object URL + anchor click); **revoke** object URL after download
  - [x] `exporting` signal; disable buttons when `loading()`, `exporting()`, or `totalCount() === 0`
  - [x] Tests: `case-registry.component.spec.ts` export scenarios

- [x] **Documentation** (AC: 7)
  - [x] README — export endpoint, formats, row cap, columns, read-only semantics, distinction from Epic 8 async reports

### Review Findings

- [x] [Review][Patch] Missing CaseWorker export 403 test [`tests/api.integration/CaseExportTests.cs`] — AC4 requires CaseWorker forbidden; only SocialWorker covered (pattern in `CaseWorker_Create_Returns403`)
- [x] [Review][Patch] Missing Content-Disposition UTC filename assertion [`tests/api.integration/CaseExportTests.cs`] — AC1 requires `cases-export-{yyyyMMdd-HHmmss}Z.{ext}` attachment header; not verified
- [x] [Review][Patch] Missing export test for absent `format` param [`tests/api.integration/CaseExportTests.cs`] — AC3 requires 400 when format missing; only invalid `csv` tested
- [x] [Review][Patch] Missing web spec for 422 over-cap error message [`case-registry.component.spec.ts`] — AC6 requires Problem Details `detail` in `errorMessage` on export 422
- [x] [Review][Patch] PDF export lacks multi-row parity test [`tests/api.integration/CaseExportTests.cs`] — pagination-ignore test covers xlsx only; PDF all-rows behavior unverified at 30 rows
- [x] [Review][Patch] PDF column headers inconsistent with story/Excel [`CasePdfExporter.cs`] — uses "Area" / "Next Visit Due" vs spec "Area (domicile)" / "Next Visit Due (UTC)"
- [x] [Review][Patch] Blob download anchor not appended to DOM [`case-api.service.ts:158`] — `anchor.click()` without `document.body.appendChild` can fail in some browsers
- [x] [Review][Defer] `CaseExportOptions.MaxRows` not validated for zero/negative [`CaseExportOptions.cs`] — deferred, misconfiguration edge; pilot default 5000 is fine

## Dev Notes

### Shared filter refactor (READ FIRST)

Export **must not** duplicate `SearchAsync` filter blocks. Refactor before adding export:

1. Validate enums / `format` (export only) — throw `CaseValidationException`
2. `ApplySearchFilters(orgScopedQuery, query)` — single `IQueryable` pipeline including ILIKE escape (`ToILikeContainsPattern`)
3. Search: `CountAsync` → paginate → DTO; Export: `CountAsync` → cap check → `ToListAsync` → generate file

Refactoring must leave all Story 2.6 search tests green.

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Stories 2.1–2.6 delivered create, lifecycle, duplicate prevention, merge, **search + presets + web registry**. **Story 2.7** adds **bulk export of filtered cases** to Excel/PDF. Story 2.8 adds assignment/handoff; 2.9 full sidebar registry + detail UI.

### Export columns (pilot)

Match registry table + operational fields supervisors need in shared reports:

| Column | Source field |
|--------|----------------|
| Crime Number | `crimeNumber` |
| ST Number | `stNumber` |
| Beneficiary Name | `beneficiaryName` |
| Stage | `currentStage` |
| Offence Type | `typeOfOffence` |
| Classification | `offenceClassification` |
| Area (domicile) | `domicile` |
| Visits | `visitCount` |
| Next Visit Due (UTC) | `nextVisitDueAtUtc` (empty if null) |
| Updated (UTC) | `updatedAtUtc` |

*Pilot omits `createdByUserId` / staff display name — Story 2.8 adds assignee; export can add staff column later.*

Use human-readable headers in Excel/PDF (e.g. "Crime Number", not camelCase).

### Search query contract (reuse from Story 2.6)

Export accepts the **same query params** as `GET /api/v1/cases/search` except pagination is ignored:

| Param | Notes |
|-------|-------|
| `format` | **Required** — `xlsx` or `pdf` |
| `q`, `currentStage`, `typeOfOffence`, `offenceClassification`, `domicile`, `createdByUserId`, `overdue` | Same semantics as search |
| `page`, `pageSize` | Ignored for export |

**Route:** `GET /api/v1/cases/search/export?format=xlsx&q=...`

### Shared filter logic (critical)

Story 2.6 `SearchAsync` in `CaseService.cs` implements org scope, ILIKE escape (`ToILikeContainsPattern`), enum validation, overdue rule, and ordering. Export **must** call the same filter pipeline:

```
ApplySearchFilters(orgScopedQuery, query) → count → if count > MaxRows throw 422 → ToList ordered → generate file
```

Do **not** copy-paste the `Where` clauses into a second method.

### File download vs envelope

JSON search returns `ApiResponse<T>`. Export returns `FileResult` — `ApiEnvelopeFilter` only wraps `ObjectResult`; file downloads are unaffected.

### Pilot volume & sync export cap

Architecture §5.7: large exports async via job queue (>30s). **Story 2.7** is pilot-scope:

- Synchronous generation when `totalCount ≤ MaxRows` (default 5000)
- **422** when over cap — user narrows filters
- Epic 8 `POST /reports/{type}/export` adds async blob + progress for operational reports; do not implement that pattern here

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `CaseService.SearchAsync` | Full filter + pagination | Refactor shared filters; add `ExportAsync` |
| `CasesController` | search, presets, CRUD | Add `GET search/export` |
| `case-registry.component` | Search, presets, table | Add export buttons + blob download |
| `case-api.service.ts` | `searchCases`, presets | Add `exportCases` |
| NuGet | No ClosedXML/QuestPDF | Add packages |
| Mobile | No registry | **No change** |

**Do not break:**
- Search, presets, create, merge, stage flows and tests
- Registry error UX (editable form on API error)
- `CoordinatorOrAbove` RBAC pattern

### API route ordering (critical)

```
GET  /api/v1/cases/search/export   ← NEW (before search-presets is OK; both static)
GET  /api/v1/cases/search          (existing)
GET  /api/v1/cases/search-presets  (existing)
...
```

### File structure (expected touch list)

```
apps/api/
├── MidiKaval.Api.csproj                             # UPDATE — ClosedXML, QuestPDF
├── appsettings.json                                 # UPDATE — CaseExport:MaxRows
├── appsettings.Development.json                     # UPDATE — CaseExport:MaxRows
├── Infrastructure/Cases/CaseService.cs            # UPDATE — refactor + ExportAsync
├── Infrastructure/Cases/CaseExcelExporter.cs        # NEW
├── Infrastructure/Cases/CasePdfExporter.cs          # NEW
├── Infrastructure/Cases/CaseExportOptions.cs        # NEW
├── Models/Cases/CaseExportQuery.cs                  # NEW (optional — or reuse CaseSearchQuery + format)
├── Controllers/V1/CasesController.cs              # UPDATE — export action
├── Program.cs                                       # UPDATE — bind options
tests/api.integration/CaseExportTests.cs             # NEW
tests/api.integration/CaseCreateTests.cs             # UPDATE — ExportCasesAsync helper
tests/api.integration/SwaggerEndpointTests.cs        # UPDATE

apps/web/src/app/features/cases/
├── registry/case-registry.component.ts/html/spec    # UPDATE — export buttons
├── services/case-api.service.ts                     # UPDATE — exportCases blob download
├── models/case.models.ts                            # UPDATE if new types

packages/api-client/                                 # REGENERATE OpenAPI snapshot
README.md                                            # UPDATE
```

### Previous story intelligence (2.6)

- **`SearchAsync`** + `ToILikeContainsPattern` for `%`/`_` literal match — export inherits via shared filters
- **Pre-wrapped `ApiResponse`** only for JSON search — export uses `File()`
- **Integration tests:** `[Collection("AuthIntegration")]`, `CaseTestData` helpers, `RbacTestData`
- **Web:** signals, `CaseApiService.extractErrorMessage()`, registry at `/cases`
- **Test baseline:** **146** .NET, **42** web, **22** mobile
- **Code review patches (2.6):** ILIKE escape, cross-user preset 404, invalid enum 400 tests — follow same RBAC/deactivated test patterns for export

### Previous story intelligence (2.1–2.5)

- **CoordinatorOrAbove** on all case mutation/list endpoints
- **Problem Details** for 400/403/422
- **OpenAPI regen:** `EXPORT_OPENAPI_PATH` + `API_OPENAPI_FILE` + `npm run generate:api-client`

### Architecture compliance

- **ClosedXML** (Excel) + **QuestPDF** (PDF) per `architecture.md` §5.7 [Source: architecture.md]
- **Business logic in `CaseService` / exporters** — not controller [Source: project-context.md]
- **No hand-edit** `packages/api-client` generated files
- **Read-only** export — no case audit requirement (search is also read-only without audit); optional future `case.export` audit is out of scope

### UX compliance

- Export buttons on registry toolbar (supervisor desk work) [Source: EXPERIENCE.md — Case registry row]
- Disable duplicate export while in flight [Source: EXPERIENCE.md — Reports generating pattern; apply to registry export buttons]
- Operational tone; no gamification [Source: DESIGN.md]

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 146 existing + export tests |
| Web | `npm run test:web` | 42 existing + export button specs |
| Mobile | `npm run test:mobile` | 22 unchanged |

**During development:**

```bash
npm run test:api:cases
npm run test:web
```

**Integration test matrix (minimum):**

| Scenario | Status |
|----------|--------|
| Export xlsx 200 + content-type + body length > 0 | 200 |
| Export pdf 200 + application/pdf | 200 |
| Export matches search filter (seed 3, filter 1) | 200 + 1 row |
| Export ignores pageSize (seed 30, export all) | 200 + ≥30 rows |
| Over MaxRows cap (`CaseExport__MaxRows=2`, seed 3) | 422 |
| Invalid format | 400 |
| Invalid `domicile` on export | 400 |
| Director export | 200 |
| Social worker export | 403 |
| Deactivated export | 403 |
| Unauthenticated | 401 |
| Swagger `/api/v1/cases/search/export` | pass |

### Scope boundaries

| In scope (2.7) | Out of scope |
|----------------|--------------|
| Sync export xlsx/pdf under row cap | Async blob job queue (Epic 8) |
| Registry export buttons (web) | Mobile export |
| Reuse search filters | New filter dimensions |
| ClosedXML + QuestPDF | Operational reports (FR-22, Epic 8) |
| OpenAPI + README | Excel import (Epic 10) |

### Definition of Done

- [x] Export returns xlsx/pdf for all filter-matching rows up to MaxRows
- [x] Shared filter logic with search (no duplication)
- [x] Web registry download buttons with in-flight disable
- [x] api-client/OpenAPI/README updated
- [x] 157 .NET (1 unit + 156 integration), 45 web, 22 mobile tests green

### OpenAPI regeneration (Windows)

```text
set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json
dotnet test tests/api.integration --filter "FullyQualifiedName~Export_Swagger_WhenRequested"
set API_OPENAPI_FILE=c:\Users\Admin\source\repos\Midi-Kaval\packages\api-client\openapi-snapshot.json
npm run generate:api-client
npm run build -w @midi-kaval/api-client
```

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.7, FR-6]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 search, §5.7 exports]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — Search and filtering]
- [Source: `_bmad-output/project-context.md` — api-client, RBAC, testing]
- [Source: `_bmad-output/implementation-artifacts/2-6-case-search-filters-and-saved-presets.md` — search contract, registry, test baseline]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — SearchAsync to refactor]
- [Source: `apps/web/src/app/features/cases/registry/case-registry.component.ts` — add export UI]

## Dev Agent Record

### Agent Model Used

Claude (Cursor Agent)

### Debug Log References

### Completion Notes List

- Validated 2026-06-16 — 8 validation fixes applied (see validation report)
- Implemented `GET /api/v1/cases/search/export` with shared `ApplySearchFilters` refactor; ClosedXML + QuestPDF exporters; `CaseExportOptions.MaxRows` cap with 422
- Web registry Export Excel/PDF buttons with blob download, in-flight disable, and 422 error surfacing via `extractErrorMessage`
- 11 new integration tests (+ `CaseExportOverCapWebApplicationFactory`); 3 new web component specs
- Test baseline: **157** .NET (1 unit + 156 integration), **45** web, **22** mobile — all green
- Code review 2026-06-16: 7 patches applied (CaseWorker 403, filename assertion, missing format, PDF parity test, PDF headers, blob anchor DOM, web 422 spec); `format` query param made nullable for consistent Problem Details
- Post-review baseline: **160** .NET integration (+3 export tests), **46** web (+1 export 422 spec)

### File List

```
apps/api/MidiKaval.Api.csproj
apps/api/appsettings.json
apps/api/appsettings.Development.json
apps/api/Program.cs
apps/api/Controllers/V1/CasesController.cs
apps/api/Infrastructure/Cases/CaseService.cs
apps/api/Infrastructure/Cases/CaseExportOptions.cs
apps/api/Infrastructure/Cases/CaseExcelExporter.cs
apps/api/Infrastructure/Cases/CasePdfExporter.cs
apps/api/Models/Cases/CaseExportRowDto.cs
tests/api.integration/CaseExportTests.cs
tests/api.integration/CaseCreateTests.cs
tests/api.integration/SwaggerEndpointTests.cs
tests/api.integration/AuthWebApplicationFactory.cs
tests/api.integration/MidiKaval.Api.IntegrationTests.csproj
apps/web/src/app/features/cases/services/case-api.service.ts
apps/web/src/app/features/cases/registry/case-registry.component.ts
apps/web/src/app/features/cases/registry/case-registry.component.html
apps/web/src/app/features/cases/registry/case-registry.component.spec.ts
packages/api-client/openapi-snapshot.json
packages/api-client/src/generated/api.ts
README.md
```
