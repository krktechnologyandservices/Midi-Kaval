# Story Validation Report — 6.2 Mobile Travel Claim Capture

**Story:** `6-2-mobile-travel-claim-capture`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (14 fixes applied)

---

## Summary

Story 6.2 correctly scopes mobile travel capture (More → Travel, receipt photos, offline draft create) per FR-18/UX-DR13 and architecture §5.5. It mirrors court list UX and visit offline patterns well. Fourteen gaps could cause queue type errors, broken idempotent replay, missed foreground sync, or receipt upload failures after offline create.

| Check | Result |
|-------|--------|
| Epic AC (More → Travel, offline draft, empty state, EXPERIENCE voice) | Pass |
| Architecture §5.5 offline draft travel claims | Pass |
| Story 6.1 API consumer (REST + receipt rule) | Pass |
| CourtScheduleScreen list UX mirror | Pass |
| CaseDetailPlaceholder attachment flow | Pass |
| QueuedMutation visit-only shape | **Fix** |
| SyncMutation `ResultTravelClaimId` + duplicate replay | **Fix** |
| `mobileSyncPushService` travel result handling | **Fix** |
| Offline scope (create-only, not PATCH) | **Fix** |
| `useSyncOnForeground` on travel screens | **Fix** |
| `mergeQueueWithTravelClaims` list merge | **Fix** |
| SyncQueueScreen visit-only copy/labels | **Fix** |
| Field-role guard copy | **Fix** |
| api-client regen after sync DTO change | **Fix** |
| Scope vs 6.3–6.4 / 8.1 | Pass |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | Refactor `QueuedMutation` to **discriminated union** — visits keep `visitId`; travel uses `localDraftKey` + claim payload (no fake `visitId`) |
| 2 | Add `ResultTravelClaimId` to `SyncMutation` entity + EF migration for idempotent `duplicate` replay (mirror `ResultVisitId`) |
| 3 | Extend `SyncMutationResultDto` with optional `travelClaim: TravelClaimDto`; update `BuildDuplicateResultAsync` / `RecordAndReturnAsync` |
| 4 | `mobileSyncPushService` — `shouldRemoveFromQueue` accepts `result.travelClaim`; return `appliedTravelClaims` map; deferred receipt upload after applied create |
| 5 | Offline v1 boundary — **create draft only** via sync; PATCH/update/submit require online |
| 6 | Wire `useSyncOnForeground` on list + form screens (mirror `ActiveVisitScreen`) |
| 7 | NEW `mergeQueueWithTravelClaims.ts` — merge server list + local-only queued drafts for display |
| 8 | `TravelClaimApiService.listMine` calls `flushOfflineQueue()` on success (mirror `VisitApiService.listToday`) |
| 9 | SyncQueueScreen — travel row labels (`Create travel claim`); empty state when only travel queued; generic empty copy |
| 10 | Field-role guard copy: *"Travel claims are for field workers only."* |
| 11 | Form — clear `autoNumber` when transport mode changes away from Auto (6.1 server clears on PATCH) |
| 12 | Local draft routing — navigate by `localDraftKey` until sync returns `claimId`; then replace |
| 13 | AC 4 — inline validation rules from Story 6.1 AC2 (amount, locations, caseIds, etc.) |
| 14 | Tasks — api-client/OpenAPI regen after sync DTO extension; `offlineQueue.test.ts` + `mergeQueueWithTravelClaims.test.ts` |

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `6-2-mobile-travel-claim-capture`.

**Out of scope confirmed:** Director approve/return (6.3), notifications (6.4), crisis-queue rows (8.1), web travel UI, offline PATCH of synced drafts.
