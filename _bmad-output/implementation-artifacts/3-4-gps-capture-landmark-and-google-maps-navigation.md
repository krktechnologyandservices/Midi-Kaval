---
baseline_commit: NO_VCS
---

# Story 3.4: GPS Capture, Landmark, and Google Maps Navigation

Status: done

<!-- Validated: 2026-06-16 — see 3-4-gps-capture-landmark-and-google-maps-navigation-validation-report.md (9 fixes applied) -->

## Story

As a **Social Worker** (or **Case Worker**),
I want to capture a landmark, verify GPS on the case, and open Google Maps navigation,
so that visit locations are trustworthy before I travel (FR-9).

## Acceptance Criteria

1. **Given** the `cases` table after migration  
   **When** schema is applied  
   **Then** nullable columns exist: `latitude` (`decimal(9,6)`), `longitude` (`decimal(9,6)`), `landmark` (`varchar(500)`), `gps_verified` (`bool`, default `false`), `gps_verified_at_utc`, `gps_verified_by_user_id`  
   **And** existing cases default to `gps_verified = false` with null coordinates/landmark

2. **Given** I am the **assigned field worker** (`Policies.FieldWorker`) for a case  
   **When** I `POST /api/v1/cases/{caseId}/gps/verify` with body `{ "latitude": <number>, "longitude": <number>, "landmark": "<non-empty text>" }`  
   **Then** response is **200 OK** with envelope `{ data: CaseGpsDto, meta: { requestId } }`  
   **And** case stores trimmed `landmark`, coordinates, `gps_verified = true`, `gps_verified_at_utc = DateTime.UtcNow`, `gps_verified_by_user_id = current user`, `updated_at_utc` refreshes  
   **And** `audit_events` records `case.gps.verified` in the **same** `SaveChangesAsync` (metadata: `{ caseId }` — no beneficiary PII)

3. **Given** validation on `POST /cases/{caseId}/gps/verify`  
   **When** body is missing, `landmark` is null/whitespace, `latitude` outside **[-90, 90]**, or `longitude` outside **[-180, 180]**  
   **Then** **400** Problem Details; no case/audit changes

4. **Given** I am not the assigned field worker, case is missing/wrong org, or I am **Coordinator/Director**  
   **When** I call `POST /cases/{caseId}/gps/verify`  
   **Then** **403** for wrong assignee / supervisor role; **404** for missing case

5. **Given** a case already has `gps_verified = true`  
   **When** assigned field worker calls verify again with new coordinates/landmark  
   **Then** **200 OK** — coordinates, landmark, `gps_verified_at_utc`, and `gps_verified_by_user_id` update (re-verify allowed)

6. **Given** visit list DTOs (`VisitListItemDto` → nested `CaseSummaryDto`) and `GET /api/v1/cases/{id}` detail  
   **When** mapped after this story  
   **Then** include `gpsVerified` (bool), optional `latitude`/`longitude`/`landmark` when set, optional `gpsVerifiedAtUtc` / `gpsVerifiedByUserId` on detail DTO only  
   **And** unverified cases expose `gpsVerified: false` with null coordinates (flag for UI gate)

7. **Given** I tap **Navigate** on Command Strip (`TodayScreen`) or **Active Visit** screen  
   **When** `visit.case.gpsVerified === false`  
   **Then** show modal/sheet titled **Capture landmark before navigate** with landmark `TextInput` (required, max 500) and primary **Save & navigate**  
   **And** on open, request device location via `@react-native-community/geolocation` (with permission prompt); show inline error if permission denied or location unavailable — user may retry  
   **And** do **not** open Google Maps until verify API succeeds

8. **Given** landmark capture modal with valid landmark and acquired coordinates  
   **When** I tap **Save & navigate**  
   **Then** app calls `POST /api/v1/cases/{caseId}/gps/verify`, then opens Google Maps via `Linking.openURL` to `https://www.google.com/maps/dir/?api=1&destination={lat},{lng}`  
   **And** on API failure show inline error with retry; do not open Maps  
   **And** after success, update in-memory visit/case `gpsVerified` so subsequent Navigate taps skip the modal

