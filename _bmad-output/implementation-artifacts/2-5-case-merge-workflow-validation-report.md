# Story Validation Report — 2.5 Case Merge Workflow

**Story:** `2-5-case-merge-workflow`  
**Validated:** 2026-06-15  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (11 fixes applied 2026-06-15)

---

## Summary

Story 2.5 correctly scopes merge to **`POST /api/v1/cases/{id}/merge`**, web/mobile wiring from the 2.4 duplicate sheet, and OpenAPI regeneration. Brownfield analysis aligns with Stories 2.1–2.4. Eight gaps could cause implementation drift: private `ValidateRequest` reuse, ambiguous UI orchestration, audit/`UpdatedAtUtc` semantics, and incomplete client/test specs.

| Check | Result |
|-------|--------|
| Epic AC coverage (FR-5, UJ-3) | Pass |
| Architecture `POST /cases/{id}/merge` | Pass |
| Story 2.4 stub replacement | Pass |
| RBAC / deactivated / 401 | Pass (401 test missing) |
| Modal stack rule (inline confirm) | Pass |
| Fill-empty merge policy | Pass (clarify audit-only path) |
| LLM dev-agent clarity | **Fix** (validation refactor, UI pattern) |
| Testability | **Fix** (matrix gaps, helpers) |

---

## Findings Applied (fixes merged into story)

### Critical (4) — fixed

1. **`ValidateIntakeRequest` extraction** — tasks require refactor before `MergeAsync`; no duplicate validation
2. **Parent-owned API pattern** — AC8 pins `DuplicateMatchSheetResult` merge action; `handleSheetResult` owns HTTP
3. **Audit vs `UpdatedAtUtc`** — AC3 clarifies audit always; timestamp only on fill-empty
4. **Mobile loading state** — AC9 Confirm disabled + "Merging…" during request

### Enhancements (4) — applied

5. **OpenAPI regen commands** — Dev Notes Windows block with env vars
6. **Web HttpClient pattern** — architecture compliance notes Story 2.4 brownfield
7. **`CaseTestData.MergeCaseAsync` + expanded matrix** — director, 401, both-match, audit-only
8. **`SwaggerEndpointTests`** — assert `merge` in swagger JSON

### Optimizations (3) — applied

- Reuse `CreateCaseRequest` as merge body (no `MergeCaseRequest.cs`)
- 422 detail copy standardized
- Remove unused `MatSnackBar` from duplicate sheet

---

## Findings Applied (original analysis)

### 1. `ValidateRequest` is private — merge cannot reuse create validation without refactor

`CaseService.ValidateRequest` is a **private** method on `CreateCaseRequest` only (`CaseService.cs`). Story tasks say "reuse validation path" but do not instruct extraction.

**Fix:** Add task to extract `ValidateIntakeRequest(CreateCaseRequest)` (or accept shared interface) returning `ValidatedCreateCaseRequest`; call from both `CreateAsync` and `MergeAsync`. Alternatively document using `CreateCaseRequest` as merge body type and refactor in same PR — **do not duplicate validation logic**.

### 2. Web merge orchestration pattern underspecified

`DuplicateMatchSheetResult` is only `{ action: 'open' } | { action: 'cancel' }`. `handleSheetResult` in `case-create.component.ts` handles `open` only. Tasks say "emit merge result **or** parent callback" — ambiguous.

**Fix:** Pin **parent-owned API** pattern:
- Extend `DuplicateMatchSheetResult` with `{ action: 'merge'; caseId: string; match: CaseDuplicateMatchDto }` emitted when user confirms inline merge (sheet does **not** call HTTP).
- `case-create.component` `handleSheetResult` calls `mergeCase` with `buildCreateRequest()` body, then navigates on 200.
- Pass `merging` signal into sheet via `MAT_DIALOG_DATA` or keep loading state in parent while dialog stays open.

