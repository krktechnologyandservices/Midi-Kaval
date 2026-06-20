---
baseline_commit: NO_VCS
---

# Story 3.5: Proximity Visit Grouping Suggestion

Status: done

<!-- Validated: 2026-06-17 — see 3-5-proximity-visit-grouping-suggestion-validation-report.md (8 fixes applied) -->

## Story

As a **Social Worker** (or **Case Worker**),
I want same-day visits grouped by proximity with a suggested route I can adjust,
so that I travel efficiently through my field day (FR-10).

## Acceptance Criteria

1. **Given** the field worker visit API surface after Stories 3.1–3.4  
   **When** OpenAPI is updated (**no database migration** — reads existing case GPS columns from Story 3.4)  
   **Then** a new read-only endpoint exists: `GET /api/v1/visits/today/grouping-suggestion`  
   **And** it is authorized with `[Authorize(Policy = Policies.FieldWorker)]`  
   **And** response envelope is `{ data: VisitGroupingSuggestionDto, meta: { requestId } }`

2. **Given** I am the assigned field worker with today's visits from the same filter as `GET /visits/today` (`Scheduled`/`InProgress`, current UTC day)  
   **When** I call `GET /api/v1/visits/today/grouping-suggestion`  
   **Then** visits whose nested `case.gpsVerified === false` are **excluded** from clustering and returned in `excluded[]` with `{ visitId, reason: "gps_unverified" }`  
   **And** visits with `gpsVerified === true` but missing `latitude`/`longitude` are treated as excluded with reason `"gps_coordinates_missing"`  
   **And** eligible visits are clustered by GPS proximity using Haversine distance on case coordinates

3. **Given** at least **two** eligible visits with verified coordinates  
   **When** grouping runs  
   **Then** `clusters[]` contains one or more clusters; each cluster has `clusterIndex` (0-based), `visitIds[]`, and optional `centroidLatitude`/`centroidLongitude`  
   **And** `suggestedVisitOrder[]` is a flat list of visit UUIDs covering **all eligible visits exactly once** (nearest-neighbor route heuristic within and across clusters — see Dev Notes)  
   **And** `legs[]` provides per-visit `{ visitId, distanceKmFromPrevious }` aligned to `suggestedVisitOrder` (first leg uses `distanceKmFromPrevious: null`)

4. **Given** fewer than two eligible visits (0 or 1)  
   **When** I call grouping suggestion  
   **Then** response is **200 OK** with `clusters: []`, `suggestedVisitOrder: []`, `legs: []`, and `message: "At least two visits with verified GPS are required for route grouping"`  
   **And** `excluded[]` still lists unverified visits when present

5. **Given** three visits today: A and B within 1.5 km, C 8 km away; all GPS verified  
   **When** integration test calls grouping suggestion  
   **Then** A and B share one cluster; C is a separate single-visit cluster (or singleton cluster per algorithm)  
   **And** `suggestedVisitOrder` respects proximity (A→B before jumping to C, or B→A — either valid if distance-optimal)

6. **Given** I open the **Today** tab with ≥2 visits after a successful `listToday()`  
   **When** the screen renders  
   **Then** a **Group nearby visits** text button appears below the title (visible only when `items.length >= 2`)  
   **And** when any visit has `case.gpsVerified === false`, an inline dismissible banner appears: `"Some visits need landmark capture before they can be grouped"` (PRD FR-9 — warn **before** requesting route; amber styling per court banner)

7. **Given** I tap **Group nearby visits**  
   **When** the API request is in flight  
   **Then** the button shows **Grouping…** and is disabled (no double-submit)  
   **When** `GET /api/v1/visits/today/grouping-suggestion` succeeds  
   **Then** a bottom sheet/modal **Suggested route** opens  
   **And** when `suggestedVisitOrder` is non-empty, list rows in that order with crime/ST headline and `distanceKmFromPrevious` when present (format `{n} km` with one decimal, e.g. `1.2 km` per mockup)  
   **And** when `suggestedVisitOrder` is empty but `message` is set, show the message prominently and hide **Apply route** (Cancel only)  
   **And** excluded visits show a warning block: `"N visit(s) skipped — capture landmark before grouping"` listing crime/ST only (no beneficiary name beyond existing strip rules)

