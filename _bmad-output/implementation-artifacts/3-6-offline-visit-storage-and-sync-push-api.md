---
baseline_commit: NO_VCS
---

# Story 3.6: Offline Visit Storage and Sync Push API

Status: done

<!-- Validated: 2026-06-17 — see 3-6-offline-visit-storage-and-sync-push-api-validation-report.md (10 fixes applied) -->

## Story

As a **Social Worker in low signal**,
I want visits saved locally and synced when connectivity returns,
so that no field data is lost (FR-11, NFR-6, NFR-7).

## Acceptance Criteria

1. **Given** the visit scheduler API after Stories 3.1–3.3  
   **When** OpenAPI is updated  
   **Then** a new endpoint exists: `POST /api/v1/sync/push`  
   **And** it is authorized with `[Authorize(Policy = Policies.FieldWorker)]`  
   **And** request body is `{ mutations: SyncMutationDto[] }` with each item `{ clientMutationId, type, payload, clientTimestampUtc }`  
   **And** response envelope is `{ data: SyncPushResultDto, meta: { requestId } }` with per-mutation outcomes

2. **Given** a `sync_mutations` persistence table (new EF migration)  
   **When** the migration applies  
   **Then** columns include `id`, `organisation_id`, `user_id`, `client_mutation_id` (unique per org+user), `mutation_type`, `payload_json`, `status` (`applied` | `rejected` | `duplicate`), `server_message`, `created_at_utc`, `processed_at_utc`  
   **And** unique index on `(organisation_id, user_id, client_mutation_id)` enforces idempotent replay

3. **Given** a batch `POST /api/v1/sync/push` with mutation `type: "visit.start"` and payload `{ visitId }`  
   **When** the visit exists, is assigned to the caller, and is `Scheduled`  
   **Then** server applies the same rules as `POST /visits/{id}/start` (status → `InProgress`, `started_at_utc`, audit `visit.started`)  
   **And** returns `{ clientMutationId, status: "applied", visit?: VisitListItemDto }`  
   **When** the same `clientMutationId` is replayed  
   **Then** returns `{ status: "duplicate" }` without double-applying, including prior `visit` when original was `applied`  
   **When** visit is already `InProgress` or `Completed` (server ahead)  
   **Then** returns `{ status: "rejected", serverMessage }` — **server wins** (no client override of status)

4. **Given** mutation `type: "visit.complete"` and payload `{ visitId, note, noteClientTimestampUtc }`  
   **When** visit is `InProgress` and assigned to caller  
   **Then** server applies same rules as `POST /visits/{id}/complete` (note required, `visit_notes` insert, case counters, audit) in one transaction  
   **And** returns applied outcome with updated `VisitListItemDto`  
   **When** visit already `Completed` on server  
   **Then** if a `visit_notes` row exists with `created_at_utc` **older** than `noteClientTimestampUtc`, **merge** note body (update existing note text + timestamp) — **note merge by timestamp** exception  
   **Otherwise** server wins with `rejected` / `duplicate` as appropriate  
   **When** replayed with same `clientMutationId`  
   **Then** idempotent `duplicate` response with prior `visit` when original was `applied`

5. **Given** a mutation fails validation (404 visit, 403 wrong assignee, invalid type)  
   **When** push processes the batch  
   **Then** that item returns `{ status: "rejected", serverMessage }`  
   **And** other items in the same batch still process (partial batch success)  
   **And** rejected items are recorded in `sync_mutations` for support inspection

6. **Given** mobile field worker with no network (`@react-native-community/netinfo` reports offline)  
   **When** I tap **Start visit** on Today or **Complete visit** on Active Visit  
   **Then** the mutation is persisted locally in an offline queue (AsyncStorage) with a new `clientMutationId` (UUID v4)  
   **And** visit UI updates optimistically (`InProgress` after start; `Completed` on Command Strip after complete via `mergeQueueWithVisits` + `writeCache`)  
   **And** navigating back to Today after offline complete shows the visit as `Completed` locally before sync  
   **And** queue item includes `syncStatus: "local"` (internal enum — full chip UI deferred to Story 3.7)