### 3. Audit vs `UpdatedAtUtc` when no fill-empty fields apply

AC3 bumps `updatedAtUtc` only when fill-empty fields change. AC1 always writes audit on success. Unspecified: merge with no field changes still returns 200 + audit but **must not** bump `UpdatedAtUtc` unless a field changed.

**Fix:** Add AC clause: successful merge **always** writes `case.merged` audit; `updatedAtUtc` changes **only** when fill-empty fields are applied.

### 4. Mobile AC9 missing in-flight merge loading state

Web AC8 requires Confirm disabled + loading during merge. Mobile AC9 omits this.

**Fix:** Add to AC9: Confirm disabled + loading label while merge request in flight (parity with web).

---

## Enhancement Opportunities (Should Add)

### 5. OpenAPI regeneration commands (Windows)

Story references `EXPORT_OPENAPI_PATH` without concrete values used in Epic 2.

**Fix:** Add to Dev Notes (from Story 2.1):
- `EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json`
- `API_OPENAPI_FILE` for `generate.mjs`
- `npm run generate:api-client` + rebuild `packages/api-client/dist` if consumers use dist

### 6. Web HTTP pattern — follow Story 2.4, not raw fetch

`project-context.md` says api-client only; `case-api.service.ts` uses `HttpClient` + types re-exported from `@midi-kaval/api-client` schemas (Story 2.4 pattern).

**Fix:** Dev note: **do not** introduce fetch; extend existing `CaseApiService` HttpClient pattern; add `MergeCaseRequest` type alias to `CreateCaseRequest` in `case.models.ts`.

### 7. Integration test helper + matrix gaps

`CaseTestData` has `CreateCaseAsync` but no merge helper. Matrix omits director 200, unauthenticated 401, both-identifiers match target, audit-only merge (no `UpdatedAtUtc` change).

**Fix:** Add `CaseTestData.MergeCaseAsync(client, token, caseId, body)`; extend matrix with those four scenarios.

### 8. `SwaggerEndpointTests` assertion

Task says extend Swagger tests but not what to assert.

**Fix:** Add `Assert.Contains("/merge", json)` (or full path `/api/v1/cases/{id}/merge` pattern) in `Get_SwaggerJson_Returns200_ContainingMetaRoute`.

---

## Optimizations (Nice to Have)

1. **Skip separate `MergeCaseRequest.cs`** — use `CreateCaseRequest` as merge body in OpenAPI/controller to avoid duplicate DTO (document in controller XML summary).
2. **422 detail copy** — suggest: `"Draft identifiers do not match the target case."`
3. **Remove `MatSnackBar` from duplicate sheet** after stub removal if unused.

---

## LLM Optimization

- Tasks web bullet "emit merge result **or** handle merge in parent" → single mandated pattern (Critical #2).
- Consolidate OpenAPI + dist rebuild into one task sub-bullet.
- Add explicit file: `case-create.component.ts` `handleSheetResult` merge branch.

---

## Checklist Results

### Epics alignment
- Merge from duplicate sheet → `POST /cases/{id}/merge` — covered
- Draft abandoned, audit links merge — covered
- Single active Crime/ST — covered (no new row + conflict checks)
- Social worker unavailable — covered

### Architecture alignment
- Dedicated merge endpoint — covered
- Same-transaction audit — covered
- CoordinatorOrAbove — covered (Director included, consistent with 2.1–2.4)

### Disaster prevention
- 103/31/21 baseline — stated
- No modal stack >1 — covered
- 2.4 create/409 re-check regression — "do not break" noted
- Route ordering — `{id}/merge` sub-route safe

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| Rich `GET /cases/{id}` | Low | Story 2.9 |
| Merge from registry/search | Low | Story 2.6+ |
| `CaseDto` omits optional fields for placeholder | Low | Acceptable for 2.5 |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 2.5. Story file updated with all validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement merge API + client wiring
2. `bmad-code-review` when complete
