---
baseline_commit: NO_VCS
---

# Story 3.7: Sync Chip and Sync Queue Mobile UI

Status: done

<!-- Validated: 2026-06-17 — see 3-7-sync-chip-and-sync-queue-mobile-ui-validation-report.md (8 fixes applied) -->

## Story

As a **Social Worker**,
I want visible sync status on my visits,
so that I trust what is saved locally vs in the cloud (FR-11, UX-DR6, UX-DR12, NFR-7).

## Acceptance Criteria

1. **Given** Story 3.6 offline queue with `syncStatus: "local" | "pending" | "error"` on queued mutations  
   **When** I view a visit on the Today Command Strip  
   **Then** the sync chip shows the correct **text label** and **color variant** for that visit's worst active queue state:
   | Queue state (per visit) | Label (UX-DR12) | Background | Foreground |
   |-------------------------|-----------------|------------|------------|
   | `local` | Saved on this device | `#F4F3FF` | `#5925DC` |
   | `pending` | Uploading | `#FFFAEB` | `#B54708` |
   | none (no queue row for visit) | Synced | `#ECFDF3` | `#027A48` |
   | `error` | Sync failed | `#FEF3F2` | `#B42318` |
   **And** priority when multiple mutations exist for one `visitId` is **error > pending > local**  
   **And** `error` mutations do **not** apply optimistic visit overlays (Story 3.6 `mergeQueueWithVisits` already filters them — chip must still reflect error state from full queue read)

2. **Given** I am on **Active Visit** (`ActiveVisitScreen`)  
   **When** the current visit has queued mutations  
   **Then** the same sync chip (label + colors) appears on the visit card header area  
   **And** when the visit has no queue mutations, chip shows **Synced**  
   **And** queue is re-read on screen focus (`useFocusEffect`) so chip updates after background sync or returning from Sync queue

3. **Given** a visit sync chip in **Sync failed** state on Today or Active Visit  
   **When** I tap the chip  
   **Then** navigation opens **More → Sync queue** screen (no silent failure)

4. **Given** **More** tab  
   **When** I open **Sync queue** (menu row or deep link from error chip)  
   **Then** I see a list of all queued mutations (FIFO order) with:
   - Visit crime/ST (resolve from cached Command Strip items or mutation `visitId` lookup)
   - Mutation type (`Start visit` / `Complete visit`)
   - Status label matching AC1 table
   - `lastError` when `syncStatus === "error"`
   **And** empty queue shows copy: **"All visits are synced."** (UX tone — not "queue empty")

5. **Given** the Sync queue screen is open and the offline queue has one or more items  
   **When** I tap **Retry sync**  
   **Then** app calls `flushOfflineQueue()` (reuse Story 3.6 — includes session refresh + mutex)  
   **And** list refreshes from `readOfflineQueue()` after completion  
   **And** success removes applied items; errors remain visible with updated `lastError`  
   **And** visits still in `pending` during flush show **Uploading** chip elsewhere until flush completes

6. **Given** device is online and queue is empty  
   **When** I view Today Command Strip  
   **Then** all visits show **Synced** chip (replaces Story 3.2 static `"Synced"` placeholder)

7. **Given** Story 3.6 sync engine is complete  
   **When** this story ships  
   **Then** **no new API endpoints** or OpenAPI changes  
   **And** no changes to `POST /api/v1/sync/push` contract  
   **And** offline enqueue / flush behavior from 3.6 remains intact

8. **Given** accessibility requirements (UX-DR6, EXPERIENCE.md)  
   **When** sync chip renders  
   **Then** `accessibilityLabel` includes the full state text (e.g. `"Sync failed"`, not color alone)  
   **And** **only** the `error` chip is a `Pressable` with `accessibilityRole="button"` and `accessibilityHint` to open sync queue  
   **And** `local`, `pending`, and `synced` chips are non-interactive (no false button affordance)

9. **Given** mobile test baseline after Story 3.6 (**70** Jest tests)  
   **When** this story ships  
   **Then** Jest tests cover at minimum:
   - `resolveVisitSyncChip` (or equivalent) — priority error > pending > local > synced
   - `CommandStripCard` — renders each chip variant with correct label text
   - Error chip press calls `onSyncChipPress`
   - `SyncQueueScreen` — lists error mutation with `lastError`; retry invokes `flushOfflineQueue`
   - `TodayScreen` — passes derived sync state per visit (mock queue fixtures)
   - `ActiveVisitScreen` — shows chip for queued visit; updates on focus refresh (mock queue)
   **And** `npm run test:mobile` passes

