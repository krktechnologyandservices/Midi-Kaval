# Story Validation Report — 3.6 Offline Visit Storage and Sync Push API

**Story:** `3-6-offline-visit-storage-and-sync-push-api`  
**Validated:** 2026-06-17  
**Validator:** bmad-create-story (validate / checklist.md)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied 2026-06-17)

---

## Summary

Story 3.6 correctly scopes FR-11 offline buffer + NFR-6 cloud authority + partial NFR-7 (queue persistence; visible inspection UI in 3.7). Split from Story 3.7 sync chip UI is sound. Ten gaps could cause **sync service exceptions instead of rejected outcomes**, **note-merge impossible via `CompleteAsync`**, **strip/cache drift after offline complete**, or **token-expired sync failures**.

| Check | Result |
|-------|--------|
| Epic 3.6 AC (queue, idempotent push, server wins, note merge) | Pass (after fixes) |
| FR-11 offline capture | Pass — start/complete offline |
| FR-11 visible sync labels | Correctly split → Story 3.7 |
| NFR-6 cloud source of truth | Pass |
| NFR-7 failed sync inspectable | Partial in 3.6 (queue + `sync_mutations`); UI in 3.7 |
| Architecture §5.5 offline pattern | Pass (AsyncStorage pilot OK) |
| Story 3.3 visit start/complete reuse | **Fix** — cannot delegate to throwing APIs |
| Story 3.5 custom route + cache | **Fix** — merge queue without clearing grouping |
| GPS verify / reschedule offline | **Fix** — explicit out of scope |
| OpenAPI / api-client regen | Pass |

---

## Critical Issues (Must Fix) — Applied

### 1. `SyncPushService` cannot call `StartAsync` / `CompleteAsync` directly

Existing methods **throw** `VisitBusinessRuleException` for conflicts (e.g. already `InProgress`, already `Completed`). Sync push must return `{ status: "rejected" }` or `duplicate`, not HTTP 422.

**Fix applied:** Task + Dev Notes — extract `ApplyVisitStartForSync` / `ApplyVisitCompleteForSync` / `MergeVisitNoteForSync` internal methods on `VisitService` (or dedicated sync applier) that return structured outcomes instead of throwing for expected conflicts.

### 2. Note merge path blocked by `CompleteAsync`

`CompleteAsync` throws when `Status == Completed` (line 301–304 `VisitService.cs`). Note-merge-by-timestamp requires a **separate code path**.

**Fix applied:** AC4 clarified; task bullet for `MergeVisitNoteForSync` updating single `visit_notes` row per `visit_id` when `noteClientTimestampUtc` is newer; audit event on merge.

### 3. Duplicate replay must return prior outcome

Idempotent replay should let client refresh cache without re-applying.

**Fix applied:** AC3/AC4 — `duplicate` responses include stored `status` and `visit` when original was `applied`.

### 4. Offline complete must update Today strip + cache immediately

AC6 navigates back after complete but did not require optimistic `Completed` on Command Strip before sync.

**Fix applied:** AC6 — after offline complete, `mergeQueueWithVisits` sets visit `Completed` in local strip and `writeCache` before navigation.

### 5. Pull-to-refresh must not clear offline queue

Dev Notes mentioned this; no AC enforced it (unlike grouping clear in 3.5).

**Fix applied:** AC12 — pull-to-refresh refreshes visits from API but **preserves** offline queue; `mergeQueueWithVisits` re-applies optimistic overlays after fetch.

### 6. Sync push must refresh auth session when online

Offline work may exceed 15-minute JWT TTL.

**Fix applied:** AC7 + task — call `authSessionService.refreshSession()` before `POST /sync/push`; if refresh fails, keep queue and set `syncStatus: "error"`.

### 7. `visit.complete` on `Scheduled` visit (server)

`ActiveStatuses` includes `Scheduled` — existing `CompleteAsync` allows complete without explicit start.

**Fix applied:** Dev Notes — document aligned behavior; offline queue should still prefer start→complete ordering but server accepts complete-from-scheduled.

### 8. FR-11 visible sync vs Story 3.7 split

PRD FR-11 requires visible sync labels; deferring all UI to 3.7 is correct but needed explicit cross-reference to avoid dev agent adding chips in 3.6.

**Fix applied:** AC10 strengthened with PRD pointer; internal `syncStatus` only in 3.6.

---

## Enhancement Opportunities — Applied

### 9. App foreground sync trigger missing from tasks

AC7 mentions foreground; no `AppState` task.

**Fix applied:** Task bullet — `useSyncOnForeground.ts` using `AppState` + NetInfo.

### 10. `mergeQueueWithVisits` + custom visit order (Story 3.5)

Optimistic status must compose with `applyDisplayOrder` / `customVisitOrder`.

**Fix applied:** Dev Notes — merge queue **before** display order; do not clear `customVisitOrder` on sync.

---

## Optimizations (Not Applied — Acceptable for v1)

- **Max batch size cap** (e.g. 50 mutations) — add if integration tests show need.
- **WatermelonDB migration** — AsyncStorage sufficient for pilot; architecture allows later upgrade.
- **Supervisor API to list `sync_mutations`** — support inspection via 3.7 mobile queue + DB admin; no director UI in 3.6.

---

## LLM Dev-Agent Notes

- Route: `POST /api/v1/sync/push` on new `SyncController` (not under `VisitsController`).
- `visit_notes` assumed **one row per completed visit** (`ToDictionary` by `visitId` in `VisitService`).
- Mobile Jest baseline: **63** tests after Story 3.5.
- NetInfo: `@react-native-community/netinfo` ^11.x (RN 0.76 compatible); mock in tests.
- Queue storage key: `midi-kaval:offline-sync-queue:v1` (separate from command strip cache).
- Out of scope: GPS verify offline, reschedule offline, travel claims offline, sync chip UI.

---

## Verdict

Story is **implementation-ready** after applied fixes. Proceed with `dev-story`.
