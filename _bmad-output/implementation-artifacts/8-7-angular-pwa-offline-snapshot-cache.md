---
baseline_commit: e92f5344
---

# Story 8.7: Angular PWA Offline Snapshot Cache

Status: done

## Story

As a **Coordinator on patchy office Wi‑Fi**,
I want read-only Crisis Queue and dashboard snapshots when offline,
So that I can still orient and triage without connectivity (NFR-12).

*Scope: **Web PWA only**. Bounded offline — Crisis Queue and Dashboard snapshots only. Mutations (create case, approve claim, start export) require network. The app shell is already prefetched via `ngsw-config.json` (Story 1.6). No changes to the API or backend.*

## Acceptance Criteria

1. **Given** the PWA is installed and the service worker is active (already configured via `provideServiceWorker` in `app.config.ts`)
   **When** the app loads and successfully fetches Crisis Queue or Dashboard data
   **Then** the response is cached locally in IndexedDB (via `@angular/service-worker` or a custom cache service)
   **And** the cache is keyed by endpoint URL for later retrieval when offline

2. **Given** the network is unavailable or an API call fails (status 0 / network error)
   **When** navigating to Crisis Queue or Dashboard
   **Then** the last cached snapshot is displayed from IndexedDB
   **And** a prominent stale-data banner is shown at the top: "You're offline — showing data from {timestamp}. Some features may be unavailable."
   **And** the stale banner uses the `info` severity color from DESIGN.md

3. **Given** existing cached data exists
   **When** the network is restored and a fresh API call succeeds
   **Then** the cache is updated with the new response
   **And** the stale banner disappears automatically

4. **Given** no cached data exists and the user is offline
   **When** navigating to Crisis Queue or Dashboard
   **Then** the existing loading state persists
   **And** the error message shows "Unable to load data — check your connection" with retry button (existing error state pattern is preserved)

5. **Given** the user attempts a mutation (create case, approve claim, start export, submit report)
   **When** the network is unavailable
   **Then** the API service returns a network error naturally (existing `extractErrorMessage` handles "Network error — check your connection")
   **And** no offline write capability is added — mutations always require connectivity

6. **Given** the ngsw-config.json exists (already configured)
   **When** the service worker activates
   **Then** the existing `app-shell` asset group with `prefetch` mode continues to provide fast repeat loads
   **And** a new `dataGroups` entry is added for the Crisis Queue and Dashboard API endpoints with `freshness` strategy and 30-second TTL

## Tasks / Subtasks

- [x] **Add API data groups to `ngsw-config.json`**
  - [x] Add `dataGroups` array with entries for:
    - `GET /api/v1/supervisor/crisis-queue` — `freshness` strategy, 30s TTL
    - `GET /api/v1/supervisor/dashboard` — `freshness` strategy, 30s TTL
  - These enable the service worker to serve cached responses when offline and check freshness when online

- [x] **Create `OnlineStateService`** — `src/app/core/services/online-state.service.ts`
  - `@Injectable({ providedIn: 'root' })` following existing service patterns
  - Signal-based: `readonly isOnline = signal(true)`
  - Listen to `window.addEventListener('online'/'offline')` events
  - Provide `lastOnlineChange` timestamp for the stale banner
  - Clean up listeners in `ngOnDestroy`

- [x] **Create `OfflineCacheService`** — `src/app/core/services/offline-cache.service.ts`
  - `@Injectable({ providedIn: 'root' })`
  - Uses localStorage for simple key-value caching with JSON serialization
  - Key-value pattern: `cacheKey = 'crisis-queue' | 'dashboard'`
  - `set<T>(key: string, data: T): void` — stores data + timestamp
  - `get<T>(key: string): CacheEntry<T> | null` — retrieves cached entry
  - `remove(key: string): void` — removes cache entry
  - Cache entries prefixed with `kaval_offline_` to avoid collisions

