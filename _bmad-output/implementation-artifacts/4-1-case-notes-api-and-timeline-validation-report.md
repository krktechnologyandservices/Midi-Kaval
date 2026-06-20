# Story Validation Report — 4.1 Case Notes API and Timeline

**Story:** `4-1-case-notes-api-and-timeline`  
**Validated:** 2026-06-19  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (7 fixes applied 2026-06-19)

---

## Summary

Story 4.1 correctly scopes the greenfield `case_notes` API (POST create, GET chronological list, audit) for FR-13/CAP-5. Brownfield alignment with Epic 2 case aggregate patterns and Epic 3 `visit_notes` boundary is strong. Seven gaps could cause compile failures, missing edge-case tests, or inconsistent audit/query behavior.

| Check | Result |
|-------|--------|
| Epic AC coverage (POST notes + timeline + audit) | Pass (expanded with explicit GET — required for 4.3) |
| SPEC CAP-5 note types and fields | Pass |
| Architecture aggregate/API conventions | Pass |
| `visit_notes` vs `case_notes` boundary | Pass |
| RBAC (`EnsureCanReadCase`) | **Fix** (private helper — must duplicate) |
| Empty timeline behavior | **Fix** (AC6b) |
| Unassigned case 403 | **Fix** (AC7) |
| Schema FK + org scoping | **Fix** |
| Audit `SubjectUserId` | **Fix** |
| Test matrix completeness | **Fix** |
| Scope vs 4.2/4.3/4.4 | Pass |

---

## Critical Issues (Must Fix)

### 1. `EnsureCanReadCase` is private — dev agent cannot reuse from `CaseService`

Story instructed "copy pattern" but did not state the method is `private static`. A dev agent would attempt `caseService.EnsureCanReadCase(...)` or leave auth unimplemented.

**Fix:** READ FIRST + task — duplicate `ResolveActorContext`, `ResolveActorRole`, `EnsureCanReadCase`, `IsSupervisorRole` inside `CaseNoteService` (same approach as `VisitService` duplicating org resolution).

### 2. Unassigned case access not in AC7

Story 2.8 established field workers get **403** when `assigned_worker_id` is null. Story 4.1 AC7 mentioned wrong assignee but not unassigned cases — dev agent might allow notes on unassigned cases.

**Fix:** AC7 — explicit 403 for unassigned case; integration test added to AC10.

### 3. Empty timeline GET unspecified

AC6 covered populated lists but not zero notes. Dev agent might return 404 or null `data`.

**Fix:** AC6b — **200** with `{ items: [] }`; test in AC10.

---

## Enhancement Opportunities (Should Add)

### 4. FK constraints missing from schema task

`visit_notes` migration has indexes but story did not require FK definitions in `CaseNoteConfiguration`. Other entities (e.g. `Case.AssignedWorkerId`) use `OnDelete(DeleteBehavior.Restrict)`.

**Fix:** Task — FK `case_id` → `cases`, `author_user_id` → `users`, Restrict delete.

### 5. Audit `SubjectUserId` not specified

Existing visit/case mutations set `SubjectUserId = actorUserId` for actor-scoped events. Story only listed metadata fields.

**Fix:** Audit Dev Notes — `SubjectUserId = actorUserId`.

### 6. Organisation scoping on note queries

Case load scopes by org, but list query should also filter `case_notes.organisation_id` for defense in depth.

**Fix:** `ListAsync` task bullet + READ FIRST item 5.

### 7. Integration test matrix gaps

Missing: empty GET, unassigned 403, deactivated 403, director supervisor parity, chronological order via DB backdate (sleep-based ordering is flaky).

**Fix:** Extended AC10 test list.

---

## Optimizations (Nice to Have)

1. **Controller null-body guard** — mirror `ScheduleVisit` `if (request is null)` at controller layer (added to task).
2. **XML doc comments** — project-context requires them for OpenAPI (added to task).
3. **Crisis queue prep-note linkage** — already noted in Dev Notes; `Court` + `actionDueAtUtc` fields support Epic 8 "no prep note" detection later (no 4.1 action).

---

## LLM Optimization

- **READ FIRST** block added at top of Dev Notes (6 pinned guardrails).
- Pinned private-helper duplication requirement (prevents compile failure).
- AC6b + AC7 edge cases stated as explicit ACs (not buried in prose).
- Test matrix uses DB backdate pattern reference (Story 2.8) instead of flaky `Thread.Sleep`.

---

## Verdict

Story is **implementable** and well-scoped vs Stories 4.2–4.5. All **7** fixes applied to the story file. Safe to run `dev-story` for Story 4.1.

**Out of scope confirmed:** attachments (4.2), UI (4.3), interventions (4.4–4.5), `visit_notes` auto-bridge, offline sync for case notes.
