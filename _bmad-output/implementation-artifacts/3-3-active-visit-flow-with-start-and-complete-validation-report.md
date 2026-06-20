# Story Validation Report — 3.3 Active Visit Flow with Start and Complete

**Story:** `3-3-active-visit-flow-with-start-and-complete`  
**Validated:** 2026-06-16  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied 2026-06-16)

---

## Summary

Story 3.3 correctly bridges the Epic 3.3 gap: `POST /visits/{id}/start`, required completion note via `visit_notes`, Active Visit mobile screen, supervisor visibility through existing `GET /cases/{id}/visits`, and the deferred 3.1 reschedule `StartedAtUtc` fix. Scope boundaries vs offline (3.6), GPS (3.4), and case notes timeline (4.1) are clear. Ten gaps could cause **api-client regen failure**, **stale Active Visit after reschedule**, **partial DB writes**, or **mobile test regressions**.

| Check | Result |
|-------|--------|
| Epic 3.3 AC (start + complete with notes, supervisor visibility, reschedule reason) | Pass |
| Architecture visit endpoints + `visit_notes` offline scope | Pass |
| Story 3.1 patterns (audit-in-transaction, RBAC, envelope) | Pass (+ transaction explicit) |
| Story 3.2 downstream (Start stub → navigation) | Pass |
| OpenAPI / api-client regen workflow | **Fix** |
| Reschedule success UX from Active Visit | **Fix** |
| Mobile test migration (stub alert test) | **Fix** |
| Navigation test harness (stack wrapper) | **Fix** |
| Weekly list `completionNote` on completed rows | **Fix** |
| Director 403 on `POST /start` test | **Fix** |
| FR-11 offline scope | Correctly deferred to 3.6 |

---

## Critical Issues (Must Fix) — Applied

### 1. OpenAPI export workflow missing

AC14 only said `npm run generate:api-client`. Project convention (Stories 2.x, 3.1) requires `EXPORT_OPENAPI_PATH` + committed `openapi-snapshot.json` + `API_OPENAPI_FILE` — `generate.mjs` defaults to live API at `:5049`.

**Fix applied:** AC14 + task bullets with Windows env vars and snapshot commit list.

### 2. Reschedule success leaves stale Active Visit

AC8 adds reschedule from Active Visit but AC9 only defined navigation after **complete**. Reschedule resets status to `Scheduled` and clears `started_at_utc` — user would remain on Active Visit with wrong state.

**Fix applied:** AC9b — navigate back to Today + refresh strip after successful reschedule.

### 3. Complete + note transaction not explicit

3.1 requires audit in same `SaveChangesAsync`. Adding `visit_notes` insert without stating single-transaction risks dev agent splitting saves → partial complete without note.

**Fix applied:** AC3 single-transaction clause; AC3b complete-on-completed **422**; task bullet on `CompleteAsync`.

### 4. `CommandStripCard.test.tsx` stub test will break

Story 3.2 AC12 includes `start visit shows stub alert` test. Wiring Start in 3.3 breaks this unless updated. AC14 did not mention it.

**Fix applied:** AC14 + mobile test task — replace with **Continue visit** label test for `InProgress`.

### 5. Start double-tap race

No loading guard on Start while `POST /start` in flight → duplicate navigation or 422.

**Fix applied:** AC7 loading / non-double-tappable clause.

### 6. `VisitTestData` / `SeedVisitAsync` gaps

`SendCompleteVisitAsync` sends no body (5 call sites). `SeedVisitAsync` does not set `StartedAtUtc` for `InProgress` seeds.

**Fix applied:** Task bullets with call-site count and `SeedVisitAsync` extension.

### 7. README not in tasks

Story 3.1 updated README visit table. New `/start` and note-on-complete change public contract.

**Fix applied:** Docs task for `README.md`.

---

## Enhancement Opportunities — Applied (2026-06-16)

### 8. TodayScreen navigation tests need stack wrapper

After `TodayStackNavigator`, `useNavigation` in `TodayScreen` requires `NavigationContainer` + stack in tests.

**Fix applied:** AC14, mobile test tasks, Testing standards dev note, optional `testNavigation.tsx` helper.

### 9. `completionNote` on weekly list for completed visits

Weekly endpoint includes `Completed` visits this week — field workers benefit from note + timestamps in weekly reporting context.

**Fix applied:** AC11b, `ListWeeklyAsync` task, weekly JSON example in dev notes, integration test bullet.

### 10. Director 403 on `POST /start` integration test

AC13 extended to include `POST /complete`; explicit `Director_StartVisit_Returns403` test mirrors Story 3.1 field-mutation RBAC matrix.

**Fix applied:** AC13 wording, AC14 test bullet, integration task.

---

## Enhancement Opportunities (Previously Optional — Now Applied)

### 8–10. See above.

---

## LLM Optimization Notes

- **Navigation header stack:** Clarified `TodayStackNavigator` vs tab `headerShown: false` to prevent duplicate Today titles (regression from 3.2 review).
- **`auth.postApi` + `envelope.data`:** Explicit mobile pattern prevents raw `fetch` reinvention.
- **`CompleteVisitRequest` JSON:** Use `{ note }` camelCase — matches `RescheduleVisitRequest` / `Program.cs` naming policy.

---

## Verdict

Story is **implementation-ready** after applied fixes. Run `bmad-dev-story` on `3-3-active-visit-flow-with-start-and-complete.md`.
