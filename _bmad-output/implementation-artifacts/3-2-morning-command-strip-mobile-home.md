---
baseline_commit: NO_VCS
---

# Story 3.2: Morning Command Strip Mobile Home

Status: done

## Story

As a **Social Worker** (or **Case Worker**),
I want the **Today** tab as my home visit queue,
so that I start the day from time-ordered visits without a dashboard (FR-12, UX-DR2, UX-DR10).

## Acceptance Criteria

1. **Given** I am authenticated as **SocialWorker** or **CaseWorker** (`auth.isFieldRole === true`)  
   **When** I open the **Today** tab (default first tab after login per Story 1.7)  
   **Then** the screen loads visit rows from **`GET /api/v1/visits/today`** only — **never** from `GET /api/v1/cases/assigned`  
   **And** response envelope is `{ data: VisitListResultDto, meta: { requestId, totalCount } }`  
   **And** rows render in API order (`scheduledAtUtc` ascending; server already filters today's scheduled/in-progress visits)

2. **Given** a visit row on the Command Strip  
   **When** rendered  
   **Then** each card shows (matching `mockups/command-strip-today.html` semantics):
   - **Crime/ST headline** — `{case.crimeNumber} · {case.stNumber}` (tabular nums, `fontWeight: '600'`)
   - **Meta line** — `Visit {n} · {case.domicile}` where `{n}` is 1-based index in today's list (not `case.visitCount`)
   - **Overdue styling** when `visit.isOverdue === true` — left border `#B42318` (4px), same as mockup `.card.overdue`
   - **Handoff whisper block** when `visit.handoffWhisper` is non-null — reuse visual pattern from `CaseDetailPlaceholderScreen` (`#EFF8FF` bg, `#175CD3` left border); text: `Handoff: {priorActions}` or combined one-liner from whisper fields
   - **Sync chip** (top-right) — static label **`Synced`** for rows loaded successfully from API in this story (full local/pending/error states deferred to Story 3.7)
   - **Actions row** — **Navigate** (secondary) and **Start visit** (primary `#0D6E6E`) buttons per card

3. **Given** the mockup court banner (`⚠ Court sitting Thursday — 2 days`)  
   **When** Today loads  
   **Then** a `CourtCountdownBanner` component exists matching mockup styles (`#FFFAEB` background, `#B54708` left border, 13px text)  
   **And** banner is **hidden when no upcoming court data** (no court API until Epic 5 — do **not** show fake court text in production UI)  
   **And** a **component test** renders the banner with fixture props to lock visual semantics for Story 5.2 integration

4. **Given** I pull down on the Today scroll view  
   **When** refresh completes  
   **Then** `GET /api/v1/visits/today` is called again (UX-DR10 pull-to-refresh on Today tab)  
   **And** loading indicator shows during refresh without clearing cached rows (see AC5)

5. **Given** cold open (app relaunch) with a previously cached strip  
   **When** Today mounts  
   **Then** last successful visit list is shown immediately from **AsyncStorage** cache  
   **And** a background fetch updates the list when network succeeds  
   **And** when **no cache** and **initial fetch in flight**, show **skeleton placeholders** (2–3 card-shaped gray blocks, not a full-screen spinner only)  
   **And** when cache `fetchedAtUtc` is **>24 hours** old and network fetch has not yet succeeded, show soft stale banner: `"Showing saved visits — pull to refresh"` (EXPERIENCE.md cold-open state)

6. **Given** `GET /api/v1/visits/today` fails (network/401/5xx)  
   **When** cached data exists  
   **Then** cached rows remain visible and an error message appears above the list with retry affordance  
   **When** no cache exists  
   **Then** show error message + retry (no blank screen)

7. **Given** I tap **Start visit** on a card  
   **When** Story 3.3 is not implemented  
   **Then** show non-blocking feedback (`Alert` or inline toast pattern): `"Visit start coming in the next update"` — button must remain tappable and accessible (do not disable; Story 3.3 wires `POST /visits/{id}/start`)

8. **Given** I tap **Navigate** on a card  
   **When** Story 3.4 is not implemented  
   **Then** show non-blocking feedback: `"Navigation opens after GPS setup"` — Story 3.4 wires Google Maps + landmark gate

9. **Given** I tap the crime/ST headline area (card body, not action buttons)  
   **When** `visit.case.id` is set  
   **Then** navigate to **Cases → Case detail** via nested tab navigation:  
   `navigation.navigate('Cases', { screen: 'CaseDetailPlaceholder', params: { caseId } })`  
   **And** update `MainTabParamList` / navigation types to support nested `Cases` stack params

10. **Given** the Today header in mockup (title + notification bell)  
    **When** rendered  
    **Then** screen title **Today** (`22px`, `#101828`) is shown  
    **And** notification bell is **omitted** in this story (Epic 7 in-app bell — no dead icon)

11. **Given** visit list is empty after successful fetch  
    **When** Today renders  
    **Then** show empty state: `"No visits scheduled for today"` with subtitle `"Pull to refresh when your coordinator schedules visits"` (UX-DR13 empty-state pattern)

12. **Given** mobile test baseline after Story 3.1  
    **When** this story ships  
    **Then** new Jest tests in `apps/mobile/__tests__/` cover at minimum:
    - Today strip renders visit rows from mocked `VisitApiService.listToday`
    - Overdue card styling when `isOverdue: true`
    - Handoff whisper block when `handoffWhisper` present
    - Pull-to-refresh triggers second API call
    - Cold-open cache: cached items shown before fetch resolves
    - `CourtCountdownBanner` renders fixture text when props provided
    **And** existing mobile tests continue to pass (`npm run test:mobile`)  
    **And** **no API changes** in this story (mobile-only; consume existing 3.1 endpoints)

## Tasks / Subtasks

- [x] **Visit API service** (AC: 1, 4, 6)
  - [x] `src/services/visits/visit.models.ts` — re-export `VisitListItemDto`, `VisitListResultDto`, `HandoffWhisperDto` from `@midi-kaval/api-client`
  - [x] `src/services/visits/VisitApiService.ts` — mirror `CaseApiService`: `listToday()` → `GET /api/v1/visits/today`, `VisitApiError`, `extractErrorMessage`, singleton `visitApiService`
  - [x] Parse envelope: `auth.getApi<VisitListResultDto>('/api/v1/visits/today')` returns `{ data, meta }` — use `data.items`

- [x] **Command Strip cache** (AC: 5, 6)
  - [x] `src/services/visits/commandStripCache.ts` — AsyncStorage key `@midi-kaval/command-strip/v1`
  - [x] Store `{ items: VisitListItemDto[], fetchedAtUtc: string }`
  - [x] `readCache()`, `writeCache(items)`, `isStale(fetchedAtUtc)` (>24h)

- [x] **UI components** (AC: 2, 3, 10)
  - [x] `src/components/CommandStripCard.tsx` — props: `visit`, `visitIndex`, `syncLabel`, `onNavigate`, `onStartVisit`, `onOpenCase`
  - [x] `src/components/CourtCountdownBanner.tsx` — props: `{ label: string } | null`; return null when no label
  - [x] `src/components/CommandStripSkeleton.tsx` — 2–3 placeholder cards

- [x] **Today screen** (AC: 1–11)
  - [x] Replace placeholder in `src/screens/today/TodayScreen.tsx`
  - [x] State: `items`, `loading`, `refreshing`, `errorMessage`, `cacheStale`
  - [x] Mount: read cache → set items → fetch → write cache; mirror `CasesListScreen` refresh pattern
  - [x] `ScrollView` + `RefreshControl` (always for field role)
  - [x] Wire action stubs (AC 7, 8) and case navigation (AC 9)

- [x] **Navigation types** (AC: 9)
  - [x] Update `src/navigation/types.ts` — `Cases: NavigatorScreenParams<CasesStackParamList> | undefined`
  - [x] Import `NavigatorScreenParams` from `@react-navigation/native`
  - [x] Type `TodayScreen` navigation prop if using `useNavigation` for cross-tab navigate

- [x] **Tests** (AC: 12)
  - [x] `__tests__/TodayScreen.test.tsx` — mock `visitApiService`, `commandStripCache`
  - [x] `__tests__/CommandStripCard.test.tsx` — overdue border, whisper, actions
  - [x] `__tests__/CourtCountdownBanner.test.tsx` — fixture banner text
  - [x] Follow `CasesListScreen.test.tsx` pattern (`react-test-renderer`, mock `useAuth`)

## Dev Notes

### Epic 3 context and scope boundaries

| In scope (3.2) | Deferred |
|----------------|----------|
| Today tab Command Strip UI from `GET /visits/today` | `POST /visits/{id}/start` (Story 3.3) |
| Pull-to-refresh, cold-open cache + skeleton | Offline queue / sync push (3.6) |
| Overdue card styling via `isOverdue` | Full sync chip states (3.7) |
| Handoff whisper on list cards | GPS / Navigate / Maps (3.4) |
| Court banner component (hidden until Epic 5 data) | Live court countdown API (5.1 / 5.2) |
| Start/Navigate button stubs with user feedback | Proximity grouping (3.5) |
| Navigate to case detail from card tap | POCSO discreet header (3.8) |
| Static "Synced" chip for online loads | Notification bell (Epic 7) |

**Critical:** Do **not** build today's queue from `caseApiService.listAssignedCases()` or client-side filtering of assigned cases. `project-context.md` and Story 3.1 explicitly forbid client-composed Command Strip.

### `isOverdue` semantics (from Story 3.1)

- Use **`visit.isOverdue`** from API — do not recompute from `case.nextVisitDueAtUtc` or local clock on the client
- A visit scheduled earlier today may have `isOverdue: true` while still appearing in today's list — intentional for Command Strip styling

### Existing code to reuse (do not reinvent)

| Artifact | Reuse |
|----------|-------|
| `CasesListScreen.tsx` | `useCallback` load pattern, `RefreshControl`, loading/refresh/error state, field-role gate |
| `CaseDetailPlaceholderScreen.tsx` | Handoff whisper `StyleSheet` colors/layout |
| `CaseApiService.ts` | Service singleton pattern, `wrapError`, envelope parsing via `AuthSessionService.getApi` |
| `AuthSessionService.ts` | `getApi<T>`, token refresh, `extractErrorMessage` |
| `@midi-kaval/api-client` | Types only — never hand-edit generated files |
| `command-strip-today.html` | Visual reference for card layout, colors, court banner |

### Visit row content mapping

```typescript
// VisitListItemDto (api-client)
{
  id, scheduledAtUtc, status, isOverdue,
  handoffWhisper?: { priorActions, openItems, nextVisitPurpose, transferredAtUtc },
  case?: { crimeNumber, stNumber, domicile, visitCount, ... }
}
```

**Meta line pilot:** `Visit {index+1} · {case.domicile}` — omit GPS/km/distance until Stories 3.4–3.5.

**Optional time display:** Format `scheduledAtUtc` as local time in meta or subtitle (e.g. `09:00`) — not required if mockup order is sufficient; list is already time-ordered.

### Court countdown (pilot)

- **No court-sitting API** exists (`apps/api` has zero court entities — Epic 5.1)
- Ship `CourtCountdownBanner` component + test with fixture `"Court sitting Thursday — 2 days"`
- `TodayScreen` passes `courtCountdown={null}` → banner not rendered
- Story 5.2 will add `useCourtCountdown()` hook calling upcoming sittings API and pass label to banner

### Navigation: Today → Case detail

`TodayScreen` lives outside `CasesStackNavigator`. Cross-tab navigation requires typed nested params:

```typescript
// types.ts
import { NavigatorScreenParams } from '@react-navigation/native';

export type MainTabParamList = {
  Today: undefined;
  Cases: NavigatorScreenParams<CasesStackParamList> | undefined;
  More: undefined;
};

// TodayScreen
navigation.navigate('Cases', {
  screen: 'CaseDetailPlaceholder',
  params: { caseId: visit.case!.id! },
});
```

Use `useNavigation<BottomTabNavigationProp<MainTabParamList>>()` or composite typing as needed.

### Cold-open cache flow

```
mount → read AsyncStorage cache → if items: setItems + show immediately
      → setLoading(true) only if no cache
      → fetch listToday()
      → on success: setItems, writeCache, clear error
      → on failure: keep cache, set errorMessage
      → if cache age > 24h: setCacheStale(true) until fresh fetch succeeds
```

### API contract (already implemented — Story 3.1)

**`GET /api/v1/visits/today`** (FieldWorker)

```json
{
  "data": {
    "items": [
      {
        "id": "uuid",
        "scheduledAtUtc": "2026-06-16T09:00:00Z",
        "status": "Scheduled",
        "isOverdue": false,
        "handoffWhisper": null,
        "case": { "crimeNumber": "...", "stNumber": "...", "domicile": "Urban", ... }
      }
    ],
    "page": 1,
    "pageSize": 1
  },
  "meta": { "requestId": "...", "totalCount": 1 }
}
```

**Dev seed path:** Coordinator `POST /api/v1/cases/{id}/visits` after transfer (see Story 3.1 README section).

### Files to create / update

```
apps/mobile/
├── src/services/visits/
│   ├── visit.models.ts                    # NEW
│   ├── VisitApiService.ts                 # NEW
│   └── commandStripCache.ts               # NEW
├── src/components/
│   ├── CommandStripCard.tsx               # NEW
│   ├── CourtCountdownBanner.tsx           # NEW
│   └── CommandStripSkeleton.tsx           # NEW
├── src/screens/today/TodayScreen.tsx      # UPDATE — replace placeholder
├── src/navigation/types.ts                # UPDATE — nested Cases params
└── __tests__/
    ├── TodayScreen.test.tsx               # NEW
    ├── CommandStripCard.test.tsx          # NEW
    └── CourtCountdownBanner.test.tsx      # NEW
```

**Do not modify:** `apps/api/**`, `packages/api-client/**` (unless OpenAPI drift — should not be needed).

### Styling notes

- Co-located `StyleSheet.create` per screen/component (existing mobile convention — no shared theme module yet)
- Align with mockup / `DESIGN.md` tokens:
  - Primary `#0D6E6E`, page `#F8FAFC`, title `#101828`, meta `#475467`
  - Card border `#EAECF0`, radius 12px (`rounded/lg`)
  - Error/overdue `#B42318`, sync chip pilot `#F4F3FF` / `#5925DC`
- `project-context.md` says avoid hardcoded hex long-term — match existing `CasesListScreen` / `CaseDetailPlaceholderScreen` pilot pattern for this story

### Regression traps

- **Do not** remove or break `CasesListScreen` assigned-cases flow (separate tab, separate API)
- **Do not** call `/visits/weekly` or `/visits/overdue` on Today tab in 3.2 — today list only; overdue styling comes from `isOverdue` on today items
- **Do not** implement `POST /visits/{id}/complete` from mobile in this story
- **Do not** add notification bell placeholder that looks broken
- Field workers still land on Today tab via `roleRouting.ts` — no change needed unless tests require it
- Run `npm run test:mobile` from repo root before marking review

### Testing standards

- Jest + `react-test-renderer` (existing pattern — no `@testing-library/react-native` in repo)
- Mock `useAuth` with `isFieldRole: true` for strip tests
- Mock `visitApiService.listToday` and `commandStripCache.readCache/writeCache`
- Per project policy: author and run mobile unit tests; full monorepo suite may be deferred to release hardening

### Previous story intelligence (3.1)

- Visit lists return `VisitListItemDto` with nested `CaseSummaryDto` + optional `handoffWhisper`
- `isOverdue` on DTO is authoritative — today+overdue overlap is server-side intentional
- Terminal cases excluded from visit lists server-side
- API integration tests: 20 tests in `VisitSchedulerTests.cs` — mobile consumes same contract
- Code review deferred: transfer/visit assignee drift, N+1 handoff on lists (acceptable pilot)

### Previous story intelligence (2.8 mobile)

- `CasesListScreen` established pull-to-refresh on Cases tab — mirror for Today
- `CaseDetailPlaceholderScreen` handoff whisper styling is the reference for list-card whisper
- Mobile test baseline was **24** tests after 2.8 — extend, do not break

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.2, FR-12, UX-DR2, UX-DR10]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/command-strip-today.html`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — command-strip-card]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — cold open, Flow 1]
- [Source: `_bmad-output/project-context.md` — Today = GET /visits/today, no client-composed queues]
- [Source: `_bmad-output/implementation-artifacts/3-1-visit-scheduler-api.md` — API contract, isOverdue overlap]
- [Source: `apps/mobile/src/screens/cases/CasesListScreen.tsx` — list/refresh pattern]
- [Source: `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx` — handoff whisper]
- [Source: `apps/mobile/src/services/cases/CaseApiService.ts` — service pattern]
- [Source: `packages/api-client/src/generated/api.ts` — VisitListItemDto schema]

## Dev Agent Record

### Agent Model Used

Auto (Cursor)

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created
- Implemented Command Strip on Today tab: `VisitApiService`, AsyncStorage cache, card/banner/skeleton components
- Pull-to-refresh, cold-open cache, stale banner, empty/error states, Start/Navigate stubs, case detail navigation
- Code review: hid duplicate Today tab header; added empty-state, error-with-cache, and case-navigation tests (36 mobile tests passing)

### File List

- apps/mobile/src/services/visits/visit.models.ts
- apps/mobile/src/services/visits/VisitApiService.ts
- apps/mobile/src/services/visits/commandStripCache.ts
- apps/mobile/src/components/CommandStripCard.tsx
- apps/mobile/src/components/CourtCountdownBanner.tsx
- apps/mobile/src/components/CommandStripSkeleton.tsx
- apps/mobile/src/screens/today/TodayScreen.tsx
- apps/mobile/src/navigation/types.ts
- apps/mobile/src/navigation/MainTabNavigator.tsx
- apps/mobile/__tests__/TodayScreen.test.tsx
- apps/mobile/__tests__/CommandStripCard.test.tsx
- apps/mobile/__tests__/CourtCountdownBanner.test.tsx

### Review Findings

- [x] [Review][Patch] Hide duplicate Today tab header — `MainTabNavigator` shows native `headerShown: true` while `TodayScreen` also renders a 22px "Today" title (mockup has single header; Cases tab already uses `headerShown: false`) [apps/mobile/src/navigation/MainTabNavigator.tsx:17]
- [x] [Review][Patch] Add empty-state integration test — AC11 requires `"No visits scheduled for today"` when fetch returns empty list [apps/mobile/__tests__/TodayScreen.test.tsx]
- [x] [Review][Patch] Add error-with-cache integration test — AC6 requires cached rows remain visible with error + retry when `listToday` fails [apps/mobile/__tests__/TodayScreen.test.tsx]
- [x] [Review][Patch] Add case-navigation integration test — AC9 nested `Cases → CaseDetailPlaceholder` navigate on card open [apps/mobile/__tests__/TodayScreen.test.tsx]
- [x] [Review][Defer] Cached rows show static "Synced" chip before fresh fetch — misleading vs Story 3.7 sync states but matches pilot AC2 for API-success path [apps/mobile/src/screens/today/TodayScreen.tsx:145] — deferred to Story 3.7
- [x] [Review][Defer] No automated test for stale banner or skeleton UI — AC5 covered manually; AC12 minimum matrix satisfied [apps/mobile/src/screens/today/TodayScreen.tsx:120] — deferred, optional hardening