8. **Given** the Suggested route sheet with a non-empty `suggestedVisitOrder`  
   **When** I use **Move up** / **Move down** controls per row (`accessibilityLabel`: `"Move visit up"` / `"Move visit down"`)  
   **Then** I can manually reorder visits client-side without API call  
   **And** distance labels recalculate from previous row using case `latitude`/`longitude` on the matching `VisitListItemDto` and shared `visitGeo.ts` (sheet receives today's items keyed by `visitId`)

9. **Given** I tap **Apply route** on the sheet  
   **When** apply succeeds  
   **Then** Command Strip re-renders visits in my chosen order (custom order overrides API `scheduledAtUtc` sort for display only)  
   **And** `CommandStripCard` meta line for verified-GPS visits shows distance suffix when grouping is active: `Visit {n} · {domicile} · {distanceKm} km` (omit distance when null / first in route)  
   **And** unverified visits keep ` · GPS unverified` suffix (Story 3.4) and remain at the **end** of the strip after grouped visits, preserving their relative API order  
   **And** custom order is persisted in AsyncStorage alongside command strip cache (`customVisitOrder: string[] | null`) until pull-to-refresh clears it  
   **And** `routeGroupingActive: true` is stored in cache so distance meta suffixes apply after relaunch

10. **Given** cold open (Story 3.2) with cached command strip including `customVisitOrder`  
    **When** Today mounts  
    **Then** visits render immediately in cached custom order (before network fetch completes)  
    **And** distance meta suffixes apply when `routeGroupingActive === true` in cache

11. **Given** grouping is active and I pull-to-refresh Today  
   **When** fresh `listToday()` succeeds  
   **Then** custom visit order is cleared; strip returns to API `scheduledAtUtc` order  
   **And** `routeGroupingActive` is cleared with custom order  
   **And** user may request grouping again

12. **Given** grouping suggestion API fails (network/5xx)  
    **When** I tapped **Group nearby visits**  
    **Then** show inline error on Today (`visitApiService.extractErrorMessage`) with retry; strip order unchanged

13. **Given** Story 3.4 GPS gate and navigate flows  
    **When** this story ships  
    **Then** `useVisitNavigation`, start/complete visit, and landmark modal behavior are **unchanged**  
    **And** visit index `{n}` in meta line reflects **display position** after custom reorder (1-based)

14. **Given** integration and mobile test baselines after Story 3.4  
    **When** this story ships  
    **Then** new API integration tests cover at minimum:
    - two nearby verified visits → non-empty `clusters` + `suggestedVisitOrder` length 2
    - unverified visit excluded with `gps_unverified`
    - single eligible visit → empty clusters + message
    - wrong role (coordinator) → **403**
    **And** mobile Jest tests cover:
    - **Group nearby visits** hidden when `<2` visits
    - unverified-GPS pre-grouping banner when any visit unverified
    - successful grouping opens sheet with mocked suggestion
    - empty `suggestedVisitOrder` shows message without Apply button
    - Apply route reorders strip cards
    - cold-open restores custom order from cache
    - excluded warning when `excluded[]` non-empty
    - `CommandStripCard` shows `1.2 km` meta suffix when `distanceKm` prop set
    **And** `npm run test:mobile` and API integration suite pass  
    **And** export OpenAPI snapshot then regenerate api-client (Windows):
      `set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json`
      → run integration tests (or API) to write snapshot
      → `set API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json` (use **absolute path** on Windows per Story 3.4 learnings)
      → `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client`

## Tasks / Subtasks

- [x] **Domain — proximity helper** (AC: 3, 5)
  - [x] `Infrastructure/Visits/GeoDistance.cs` — static Haversine `DistanceKm(lat1, lon1, lat2, lon2)`; unit-testable pure functions
  - [x] `Infrastructure/Visits/VisitProximityGrouper.cs` — cluster + nearest-neighbor ordering (injectable for tests)

- [x] **API — DTOs** (AC: 1–4)
  - [x] `Models/Visits/VisitGroupingDtos.cs` — `VisitGroupingSuggestionDto`, `VisitGroupingClusterDto`, `VisitGroupingLegDto`, `VisitGroupingExcludedDto`

- [x] **Service — GetTodayGroupingSuggestionAsync** (AC: 2–5)
  - [x] `VisitService.GetTodayGroupingSuggestionAsync` — reuse today's visit query via existing `ListTodayAsync` internals (do **not** duplicate SQL filters)
  - [x] Partition eligible vs excluded; run grouper; build `legs` distances

- [x] **Controller** (AC: 1)
  - [x] `GET today/grouping-suggestion` on `VisitsController` — **must be registered before** `{id:guid}` routes if any conflict (place next to `ListToday`)
  - [x] XML doc comments for OpenAPI

- [x] **Tests — API integration** (AC: 14)
  - [x] Extend `VisitSchedulerTests.cs` or add `VisitGroupingTests.cs`
  - [x] Reuse `VisitTestData.ScheduleVisitAsync` + `CaseTestData.VerifyCaseGpsAsync` with coordinates ~1 km apart (Bangalore pilot coords from CaseGpsTests)

- [x] **API client + OpenAPI snapshot** (AC: 14)
  - [x] Regenerate snapshot + `@midi-kaval/api-client`

- [x] **Docs** (AC: 14)
  - [x] Update `README.md` visit scheduler table with grouping endpoint

- [x] **Mobile — API service** (AC: 7, 12)
  - [x] `VisitApiService.getTodayGroupingSuggestion()` → `GET /api/v1/visits/today/grouping-suggestion`
  - [x] Re-export DTO types from `@midi-kaval/api-client` in `visit.models.ts`

- [x] **Mobile — geo + order helpers** (AC: 8)
  - [x] `src/services/visits/visitGeo.ts` — Haversine (mirror API formula), `formatDistanceKm`
  - [x] `src/services/visits/visitDisplayOrder.ts` — apply/clear custom order; merge excluded tail

- [x] **Mobile — cache extension** (AC: 9–11)
  - [x] Extend `CommandStripCachePayload` with `customVisitOrder?: string[] | null` and `routeGroupingActive?: boolean`
  - [x] Change `writeCache(items, options?)` signature; update `TodayScreen` callers (currently two `writeCache` sites)
  - [x] `readCache()` restores custom order + grouping flag on cold open
  - [x] Clear custom order and `routeGroupingActive` on successful refresh fetch

- [x] **Mobile — UI** (AC: 6–13)
  - [x] `UnverifiedGpsGroupingBanner.tsx` — dismissible pre-grouping warning (PRD FR-9)
  - [x] `SuggestedRouteSheet.tsx` — list, move up/down, Apply / Cancel, excluded warning, empty-order state
  - [x] `TodayScreen.tsx` — **Group nearby visits** CTA + loading state, wire sheet, display-order state, distance map for cards
  - [x] `CommandStripCard.tsx` — optional `distanceKm?: number | null` appended to meta when verified

- [x] **Mobile — Tests** (AC: 14)
  - [x] `SuggestedRouteSheet.test.tsx`
  - [x] Update `TodayScreen.test.tsx`, `CommandStripCard.test.tsx`
  - [x] `visitGeo.test.ts` — known distance sanity check

### Review Findings

- [x] [Review][Defer] AC5 three-visit integration scenario not in `VisitGroupingTests.cs` [`tests/api.integration/VisitGroupingTests.cs`] — deferred; `VisitProximityGrouperTests.Group_VisitsEightKmApart` covers clustering behavior at unit level
- [x] [Review][Defer] No integration test for `gps_coordinates_missing` exclusion [`apps/api/Infrastructure/Visits/VisitService.cs:658`] — deferred; exclusion logic implemented and mirrors `gps_unverified` path
- [x] [Review][Defer] No mobile Jest test for grouping API failure + inline retry (AC12) [`apps/mobile/src/screens/today/TodayScreen.tsx:621`] — deferred; retry UI implemented, test gap only

## Dev Notes

### Scope boundary (critical)

| In scope (3.5) | Deferred |
|----------------|----------|
| Grouping suggestion API (read-only) | Persisting reordered schedule server-side |
| Proximity clusters + suggested route | Embedded map / turn-by-turn in app |
| Manual reorder UI (display order only) | Offline grouping without network (3.6) |
| Distance km on meta when route applied | Full sync chip states (3.7) |
| Exclude/warn unverified GPS visits | POCSO discreet header (3.8) |
| **Group nearby visits** on Today tab | Web UI (mobile-only field workflow) |

**Critical:** Grouping is a **field-efficiency suggestion**. It does **not** mutate `visits.scheduled_at_utc`, does **not** write audit events, and does **not** replace `GET /visits/today` as the source of truth. Supervisors still see scheduler order in APIs.

### Clustering algorithm (v1 pilot — implement exactly)

Use deterministic, test-friendly logic (no ML, no external routing APIs):

1. **Eligibility:** today's visits for current worker matching `ListTodayAsync` filter; case `GpsVerified && Latitude && Longitude`.
2. **Clustering:** single-linkage agglomerative with **max distance 3.0 km** between any two visits in the same cluster (Haversine). Visits beyond 3 km from all cluster members start a new cluster.
3. **Cluster ordering:** sort clusters by centroid latitude ascending (stable tie-break longitude) — sufficient for pilot; document in code comment.
4. **Within-cluster + cross-cluster route:** nearest-neighbor greedy starting from the eligible visit with earliest `scheduledAtUtc` (tie-break `visitId`).
5. **Leg distances:** Haversine between consecutive visits in final `suggestedVisitOrder`; round to **one decimal** km for DTO/mobile display.

Constants live in `VisitProximityGrouper` as `public const double MaxClusterDistanceKm = 3.0` so integration tests can rely on 1.5 km / 8 km scenario in AC5.

**Types:** Case `Latitude`/`Longitude` are `decimal?` in EF — cast to `double` at the Haversine boundary in `GeoDistance.DistanceKm`.

### API contract

**Grouping suggestion**

```
GET /api/v1/visits/today/grouping-suggestion
Authorization: Bearer <field worker>
→ 200 {
  data: {
    clusters: [{ clusterIndex, visitIds[], centroidLatitude?, centroidLongitude? }],
    suggestedVisitOrder: Guid[],
    legs: [{ visitId, distanceKmFromPrevious }],
    excluded: [{ visitId, reason }],
    eligibleCount: number,
    excludedCount: number,
    message?: string
  },
  meta: { requestId }
}
```

**Reason codes:** `gps_unverified` | `gps_coordinates_missing`

### Files to read before coding (UPDATE — do not skip)

| File | Current state | This story changes |
|------|---------------|-------------------|
| `apps/api/Infrastructure/Visits/VisitService.cs` | `ListTodayAsync`, no grouping | Add `GetTodayGroupingSuggestionAsync`; extract shared today-query if needed |
| `apps/api/Controllers/V1/VisitsController.cs` | `GET today/weekly/overdue` only | Add `GET today/grouping-suggestion` |
| `apps/api/Models/Visits/VisitDtos.cs` | List/mutation DTOs only | Add grouping DTOs file |
| `apps/mobile/src/screens/today/TodayScreen.tsx` | Time-ordered strip from API | Group CTA, custom order, sheet |
| `apps/mobile/src/components/CommandStripCard.tsx` | Meta: domicile + GPS unverified | Optional distance km suffix |
| `apps/mobile/src/services/visits/commandStripCache.ts` | `{ items, fetchedAtUtc }` | Optional `customVisitOrder` |
| `apps/mobile/src/services/visits/VisitApiService.ts` | No grouping call | `getTodayGroupingSuggestion` |
| `tests/api.integration/VisitSchedulerTests.cs` | Scheduler coverage | Pattern for field worker session + schedule |
| `tests/api.integration/CaseGpsTests.cs` | `VerifyCaseGpsAsync` helper coords | Reuse verify pattern for test cases |

**Preserve:** Story 3.4 `useVisitNavigation` / `CaptureLandmarkModal` / GPS verify API; visit start/complete/reschedule; `ApiEnvelopeFilter`; handoff whisper on cards; overdue styling; cold-open cache behavior.

### Mobile UX (Proximity Day Pack)

Brainstorming name: **Proximity Day Pack** [Source: `brainstorming-session-2026-06-12-1530.md` Field UX #4]

- Pre-grouping banner (when any unverified GPS): `"Some visits need landmark capture before they can be grouped"` — dismissible, amber (`#FFFAEB` / `#B54708` left border)
- CTA label: **Group nearby visits** (text button, `#175CD3`, below title/banner)
- Sheet title: **Suggested route**
- Primary apply: **Apply route** (`#0D6E6E`); secondary **Cancel**
- Excluded warning: amber left border (`#B54708` / `#FFFAEB`) — mirrors court banner pattern
- Meta with distance matches mockup: `Visit 2 · Slum domicile · 1.2 km` [Source: `mockups/command-strip-today.html`]
- Do **not** add notification bell or sync chip changes (3.7)

### Architecture compliance

- REST `/api/v1`, dedicated endpoint — **never** client-compose clusters from `GET /cases/assigned` [Source: `architecture.md` §5.3, `project-context.md`]
- GPS on **Case** aggregate; grouping reads case coordinates [Source: Story 3.4, `case-and-lifecycle.md`]
- Field worker policy on visit endpoints [Source: `roles-and-access.md`]
- Mobile-only field workflow; web does not surface grouping [Source: `architecture.md` §5.4]
- No audit write for read-only suggestion endpoint
- Haversine in API domain/infrastructure — no Google Distance Matrix API (cost/offline)

### Library / framework requirements

| Layer | Requirement |
|-------|-------------|
| API | Pure C# Haversine — **no** NuGet geolocation library required |
| Mobile | Pure TS Haversine in `visitGeo.ts` — **no** new RN maps dependency |
| API client | Regenerate from OpenAPI — **never** hand-edit `packages/api-client/src/generated/*` |
| .NET | Stay on **.NET 8** / EF Core 8 |

### Testing standards

**API:** `[Collection("AuthIntegration")]`. Schedule visits via coordinator; transfer to worker; verify GPS with distinct coordinates:

```csharp
// ~1.1 km apart (Bangalore area)
await CaseTestData.VerifyCaseGpsAsync(client, workerToken, caseA, 12.9716, 77.5946, "Hall A");
await CaseTestData.VerifyCaseGpsAsync(client, workerToken, caseB, 12.9800, 77.5946, "Hall B");
await CaseTestData.VerifyCaseGpsAsync(client, workerToken, caseC, 13.0500, 77.5946, "Far C");
```

**Mobile:** Jest + `react-test-renderer`. Mock `visitApiService.getTodayGroupingSuggestion`. Baseline **50** tests after Story 3.4.

**Unit:** Add `tests/api.unit/VisitProximityGrouperTests.cs` for cluster boundary at 3 km (fast, no DB).

### Previous story intelligence (3.4)

- `CaseSummaryDto` already exposes `gpsVerified`, `latitude`, `longitude` on visit list items — grouping eligibility is available without new case fields
- Meta line ` · GPS unverified` when `!gpsVerified`; verified visits omit suffix — **3.5 adds optional ` · {n} km`** when route applied
- `applyCaseGpsUpdate` pattern in `TodayScreen` — after verify, visit may become grouping-eligible; clearing custom order on refresh avoids stale exclusions
- OpenAPI regen: use **absolute** `API_OPENAPI_FILE` path on Windows (`generate.mjs` fix from 3.4)
- Shared navigate logic in `useVisitNavigation.ts` — do not duplicate

### Previous story intelligence (3.2–3.3)

- Command Strip loads **only** from `GET /visits/today` — grouping is additive UI on top of same items
- `visitIndex` was 1-based display index in API order — now follows display order after apply
- Pull-to-refresh pattern must not clear rows during refresh (keep items visible)

### Project structure notes

- Grouper logic: `apps/api/Infrastructure/Visits/VisitProximityGrouper.cs`
- Mobile sheet: `apps/mobile/src/components/SuggestedRouteSheet.tsx`
- Geo helper: `apps/mobile/src/services/visits/visitGeo.ts`
- Do not add web components
- Do not mutate `scheduled_at_utc` to reflect suggested route

### Latest technical information

- **Haversine formula** is stable and appropriate for pilot-scale visit distances (<50 km). Earth radius use **6371 km** (WGS84 mean). No API keys or network calls.
- **React Native** has no built-in drag-reorder in core — use explicit Move up/down buttons for v1 (accessible, testable). Avoid adding `react-native-draggable-flatlist` unless already in monorepo (it is not).
- **Google Maps Distance Matrix** is intentionally **not** used — offline-friendly, zero cost, matches PRD "GPS cluster" not driving directions.

### Project context reference

- Do not compose Command Strip or grouping from generic case lists [`project-context.md`]
- Only visits/notes/claims sync offline later — grouping requires network in 3.5 [`project-context.md`]
- FR-10 excludes unverified GPS until landmark captured [`epics.md`, `prd.md` FR-10]

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.5, FR-10]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-10]
- [Source: `_bmad-output/specs/spec-kaval-online/field-and-court-operations.md` — Proximity day pack]
- [Source: `_bmad-output/specs/spec-kaval-online/SPEC.md` — CAP-4 proximity clustering]
- [Source: `_bmad-output/brainstorming/brainstorming-session-2026-06-12-1530.md` — Field UX #4]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/command-strip-today.html` — 1.2 km meta]
- [Source: `_bmad-output/implementation-artifacts/3-4-gps-capture-landmark-and-google-maps-navigation.md` — GPS fields, distance deferred to 3.5]
- [Source: `_bmad-output/implementation-artifacts/3-2-morning-command-strip-mobile-home.md` — Command Strip patterns]
- [Source: `apps/api/Infrastructure/Visits/VisitService.cs` — `ListTodayAsync`]
- [Source: `apps/api/Controllers/V1/VisitsController.cs`]
- [Source: `apps/mobile/src/screens/today/TodayScreen.tsx`]

## Dev Agent Record

### Agent Model Used

Composer (Cursor Agent)

### Debug Log References

- API integration tests require Docker/Testcontainers; grouping tests added in `VisitGroupingTests.cs` (not run locally without Docker).
- OpenAPI snapshot exported via `SwaggerEndpointTests.Export_Swagger_WhenRequested` with absolute `EXPORT_OPENAPI_PATH`.
- Fixed cold-open race: `fetchVisits` now accepts cache overrides so custom route order survives background refresh.

### Completion Notes List

- Added `GET /api/v1/visits/today/grouping-suggestion` with Haversine clustering (3 km threshold), nearest-neighbor route, excluded unverified GPS visits, and leg distances.
- Mobile **Group nearby visits** flow: pre-grouping banner, suggested route sheet with reorder, apply display-only route, distance km on meta, AsyncStorage persistence.
- API unit: 3 grouper tests; mobile: 63/63 Jest tests pass (+13 new/updated).
- OpenAPI snapshot regenerated; `@midi-kaval/api-client` rebuilt.

### File List

**API**
- `apps/api/Infrastructure/Visits/GeoDistance.cs`
- `apps/api/Infrastructure/Visits/VisitProximityGrouper.cs`
- `apps/api/Models/Visits/VisitGroupingDtos.cs`
- `apps/api/Infrastructure/Visits/VisitService.cs`
- `apps/api/Controllers/V1/VisitsController.cs`

**Tests**
- `tests/api.unit/VisitProximityGrouperTests.cs`
- `tests/api.integration/VisitGroupingTests.cs`
- `tests/api.integration/VisitTestData.cs`

**API client**
- `packages/api-client/openapi-snapshot.json`
- `packages/api-client/src/generated/api.ts`
- `packages/api-client/dist/generated/api.d.ts`

**Mobile**
- `apps/mobile/src/services/visits/visitGeo.ts`
- `apps/mobile/src/services/visits/visitDisplayOrder.ts`
- `apps/mobile/src/services/visits/commandStripCache.ts`
- `apps/mobile/src/services/visits/VisitApiService.ts`
- `apps/mobile/src/services/visits/visit.models.ts`
- `apps/mobile/src/components/UnverifiedGpsGroupingBanner.tsx`
- `apps/mobile/src/components/SuggestedRouteSheet.tsx`
- `apps/mobile/src/components/CommandStripCard.tsx`
- `apps/mobile/src/screens/today/TodayScreen.tsx`
- `apps/mobile/__tests__/visitGeo.test.ts`
- `apps/mobile/__tests__/SuggestedRouteSheet.test.tsx`
- `apps/mobile/__tests__/TodayScreen.test.tsx`
- `apps/mobile/__tests__/CommandStripCard.test.tsx`

**Docs**
- `README.md`

## Change Log

- 2026-06-17 — Story created with comprehensive developer context (ready-for-dev).
- 2026-06-17 — Validation: 8 fixes applied (pre-grouping warning, cold-open order restore, empty-order sheet, cache shape, AC renumber).
- 2026-06-17 — Code review complete; status → done. Three test-coverage gaps deferred (AC5 integration scenario, gps_coordinates_missing, grouping error retry Jest).