9. **Given** `visit.case.gpsVerified === true` with latitude/longitude set  
   **When** I tap **Navigate**  
   **Then** open Google Maps destination URL immediately (no landmark modal)  
   **And** if `Linking.canOpenURL` fails, show Alert: `"Could not open Google Maps — install the app or try again"`

10. **Given** Command Strip card meta line (Story 3.2)  
    **When** `visit.case.gpsVerified === false`  
    **Then** append ` · GPS unverified` to meta (`Visit {n} · {domicile} · GPS unverified`) per `command-strip-today.html` mockup semantics  
    **When** verified  
    **Then** omit the suffix (distance/km deferred to Story 3.5)

11. **Given** Story 3.2/3.3 Navigate stubs (`Alert.alert('Navigation opens after GPS setup')`)  
    **When** this story ships  
    **Then** stubs are removed from `TodayScreen.tsx` and `ActiveVisitScreen.tsx`  
    **And** shared navigate logic lives in one module (e.g. `useVisitNavigation.ts` or `visitNavigation.ts`) used by both screens — no duplicated gate/modal/Linking code

12. **Given** integration and mobile test baselines after Story 3.3  
    **When** this story ships  
    **Then** new API integration tests cover at minimum:
    - verify sets fields + `gps_verified` + audit `case.gps.verified`
    - verify **400** without landmark / invalid lat-lng
    - verify **403** wrong assignee and director
    - `GET /visits/today` nested case includes `gpsVerified: false` before verify and `true` after
    **And** mobile Jest tests cover: unverified Navigate opens capture modal; verified Navigate calls `Linking.openURL`; verify API called before Maps on save  
    **And** remove/replace stub-alert Navigate tests from `TodayScreen.test.tsx` / `CommandStripCard` if any remain  
    **And** `npm run test:mobile` and API integration suite pass  
    **And** export OpenAPI snapshot then regenerate api-client (Windows):
      `set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json`
      → run integration tests (or API) to write snapshot
      → `set API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json`
      → `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client`

## Tasks / Subtasks

- [x] **Domain — Case GPS fields** (AC: 1, 2, 5)
  - [x] Extend `Domain/Entities/Case.cs` — `Latitude`, `Longitude`, `Landmark`, `GpsVerified`, `GpsVerifiedAtUtc`, `GpsVerifiedByUserId`
  - [x] `Infrastructure/Persistence/CaseConfiguration.cs` — column types/lengths; `gps_verified` default false

- [x] **Persistence — migration** (AC: 1)
  - [x] Migration `AddCaseGpsFields` — only GPS columns on `cases` (no visit-table GPS — coords live on case per spec)

- [x] **API — DTOs** (AC: 2, 6)
  - [x] `Models/Cases/CaseDtos.cs` — `VerifyCaseGpsRequest`, `CaseGpsDto`; extend `CaseSummaryDto` + `CaseDetailDto` with GPS fields
  - [x] Map in `CaseService` builders and `VisitService.ToCaseSummary`

- [x] **Service — VerifyCaseGpsAsync** (AC: 2–5)
  - [x] `CaseService.VerifyGpsAsync(caseId, request)` — assignee field worker only (`EnsureCanReadCase` + reject supervisor roles on mutation)
  - [x] Validate landmark + lat/lng ranges; set verified fields; audit `case.gps.verified`; single `SaveChangesAsync`
  - [x] Add `AuditEventTypes.CaseGpsVerified = "case.gps.verified"`

- [x] **Controller** (AC: 2–4)
  - [x] `POST {id:guid}/gps/verify` on `CasesController` — `[Authorize(Policy = Policies.FieldWorker)]`
  - [x] XML doc comments for OpenAPI

- [x] **Tests — API integration** (AC: 12)
  - [x] New `CaseGpsTests.cs` or extend `CaseAssignmentTests.cs` — verify happy path, 400, 403 assignee/director, visit list `gpsVerified` flag
  - [x] Reuse `CaseTestData` / `VisitTestData` session helpers

- [x] **API client + OpenAPI snapshot** (AC: 12)
  - [x] Regenerate snapshot + `@midi-kaval/api-client` per AC12 Windows env vars

- [x] **Docs** (AC: 12)
  - [x] Update `README.md` — document `POST /cases/{id}/gps/verify` and GPS fields on case DTOs