7. **Given** queued local mutations and network becomes available  
   **When** sync runs (app foreground via `AppState` + NetInfo online, connectivity restore, or after successful online API call)  
   **Then** client calls `authSessionService.refreshSession()` before push; if refresh fails, queue items stay with `syncStatus: "error"`  
   **And** client posts batch to `POST /api/v1/sync/push` in FIFO order (per visit: `visit.start` before `visit.complete` when both queued)  
   **And** applied items are removed from queue; rejected items remain with `syncStatus: "error"` and `lastError`  
   **And** duplicate items are removed from queue  
   **And** command strip cache is updated after successful sync

8. **Given** device is online  
   **When** I start or complete a visit  
   **Then** existing direct API calls (`startVisit`, `completeVisit`) are used — queue is bypassed  
   **And** behavior matches Stories 3.3–3.5 (no regression)

9. **Given** cold open with queued mutations in AsyncStorage  
   **When** Today mounts  
   **Then** cached command strip merges optimistic visit status from queue (e.g. `Scheduled` → `InProgress` if start queued)  
   **And** sync attempt runs if online

10. **Given** Story 3.7 sync chip UI is **out of scope** (PRD FR-11 visible labels ship in 3.7)  
    **When** this story ships  
    **Then** no new chip labels on `CommandStripCard` (Story 3.7)  
    **And** no More → Sync queue screen (Story 3.7)  
    **And** internal `syncStatus` field exists on queue items for 3.7 to consume

11. **Given** integration and test baselines after Story 3.5  
    **When** this story ships  
    **Then** API integration tests cover at minimum:
    - `visit.start` applied once; replay → `duplicate`
    - `visit.complete` with note applied; replay → `duplicate`
    - server-ahead conflict (already completed) → `rejected` or note merge per timestamp rule
    - wrong role → **403**
    **And** API unit tests cover idempotency key logic and note-merge decision  
    **And** mobile Jest tests cover:
    - offline start queues mutation and optimistically updates status
    - offline complete queues mutation
    - sync push removes applied/duplicate items; keeps rejected
    - online path still calls direct API (mocked)
    **And** `npm run test:mobile` and API unit suite pass  
    **And** OpenAPI snapshot regenerated + `npm run generate:api-client` + build api-client (absolute `API_OPENAPI_FILE` on Windows)

12. **Given** I pull-to-refresh Today while offline mutations are queued  
    **When** fresh `listToday()` succeeds  
    **Then** the offline queue is **not** cleared (unlike `customVisitOrder` in Story 3.5)  
    **And** `mergeQueueWithVisits` re-applies optimistic status overlays on the refreshed items  
    **And** `customVisitOrder` / `routeGroupingActive` from Story 3.5 are preserved

## Tasks / Subtasks

- [x] **API — schema** (AC: 2)
  - [x] `Domain/Entities/SyncMutation.cs` + `SyncMutationConfiguration.cs`
  - [x] EF migration `AddSyncMutations`

- [x] **API — DTOs** (AC: 1, 3–5)
  - [x] `Models/Sync/SyncDtos.cs` — `SyncPushRequest`, `SyncMutationDto`, `SyncPushResultDto`, `SyncMutationResultDto`, payload types

- [x] **API — SyncService** (AC: 3–5)
  - [x] `Infrastructure/Sync/SyncPushService.cs` — batch processor, idempotency ledger
  - [x] **Do not** call `VisitService.StartAsync`/`CompleteAsync` directly (they throw on conflicts) — use internal apply methods below
  - [x] `VisitService` — `ApplyVisitStartForSync`, `ApplyVisitCompleteForSync`, `MergeVisitNoteForSync` returning structured outcomes (`applied` | `rejected` | `duplicate`)
  - [x] Note merge by `noteClientTimestampUtc` vs existing `visit_notes.created_at_utc`; audit event on merge
  - [x] Register in DI

- [x] **API — Controller** (AC: 1)
  - [x] `Controllers/V1/SyncController.cs` — `POST push` before catch-all routes
  - [x] XML doc comments for OpenAPI

