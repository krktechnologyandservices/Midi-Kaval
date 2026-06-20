# Story Validation Report — 5.2 Court Schedule UI

**Story:** `5-2-court-schedule-ui-web-case-detail-and-mobile`  
**Validated:** 2026-06-19  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied 2026-06-19)

---

## Summary

Story 5.2 correctly scopes UI-only consumption of Story 5.1 APIs across web case detail, mobile case detail, Today countdown banner, and mobile Court schedule surface. Alignment with Epic 5.2, UX-DR9/DR13, and Story 4.5 intervention UI patterns is strong. Ten gaps could cause **`isPastDue` field confusion**, missing **terminal-case UX**, incomplete **Court schedule loading states**, or **week-boundary drift** vs visit scheduler.

| Check | Result |
|-------|--------|
| Epic 5.2 AC (view/update, chips, empty state, mobile Today + case detail) | Pass |
| Story 5.1 API reuse (no new endpoints) | Pass |
| UX-DR9 past-due critical accent | Pass (+ client vs API `isPastDue` clarified) |
| UX-DR13 empty copy on mobile court schedule | Pass |
| `CourtCountdownBanner` / `useCourtCountdown` wiring (Story 3.2 deferral) | Pass |
| `CourtSittingDto` vs `CourtSittingScheduleItemDto` shape | **Fix** |
| Terminal case create 422 UX | **Fix** |
| Postponed backfill create rules | **Fix** |
| Court schedule loading/error/retry | **Fix** |
| UTC week boundary alignment | **Fix** (reference `VisitService.GetUtcWeekBounds`) |
| Field-worker-only upcoming API on mobile | **Fix** |
| Jobs/UI scope boundary (5.3–5.4) | Pass |
| Accessibility (chip text, not color alone) | **Fix** |

---

## Critical Issues (Must Fix)

### 1. Case nested list has no `isPastDue` field

`CourtSittingDto` (nested `GET /cases/{id}/court-sittings`) does **not** include `isPastDue`. Only `CourtSittingScheduleItemDto` on upcoming does. AC1 implied API field; dev notes contradicted themselves.

**Fix:** AC1/AC4 — compute past-due client-side on case detail: `status === 'Upcoming' && new Date(scheduledAtUtc) < now` (mirror `isOverdue()` in `CaseInterventionsComponent`). Upcoming/Court schedule screens use API `isPastDue`.

### 2. Terminal case create must surface API 422

Story 5.1 blocks create on `TerminationExclusion` with **422**. UI forms need explicit error display.

**Fix:** AC2 — terminal case POST → show API error via `extractErrorMessage` (no silent failure).

### 3. Postponed create with past date allowed (backfill)

API allows `Postponed` + past `scheduledAtUtc`; `Upcoming` requires future date. Form client validation must not block Postponed backfill.

**Fix:** AC2 — when `status=Postponed`, do not require future `scheduledAtUtc`; when `Upcoming`, enforce future date before submit (match intervention due-date pattern).

---

## Enhancement Opportunities (Should Add)

### 4. Court schedule screen loading/error/retry

AC6 listed rows and empty state but not loading/error — inconsistent with interventions/visits.

**Fix:** AC6 — loading spinner, error + Retry, pull-to-refresh.

### 5. `useCourtCountdown` must refresh on Today pull-to-refresh

Banner would go stale if only fetched on mount.

**Fix:** AC5 — re-fetch upcoming when Today `onRefresh` runs (same cycle as visit list).

### 6. UTC week filter must match `VisitService.GetUtcWeekBounds`

Pseudo-code helper was incomplete; drift would show wrong week slice.

**Fix:** Dev notes — copy Mon–Sun UTC logic from `VisitService.GetUtcWeekBounds` (lines ~994–1000); extract shared mobile util `utcWeekBounds.ts` if helpful.

### 7. Court schedule / upcoming: field-worker only

`GET /court-sittings/upcoming` returns **403** for coordinators. Mobile Court schedule route must guard `auth.isFieldRole` (mirror Today Command Strip).

**Fix:** AC6 — non-field roles see explanatory message, not a crash.

### 8. Status chip accessibility (UX-DR9)

EXPERIENCE.md — severity/status must not rely on color alone.

**Fix:** AC1/AC6 — chips render visible text `Upcoming` / `Attended` / `Postponed`; past-due adds `Overdue` badge text.

### 9. Web `case-detail-placeholder.component.spec.ts` update

Embedding new component should extend existing detail spec (interventions pattern).

**Fix:** Task bullet under web case detail.

### 10. Datetime → UTC ISO on submit

API requires UTC timestamps. Interventions convert local datetime input via `toISOString()`.

**Fix:** Dev notes — web/mobile forms use local datetime picker → `scheduledAtUtc` / `nextCourtAtUtc` as ISO UTC strings on API calls.

---

## Optimizations (Applied in story)

- Banner uses **first upcoming item** (API sorts ascending) — past-due sittings surface first when present.
- Shared `isCourtSittingPastDue(dto)` helper name documented for web + mobile case detail.

---

## Verdict

Story is **ready for dev-story** after applied fixes. No API changes required for this story.