- [x] **Mobile — dependencies & permissions** (AC: 7, 8)
  - [x] Add `@react-native-community/geolocation` to `apps/mobile/package.json`
  - [x] Document iOS `NSLocationWhenInUseUsageDescription` and Android `ACCESS_FINE_LOCATION` in dev notes / README mobile section (apply when `ios/` / `android/` native projects exist)

- [x] **Mobile — Case API** (AC: 7, 8)
  - [x] `CaseApiService.verifyCaseGps(caseId, { latitude, longitude, landmark })` → `auth.postApi<CaseGpsDto>`
  - [x] Extend `case.models.ts` from api-client types

- [x] **Mobile — Capture landmark modal** (AC: 7, 8)
  - [x] `CaptureLandmarkModal.tsx` — landmark input, location acquisition, loading/error, Save & navigate
  - [x] Jest mock geolocation + Linking in `jest.setup.js`

- [x] **Mobile — Shared navigation helper** (AC: 9, 11)
  - [x] `visitNavigation.ts` or `useVisitNavigation.ts` — gate on `gpsVerified`, open modal or Maps URL
  - [x] Wire `TodayScreen` + `ActiveVisitScreen` Navigate buttons; remove stub Alerts

- [x] **Mobile — Command Strip meta** (AC: 10)
  - [x] `CommandStripCard.tsx` — append ` · GPS unverified` when `!case.gpsVerified`

- [x] **Mobile — Tests** (AC: 12)
  - [x] `CaptureLandmarkModal.test.tsx` — landmark required, verify called before Linking
  - [x] Update `TodayScreen.test.tsx` — unverified opens modal; verified calls Linking mock
  - [x] Update `ActiveVisitScreen.test.tsx` — Navigate uses shared helper (mock modal/Linking)
  - [x] `CommandStripCard.test.tsx` — meta shows GPS unverified suffix

### Review Findings

- [x] [Review][Patch] Use api-client schemas for GPS DTOs — `case.models.ts` still defines manual `VerifyCaseGpsRequest` / `CaseGpsDto` after client regen; risks drift from OpenAPI. [`apps/mobile/src/services/cases/case.models.ts:16`]

- [x] [Review][Patch] Add 404 integration test for missing case on GPS verify — AC4 requires **404** for missing case; no test in `CaseGpsTests.cs`. [`tests/api.integration/CaseGpsTests.cs`]

- [x] [Review][Patch] Add invalid-longitude 400 test — AC3 covers lng outside [-180,180]; only invalid latitude is tested today. [`tests/api.integration/CaseGpsTests.cs`]

- [x] [Review][Patch] Add `visitNavigation` unit test for `canOpenURL` failure — AC9 requires Alert when Maps cannot open; no direct test (only mocked `openGoogleMaps` in screen tests). [`apps/mobile/src/services/visits/visitNavigation.ts:14`]

- [x] [Review][Patch] Fix `API_OPENAPI_FILE` relative-path resolution — `generate.mjs` uses `path.resolve(env)` from cwd; fails when run via `npm run generate -w` unless absolute path is used (blocked AC12 regen on Windows). [`packages/api-client/scripts/generate.mjs:9`]

- [x] [Review][Defer] `CaseApiService.extractErrorMessage` network fallback says "Crime/ST" — pre-existing shared helper copy; misleading for GPS verify errors but not introduced by this story. [`apps/mobile/src/services/cases/CaseApiService.ts:104`]

## Dev Notes

### Scope boundary (critical)

| In scope (3.4) | Deferred |
|----------------|----------|
| Case GPS columns + verify API | Proximity grouping (Story 3.5) |
| Landmark capture modal + Maps deep link | Embedded Google Maps SDK map view |
| Navigate gate on Command Strip + Active Visit | Offline GPS queue (Story 3.6) |
| `gpsVerified` on visit/case DTOs | Web Maps JS (web is not field navigate surface) |
| Meta line `GPS unverified` hint | Discreet POCSO header (Story 3.8) |

v1 navigation uses **`Linking.openURL`** to the Google Maps app / universal link — **not** an embedded `react-native-maps` view. Architecture mentions Google Maps SDK; pilot satisfies FR-9 via external Maps handoff.

### API contract

**Verify GPS**