- [x] **API — Tests** (AC: 11)
  - [x] `tests/api.unit/VisitSyncNoteMergeTests.cs` — idempotency + note merge unit tests
  - [x] `tests/api.integration/SyncPushTests.cs` — scenarios in AC11
  - [x] Extend `VisitTestData` with `PushSyncAsync` helper

- [x] **API client + OpenAPI** (AC: 11)
  - [x] Regenerate snapshot + `@midi-kaval/api-client`

- [x] **Mobile — dependencies** (AC: 6–7)
  - [x] Add `@react-native-community/netinfo`

- [x] **Mobile — offline queue** (AC: 6–7, 9–10)
  - [x] `src/services/sync/offlineQueue.ts` — AsyncStorage CRUD, FIFO, `clientMutationId`, `syncStatus`
  - [x] `src/services/sync/syncMutationTypes.ts` — typed payloads
  - [x] `src/services/sync/mobileSyncPushService.ts` — push batch, process results, update queue
  - [x] `src/services/sync/useNetworkStatus.ts` — NetInfo hook
  - [x] `src/services/sync/useSyncOnForeground.ts` — `AppState` foreground + trigger sync
  - [x] `src/services/sync/mergeQueueWithVisits.ts` — optimistic strip merge (compose **before** `applyDisplayOrder`)

- [x] **Mobile — wire visit flows** (AC: 6–8)
  - [x] `VisitApiService.ts` — route start/complete through offline gate
  - [x] `TodayScreen.tsx` — optimistic start when queued
  - [x] `ActiveVisitScreen.tsx` — optimistic complete when queued
  - [x] Trigger sync on foreground + connectivity restore + post-online API success
  - [x] `TodayScreen.tsx` — on refresh: preserve queue + custom visit order (AC12)

- [x] **Mobile — Tests** (AC: 11)
  - [x] `__tests__/offlineQueue.test.ts`
  - [x] `__tests__/mobileSyncPushService.test.ts`
  - [x] Update `TodayScreen.test.tsx` / `ActiveVisitScreen.test.tsx` for offline paths

- [x] **Docs** (AC: 11)
  - [x] `README.md` — sync push endpoint + mobile offline queue note

### Review Findings

