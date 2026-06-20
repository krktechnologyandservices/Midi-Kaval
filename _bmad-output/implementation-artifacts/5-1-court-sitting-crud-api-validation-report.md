# Story Validation Report — 5.1 Court Sitting CRUD API

**Story:** `5-1-court-sitting-crud-api`  
**Validated:** 2026-06-19  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (9 fixes applied 2026-06-19)

---

## Summary

Story 5.1 correctly scopes greenfield `court_sittings` API (nested CRUD, field-worker upcoming list, audit) for FR-15. Alignment with Epic 5.1, architecture `GET /court-sittings/upcoming`, and Story 4.4 intervention patterns is strong. Nine gaps could cause **POCSO leakage**, **terminal-case schedule pollution**, ambiguous create validation, or **RBAC/test drift** vs visit scheduler conventions.

| Check | Result |
|-------|--------|
| Epic 5.1 AC coverage (CRUD, notes/outcome, schedule) | Pass (+ explicit GET list/single) |
| Architecture court endpoints | Pass |
| Separate from `CaseNoteType.Court` | Pass |
| Story 4.4 intervention mirror (RBAC, audit, no DELETE) | Pass |
| POCSO redaction on field-worker upcoming list | **Fix** |
| `TerminationExclusion` exclusion / create guard | **Fix** |
| Invalid `status` on create → 400 | **Fix** |
| Create `Attended` / `Postponed` rules | **Fix** |
| Deactivated user 403 | **Fix** |
| `isPastDue` + `meta.totalCount` on upcoming | **Fix** |
| Audit event string constants | **Fix** |
| Integration test matrix gaps | **Fix** |
| UI / jobs scope boundary (5.2–5.4) | Pass |

---

## Critical Issues (Must Fix)

### 1. Upcoming list must POCSO-redact `CaseSummaryDto`

`VisitService` field-worker lists call `CaseDtoMapper.ToCaseSummary(case, redactPocsoForFieldWorker: true)`. Story AC9 references `ToCaseSummary` but omits redaction — mobile Command Strip would leak beneficiary names on POCSO cases.

**Fix:** AC9 — beneficiary names redacted per Story 3.8 rules (`redactPocsoForFieldWorker: true`).

### 2. Exclude terminal cases from upcoming + block create on terminal case

Visits exclude `TerminationExclusion` from field-worker lists and block scheduling on terminal cases. Court sittings on closed cases would pollute Command Strip and miss-escalation scope (5.4).

**Fix:** AC9 — exclude cases at `TerminationExclusion`; AC1b — create on terminal case → **422**.

### 3. Invalid `status` on create must 400 (not silent default)

Interventions review (4.4) required `ParseRequiredOrDefaultStatus` pattern. Dev notes mention it but AC10 is generic.

**Fix:** Dedicated AC — invalid `status` on create/update → **400** with allowed values.

### 4. Create with `Attended` / `Postponed` validation undefined

Dev agent may allow `Attended` without `outcome` on POST, or reject `Postponed` with past dates needed for backfill.

**Fix:** AC — `Attended` on create requires `outcome`; `Postponed` on create allows past `scheduledAtUtc`; `Upcoming` requires future date (existing AC2).

### 5. Deactivated user handling unspecified

Visit scheduler AC9 pattern: deactivated users → **403** `AuthService.DeactivatedMessage` on all endpoints.

**Fix:** New AC for deactivated users on court sitting endpoints.

---

## Enhancement Opportunities (Should Add)

### 6. `isPastDue` on schedule DTO

Dev notes mention computed flag; AC9 should require it for Story 5.2 UI/tests (`Upcoming` + `scheduledAtUtc < now`).

### 7. Upcoming response envelope `meta.totalCount`

Visit list endpoints return `meta.totalCount`. AC9 should match for Command Strip badge consistency.

### 8. Audit `AuditEventTypes` string values

Tasks reference `court.sitting.created` without pinning constants (Story 3.1 validation pattern).

**Fix:** Task bullet — `CourtSittingCreated = "court.sitting.created"`, `CourtSittingUpdated = "court.sitting.updated"`.

### 9. Integration test matrix gaps

Add explicit tests: terminal case create **422**, invalid status create **400**, upcoming POCSO initials, director **403** on upcoming, deactivated **403**, `isPastDue` true for past-due Upcoming row.

---

## Optimizations (Nice to Have)

### 10. `CaseTestData` helper names

Pin helpers: `BuildCourtSittingRequest`, `CreateCourtSittingAsync`, `SendListCourtSittingsAsync`, `ListUpcomingCourtSittingsAsync` (mirror interventions).

### 11. Upcoming query index note

Add dev note: composite filter uses `cases.assigned_worker_id` join — index `(organisation_id, status, scheduled_at_utc)` on `court_sittings` plus existing case assignee index is sufficient for pilot scale.

---

## LLM Optimization

- Terminal-case + POCSO rules in dedicated ACs prevent visit-scheduler regression.
- Create-status matrix (Upcoming/Attended/Postponed) in one AC block reduces ambiguous POST validation.
- `isPastDue` on DTO gives 5.2 a testable contract without UI in this story.

---

## Verdict

All **9** recommended fixes (5 critical, 4 enhancements) were applied to `5-1-court-sitting-crud-api.md` on 2026-06-19. Story is **ready for `dev-story`**.