10. **Given** scope boundaries  
    **When** this story ships  
    **Then** no POCSO discreet header changes (Story 3.8)  
    **And** no draft travel claim offline queue (Epic 6)  
    **And** no WatermelonDB/SQLite migration  
    **And** no web/PWA sync UI (mobile field app only)

## Tasks / Subtasks

- [x] **Mobile — sync chip resolver** (AC: 1, 6, 8)
  - [x] `src/services/sync/resolveVisitSyncChip.ts` — map `visitId` + `QueuedMutation[]` → `{ state, label, backgroundColor, color }`
  - [x] Export `SYNC_CHIP_LABELS` constants aligned with UX-DR12 (no "Offline mode engaged")

- [x] **Mobile — shared SyncChip component** (AC: 1, 2, 8)
  - [x] `src/components/SyncChip.tsx` — label, colors, optional `onPress` when `state === 'error'`

- [x] **Mobile — CommandStripCard** (AC: 1, 3, 8)
  - [x] Replace `syncLabel: string` with `SyncChip` component (or `syncChip` presentation prop)
  - [x] Wire `onSyncChipPress` for error state only

- [x] **Mobile — TodayScreen wiring** (AC: 1, 3, 6)
  - [x] Derive per-visit chip from `offlineQueue` (already in state from Story 3.6)
  - [x] Error chip: `navigation.getParent()?.navigate('More', { screen: 'SyncQueue' })` (same cross-tab pattern as Cases navigate)
  - [x] Remove hardcoded `syncLabel="Synced"`

- [x] **Mobile — ActiveVisitScreen chip** (AC: 2, 3)
  - [x] `useFocusEffect` → `readOfflineQueue()`; resolve chip for `visit.id`
  - [x] Reuse `SyncChip`; error tap → More → Sync queue

- [x] **Mobile — More navigation stack** (AC: 4, 5)
  - [x] `MoreStackNavigator` — `MoreHome` (existing logout screen) + `SyncQueue`
  - [x] Update `MainTabParamList.More` → `NavigatorScreenParams<MoreStackParamList>`
  - [x] `MainTabNavigator` — wrap `MoreScreen` in stack
  - [x] Add **Sync queue** row on `MoreScreen` → `navigation.navigate('SyncQueue')`

- [x] **Mobile — SyncQueueScreen** (AC: 4, 5)
  - [x] `src/screens/more/SyncQueueScreen.tsx` — FIFO list, retry button, empty state
  - [x] `useFocusEffect` refresh from `readOfflineQueue()` on each visit
  - [x] Resolve visit labels from `readCache()` items by `visitId`
  - [x] Retry: `await flushOfflineQueue()` then `readOfflineQueue()` refresh

- [x] **Mobile — Tests** (AC: 9)
  - [x] `__tests__/resolveVisitSyncChip.test.ts`
  - [x] Update `CommandStripCard.test.tsx` — chip variants + error press
  - [x] `__tests__/SyncQueueScreen.test.tsx`
  - [x] Update `TodayScreen.test.tsx` — chip state from mocked queue
  - [x] Update `ActiveVisitScreen.test.tsx` — chip + focus refresh mock

- [x] **Docs** (AC: 7)
  - [x] `README.md` — brief note on sync chip states + More → Sync queue (field app section)

### Review Findings

- [x] [Review][Patch] Uploading chip not shown during flush on Today [TodayScreen.tsx:171]
- [x] [Review][Patch] Active Visit chip stale after background flush while screen focused [ActiveVisitScreen.tsx:43]
- [x] [Review][Patch] CommandStripCard missing pending chip variant test [CommandStripCard.test.tsx]
- [x] [Review][Patch] Sync queue screen flashes full loading spinner on every refocus [SyncQueueScreen.tsx:40]
- [x] [Review][Patch] Error rows hide lastError when field is empty [SyncQueueScreen.tsx:96]

## Dev Notes

### Scope boundary (critical)

| In scope (3.7) | Out of scope |
|----------------|--------------|
| Sync chip UI on Command Strip + Active Visit | `POST /sync/push` / API changes (3.6) |
| More → Sync queue screen + retry | POCSO discreet mode (3.8) |
| Navigation to sync queue from error chip | Travel claim offline queue (Epic 6) |
| Text labels per UX-DR6/UX-DR12 | WatermelonDB migration |
| Jest coverage for chip + queue UI | Supervisor web sync UI |

