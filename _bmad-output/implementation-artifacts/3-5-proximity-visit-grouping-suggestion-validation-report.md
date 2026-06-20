# Story Validation Report — 3.5 Proximity Visit Grouping Suggestion

**Story:** `3-5-proximity-visit-grouping-suggestion`  
**Validated:** 2026-06-17  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (8 fixes applied 2026-06-17)

---

## Summary

Story 3.5 correctly implements FR-10 (proximity day pack) on top of Story 3.4 GPS fields and the Command Strip from 3.2. Scope boundaries (display-only reorder, no schedule mutation, mobile-only, no offline grouping) are sound. Eight gaps could cause **PRD warning omission**, **cold-open order loss**, **empty-suggestion UX bugs**, or **misleading migration work**.

| Check | Result |
|-------|--------|
| Epic 3.5 AC (clusters, exclude unverified, reorder) | Pass |
| FR-10 PRD (GPS cluster + reorder; exclude unverified) | Pass (after fix) |
| SPEC CAP-4 proximity day pack | Pass |
| Brainstorming Field UX #4 (unverified GPS warning) | **Fix** |
| Story 3.4 GPS fields on `CaseSummaryDto` | Pass — reuse, no migration |
| Dedicated API endpoint (not client-composed) | Pass |
| Command Strip meta `1.2 km` deferred from 3.4 | Pass |
| Display-only reorder (no `scheduled_at_utc` mutation) | Pass |
| OpenAPI / api-client regen workflow | Pass |
| Cold-open cache continuity (Story 3.2) | **Fix** |
| Offline sync / POCSO discreet | Correctly deferred to 3.6 / 3.8 |

---

## Critical Issues (Must Fix) — Applied

### 1. AC1 implied unnecessary database migration

Grouping reads existing case GPS columns from Story 3.4. "Schema updated" would mislead the dev agent into creating a migration.

**Fix applied:** AC1 now states **no database migration**; OpenAPI only.

### 2. PRD FR-9 consequence missing — warn before suggesting route

PRD: *"Unverified GPS flagged; proximity grouping warns before suggesting route."* Original story only warned inside the sheet after the API call.

**Fix applied:** AC6 — dismissible inline banner on Today when any visit has `gpsVerified === false`; new `UnverifiedGpsGroupingBanner.tsx` task.

### 3. Cold-open would lose custom visit order

Story 3.2 requires immediate cached strip on relaunch. Custom order in AsyncStorage without mount-time restore would flash API order then jump.

**Fix applied:** AC10 + cache task — restore `customVisitOrder` and `routeGroupingActive` on `readCache()` / Today mount.

### 4. Empty `suggestedVisitOrder` sheet behavior unspecified

When all visits are unverified (or only one eligible), API returns 200 with `message` and empty order. Opening a sheet with Apply enabled would be a dead end.

**Fix applied:** AC7 — show message prominently; hide **Apply route** when order is empty.

---

## Enhancement Opportunities — Applied

### 5. `writeCache` signature change not called out

`commandStripCache.writeCache(items)` is used in two `TodayScreen` sites; adding `customVisitOrder` requires signature update.

**Fix applied:** Task bullets for `writeCache(items, options?)` and caller updates.

### 6. Grouping CTA loading / double-submit

No in-flight guard on grouping API call.

**Fix applied:** AC7 — **Grouping…** disabled state.

### 7. Manual reorder needs visit coordinates on sheet

Distance recalc after Move up/down requires lat/lng from nested case, not visit IDs alone.

**Fix applied:** AC8 — sheet keyed by `visitId` against today's `VisitListItemDto[]`; accessibility labels on reorder controls.

### 8. `decimal?` vs `double` in Haversine

Case coordinates are `decimal?` in EF entities; Haversine math uses `double`.

**Fix applied:** Dev Notes — cast at `GeoDistance` boundary.

---

## Optimizations (Not Applied — Acceptable for v1)

- **Nearest-neighbor vs optimal TSP:** Greedy heuristic is sufficient for pilot (≤20 visits/day); document only.
- **Device-location distance when not grouped:** Mockup `1.2 km` is route-leg distance in grouped mode only — correctly scoped.
- **Persist dismissed banner state:** Session dismiss is enough for v1; no AsyncStorage for banner.

---

## LLM Dev-Agent Notes

- Route `GET today/grouping-suggestion` adjacent to `GET today` in `VisitsController` — no conflict with `{id:guid}` routes.
- Reuse `ListTodayAsync` filter internals; do not duplicate SQL predicates.
- `CaseTestData.VerifyCaseGpsAsync` lives in `tests/api.integration/CaseCreateTests.cs` (class `CaseTestData`).
- Test coords: A `12.9716`, B `12.9800` (~0.9 km); C `13.0500` (~8.7 km from A) — validates 3 km cluster threshold.

---

## Verdict

Story is **implementation-ready** after applied fixes. Proceed with `dev-story`.