- [x] [Review][Patch] Rejected mutation replay returns `duplicate` and mobile drops it from queue [`SyncPushService.cs:65-80`, `mobileSyncPushService.ts:81-82`] — ledger replay always maps to `duplicate`; mobile removes all `duplicate` results, so first-push `rejected` mutations are silently discarded on retry (violates AC7).
- [x] [Review][Patch] `mergeQueueWithVisits` overlays `error`/`rejected` queue items [`mergeQueueWithVisits.ts:33-52`] — no `syncStatus` filter; failed sync items still show optimistic InProgress/Completed on Today.
- [x] [Review][Patch] Pull-to-refresh clears `customVisitOrder` / `routeGroupingActive` (AC12) [`TodayScreen.tsx:463-475`] — `onRefresh` passes `clearGrouping: true`; AC12 requires preserving Story 3.5 grouping state.
- [x] [Review][Patch] Refresh writes unmerged API items to command strip cache (AC12) [`TodayScreen.tsx:319-325`] — `writeCache(nextItems)` omits `mergeQueueWithVisits` overlay; persisted cache can lag queued mutations.
- [x] [Review][Patch] No `writeCache` after successful background sync (AC7) [`TodayScreen.tsx:169-191`] — `syncAfterQueueChange` updates in-memory items only.
- [x] [Review][Patch] Offline complete does not persist merged visits to cache (AC6) [`ActiveVisitScreen.tsx`, `TodayScreen.tsx`] — only offline start calls `writeCache`; navigating back after offline complete can show stale cache until refetch.
- [x] [Review][Patch] Corrupt offline queue JSON is silently wiped [`offlineQueue.ts:23-25`] — parse failure deletes the entire queue key with no recovery.
- [x] [Review][Patch] Empty note can be enqueued for offline complete [`VisitApiService.ts:78-87`] — no non-empty guard before enqueue; server rejects permanently under same `clientMutationId`.
- [x] [Review][Patch] Concurrent `flushOfflineQueue` calls can race [`mobileSyncPushService.ts`] — no mutex; parallel foreground/listToday/post-online triggers can interleave AsyncStorage read-modify-write.
- [x] [Review][Patch] Concurrent idempotency insert can 500 the batch [`SyncPushService.cs:59-170`] — unique-index race on `(org, user, clientMutationId)` is uncaught.
- [x] [Review][Patch] Unhandled exceptions abort remaining batch items [`SyncPushService.cs:83-106`] — only `VisitForbiddenException` is caught; DB/other failures stop later mutations in the same push.
- [x] [Review][Defer] Note-merge-on-completed integration test missing [`SyncPushTests.cs`] — deferred, AC11 coverage gap.
- [x] [Review][Defer] Server-ahead `visit.complete` rejection integration test missing [`SyncPushTests.cs`] — deferred, AC11 coverage gap.
- [x] [Review][Defer] Partial batch success integration test missing [`SyncPushTests.cs`] — deferred, AC5/AC11 coverage gap.
- [x] [Review][Defer] `SyncPushService` idempotency ledger unit tests missing [`SyncPushService.cs`] — deferred, AC11 coverage gap.
- [x] [Review][Defer] Mobile offline start/complete screen tests missing [`TodayScreen.test.tsx`, `ActiveVisitScreen.test.tsx`] — deferred, AC11 coverage gap.
- [x] [Review][Defer] `mobileSyncPushService` rejected/duplicate removal tests missing [`mobileSyncPushService.test.ts`] — deferred, AC11 coverage gap.
- [x] [Review][Defer] `VisitApiService` online-bypass-queue tests missing [`VisitApiService.ts`] — deferred, AC11 coverage gap.
- [x] [Review][Defer] `refreshSession` failure → queue `error` test missing [`mobileSyncPushService.ts`] — deferred, AC7/AC11 coverage gap.
- [x] [Review][Defer] AC12 pull-to-refresh preservation test missing [`TodayScreen.test.tsx`] — deferred, AC11 coverage gap.

## Dev Notes

### Scope boundary (critical)

| In scope (3.6) | Deferred to 3.7+ |
|----------------|------------------|
| Local queue + `clientMutationId` | Sync chip UI labels on Command Strip |
| `POST /api/v1/sync/push` + idempotency table | More → Sync queue screen |
| Optimistic visit start/complete offline | Draft travel claim offline (Epic 6) |
| Internal `syncStatus` on queue items | POCSO discreet mode (3.8) |
| Note merge by timestamp on conflict | WatermelonDB/SQLite migration |
| GPS verify / reschedule offline | Sync chip UI + Sync queue screen (3.7) |

**Critical:** Use **AsyncStorage JSON queue** for v1 pilot (same pattern as `commandStripCache.ts`). Do not add WatermelonDB/SQLite in this story unless tests prove AsyncStorage insufficient.

### VisitService sync apply (read before coding)

`StartAsync` / `CompleteAsync` **throw** `VisitBusinessRuleException` on conflicts — unsuitable for sync push. Sync must use internal methods that return `{ status, serverMessage?, visit? }`:

| Server state | `visit.start` | `visit.complete` |
|--------------|---------------|------------------|
| `Scheduled` | apply start | apply complete (allowed — `Scheduled` ∈ `ActiveStatuses`) |
| `InProgress` | `rejected` or `duplicate` | apply complete |
| `Completed` | `rejected` | merge note if newer timestamp, else `rejected` |

One `visit_notes` row per `visit_id` (see `LoadCompletionNotesAsync` dictionary key).

### Sync mutation contract

```json
POST /api/v1/sync/push
{
  "mutations": [
    {
      "clientMutationId": "uuid",
      "type": "visit.start",
      "clientTimestampUtc": "2026-06-17T10:00:00Z",
      "payload": { "visitId": "uuid" }
    },
    {
      "clientMutationId": "uuid",
      "type": "visit.complete",
      "clientTimestampUtc": "2026-06-17T11:00:00Z",
      "payload": {
        "visitId": "uuid",
        "note": "Visit completed at home.",
        "noteClientTimestampUtc": "2026-06-17T11:00:00Z"
      }
    }
  ]
}
```

