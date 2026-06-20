# Story Validation Report — 3.7 Sync Chip and Sync Queue Mobile UI

**Story:** `3-7-sync-chip-and-sync-queue-mobile-ui`  
**Validated:** 2026-06-17  
**Validator:** bmad-create-story (validate / checklist.md)  
**Verdict:** **PASS — ready for dev-story** (8 fixes applied 2026-06-17)

---

## Summary

Story 3.7 correctly completes FR-11 visible sync labels (deferred from 3.6) and NFR-7 inspectable queue UI. Scope is mobile-only with no API churn — sound. Eight gaps could cause **stale chips after background sync**, **wrong cross-tab navigation typing**, **retry button ignoring pending rows**, or **duplicated chip styling** between Command Strip and Active Visit.

| Check | Result |
|-------|--------|
| Epic 3.7 AC (chip labels, sync queue, error tap) | Pass (after fixes) |
| FR-11 visible sync status | Pass — fulfills 3.6 deferral |
| UX-DR6 / UX-DR12 labels & colors | Pass — matches DESIGN.md tokens |
| NFR-7 queue inspectable | Pass — Sync queue screen |
| Story 3.6 queue contract reuse | Pass — read-only consumption |
| Active Visit chip refresh after sync | **Fix** — focus refresh required |
| Cross-tab More navigation | **Fix** — `getParent()` pattern |
| Only error chip pressable | **Fix** — explicit AC |
| Shared chip component | **Fix** — task added |
| Test task completeness | **Fix** — ActiveVisit tests |

---

## Critical Issues (Must Fix) — Applied

### 1. Active Visit chip can go stale after background sync

`TodayScreen` holds `offlineQueue` in state and updates via `syncAfterQueueChange` / `refreshOfflineQueue`. `ActiveVisitScreen` has no queue subscription — chip would freeze at mount.

**Fix applied:** AC2 + task — `useFocusEffect` (or `useIsFocused` + `readOfflineQueue`) on Active Visit and Sync queue screens to refresh queue on focus.

### 2. Cross-tab navigation from Today stack

`TodayScreen` uses `NativeStackNavigationProp<TodayStackParamList>` only. Navigating to More → Sync queue requires tab parent (existing pattern: `navigation.getParent()?.navigate('Cases', …)` at line ~780).

**Fix applied:** Dev Notes + task — use `navigation.getParent()?.navigate('More', { screen: 'SyncQueue' })`; type with `CompositeNavigationProp` or document `getParent()` cast.

### 3. Only error chip should be pressable

UX-DR6: error tap → Sync queue. Story AC3 covers error only; dev agent might make all chips pressable.

**Fix applied:** AC8 expanded — local/pending/synced chips are **not** buttons; only `error` uses `Pressable`.

### 4. Retry scope too narrow in AC5

AC5 limited retry to `error` or `local`; `pending` rows exist during flush and should remain visible; retry should run flush for **any non-empty queue**.

**Fix applied:** AC5 — Retry when queue has any items; flush processes all; pending shows **Uploading** chip during flight.

---

## Enhancement Opportunities — Applied

### 5. Shared `SyncChip` component

Chip styles duplicated across `CommandStripCard` and `ActiveVisitScreen` risks drift.

**Fix applied:** Task — `src/components/SyncChip.tsx` (presentation + optional error `onPress`).

### 6. Sync queue screen focus refresh

User may retry, navigate to Today, then return — list must re-read queue.

**Fix applied:** Task on `SyncQueueScreen` — `useFocusEffect` → `readOfflineQueue()`.

### 7. ActiveVisitScreen tests missing from task list

AC9 implies Active Visit chip coverage; tasks only listed Today + CommandStripCard.

**Fix applied:** Task + AC9 bullet — update `ActiveVisitScreen.test.tsx`.

### 8. Label constants single source

UX-DR12 exact strings must not drift between resolver, card, and tests.

**Fix applied:** Task — export `SYNC_CHIP_LABELS` from `resolveVisitSyncChip.ts` (or adjacent constants module).

---

## Optimizations (Not Applied — Acceptable for v1)

- **More tab badge** for error count — not in epic; Sync queue menu row is sufficient.
- **Pull-to-refresh on Sync queue** — Retry button covers manual refresh.
- **Supervisor read-only sync queue API** — NFR-7 satisfied by mobile inspection screen.

---

## LLM Dev-Agent Notes

- Mobile Jest baseline: **70** tests after Story 3.6.
- No OpenAPI / api-client regen (mobile UI only).
- Do **not** modify `mergeQueueWithVisits` filter logic from 3.6 code review.
- Queue key: `midi-kaval:offline-sync-queue:v1`.
- Chip reads **full** queue (including `error` rows); overlays use filtered queue only.
- `useFocusEffect` from `@react-navigation/native` — not yet used in codebase; safe to introduce.

---

## Verdict

Story is **implementation-ready** after applied fixes. Proceed with `bmad-dev-story`.