- [x] **Create `StaleBannerComponent`** — `src/app/shared/stale-banner/stale-banner.component.ts`
  - Standalone component with `@Input() staleTimestamp: Date | null`
  - Renders an info-colored banner: "You're offline — showing data from {time}. Some features may be unavailable."
  - Uses inline styles with info severity colors (blue #eef4ff/#c7d7fe/#175cd3)
  - Uses Angular Material `MatIconModule` for the info icon
  - Accessible: `role="status"` with `aria-live="polite"`

- [x] **Update `CrisisQueuePageComponent`** — `src/app/features/shell/pages/crisis-queue-page.component.ts`
  - Inject `OnlineStateService` and `OfflineCacheService`
  - On successful `load()`: cache items via `offlineCache.set('crisis-queue', data)`
  - On network error in `load()`: try `offlineCache.get('crisis-queue')`:
    - If cached data exists, display it with stale banner and timestamp
    - If no cached data, show existing error state with extracted error message
  - `autoRefresh()` also caches on success and falls back to stale cache on error
  - Added `staleTimestamp` signal derived from cache timestamp
  - Added `StaleBannerComponent` to template imports and template
  - **Existing states preserved** (loading skeleton, error state, empty state, data list)

- [x] **Update `DashboardPageComponent`** — `src/app/features/shell/pages/dashboard-page.component.ts`
  - Identical pattern to Crisis Queue changes above
  - Cache key: `'dashboard'`
  - Stale banner shown when displaying cached dashboard snapshot
  - Existing states (loading, error, empty, data) preserved

- [x] **Verify mutations still require connectivity**
  - Confirmed all existing API services (`CaseApiService`, `TravelClaimApiService`, `ReportsApiService`, `AttachmentApiService`, `CrisisQueueApiService`) already handle network errors via `extractErrorMessage` returning "Network error — check your connection"
  - No changes needed to mutation endpoints — they naturally fail when offline

## Dev Notes

### File locations

| File | Purpose | Action |
|------|---------|--------|
| `src/app/core/services/online-state.service.ts` | Network state tracking | New |
| `src/app/core/services/offline-cache.service.ts` | IndexedDB/localStorage cache | New |
| `src/app/shared/stale-banner/stale-banner.component.ts` | Stale data banner | New |
| `src/app/features/shell/pages/crisis-queue-page.component.ts` | Offline snapshot for Crisis Queue | Update |
| `src/app/features/shell/pages/dashboard-page.component.ts` | Offline snapshot for Dashboard | Update |
| `ngsw-config.json` | Service worker data groups | Update |

### Existing infrastructure (leverage)

- **`@angular/service-worker`**: Already registered in `app.config.ts` via `provideServiceWorker('ngsw-worker.js')` with `registerWhenStable:30000`
- **`ngsw-config.json`**: Already has `assetGroups` with `app-shell` prefetch — just need to add `dataGroups`
- **`OnlineStateService`** approach: Angular has no built-in online/offline signal; create a lightweight wrapper around `window.addEventListener`
- **Error handling**: All API services already have `extractErrorMessage` returning "Network error — check your connection" for `status === 0`
- **Existing patterns**: Crisis Queue and Dashboard components already handle `loading`, `errorMessage`, `jobs`/`items` signals with skeletons and error states
- **Signals**: Both pages use `signal()` and `computed()` — injecting cache behavior via `effect()` or inline in `load()` is natural

### Existing ngsw-config.json

```json
{
  "$schema": "./node_modules/@angular/service-worker/config/schema.json",
  "index": "/index.html",
  "assetGroups": [
    {
      "name": "app-shell",
      "installMode": "prefetch",
      "resources": {
        "files": ["/favicon.ico", "/index.html", "/*.css", "/*.js"]
      }
    }
  ]
}
```

### Crisis Queue and Dashboard API endpoints

- `GET /api/v1/supervisor/crisis-queue` — returns `CrisisQueueItemDto[]`
- `GET /api/v1/supervisor/dashboard` — returns `DashboardResultDto`

Both are consumed via `DashboardApiService` (for dashboard) and `CrisisQueueApiService` (for crisis queue).

### Stale banner copy

"You're offline — showing data from {time}. Some features may be unavailable."

Where `{time}` is formatted using the existing `formatTimestamp` pattern (e.g., "Jun 20, 2026, 10:30 AM").

### Critical don'ts

- **Do NOT** modify API files — this story is web PWA only
- **Do NOT** add offline write capability — mutations always require network (NFR-12)
- **Do NOT** modify `app.config.ts` — service worker is already registered
- **Do NOT** modify `app.routes.ts` or `supervisor-shell.component.ts` — no route/nav changes
- **Do NOT** use `localStorage` for complex objects without JSON serialization — use IndexedDB wrapper or structured `localStorage`
- **Do NOT** cache report export data, case search results, or admin data — only Crisis Queue and Dashboard snapshots per NFR-12
- **Do NOT** show worker rankings or performance scores (NFR-11)

### Regressions to prevent

- Existing Crisis Queue error state with retry button must still work when offline and no cache exists
- Existing Dashboard loading skeleton must still appear on initial load
- Page transitions must not be blocked by cache writes (fire-and-forget)
- The stale banner must not interfere with existing `role="alert"` error messages
- Service worker update detection must still work (existing Angular handling)

### Polling interaction

Crisis Queue auto-refreshes every 30s. When offline:
- `setInterval` fires but the API call fails (status 0 network error)
- The existing error handling in `refresh()` catches the error
- The offline cache service serves the last good data
- The stale banner is shown

When online is restored:
- The next interval tick succeeds
- Fresh data replaces cached data
- The stale banner disappears

This works without changes to the polling logic itself — just the fallback path.

## References

- Epic 8: NFR-12 → `_bmad-output/planning-artifacts/epics.md`
- Architecture Web PWA §5.4 → `_bmad-output/planning-artifacts/architecture.md` (offline scope table)
- Existing service worker config: `apps/web/ngsw-config.json`
- Existing app config: `apps/web/src/app/app.config.ts`
- Crisis Queue page: `apps/web/src/app/features/shell/pages/crisis-queue-page.component.ts`
- Dashboard page: `apps/web/src/app/features/shell/pages/dashboard-page.component.ts`
- Dashboard API service: `apps/web/src/app/features/shell/services/dashboard-api.service.ts`

## Previous Story Intelligence (8.6 — Reports Web UI with Export Progress)

### Key learnings

- All API services follow the same pattern: `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `firstValueFrom`, custom error class
- Error handling for network failures (`status === 0`) returns "Network error — check your connection"
- Services do not throw on network errors; they wrap them in typed error classes (`ReportsApiError`, `DashboardApiError`, etc.)
- The `extractErrorMessage` pattern is consistent across all services
- Standalone components with `inject()` for DI, signals for state
- All pages use `loading()`, `errorMessage()`, and data signals with `@if/@else` control flow templates

### Files created in Story 8.6

- `apps/web/src/app/features/shell/services/reports-api.service.ts`
- `apps/web/src/app/features/shell/reports.models.ts`
- `apps/web/src/app/features/shell/pages/reports-page.component.html`
- `apps/web/src/app/features/shell/pages/reports-page.component.scss`
- `apps/web/src/app/features/shell/pages/reports-page.component.spec.ts`
- `apps/web/src/app/features/shell/pages/reports-page.component.ts` (modified)

### Review outcomes (patch findings)

All 10 patch findings from the 8.6 code review were applied, including fixing datepicker handling, polling status transitions, and test mocks. Key patterns relevant to 8.7:
- Components handle errors in `catch` blocks and surface via `errorMessage` signals
- Polling uses `setInterval` with cleanup in `OnDestroy`
- Network errors return status 0 which maps to "Network error — check your connection"

## Dev Agent Record

### Implementation Plan

1. **Updated `ngsw-config.json`**: Added `dataGroups` array with `freshness` strategy entries for Crisis Queue and Dashboard API endpoints (30s TTL, max 1 cached response each)
2. **Created `online-state.service.ts`**: Wraps `window.addEventListener('online'/'offline')` in an injectable service with `isOnline` signal and `lastOnlineChange` timestamp
3. **Created `offline-cache.service.ts`**: localStorage-backed key-value cache with `set<T>(key, data)`, `get<T>(key)`, and `remove(key)` methods. Stores data + ISO timestamp in a `CacheEntry<T>` envelope
4. **Created `stale-banner.component.ts`**: Standalone component with `@Input() staleTimestamp` — renders info-colored banner "You're offline — showing data from {time}. Some features may be unavailable."
5. **Updated `crisis-queue-page.component.ts`**: Injected `OnlineStateService` and `OfflineCacheService`. On success: caches data. On error: tries `tryServeFromCache()` which either serves cached data with a stale timestamp or shows the existing error state
6. **Updated `crisis-queue-page.component.html`**: Added `<app-stale-banner>` after the header
7. **Updated `dashboard-page.component.ts`**: Identical pattern to Crisis Queue — caches dashboard DTO on success, falls back to `tryServeFromCache()` on error
8. **Updated `dashboard-page.component.html`**: Added `<app-stale-banner>` after the header
9. **Verified mutations**: All mutation API services already handle network errors — no changes needed

### Key Technical Decisions

- **localStorage over IndexedDB**: Crisis Queue and Dashboard snapshot data is small (~50KB at most). localStorage provides simpler synchronous API, avoiding IndexedDB async complexity. Cache size is bounded (2 keys max) so storage quotas are not a concern.
- **Separate `tryServeFromCache` method**: Extracted cache fallback logic to a private method used by both `load()` and `autoRefresh()`, keeping the error handling consistent.
- **`StaleBannerComponent` inline styles**: Follows existing pattern of standalone components with co-located styles. Uses info severity colors (blue) matching DESIGN.md semantic tokens.
- **`ngsw-config.json` `freshness` strategy with 30s TTL**: The service worker checks freshness first (network), falling back to cache after 5s timeout. This matches the Crisis Queue's existing 30s auto-refresh interval.
- **`autoRefresh` fallback**: When auto-refresh fails while already showing stale data, it stays silent. Only falls back to cache when not already showing stale data.

### Completion Notes

- All 6 story tasks are complete
- No route, guard, or nav changes needed — purely a PWA caching enhancement
- Existing loading skeletons, error states with retry, empty states, and page structures are fully preserved
- Mutations naturally fail when offline — no write cache was added (per NFR-12)
- TypeScript compilation: zero errors in new/modified files (pre-existing errors in unrelated test files are unchanged)

## File List

### New files
- `apps/web/src/app/core/services/online-state.service.ts` — Network state tracking service
- `apps/web/src/app/core/services/offline-cache.service.ts` — localStorage offline cache service
- `apps/web/src/app/shared/stale-banner/stale-banner.component.ts` — Stale data banner component

### Modified files
- `apps/web/ngsw-config.json` — Added dataGroups for Crisis Queue and Dashboard API endpoints
- `apps/web/src/app/features/shell/pages/crisis-queue-page.component.ts` — Offline cache fallback + StaleBannerComponent import
- `apps/web/src/app/features/shell/pages/crisis-queue-page.component.html` — Added app-stale-banner element
- `apps/web/src/app/features/shell/pages/dashboard-page.component.ts` — Offline cache fallback + StaleBannerComponent import
- `apps/web/src/app/features/shell/pages/dashboard-page.component.html` — Added app-stale-banner element

### Change Log

- **2026-06-20**: All 4 patch findings from code review applied. Fixes: Dashboard `showingStale` computed + autoRefresh guard, CrisisQueue `load()` `refreshing()` guard, pass original error to `tryServeFromCache` in both components.

- **2026-06-20**: Story 8.7 implemented — Angular PWA offline snapshot cache for Crisis Queue and Dashboard.
  - Added `dataGroups` to `ngsw-config.json` for API endpoint freshness caching
  - Created `OnlineStateService` with `isOnline` signal and browser event listeners
  - Created `OfflineCacheService` with localStorage-backed key-value cache
  - Created `StaleBannerComponent` for "You're offline" info banner
  - Updated Crisis Queue and Dashboard pages to serve cached snapshots when offline
  - Verified mutation services require connectivity naturally (no changes needed)

### Review Findings

#### Patch (actionable fixes)

- [x] [Review][Patch] Dashboard `autoRefresh()` stale-banner guard never triggers after initial online load [`dashboard-page.component.ts:91`]
      Uses `if (!this.data() || this.data() === null)` which is false after a successful load. Should use `showingStale()` computed like CrisisQueue. This means the Dashboard silently shows stale data without the required stale-data banner when the connection drops after the initial page load (AC 2 violation).

- [x] [Review][Patch] Dashboard missing `showingStale` computed signal [`dashboard-page.component.ts`]
      Without this computed, the autoRefresh guard cannot correctly detect stale state. Add `readonly showingStale = computed(() => this.staleTimestamp() !== null)` — identical to CrisisQueue pattern.

- [x] [Review][Patch] CrisisQueue `load()` missing `refreshing()` guard [`crisis-queue-page.component.ts:63`]
      `load()` only guards on `loadingGuard`, not `refreshing()`. A Retry button click during auto-refresh can create concurrent API calls. Dashboard `load()` correctly guards with `|| this.refreshing()`.

- [x] [Review][Patch] Auto-refresh error loses original error context [`crisis-queue-page.component.ts:102`, `dashboard-page.component.ts:116`]
      `tryServeFromCache(undefined)` discards the original error. When cache is absent, shows generic "Unable to load data" instead of the actual API error (e.g., 401/503). Pass original error to fallback.

#### Deferred (real issues, not actionable now)

- [x] [Review][Defer] `OnlineStateService` injected but never called in either component
- [x] [Review][Defer] Invalid Date from unvalidated cache timestamp
- [x] [Review][Defer] SSR crash from `navigator`/`window` globals
- [x] [Review][Defer] SW freshness timeout creates false "just now" timestamps
- [x] [Review][Defer] Stale schema in localStorage after deployment
- [x] [Review][Defer] Corrupted localStorage entries silently invisible
- [x] [Review][Defer] CrisisQueue error hides existing data (error state replaces stale list)
- [x] [Review][Defer] localStorage quota exceeded silently dropped
- [x] [Review][Defer] `maxSize: 1` can cache error responses in SW
- [x] [Review][Defer] Inconsistent stale-handling patterns (CQ vs Dashboard)

#### Dismissed (noise / false positive)

- Cache timestamp semantics (cache-write time vs last-online-change time) — minor cosmetic, no practical impact