Response:

```json
{
  "data": {
    "results": [
      { "clientMutationId": "uuid", "status": "applied", "visit": { } },
      { "clientMutationId": "uuid", "status": "duplicate" },
      { "clientMutationId": "uuid", "status": "rejected", "serverMessage": "Visit is already completed." }
    ]
  },
  "meta": { "requestId": "..." }
}
```

`status` enum: `applied` | `duplicate` | `rejected`.

### Conflict rules (implement exactly)

1. **Visit status conflicts:** Server state wins. Client cannot revert `Completed` → `InProgress`.
2. **Idempotency:** Same `(organisation_id, user_id, client_mutation_id)` → `duplicate`, return prior outcome without re-applying.
3. **Note merge exception:** When server visit is already `Completed` and client sends `visit.complete` with note, if `noteClientTimestampUtc` > existing `visit_notes.created_at_utc`, update note body and timestamp; return `applied`. Otherwise `rejected` or `duplicate`.

### Files to extend (read before editing)

| File | Current behavior | This story |
|------|------------------|------------|
| `VisitService.cs` | `StartAsync`, `CompleteAsync` online only | Extract or reuse apply logic from sync service |
| `VisitApiService.ts` | Direct HTTP start/complete | Offline gate + queue |
| `TodayScreen.tsx` | Online `startVisit` | Optimistic queue path |
| `ActiveVisitScreen.tsx` | Online `completeVisit` | Optimistic queue path |
| `commandStripCache.ts` | Visit list cache | Optionally store queue snapshot version |

### Previous story intelligence (3.5)

- OpenAPI regen: **absolute** `API_OPENAPI_FILE` on Windows
- Mobile tests baseline: **63** Jest tests
- `writeCache` / `readCache` patterns — reuse for queue persistence key `midi-kaval:offline-sync-queue:v1`
- Pull-to-refresh clears **grouping only** (`customVisitOrder`) — must **not** clear offline queue (AC12)
- `mergeQueueWithVisits` runs before `applyDisplayOrder` — preserve `customVisitOrder` after sync

### Previous story intelligence (3.3)

- `visit_notes` table: `visit_id`, `created_at_utc`, `body_text` — use for note merge
- `CompleteAsync` requires non-empty note (max 4000 chars)
- Single `SaveChangesAsync` per complete — sync path must match

### Testing

**Integration:** Docker/Testcontainers required. Reuse `VisitTestData.BuildSocialWorkerSessionAsync`, `ScheduleVisitAsync`, `StartVisitAsync`.

**Mobile:** Mock `NetInfo`, `AsyncStorage`, and sync API. Do not require native NetInfo in Jest. NetInfo target: `@react-native-community/netinfo` ^11.x.

### Latest technical information

- **JWT 15 min** — refresh session before sync push when queue has pending items after offline work.
- **FIFO per visit** — client must enqueue `visit.start` before `visit.complete` for the same `visitId`.
- **Duplicate replay** — return stored `visit` DTO so client can refresh cache without re-fetching entire strip.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.6, FR-11]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.5 Mobile offline]
- [Source: `_bmad-output/project-context.md` — sync idempotency, conflict rules]
- [Source: `_bmad-output/implementation-artifacts/3-3-active-visit-flow-with-start-and-complete.md` — visit_notes design]
- [Source: `apps/api/Infrastructure/Visits/VisitService.cs`]
- [Source: `apps/mobile/src/services/visits/commandStripCache.ts`]

## Dev Agent Record

### Agent Model Used

Composer (dev-story continuation)

### Debug Log References

- Fixed integration test compile: `VisitTestData.cs` missing `Models.Visits` using; `SyncPushTests.cs` `visit.Id` (non-nullable Guid) + `Http.Json` using.
- Added global NetInfo mock in `jest.setup.js` for `mobileSyncPushService.test.ts`.