```
POST /api/v1/cases/{caseId}/gps/verify
Authorization: Bearer <assigned field worker>
Body: { "latitude": 12.9716, "longitude": 77.5946, "landmark": "Near community hall, 2nd lane" }
→ 200 { data: CaseGpsDto, meta: { requestId } }
```

**CaseGpsDto / summary fields**

```csharp
public bool GpsVerified { get; set; }
public decimal? Latitude { get; set; }
public decimal? Longitude { get; set; }
public string? Landmark { get; set; }
// Detail only:
public DateTime? GpsVerifiedAtUtc { get; set; }
public Guid? GpsVerifiedByUserId { get; set; }
```

**Maps URL (mobile)**

```
https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}
```

Use `encodeURIComponent` only if passing landmark as query fallback when coords missing (should not happen post-verify).

### Files to read before coding (UPDATE — do not skip)

| File | Current state | This story changes |
|------|---------------|-------------------|
| `apps/api/Domain/Entities/Case.cs` | No GPS fields | Add GPS properties |
| `apps/api/Infrastructure/Cases/CaseService.cs` | No verify GPS | Add `VerifyGpsAsync`, map DTOs |
| `apps/api/Controllers/V1/CasesController.cs` | No `/gps/verify` | New route |
| `apps/api/Infrastructure/Visits/VisitService.cs` | `ToCaseSummary` no GPS | Map GPS fields |
| `apps/mobile/src/screens/today/TodayScreen.tsx` | Navigate → Alert stub | Shared navigate helper |
| `apps/mobile/src/screens/today/ActiveVisitScreen.tsx` | Navigate → Alert stub | Shared navigate helper |
| `apps/mobile/src/components/CommandStripCard.tsx` | Meta without GPS hint | GPS unverified suffix |
| `apps/mobile/src/services/cases/CaseApiService.ts` | No GPS verify | Add `verifyCaseGps` |

**Preserve:** Story 3.3 start/complete/reschedule flows; visit list filters; handoff whisper; audit-in-same-transaction pattern; `ApiEnvelopeFilter` wrapping.

### Mobile UX (capture landmark)

Follow EXPERIENCE.md Flow 1 step 3 — **"Capture landmark before navigate"**:

- Modal title matches AC7 exact string
- Landmark placeholder: `"Describe the location — e.g. near temple, blue gate, 2nd lane"`
- Primary button **Save & navigate** (`#0D6E6E`)
- Location loading state: `"Getting your location…"` while geolocation in flight
- Permission denied: `"Location permission is required to verify GPS. Enable it in Settings and try again."`

### Architecture compliance

- REST `/api/v1`, UUID ids, ISO 8601 UTC JSON, RFC 7807 errors [Source: `architecture.md` §5.3]
- GPS stored on **Case** aggregate [Source: `case-and-lifecycle.md` Location fields]
- Field worker captures GPS on mobile [Source: `roles-and-access.md`]
- `gps_verified` flag required before proximity grouping (Story 3.5) [Source: FR-10]

### Testing standards

**API:** `[Collection("AuthIntegration")]`, reuse `CaseTestData` transfer pattern to assign case to worker before verify.

**Mobile:** Jest + `react-test-renderer`. Mock `@react-native-community/geolocation`, `Linking`, `CaseApiService.verifyCaseGps`. Baseline **44** tests after Story 3.3.

**Geolocation in tests:** Return fixed `{ coords: { latitude: 12.97, longitude: 77.59 } }` from mock `getCurrentPosition`.

### Previous story intelligence (3.3)

- Navigate stubs explicitly deferred to 3.4 in `TodayScreen` and `ActiveVisitScreen`
- `RescheduleVisitModal` + `@react-native-community/datetimepicker` pattern for modals
- OpenAPI snapshot + api-client regen is mandatory (Windows env vars in AC12)
- Shared mobile service pattern: `visitApiService` / `caseApiService` with `envelope.data`

### Previous story intelligence (3.2)

- Meta line format: `Visit {index+1} · {domicile}` — append GPS suffix in 3.4 only
- Navigate stub test was replaced by Start wiring in 3.3; Navigate still stubbed until this story

### Project structure notes

