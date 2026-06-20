# Story Validation Report — 2.8 Case Assignment Transfer and Handoff Whisper

**Story:** `2-8-case-assignment-transfer-and-handoff-whisper`  
**Validated:** 2026-06-16  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (9 fixes applied 2026-06-16)

---

## Summary

Story 2.8 correctly scopes assignment transfer API, `case_assignments` history, per-resource case read for field workers, handoff whisper (FR-7 / CAP-15), and staff filter upgrade. Brownfield alignment with Epic 2 (2.1 deferred `assigned_worker_id`, 2.6 staff filter pilot, 2.7 shared filters) is strong. Nine gaps could cause an off-by-one whisper window, blocked field-worker API access, dead web UX assumptions, or an unusable coordinator transfer form.

| Check | Result |
|-------|--------|
| Epic AC coverage (FR-7 whisper) | Pass (expanded with transfer API — matches story title) |
| SPEC CAP-15 three-line handoff | Pass |
| Architecture assignments child aggregate | Pass |
| Per-resource auth (Epic 1.8 deferred) | **Fix** (`[Authorize]` + `CaseForbiddenException`) |
| 7-day visibility rule | **Fix** (calendar-day formula) |
| Web vs mobile role routing (UX-DR14) | **Fix** (mobile assignee; web supervisor) |
| UX handoff-whisper tokens | Pass |
| Scope vs Epic 7 / 4 / 8 / 2.9 | Pass |
| Testability | **Fix** (matrix gaps + export filter) |

---

## Critical Issues (Must Fix)

### 1. Seven-day whisper window off-by-one

Original Dev Notes used `assignedAtUtc >= UtcNow.Date.AddDays(-7)`, which keeps whisper visible on **day 8** after transfer. Epics require hidden from day 8 (transfer day = day 1; visible days 1–7).

**Fix:** READ FIRST + Dev Notes — use `(UtcNow.Date - assignedAtUtc.Date).Days < 7`.

### 2. `GET /cases/{id}` policy would block field workers

All existing case endpoints use `CoordinatorOrAbove`. Story said "mixed policy" but did not forbid `CoordinatorOrAbove` on GET — dev agent would copy existing controller pattern and break AC3.

**Fix:** Task + READ FIRST — `[Authorize]` only on GET; per-resource check in `GetDetailAsync`.

### 3. `CaseForbiddenException` referenced but not in codebase

`CaseService.cs` defines `CaseValidationException`, `CaseNotFoundException`, etc. — no forbidden type. Story referenced throwing `CaseForbiddenException` without defining it.

**Fix:** Add sealed `CaseForbiddenException`; map to 403 in controller (mirror `CaseNotFoundException` → 404).

### 4. AC6 assumed field workers open web case detail

`app.routes.ts` applies `supervisorGuard` to `/cases/:id`. Field workers (`isMobileOnlyRole`) redirect to `/mobile-only` (UX-DR14). Web assignee path is unreachable.

**Fix:** Split AC6 — mobile assignee sees whisper; web coordinator sees detail + transfer form (whisper null from API); reusable whisper component for tests/Epic 3.

---

## Enhancement Opportunities (Should Add)

### 5. Coordinator assignee picker underspecified

Story offered UUID text input OR optional endpoint — ambiguous for dev agent.

**Fix:** Standardize on `GET /api/v1/users/field-workers` (CoordinatorOrAbove); task + AC7 updated.

### 6. Transfer 404 missing

No AC for unknown case id on transfer — dev might return 422 or 500.

**Fix:** AC1 — 404 when case not in organisation.

### 7. Export filter not tied to new assignee param

Story 2.7 refactored `ApplySearchFilters` — new `assignedWorkerUserId` must flow to export automatically; not stated.

**Fix:** Integration test matrix — export respects `assignedWorkerUserId`.

### 8. `CaseSummaryDto` assignee fields marked optional

Assigned list reuses `CaseSummaryDto`; leaving fields optional invites incomplete DTO mapping.

**Fix:** Require `assignedWorkerUserId`, `assignedAtUtc` on summary DTO in tasks.

### 9. Integration test matrix gaps

Missing: director GET detail 200, field worker POST transfer 403, unassigned case GET 403, transfer 404.

**Fix:** Extended AC10 and test matrix.

---

## Optimizations (Nice to Have)

1. **Backdate whisper test** — direct DB update of `assigned_at_utc` in integration test (no test-only API hook needed).
2. **Route references** — cite `app.routes.ts` and `supervisor.guard.ts` in References.
3. **Epic 3 reuse** — note Command Strip will consume same `HandoffWhisperDto` / component later.

---

## LLM Optimization

- **READ FIRST** block at top of Dev Notes (auth policy + 7-day formula + web/mobile split).
- Pinned endpoint policies and exception types in task bullets.
- Removed ambiguous assignee-picker alternatives.

---

## Verdict

Story is **implementable** and well-scoped vs 2.9, Epic 4 notes, Epic 7 notifications, and Epic 8 crisis queue. All **9** fixes applied to the story file. Safe to run `dev-story` for Story 2.8.

**Baseline to beat:** 160 .NET integration, 46 web, 22 mobile (per Story 2.7 completion notes).