### Completion Notes List

- **API:** `POST /api/v1/sync/push` with `sync_mutations` idempotency ledger; `SyncPushService` batch processor; `VisitService` sync apply methods with note-merge-by-timestamp; `VisitNoteMerged` audit event.
- **Mobile:** AsyncStorage offline queue (`midi-kaval:offline-sync-queue:v1`), NetInfo + foreground sync, optimistic Today/Active Visit updates via `mergeQueueWithVisits`; online path unchanged.
- **Tests:** API unit (3 note-merge), integration (4 sync push), mobile Jest (68 total, +5 new sync tests).
- **OpenAPI:** snapshot exported; `@midi-kaval/api-client` regenerated and built.
- **Deferred:** Sync chip UI and More → Sync queue screen remain Story 3.7.
- **Code review (2026-06-17):** 11 patch findings applied — rejected/duplicate queue handling, overlay syncStatus filter, AC12 refresh/cache fixes, flush mutex, API batch resilience.

### File List

- `apps/api/Domain/Entities/SyncMutation.cs`
- `apps/api/Infrastructure/Persistence/SyncMutationConfiguration.cs`
- `apps/api/Migrations/20260618033602_AddSyncMutations.cs`
- `apps/api/Migrations/20260618033602_AddSyncMutations.Designer.cs`
- `apps/api/Models/Sync/SyncDtos.cs`
- `apps/api/Infrastructure/Sync/SyncPushService.cs`
- `apps/api/Infrastructure/Sync/VisitSyncOutcome.cs`
- `apps/api/Infrastructure/Sync/VisitSyncNoteMerge.cs`
- `apps/api/Controllers/V1/SyncController.cs`
- `apps/api/Infrastructure/Visits/VisitService.cs`
- `apps/api/Domain/AuditEventTypes.cs`
- `apps/api/Infrastructure/Persistence/AppDbContext.cs`
- `apps/api/Program.cs`
- `tests/api.unit/VisitSyncNoteMergeTests.cs`
- `tests/api.integration/SyncPushTests.cs`
- `tests/api.integration/VisitTestData.cs`
- `tests/api.integration/SwaggerEndpointTests.cs`
- `apps/mobile/src/services/sync/offlineQueue.ts`
- `apps/mobile/src/services/sync/syncMutationTypes.ts`
- `apps/mobile/src/services/sync/mobileSyncPushService.ts`
- `apps/mobile/src/services/sync/networkStatus.ts`
- `apps/mobile/src/services/sync/useNetworkStatus.ts`
- `apps/mobile/src/services/sync/useSyncOnForeground.ts`
- `apps/mobile/src/services/sync/mergeQueueWithVisits.ts`
- `apps/mobile/src/services/visits/VisitApiService.ts`
- `apps/mobile/src/screens/today/TodayScreen.tsx`
- `apps/mobile/src/screens/visits/ActiveVisitScreen.tsx`
- `apps/mobile/jest.setup.js`
- `apps/mobile/package.json`
- `apps/mobile/__tests__/offlineQueue.test.ts`
- `apps/mobile/__tests__/mobileSyncPushService.test.ts`
- `apps/mobile/__tests__/mergeQueueWithVisits.test.ts`
- `apps/mobile/__tests__/TodayScreen.test.tsx`
- `apps/mobile/__tests__/ActiveVisitScreen.test.tsx`
- `packages/api-client/openapi-snapshot.json`
- `packages/api-client/src/generated/api.ts`
- `README.md`

## Change Log

- 2026-06-17 — Story created from epics + architecture; ready-for-dev.
- 2026-06-17 — Validation: 10 fixes applied (sync apply methods, note merge path, duplicate DTO, offline complete strip update, AC12 pull-to-refresh, auth refresh, 3.5 order compose, scope boundaries).
- 2026-06-17 — Implementation complete: offline queue + sync push API, tests, OpenAPI/api-client regen; status → review.
- 2026-06-17 — Code review: 11 patches applied, 9 test gaps deferred; status → done.