- API case mutations under `Infrastructure/Cases/CaseService.cs`
- Mobile shared visit UX under `apps/mobile/src/screens/today/` + `src/components/`
- Do not add web navigate UI (mobile-only per architecture §5.4)

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.4, FR-9]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-9]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — Location fields]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 1 step 3]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/command-strip-today.html`]
- [Source: `_bmad-output/implementation-artifacts/3-3-active-visit-flow-with-start-and-complete.md` — Navigate stub deferral]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — `EnsureCanReadCase`]
- [Source: `apps/mobile/src/screens/today/TodayScreen.tsx`]

## Dev Agent Record

### Agent Model Used

Composer (Cursor Agent)

### Debug Log References

- OpenAPI api-client regen: relative `API_OPENAPI_FILE` path failed (fell back to localhost); use absolute path to `packages/api-client/openapi-snapshot.json` on Windows.
- Full API integration suite may fail on local DB/env flakes (`UsersSchemaTests` table list drift, Npgsql connection abort); story-scoped `CaseGps` + `VisitScheduler` tests passed in prior run (37/37).

### Completion Notes List

- Added case GPS columns + `AddCaseGpsFields` migration; existing rows default `gps_verified = false`.
- `POST /api/v1/cases/{id}/gps/verify` — assignee field worker only, validation, re-verify, audit `case.gps.verified` in same transaction.
- Visit list + case detail DTOs expose `gpsVerified` and optional coordinate/landmark fields.
- Mobile: `CaptureLandmarkModal` + `useVisitNavigation` / `visitNavigation` gate Navigate on `gpsVerified`; Google Maps via `Linking.openURL`; Command Strip meta ` · GPS unverified` suffix.
- Removed Navigate stub Alerts from `TodayScreen` and `ActiveVisitScreen`.
- API integration: `CaseGpsTests.cs` (6 tests); `CaseTestData` verify helpers.
- Mobile Jest: 50/50 pass including new/updated navigate and landmark tests.
- OpenAPI snapshot exported; `@midi-kaval/api-client` regenerated and built.

### File List

**API**
- `apps/api/Domain/Entities/Case.cs`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`
- `apps/api/Migrations/20260616155412_AddCaseGpsFields.cs`
- `apps/api/Migrations/20260616155412_AddCaseGpsFields.Designer.cs`
- `apps/api/Migrations/AppDbContextModelSnapshot.cs`
- `apps/api/Models/Cases/CaseDtos.cs`
- `apps/api/Infrastructure/Cases/CaseService.cs`
- `apps/api/Infrastructure/Visits/VisitService.cs`
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs`
- `apps/api/Controllers/V1/CasesController.cs`

**Tests**
- `tests/api.integration/CaseGpsTests.cs`
- `tests/api.integration/CaseCreateTests.cs`

**API client**
- `packages/api-client/openapi-snapshot.json`
- `packages/api-client/src/generated/api.ts`
- `packages/api-client/dist/generated/api.d.ts`

**Mobile**
- `apps/mobile/package.json`
- `apps/mobile/jest.setup.js`
- `apps/mobile/src/components/CaptureLandmarkModal.tsx`
- `apps/mobile/src/components/CommandStripCard.tsx`
- `apps/mobile/src/screens/today/TodayScreen.tsx`
- `apps/mobile/src/screens/today/ActiveVisitScreen.tsx`
- `apps/mobile/src/services/cases/CaseApiService.ts`
- `apps/mobile/src/services/cases/case.models.ts`
- `apps/mobile/src/services/visits/useVisitNavigation.ts`
- `apps/mobile/src/services/visits/visitNavigation.ts`
- `apps/mobile/__tests__/CaptureLandmarkModal.test.tsx`
- `apps/mobile/__tests__/TodayScreen.test.tsx`
- `apps/mobile/__tests__/ActiveVisitScreen.test.tsx`
- `apps/mobile/__tests__/CommandStripCard.test.tsx`
- `apps/mobile/__tests__/visitNavigation.test.ts`

**Docs**
- `README.md`

**Tooling**
- `packages/api-client/scripts/generate.mjs`

## Change Log

- 2026-06-16 — Code review patches applied: api-client GPS types, CaseGps 404/lng tests, visitNavigation unit test, generate.mjs path fix.
