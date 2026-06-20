# Story Validation Report — 2.3 Unique Crime and ST Constraints with Duplicate Check

**Story:** `2-3-unique-crime-and-st-constraints-with-duplicate-check`  
**Validated:** 2026-06-15  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (after 7 fixes applied)

---

## Summary

Story 2.3 correctly scopes duplicate prevention to **`POST /api/v1/cases/check-duplicate` API only** (no web/mobile, no merge). Brownfield analysis aligns with Stories 2.1–2.2 patterns. Seven fixes merged to prevent normalization drift, ambiguous OR-query behaviour, incomplete test matrix, and unclear `matchedOn` JSON contract.

| Check | Result |
|-------|--------|
| Epic AC coverage (FR-4, FR-5) | Pass |
| Architecture dedicated endpoint | Pass |
| Story 2.1 create 409 regression | Pass (AC6) |
| No migration unless gap | Pass |
| RBAC / deactivated / 401 | Pass |
| LLM dev-agent clarity | Fixed (normalizer, dedupe, matchedOn) |
| Testability | Pass (after matrix expansion) |

---

## Findings Applied (fixes merged into story)

### Major (4) — fixed

**Normalization drift risk between create and check-duplicate**  
Create uses inline `Trim().ToUpperInvariant()`; duplicate check could diverge.  
**Fix:** Mandate shared `NormalizeIdentifier` helper used by both `CreateAsync` and `CheckDuplicateAsync`.

**OR query when only one identifier sent**  
Blind OR against empty normalized ST could match incorrectly if not gated.  
**Fix:** Task specifies apply crime/ST predicates only for non-empty normalized fields.

**Duplicate rows in `matches` when one case matches both identifiers**  
Single case could appear twice without dedupe.  
**Fix:** AC2 + task require distinct `caseId` and `matchedOn: Both` when applicable.

**`hasMatch` vs `matches` consistency unspecified**  
**Fix:** `hasMatch` true iff `matches.length > 0`.

### Enhancements (3) — applied

1. **`matchedOn` JSON convention** — string values `CrimeNumber` | `StNumber` | `Both` (PascalCase, aligned with `currentStage`)
2. **AC4 expanded** — whitespace-only single field → 400; AC4b max-length 400
3. **Test matrix expanded** — director, null body, case worker 403, no-audit assertion, over-max-length

---

## Checklist Results

### Epics alignment
- `POST /cases/check-duplicate` returns match summary without create — covered
- DB unique constraints + 409 on forced save — covered (AC6 regression)
- Coordinator persona — covered (+ Director via policy)

### Architecture alignment
- Dedicated endpoint (not client-composed search) — covered
- Tenant `organisation_id` scoping — covered
- Read-only probe (no audit on check) — covered
- Case create requires network for duplicate check — noted for Story 2.4

### Disaster prevention
- 84/19/17 regression baseline — stated
- `AuthWebApplicationFactory` + `CaseTestData` reuse — covered
- No web/mobile UI (AC10) — covered
- Route: `[HttpPost("check-duplicate")]` — clarified (no guid ambiguity with current controller)

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| Merge workflow (`POST /cases/{id}/merge`) | Low | Story 2.5 |
| Duplicate match sheet UI | Low | Story 2.4 |
| Field worker create + check policy change | Low | Deferred — v1 create remains CoordinatorOrAbove |
| `UsersSchemaTests` update | N/A | No migration expected |
| TerminationExclusion included in matches | Low | Documented — intentional for v1 |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 2.3. Story file updated with validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement `CheckDuplicateAsync`, controller endpoint, integration tests
2. Code review — say **"1"** after implementation
3. `bmad-create-story` for Story 2.4 when 2.3 is `done`
