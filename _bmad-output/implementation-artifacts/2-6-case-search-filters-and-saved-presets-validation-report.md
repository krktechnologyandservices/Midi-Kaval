# Story Validation Report — 2.6 Case Search, Filters, and Saved Presets

**Story:** `2-6-case-search-filters-and-saved-presets`  
**Validated:** 2026-06-15  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (12 fixes applied 2026-06-15)

---

## Summary

Story 2.6 correctly scopes `GET /api/v1/cases/search`, preset CRUD, migration for `next_visit_due_at_utc`, and a minimal web registry at `/cases`. Brownfield alignment with Stories 2.1–2.5 is strong. Twelve gaps could cause pagination meta bugs, FR-6 full-text coverage gaps, ambiguous `q` semantics, or incomplete test matrices.

| Check | Result |
|-------|--------|
| Epic AC coverage (FR-6 search/filter/presets) | Pass (area full-text needs clarify) |
| Architecture `GET /cases/search` + pagination | **Fix** (`TotalCount` not wired today) |
| Story 2.5 / 2.4 patterns (HttpClient, RBAC) | Pass |
| Scope vs 2.7–2.9 | Pass |
| Modal stack / supervisor web | **Fix** (explicit `supervisorGuard` on route) |
| LLM dev-agent clarity | **Fix** (`q` OR/AND, envelope meta) |
| Testability | **Fix** (401, director, preset RBAC gaps) |

---

## Critical Issues (Must Fix)

### 1. `meta.totalCount` — `ApiEnvelopeFilter` does not set it today

`ApiEnvelopeFilter.WrapTyped` only sets `RequestId` (`ApiEnvelopeFilter.cs`). `ApiMeta.TotalCount` exists but is unused. Story says "extend filter **or** return `ApiResponse<>`" — too ambiguous; dev agent may return 200 without `totalCount`.

**Fix:** Pin implementation: search action returns `Ok(new ApiResponse<CaseSearchResultDto>(result, new ApiMeta { RequestId = ..., TotalCount = total }))` so filter skips re-wrap (`IsAlreadyWrapped`), **or** set `HttpContext.Items["TotalCount"]` and teach filter to read it. Add explicit controller task bullet.

### 2. FR-6 full-text missing **area** in `q`

PRD FR-6 and `case-and-lifecycle.md` require full-text on crime, ST, name, contact, **area**. Story `q` covers four fields; `domicile` is filter-only.

**Fix:** AC1 — `q` also matches `domicile` enum string (pilot **area** proxy), e.g. ILIKE on stored domicile value (`Urban`, `Rural`, …).

### 3. `q` boolean semantics unspecified

Unclear whether `q` uses OR across columns (expected) while filters use AND.

**Fix:** AC1 clause: `q` matches if **any** searchable column matches (OR); filter params combine with **AND** against the `q` subset.

### 4. `q` normalization wording is ambiguous

"identifier tokens in q are normalized" is unclear for multi-word name searches.

**Fix:** Clarify: when comparing against `crime_number` / `st_number`, apply `Trim()` + `ToUpperInvariant()` on **both** `q` and column values; `beneficiaryName` / `beneficiaryContact` / domicile use case-insensitive substring without uppercasing the whole query.

---

## Enhancement Opportunities (Should Add)

### 5. Web route guard not in AC8

`/cases/new` and `/cases/:id` use `supervisorGuard`; AC8 omits it for registry.

**Fix:** AC8 — route `canActivate: [authGuard, supervisorGuard]`.

### 6. Integration test matrix gaps

Missing vs Story 2.5 patterns: unauthenticated **401** (search + presets), **Director** search 200, **CaseWorker** preset 403, deactivated preset 403.

**Fix:** Extend AC10 and integration test matrix.

### 7. Preset request validation AC missing

No 400 for empty/whitespace `name` or `name` > 64 chars; no validation that `filters` object is present.

**Fix:** AC7 — **400** for invalid preset name; optional empty `filters` allowed (means "no filters").

### 8. `CaseTestData` search helper

Story mentions `SeedCaseAsync` variants but not HTTP helper.

**Fix:** Add `CaseTestData.SearchCasesAsync(client, token, queryString)` and `CreatePresetAsync` helpers (mirror `MergeCaseAsync` from 2.5).

### 9. Registry error recovery (2.5 lesson)

Code review on 2.5 fixed merge error leaving save blocked. Registry should not leave filters/results stuck after API error.

**Fix:** Dev note — on search error, keep form state editable; show `errorMessage`; do not clear results silently without user action.

### 10. Placeholder detail router `state` type

`CaseDetailPlaceholderComponent` types `state.summary` as `CaseDuplicateMatchDto | CaseDto`. `CaseSummaryDto` has compatible fields but should be explicit.

**Fix:** Extend placeholder `CaseDetailState.summary` union to include `CaseSummaryDto` (or document registry passes `CaseSummaryDto` fields subset).

### 11. Swagger assertion specificity

Task says assert `search` — substring could false-positive.

**Fix:** Assert `/api/v1/cases/search` and `search-presets` paths in `SwaggerEndpointTests`.

### 12. List index migration — make mandatory

Task lists `(organisation_id, updated_at_utc)` index alongside optional `pg_trgm` — dev may skip both.

**Fix:** Mark org + `updated_at_utc` composite index as **required** in migration task; `pg_trgm` remains optional enhancement.

---

## Optimizations (Nice to Have)

1. **Performance AC4** — "p95 integration test" is flaky in CI; prefer "seed ≥50 cases, assert wall-clock <2s on single-page crime search" or document as best-effort assertion.
2. **Preset POST response** — specify **201** body includes created `CaseSearchPresetDto` (envelope), not only Location header.
3. **Empty search state** — AC8 UX copy for zero results ("No cases match your filters") vs error state.

---

## LLM Optimization

- Move **TotalCount implementation pattern** into a single "Pagination envelope" callout box at top of Dev Notes.
- Collapse pilot semantics table + search contract duplicate param lists (already mostly aligned).
- Add one **query composition** diagram in prose: `(org scope) AND (q-OR-block) AND (filter-AND-block)`.

---

## Verdict

Story is **implementable** and well-scoped vs 2.7–2.9. Apply critical fixes **1–4** before `dev-story` to prevent pagination and FR-6 gaps. Enhancements **5–12** reduce review churn.
