# Story Validation Report — 2.7 Case Export to Excel and PDF

**Story:** `2-7-case-export-to-excel-and-pdf`  
**Validated:** 2026-06-16  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (8 fixes applied 2026-06-16)

---

## Summary

Story 2.7 correctly scopes synchronous `GET /api/v1/cases/search/export` for Coordinator/Director, reusing Story 2.6 search filters, with ClosedXML/QuestPDF per architecture §5.7 and web registry download buttons. Brownfield alignment with 2.6 (`SearchAsync`, `CaseSearchQuery`, registry at `/cases`, RBAC) is strong. Eight gaps could cause filter duplication, flaky over-cap tests, missing 422 UX, or ambiguous controller binding.

| Check | Result |
|-------|--------|
| Epic AC coverage (FR-6 export) | Pass |
| Architecture ClosedXML + QuestPDF + sync cap | **Fix** (pin versions + QuestPDF license) |
| Story 2.6 search contract reuse | **Fix** (explicit refactor steps; no duplicate `Where`) |
| Scope vs 2.8–2.9 / Epic 8 async | Pass |
| File download vs JSON envelope | Pass |
| LLM dev-agent clarity | **Fix** (`format` binding, exception mapping, READ FIRST refactor) |
| Testability | **Fix** (over-cap env trick, director + invalid enum on export) |

---

## Critical Issues (Must Fix)

### 1. Shared filter logic — risk of copy-paste duplication

`CaseService.SearchAsync` already implements org scope, ILIKE escape (`ToILikeContainsPattern`), enum validation, overdue rule, and ordering (~140 lines). Story mentioned "reuse" but did not mandate refactor order — dev agent could fork filters into `ExportAsync`.

**Fix:** Added **Shared filter refactor (READ FIRST)** Dev Notes section and task bullets: extract validation + `ApplySearchFilters(IQueryable, CaseSearchQuery)`; require all 2.6 search tests green after refactor.

### 2. Controller `format` binding ambiguous

`CaseSearchQuery` has no `format` property. Story did not specify action signature — dev might add `format` to DTO or miss `[FromQuery]` binding.

**Fix:** Task specifies `Export([FromQuery] string format, [FromQuery] CaseSearchQuery query, ...)` with explicit note that `format` is **not** on `CaseSearchQuery`.

### 3. Over-cap integration test impractical

Original AC implied seeding 5001 cases for 422 — slow and brittle in Testcontainers CI.

**Fix:** AC8 + test matrix — set `CaseExport__MaxRows=2` via environment variable (same pattern as `AuthWebApplicationFactory.ApplyTestConfiguration`) and seed 3 cases.

### 4. Web 422 over-cap UX missing

AC6 disabled buttons only when `totalCount === 0`. Over-cap returns 422 while `totalCount > 0` — buttons stay enabled but story did not require surfacing `detail` (user would see generic error or silent failure).

**Fix:** AC6 — show Problem Details `detail` in `errorMessage` on 422; keep filter form editable (2.6 pattern).

---

## Enhancement Opportunities (Should Add)

### 5. NuGet versions and QuestPDF license unspecified

Architecture names ClosedXML + QuestPDF but story left versions open — restore drift risk. QuestPDF requires explicit community license for non-commercial use.

**Fix:** Pin ClosedXML `0.104.2`, QuestPDF `2024.12.3`; `QuestPDF.Settings.License = LicenseType.Community` in `Program.cs`.

### 6. Integration test matrix gaps vs 2.6 RBAC patterns

Missing **Director** export 200 and **invalid filter enum** 400 on export path (2.6 added these for search after code review).

**Fix:** Extended AC8 and integration test matrix with director 200 and invalid `domicile` → 400.

### 7. Controller exception mapping not explicit

Stage transition already maps `CaseValidationException` → 400 and `CaseBusinessRuleException` → 422. Export story did not reference same helpers.

**Fix:** Controller task — catch and map same exceptions for export action.

### 8. Web blob download leak

Object URLs from `URL.createObjectURL` should be revoked after anchor click to avoid memory leaks on repeated exports.

**Fix:** Task bullet — revoke object URL after download.

---

## Optimizations (Nice to Have)

1. **UTC filename** — AC1 now uses `cases-export-{yyyyMMdd-HHmmss}Z.{ext}` for unambiguous audit timestamps.
2. **Export columns** — note that staff/assignee column deferred to Story 2.8 (pilot matches registry table).
3. **appsettings.Development.json** — added to expected touch list alongside `appsettings.json`.
4. **Swagger assertion** — full path `/api/v1/cases/search/export` (not substring `search/export`).
5. **xlsx magic bytes** — optional future test (`PK\x03\x04` ZIP header); not required for pilot.

---

## LLM Optimization

- **READ FIRST** callout at top of Dev Notes for refactor-before-export sequence.
- Pinned package versions and exception mapping in task bullets (scannable, unambiguous).
- Test matrix uses env-based cap instead of prose "seed 5001".

---

## Verdict

Story is **implementable** and well-scoped vs 2.8–2.9 and Epic 8 async reports. All **8** fixes applied to the story file. Safe to run `dev-story` for Story 2.7.

**Baseline to beat:** 146 .NET (1 unit + 145 integration), 42 web, 22 mobile.
