# Story Validation Report — 3.1 Visit Scheduler API

**Story:** `3-1-visit-scheduler-api`  
**Validated:** 2026-06-16  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (13 fixes applied 2026-06-16)

---

## Summary

Story 3.1 correctly scopes the `visits` table, dedicated list endpoints (`today` / `weekly` / `overdue`), complete/reschedule mutations, coordinator schedule path, and supervisor case-visit history. Alignment with epics FR-8, architecture dedicated endpoints, and Epic 2 patterns (`CaseSummaryDto`, audit-in-transaction, `Policies.FieldWorker`) is strong. Six gaps could cause **stale registry overdue filters**, ambiguous schedule validation, or **403/envelope inconsistencies** that break integration tests.

| Check | Result |
|-------|--------|
| Epic 3.1 AC coverage (lists, complete, reschedule, supervisor reason) | Pass (+ schedule endpoint justified) |
| Architecture `GET /visits/today` + visit mutations | Pass |
| No client-composed Command Strip (`project-context`) | Pass |
| `next_visit_due_at_utc` write path (Story 2.6) | **Fix** (clear on complete) |
| Schedule / business-rule 422 matrix | **Fix** |
| RBAC matrix (coordinator on field endpoints) | **Fix** |
| Response envelope / `meta.totalCount` | **Fix** (AC7) |
| Story 3.2 downstream (handoff on strip row) | **Enhance** |
| POCSO list DTO rules | Defer (no `sensitivity_level` column yet) |
| LLM dev-agent clarity | **Enhance** (overlap today+overdue) |

---

## Critical Issues (Must Fix)

### 1. Complete must clear `cases.next_visit_due_at_utc`

Story 2.6 overdue **case search** uses `cases.next_visit_due_at_utc < now`. AC3 increments `visit_count` but never clears `next_visit_due_at_utc`. After complete, the case stays “overdue” in registry search while the visit is `Completed`.

**Fix:** AC3 — on successful complete, set `cases.next_visit_due_at_utc = null` (next visit is coordinator-scheduled via AC8).

### 2. Schedule endpoint validation ACs incomplete

AC8 lacks guards that `CaseService` will need for safe pilot behavior:

| Scenario | Expected status |
|----------|-----------------|
| `scheduledAtUtc` in the past | **400** |
| Case at `TerminationExclusion` | **422** |
| `assigneeUserId` not a field worker in org | **422** |
| Case already has a visit with `status` ∈ {`Scheduled`, `InProgress`} | **422** (one active visit per case in pilot) |

**Fix:** Extend AC8 with these rows; add matching `VisitService` task bullets.

### 3. Reschedule on completed visit — status code unspecified

AC4 covers complete-on-completed → **422**. Reschedule on `Completed` visit is undefined (dev agent may return 400 or 500).

**Fix:** AC5 or new AC clause — reschedule on `Completed` → **422**.

### 4. Coordinator/Director denied on field-worker list endpoints

Test task mentions “coordinator 403 on `GET /visits/today`” but no AC states it. Dev agent may allow coordinators on field endpoints.

**Fix:** New AC — `Policies.CoordinatorOrAbove` roles receive **403** on `GET /visits/today|weekly|overdue` and field-worker mutations.

### 5. AC7 response envelope ambiguous

AC7 says “200 with time-ordered visits” but not `ApiResponse<VisitListResultDto>` + `meta.totalCount`. AC1 pins envelope for lists; AC7 should match.

**Fix:** AC7 — `200` with `{ data: VisitListResultDto, meta: { requestId, totalCount } }`.

### 6. `AuditEventTypes` constant values

Tasks say `VisitScheduled` etc. without string values. Existing pattern: `CaseCreated = "case.created"`.

**Fix:** Task bullet — `VisitScheduled = "visit.scheduled"`, `VisitCompleted = "visit.completed"`, `VisitRescheduled = "visit.rescheduled"`.

---

## Enhancement Opportunities (Should Add)

### 7. Today + overdue overlap is intentional

A visit scheduled earlier today with `scheduled_at_utc < now` matches **both** today and overdue filters. Command strip mockup shows overdue styling on today’s cards — document in dev notes so dev agent does not “dedupe” incorrectly.

### 8. Story 3.2 prep — optional `handoffWhisper` on list items

`command-strip-today.html` shows handoff block on visit rows. Story defers whisper to 3.2 but reusing `CaseService` handoff logic on `VisitListItemDto` in 3.1 avoids a 3.2 API breaking change.

**Fix:** Dev note — optional `HandoffWhisperDto?` on `VisitListItemDto` for assignee when transfer ≤7 days (same rules as `GET /cases/{id}`); omit for coordinator case-visits list if noisy.

### 9. `CasesController` route placement note

Add dev note: register `GET/POST {id:guid}/visits` **before** `GET {id:guid}` in `CasesController` (ASP.NET usually resolves longer templates, but explicit ordering matches Story 2.x convention and avoids `{id}` swallowing `visits` if mis-typed).

### 10. Integration test matrix gaps

Add to AC12 / test task:

- Schedule **400** past `scheduledAtUtc`
- Schedule **422** terminal case / duplicate active visit / invalid assignee
- Reschedule **422** on completed visit
- Complete clears `next_visit_due_at_utc` (assert DB)
- Director **403** on `GET /visits/today`

### 11. `VisitTestData` helper signatures

Pin helpers: `ScheduleVisitAsync(client, token, caseId, scheduledAtUtc, assigneeUserId?)`, `CompleteVisitAsync`, `RescheduleVisitAsync` (mirror `CaseTestData` style in `CaseCreateTests.cs`).

---

## Optimizations (Nice to Have)

### 12. Test baseline count

Replace “Epic 2 test baseline” with “all existing tests pass” or current approximate .NET count — avoids stale number drift.

### 13. POCSO deferral note

Add dev note: `CaseSummaryDto.beneficiaryName` full name is acceptable until `sensitivity_level` column exists (Story 3.8); no initials-only rule in 3.1.

---

## LLM Optimization

- AC8 validation table (critical #2) packs multiple rules into one scannable block — reduces ambiguity vs prose spread across tasks.
- Pin `next_visit_due_at_utc = null` on complete in AC3 — single line prevents registry/search regression.
- Explicit coordinator **403** on field endpoints — prevents RBAC implementation drift.

---

## Verdict

All **13** recommended fixes (6 critical, 5 enhancements, 2 optimizations) were applied to `3-1-visit-scheduler-api.md` on 2026-06-16. Story is **ready for `dev-story`**.
