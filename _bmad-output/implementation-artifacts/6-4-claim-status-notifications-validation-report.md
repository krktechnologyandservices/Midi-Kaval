# Story Validation Report — 6.4 Claim Status Notifications

**Story:** `6-4-claim-status-notifications`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (11 fixes applied)

---

## Summary

Story 6.4 correctly scopes notification copy refinement, push defer logging, mobile claimant consumption, and monthly totals regression on top of Story **6.3** in-app rows and Story **4.5** notification API. Alignment with Epics 7–8 boundaries is sound. Eleven gaps could cause copy drift, wrong push timing, mobile envelope parse failures, or ambiguous deep-link scope.

| Check | Result |
|-------|--------|
| Epic AC (in-app + push intent + monthly totals signal) | Pass (push = defer log; totals = API regression test) |
| Story 6.3 notification rows already exist | Pass — refine, don't duplicate store |
| Story 4.5 GET/PATCH notifications API | Pass |
| Approve-with-comment body bug (raw comment only) | **Fix** |
| Return body bug (comment-only body today) | **Fix** |
| Copy single source of truth | **Fix** — refactor `CreateTravelClaimDecisionNotificationForSave` |
| Push defer log timing | **Fix** — after `SaveChangesAsync` |
| Mobile envelope parsing | **Fix** — `getApi` + camelCase `items` |
| Field-worker guard on Notifications | **Fix** |
| Non-travel notification tap scope | **Fix** — AC 4b mark-read only |
| Read-only form status chip missing today | **Fix** — AC 3 |
| Monthly totals test params | **Fix** — use `claimDate` year/month |
| Scope vs 7.1/7.2/7.4/8.4 | Pass |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | Refactor `CreateTravelClaimDecisionNotificationForSave` to accept `(claim, caseId, eventType, decisionComment)` — build title/body **inside** `NotificationService` via `TravelClaimNotificationCopy` |
| 2 | Remove caller-supplied title/body strings from `TravelClaimService.ApproveAsync` / `ReturnAsync` |
| 3 | Push defer log **after** successful `SaveChangesAsync` (not inside pre-save helper) |
| 4 | AC1/AC2 — return notification must include destination + amount + Director note (not comment-only as implemented today) |
| 5 | AC3 — status chip on read-only form; decision section **only** Approved/Returned; Submitted shows status only |
| 6 | AC4 — field-worker guard; `getApi`/`patchApi` envelope parsing documented |
| 7 | AC **4b** — non-travel rows mark read only; deep links deferred to 7.4 |
| 8 | AC5 — monthly totals query uses `claimDate` year/month; add `ListTravelClaimMonthlyTotalsAsync` CaseTestData helper |
| 9 | Mobile `notification.models.ts` task (no shared api-client package on mobile) |
| 10 | Integration tests — explicit assertions for approve with/without comment body shape |
| 11 | Dev note — epic "mobile push" satisfied by in-app row + defer log; FCM in 7.2; no OpenAPI route changes |

---

## Remaining Notes (non-blocking)

- **Travel list already shows status chips** — form read-only view lacked visible status; AC3 now requires it.
- **Story 7.4** owns notification bell + full centre polish; this story is More → Notifications list only.
- **Dashboard widget UI** — Epic 8.4; AC5 verifies existing monthly totals API only.
- **Submitted claims in totals** — already covered by `TravelClaimApiTests.MonthlyTotals_Coordinator_ReturnsSubmittedTotals`; 6.4 adds approve/return transitions.

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `6-4-claim-status-notifications`.

**Out of scope confirmed:** device tokens (7.1), FCM/APNs delivery (7.2), email templates (7.3), notification bell (7.4), dashboard widgets (8.4), intervention/court notification deep links (7.4).