**Critical:** This story is **mobile UI only**. Story 3.6 already owns queue persistence, `flushOfflineQueue`, and optimistic overlays. **Extend — do not rewrite** `offlineQueue.ts`, `mobileSyncPushService.ts`, or `mergeQueueWithVisits.ts` unless a minimal hook export is needed.

### Sync chip state resolution (implement exactly)

```typescript
// Priority for multiple mutations on same visitId:
// error > pending > local > synced (no matching queue rows)

type SyncChipState = 'local' | 'pending' | 'synced' | 'error';

// Labels — UX-DR12 / EXPERIENCE.md (use exactly):
// local   → "Saved on this device"
// pending → "Uploading"
// synced  → "Synced"
// error   → "Sync failed"
```

**Synced vs local:** A visit is **Synced** when it has **no** queue rows, or only rows that were removed after successful push. Do not show **Synced** while a `local`/`pending`/`error` row exists for that `visitId`.

**Error overlay interaction:** Story 3.6 code review fixed `mergeQueueWithVisits` to skip `error` rows for optimistic status. The chip must read the **full** `offlineQueue` (including errors) so users still see **Sync failed** even when visit status reverted to server truth.

### Files to extend (read before editing)

| File | Current behavior | This story |
|------|------------------|------------|
| `CommandStripCard.tsx` | Static `syncLabel` string; purple chip style always | Per-state colors; error pressable |
| `TodayScreen.tsx` | `syncLabel="Synced"` hardcoded; `offlineQueue` in state | Derive chip per visit; navigate on error |
| `ActiveVisitScreen.tsx` | No sync chip | Chip + error navigation |
| `MoreScreen.tsx` | Logout only | Link to Sync queue |
| `MainTabNavigator.tsx` | `MoreScreen` direct | `MoreStackNavigator` |
| `types.ts` | `More: undefined` | `MoreStackParamList` |
| `offlineQueue.ts` | CRUD + `syncStatus` | Read-only consumption |
| `mobileSyncPushService.ts` | `flushOfflineQueue` with mutex | Call from Sync queue retry |

### Navigation pattern (More stack)

Follow `CasesStackNavigator` pattern:

```typescript
export type MoreStackParamList = {
  MoreHome: undefined;
  SyncQueue: undefined;
};

export type MainTabParamList = {
  Today: NavigatorScreenParams<TodayStackParamList> | undefined;
  Cases: NavigatorScreenParams<CasesStackParamList> | undefined;
  More: NavigatorScreenParams<MoreStackParamList> | undefined;
};
```

**Cross-tab navigate from Today error chip:**

```typescript
// TodayScreen is inside TodayStack — use tab parent (see Cases cross-tab at ~line 780)
navigation.getParent()?.navigate('More', { screen: 'SyncQueue' });
```

Use typed `CompositeNavigationProp` if needed, or document `getParent()` navigation cast (project already uses this pattern for Cases).

### Sync queue screen UX

| Element | Behavior |
|---------|----------|
| Row title | `{crimeNumber} · {stNumber}` from cache lookup, else `Visit {shortId}` |
| Subtitle | `Start visit` or `Complete visit` + status label |
| Error detail | `lastError` in `#B42318` body text |
| Retry | Primary button; disabled while `flushOfflineQueue` in flight |
| Empty | `"All visits are synced."` centered |

Support inspection for NFR-7 — coordinators/support can ask field worker to open this screen.

### Previous story intelligence (3.6)

- Queue key: `midi-kaval:offline-sync-queue:v1` in AsyncStorage
- `syncStatus` values: `local`, `pending`, `error` (no `synced` on queue rows — synced = absent from queue)
- `flushOfflineQueue`: mutex, `refreshSession` first, removes `applied` + `duplicate` with visit payload; keeps `error`
- `mergeQueueWithVisits`: only overlays `local`/`pending` — chip must use raw queue for error display
- Mobile Jest baseline: **70** tests; NetInfo mocked in `jest.setup.js`
- Code review patches applied — do not regress queue loss or AC12 refresh behavior
- Deferred tests from 3.6 review — **this story should cover** chip/queue UI gaps listed in AC9 (partially closes 3.6 deferrals)

### Screen focus refresh (required)

`useSyncOnForeground` on Today updates queue via `syncAfterQueueChange`. **Active Visit** and **Sync queue** are outside that tree — they must call `readOfflineQueue()` on focus (`useFocusEffect` from `@react-navigation/native`) or chips/lists go stale after background flush.

### Previous story intelligence (3.2)

- `CommandStripCard` already has `syncChip` positioned top-right (`styles.syncChip`)
- Story 3.2 shipped static `"Synced"` placeholder — replace with dynamic resolver
- Design tokens: pilot purple `#F4F3FF` / `#5925DC` was local-only stand-in; now use full 4-state palette from DESIGN.md

### Architecture compliance

- **§5.5 Mobile offline:** UI sync chip states per EXPERIENCE.md — this story fulfills the UI row deferred from architecture
- **No API layer changes** — conflict rules and idempotency remain server-side from 3.6
- **AsyncStorage queue** remains v1 persistence (not WatermelonDB)

### Testing

**Unit:** `resolveVisitSyncChip` pure function — table-driven tests for priority and labels.

**Component:** Mock `flushOfflineQueue`, `readOfflineQueue`, `readCache` in `SyncQueueScreen.test.tsx`.

**Integration-style:** `TodayScreen.test.tsx` — set `offlineQueue` mock fixture; assert chip text in rendered card.

**Do not require** native NetInfo or device tests in Jest.

### Latest technical information

- `@react-native-community/netinfo` ^11.x already installed (Story 3.6)
- React Navigation nested navigators: use `NavigatorScreenParams` on tab param (React Navigation 6.x — project standard)
- No new npm dependencies expected

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.7, FR-11, UX-DR6, UX-DR12]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Sync chip, State Patterns, Voice table]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — `sync-chip-*` tokens]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.5 Mobile offline UI row]
- [Source: `_bmad-output/implementation-artifacts/3-6-offline-visit-storage-and-sync-push-api.md` — queue contract, deferred UI]
- [Source: `apps/mobile/src/components/CommandStripCard.tsx`]
- [Source: `apps/mobile/src/services/sync/offlineQueue.ts`]
- [Source: `apps/mobile/src/services/sync/mobileSyncPushService.ts`]
- [Source: `apps/mobile/src/screens/today/TodayScreen.tsx`]

## Dev Agent Record

### Agent Model Used

Composer (dev-story continuation)

### Debug Log References

- SyncQueueScreen Jest: `useFocusEffect` mock must defer via `useEffect` to avoid infinite re-render loop from `setLoading(true)` on focus.

### Completion Notes List

- Implemented four-state sync chip resolver (`error > pending > local > synced`) with UX-DR12 labels and DESIGN.md colors.
- Wired `SyncChip` on Command Strip and Active Visit; error chip navigates cross-tab to More → Sync queue.
- Added `MoreStackNavigator` with Sync queue screen (FIFO list, empty state, retry via `flushOfflineQueue`).
- Mobile Jest: **94** tests passing (22 new/updated for chip + queue UI; +2 from review patches).
- Code review patches: optimistic **Uploading** chip before flush (Today + Active Visit), `useSyncOnForeground` on Active Visit, sync queue refocus UX, error fallback copy.

### File List

- `apps/mobile/src/services/sync/resolveVisitSyncChip.ts` (new)
- `apps/mobile/src/components/SyncChip.tsx` (new)
- `apps/mobile/src/components/CommandStripCard.tsx` (modified)
- `apps/mobile/src/screens/today/TodayScreen.tsx` (modified)
- `apps/mobile/src/screens/today/ActiveVisitScreen.tsx` (modified)
- `apps/mobile/src/navigation/types.ts` (modified)
- `apps/mobile/src/navigation/MainTabNavigator.tsx` (modified)
- `apps/mobile/src/navigation/MoreStackNavigator.tsx` (new)
- `apps/mobile/src/screens/more/MoreScreen.tsx` (modified)
- `apps/mobile/src/screens/more/SyncQueueScreen.tsx` (new)
- `apps/mobile/__tests__/resolveVisitSyncChip.test.ts` (new)
- `apps/mobile/__tests__/SyncQueueScreen.test.tsx` (new)
- `apps/mobile/__tests__/CommandStripCard.test.tsx` (modified)
- `apps/mobile/__tests__/TodayScreen.test.tsx` (modified)
- `apps/mobile/__tests__/ActiveVisitScreen.test.tsx` (modified)
- `README.md` (modified)

## Change Log

- 2026-06-17 — Story created from epics + UX + Story 3.6 handoff; ready-for-dev.
- 2026-06-17 — Validation: 8 fixes applied (focus refresh, cross-tab nav, error-only pressable, shared SyncChip, retry scope, tests).
- 2026-06-17 — Code review: 5 patches applied (pending chip during flush, Active Visit sync refresh, tests, sync queue UX).
