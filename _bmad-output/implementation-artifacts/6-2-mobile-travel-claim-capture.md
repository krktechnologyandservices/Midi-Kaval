
---
baseline_commit: NO_VCS
---

# Story 6.2: Mobile Travel Claim Capture

<!-- Validated: 2026-06-20 — see 6-2-mobile-travel-claim-capture-validation-report.md (14 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Social Worker**,
I want to create travel claims from the More tab with receipt photos,
so that I can submit allowances from the field (FR-18, UX-DR13).

*Scope: **Mobile UI + offline draft sync** — More → Travel list/form screens, `TravelClaimApiService`, receipt capture via existing `AttachmentApiService` (`resourceType: TravelClaim`), online submit via Story **6.1** REST API, offline draft queue via extended `POST /sync/push`. **No** Director approve/return web UI (Story **6.3**), **no** claimant notifications (Story **6.4**), **no** crisis-queue claim rows (Story **8.1**), **no** web travel UI.*

## Acceptance Criteria

1. **Given** I am an authenticated field worker on mobile (`AuthContext.isFieldRole`)  
   **When** I open **More → Travel**  
   **Then** I see `TravelClaimsListScreen` with my claims from `GET /api/v1/travel-claims`  
   **And** loading spinner, error+retry, pull-to-refresh (mirror `CourtScheduleScreen`)  
   **And** non-field roles see *"Travel claims are for field workers only."* (mirror `CourtScheduleScreen` guard)

2. **Given** I have no travel claims  
   **When** the list loads successfully  
   **Then** empty state shows **"No claims yet"** and a primary **Create claim** CTA (UX-DR13, EXPERIENCE.md)

3. **Given** the list shows claims  
   **When** rendered  
   **Then** each row shows `claimDate`, `destination`, `transportMode`, `amount`, `status`  
   **And** ordered same as API (`claimDate` desc)  
   **And** tapping a **Draft** claim opens edit form; **Submitted/Approved/Returned** opens read-only detail (no edit/submit)

4. **Given** I tap **Create claim** (or edit a Draft)  
   **When** `TravelClaimFormScreen` opens  
   **Then** fields: `claimDate`, `startLocation`, `destination`, `transportMode` (Bus/Auto/Petrol/Other), `amount`, `autoNumber` (required when Auto), `notes`, linked **case** (at least one from `GET /api/v1/cases/assigned`)  
   **And** validation messages match Story **6.1** AC2:
   - `claimDate` required; `startLocation` / `destination` required, max 256 chars
   - `transportMode` ∈ Bus, Auto, Petrol, Other
   - `amount` > 0 and ≤ 999999.99
   - Auto → `autoNumber` required (max 32 chars); switching away from Auto clears `autoNumber` in form state
   - `caseIds` — at least one, no duplicates; only cases from `listAssignedCases()` (readable assigned cases)
   - `notes` optional, max 2000 chars
   **And** save creates/updates Draft via `POST` / `PATCH /api/v1/travel-claims` (**online only** for PATCH)

5. **Given** a Draft claim saved on the server (`claimId` known)  
   **When** I attach a receipt (Bus/Auto/Petrol — required before submit; optional for Other)  
   **Then** flow mirrors case-note attachments: `DocumentPicker` → `presign` (`resourceType: TravelClaim`, `resourceId: claimId`) → `fetch` PUT → `confirm`  
   **And** allowed types/size from `ALLOWED_ATTACHMENT_CONTENT_TYPES` / `MAX_ATTACHMENT_BYTES`  
   **And** confirmed receipt shown in form (thumbnail/name); can replace while Draft

6. **Given** a Draft claim with required receipt (Bus/Auto/Petrol)  
   **When** I tap **Submit** while online  
   **Then** `POST /api/v1/travel-claims/{id}/submit` succeeds → status `Submitted`, navigate back to list  
   **And** API **422** without receipt shows user-facing message: *"Receipt image is required for Bus, Auto, and Petrol claims before submit."*

7. **Given** I am offline (`isDeviceOffline()` or network error on create)  
   **When** I save a **new** draft claim on the form  
   **Then** claim payload is queued locally (`travel.claim.create` — same AsyncStorage queue as visits)  
   **And** UI shows sync chip **"Saved on this device"** (`sync-local` per EXPERIENCE.md) — **not** "Offline mode engaged"  
   **And** list shows queued draft with local-only badge until synced  
   **And** **offline v1 is create-only** — editing a server-synced Draft or PATCH updates require online (no `travel.claim.update` mutation)

7b. **Given** travel list or form is focused  
   **When** device returns online  
   **Then** `useSyncOnForeground` triggers `flushOfflineQueue` (mirror `ActiveVisitScreen` / `TodayScreen`)

8. **Given** a queued draft claim comes back online  
   **When** sync runs (`flushOfflineQueue` / foreground hook)  
   **Then** `POST /api/v1/sync/push` applies `travel.claim.create` mutation → server Draft created  
   **And** `SyncMutationResultDto.travelClaim` returns created `TravelClaimDto` with `id`  
   **And** pending local receipt (if captured offline) uploads via presign/confirm **after** server `claimId` returned (read `localReceiptUri` from queue row before removal)  
   **And** chip moves to **"Synced"** / pending states per existing sync patterns  
   **And** user may submit when online (submit stays REST, not sync mutation)  
   **And** local list row keyed by `localDraftKey` is replaced with server `claimId` after apply

8b. **Given** sync push returns `rejected` for a travel mutation  
   **When** Sync queue screen shows the row  
   **Then** label identifies travel claim (`Create travel claim` / destination + date — not visit-only copy)  
   **And** user can retry from More → Sync queue  
   **And** empty queue copy is generic (not *"All visits are synced"* when travel-only queue possible)

9. **Given** regression safety  
   **When** story ships  
   **Then** existing visit offline sync, case notes, court schedule, More tab logout unchanged  
   **And** Jest tests cover list empty state, form validation, receipt-required submit guard, offline enqueue  
   **And** API integration tests cover new `travel.claim.create` sync mutation (if backend extended)

## Tasks / Subtasks

- [x] **TravelClaimApiService** (AC: 1, 4, 6)
  - [x] `apps/mobile/src/services/travel/TravelClaimApiService.ts` — `listMine`, `get`, `create`, `update`, `submit` via `AuthSessionService`
  - [x] `apps/mobile/src/services/travel/travel.models.ts` — re-export `TravelClaimDto`, requests from `@midi-kaval/api-client`; constants `TRANSPORT_MODES`, `requiresReceipt(mode)`, `RECEIPT_REQUIRED_MODES`
  - [x] Error wrapping mirroring `CourtApiService` / `TravelClaimApiError` (`network` vs `http` kinds)
  - [x] `listMine` calls `flushOfflineQueue()` on success (mirror `VisitApiService.listToday`)

- [x] **Navigation + More entry** (AC: 1, 2)
  - [x] Extend `MoreStackParamList`: `TravelClaimsList`, `TravelClaimForm` (`claimId?`, `localDraftKey?`, `mode: 'create' | 'edit' | 'view'`)
  - [x] Register screens in `MoreStackNavigator.tsx`
  - [x] Add **Travel** menu row in `MoreScreen.tsx` (above Sync queue)

- [x] **TravelClaimsListScreen** (AC: 1–3, 7–7b)
  - [x] Mirror `CourtScheduleScreen` loading/error/refresh/field-role guard
  - [x] Empty state: "No claims yet" + Create CTA
  - [x] NEW `mergeQueueWithTravelClaims.ts` — merge server list + pending local drafts (`localDraftKey`)
  - [x] Status chip styling for Draft / Submitted / Approved / Returned
  - [x] `useSyncOnForeground` + sync chip for local-only rows (`resolveTravelSyncChip` or generalized helper)

- [x] **TravelClaimFormScreen** (AC: 4–6, 7–7b)
  - [x] Create + edit Draft (online); read-only for non-Draft
  - [x] Route local-only drafts by `localDraftKey` until sync assigns `claimId`
  - [x] Case link picker from `caseApiService.listAssignedCases()` (min 1 case)
  - [x] Conditional `autoNumber` when Auto; clear when mode changes away from Auto
  - [x] Receipt section: pick image → upload via `attachmentApiService` + `resourceType: 'TravelClaim'`
  - [x] Submit button (online only); disable with inline hint when receipt required but missing
  - [x] Handle submit **422** (missing receipt, non-Draft TOCTOU) with user-facing messages
  - [x] Accessible labels + `AccessibleErrorRegion` for API errors
  - [x] `useSyncOnForeground` when form has local-only draft

- [x] **Offline draft sync** (AC: 7–8b)
  - [x] **API:** Add `travel.claim.create` to `SyncMutationTypes` + `TravelClaimCreateSyncPayload` in `SyncDtos.cs` (mirror `CreateTravelClaimRequest` — no `localReceipt*`)
  - [x] **API:** `SyncMutation` entity — add `ResultTravelClaimId` + EF migration (idempotent duplicate replay)
  - [x] **API:** `SyncPushService` — apply via `TravelClaimService.CreateAsync`; validation/not-found/forbidden → `rejected`; return `travelClaim` in `SyncMutationResultDto`; `BuildDuplicateResultAsync` hydrates when `ResultTravelClaimId` set
  - [x] **Mobile:** Refactor `syncMutationTypes.ts` — discriminated union (`visit.*` with `visitId` | `travel.claim.create` with `localDraftKey` + payload + optional receipt fields)
  - [x] **Mobile:** `offlineQueue.ts` — `enqueueTravelClaimDraft(...)` alongside visit enqueue
  - [x] **Mobile:** `mobileSyncPushService` — map travel mutations; `shouldRemoveFromQueue` checks `result.travelClaim`; return `appliedTravelClaims`; deferred receipt upload after create
  - [x] **Mobile:** Update `SyncQueueScreen` + extract `resolveSyncQueueLabel.ts` (visit + travel row titles/labels)
  - [x] **Mobile:** Show sync chip on form/list when draft is local-only

- [x] **Tests** (AC: 9)
  - [x] `TravelClaimsListScreen.test.tsx` — empty state, list rows, error retry, field-role guard
  - [x] `TravelClaimFormScreen.test.tsx` — Auto requires autoNumber, receipt required before submit (mocked API), offline enqueue
  - [x] `mergeQueueWithTravelClaims.test.ts` — local draft merged into list
  - [x] `offlineQueue.test.ts` — travel claim enqueue (extend existing)
  - [x] `resolveSyncQueueLabel.test.ts` — travel mutation labels
  - [x] `tests/api.integration/SyncPushTravelClaimTests.cs` — `travel.claim.create` applied + idempotent duplicate returns `travelClaim`
  - [x] Regression: existing `VisitApiService` / `SyncPushTests` still pass

- [x] **Docs + api-client**
  - [x] README mobile section: More → Travel, offline draft create-only, receipt rule reference to API
  - [x] Regenerate OpenAPI snapshot + `packages/api-client` after `SyncMutationResultDto` extension

### Review Findings

- [x] [Review][Decision] After local draft syncs on the form screen — **A)** `navigation.replace` to `claimId` edit mode when sync removes the queue row (applied)
- [x] [Review][Patch] Deferred receipt upload failures are swallowed silently [`mobileSyncPushService.ts:196-201`] — upload before queue removal; mark error on failure
- [x] [Review][Patch] `BuildDuplicateResultAsync` uncaught `GetAsync` can fail entire sync push batch [`SyncPushService.cs:134-138`]
- [x] [Review][Patch] Local-draft form hides receipt section — AC5 requires showing captured receipt name while Draft [`TravelClaimFormScreen.tsx:569-590`]
- [x] [Review][Patch] `claimDate` uses `toISOString()` — can shift calendar day across timezones [`TravelClaimFormScreen.tsx:52-54`]
- [x] [Review][Patch] Missing Jest test for receipt-required submit guard (AC9) [`TravelClaimFormScreen.test.tsx`]
- [x] [Review][Patch] `useSyncOnForeground` on form should reload/navigate to server claim after sync removes local draft [`TravelClaimFormScreen.tsx:211-216`]
- [x] [Review][Patch] Client form lacks duplicate `caseIds` validation (AC4) [`TravelClaimFormScreen.tsx:56-107`]
- [x] [Review][Defer] `void flushOfflineQueue()` in `listMine` races with queue read — matches existing visit pattern [`TravelClaimApiService.ts:24`] — deferred, pre-existing pattern

## Dev Notes

### READ FIRST

1. **Story 6.1 API is done** — use REST endpoints as-is; do not re-implement server CRUD. Regenerated types live in `packages/api-client/src/generated/api.ts`.
2. **Mirror Story 4.3 attachments** — `CaseDetailPlaceholderScreen` lines ~236–322: `DocumentPicker.pickSingle` → `attachmentApiService.presign/confirm/uploadToPresignedUrl`. Only change: `resourceType: 'TravelClaim'`, `resourceId: claimId`. Claim must exist on server (**Draft**) before presign — create/save draft first.
3. **Mirror Story 5.2 court list UX** — `CourtScheduleScreen.tsx` for loading/error/refresh/empty; entry is on **More** tab per UX (not Today).
4. **Mirror Story 3.6/3.7 offline** — `VisitApiService.ts` + `offlineQueue.ts` + `mobileSyncPushService.ts`. Today sync only supports `visit.start` / `visit.complete` — **you must extend both mobile queue and `SyncPushService`** for `travel.claim.create`.
5. **Epic 6 boundaries** — **6.3** Director web approve; **6.4** notifications; **8.1** crisis-queue pending rows. Mobile shows status but cannot approve/return.
6. **Submit is online-only** — server receipt validation (422) requires confirmed attachment blob. Offline path: queue draft → sync create → upload receipt when online → user taps Submit.
7. **Offline v1 is create-only** — no `travel.claim.update` mutation; PATCH and submit require online. Do not reuse `visitId` on travel queue rows — use discriminated `QueuedMutation` union with `localDraftKey`.
8. **Sync idempotency** — add `ResultTravelClaimId` on `SyncMutation` (mirror `ResultVisitId`) so duplicate replay returns `travelClaim` in push result.
9. **Voice/tone (EXPERIENCE.md)** — Empty: "No claims yet". Offline chip: "Saved on this device." Sync pending/error chips reuse existing copy. No gamified language.

### API contract (Story 6.1 — consumer reference)

| Action | Method | Path |
|--------|--------|------|
| List mine | GET | `/api/v1/travel-claims` |
| Create | POST | `/api/v1/travel-claims` |
| Get | GET | `/api/v1/travel-claims/{id}` |
| Update Draft | PATCH | `/api/v1/travel-claims/{id}` |
| Submit | POST | `/api/v1/travel-claims/{id}/submit` |
| Presign receipt | POST | `/api/v1/attachments/presign` (`TravelClaim`) |
| Confirm receipt | POST | `/api/v1/attachments/confirm` |

**Receipt rule:** Bus/Auto/Petrol require confirmed attachment before submit; Other exempt. Auto requires `autoNumber`.

**DTOs:** `CreateTravelClaimRequest`, `UpdateTravelClaimRequest`, `TravelClaimDto`, `TravelClaimListResultDto` — import via `components['schemas']` in `travel.models.ts`.

### Offline architecture (extend v1 visit queue)

| Layer | Current | Story 6.2 change |
|-------|---------|------------------|
| Storage | AsyncStorage `midi-kaval:offline-sync-queue:v1` | Discriminated union: visit rows + travel rows (`localDraftKey`) |
| API sync | `visit.start`, `visit.complete` only | Add `travel.claim.create` |
| DB replay | `SyncMutation.ResultVisitId` | Add `ResultTravelClaimId` + migration |
| Conflict | Server wins | Same — idempotent `clientMutationId` |
| Receipt blob | N/A | Store `localReceiptUri` on mobile queue row only; upload after create returns `claimId` |
| Flush hook | `listToday` / foreground | Also `listMine` + travel screens `useSyncOnForeground` |

**Mobile `QueuedMutation` shape (discriminated):**

```ts
// visit.start | visit.complete
{ type: 'visit.start' | 'visit.complete'; visitId: string; ... }
// travel.claim.create
{
  type: 'travel.claim.create';
  localDraftKey: string; // stable until server claimId assigned
  claimDate: string;
  startLocation: string;
  destination: string;
  transportMode: string;
  amount: number;
  autoNumber?: string | null;
  notes?: string | null;
  caseIds: string[];
  localReceiptUri?: string;
  receiptFileName?: string;
  receiptContentType?: string;
  syncStatus: SyncStatus;
}
```

**Server `travel.claim.create` payload** (no receipt fields):

```json
{
  "claimDate": "2026-06-15",
  "startLocation": "Office",
  "destination": "District Court",
  "transportMode": "Bus",
  "amount": 45.50,
  "autoNumber": null,
  "notes": "Client visit",
  "caseIds": ["<uuid>"]
}
```

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/mobile/src/services/travel/TravelClaimApiService.ts` |
| NEW | `apps/mobile/src/services/travel/travel.models.ts` |
| NEW | `apps/mobile/src/screens/travel/TravelClaimsListScreen.tsx` |
| NEW | `apps/mobile/src/screens/travel/TravelClaimFormScreen.tsx` |
| NEW | `apps/mobile/__tests__/TravelClaimsListScreen.test.tsx` |
| NEW | `apps/mobile/__tests__/TravelClaimFormScreen.test.tsx` |
| NEW | `apps/mobile/src/services/sync/mergeQueueWithTravelClaims.ts` |
| NEW | `apps/mobile/src/services/sync/resolveSyncQueueLabel.ts` |
| NEW | `apps/mobile/__tests__/mergeQueueWithTravelClaims.test.ts` |
| NEW | `apps/mobile/__tests__/resolveSyncQueueLabel.test.ts` |
| NEW | `tests/api.integration/SyncPushTravelClaimTests.cs` |
| UPDATE | `apps/api/Domain/Entities/SyncMutation.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/SyncMutationConfiguration.cs` |
| UPDATE | `apps/api/Migrations/*_AddSyncMutationTravelClaimResult.cs` (new migration) |
| UPDATE | `apps/mobile/src/navigation/types.ts` |
| UPDATE | `apps/mobile/src/navigation/MoreStackNavigator.tsx` |
| UPDATE | `apps/mobile/src/screens/more/MoreScreen.tsx` |
| UPDATE | `apps/mobile/src/services/sync/syncMutationTypes.ts` |
| UPDATE | `apps/mobile/src/services/sync/offlineQueue.ts` |
| UPDATE | `apps/mobile/src/services/sync/mobileSyncPushService.ts` |
| UPDATE | `apps/mobile/src/screens/more/SyncQueueScreen.tsx` |
| UPDATE | `apps/api/Models/Sync/SyncDtos.cs` |
| UPDATE | `apps/api/Infrastructure/Sync/SyncPushService.cs` |
| UPDATE | `packages/api-client/openapi-snapshot.json` |
| UPDATE | `packages/api-client/src/generated/api.ts` |
| UPDATE | `README.md` |

**Reuse without modification:** `AttachmentApiService.ts`, `caseApiService.listAssignedCases`, `ALLOWED_ATTACHMENT_CONTENT_TYPES`, `MAX_ATTACHMENT_BYTES`, `useSyncOnForeground`, `SyncChip` component.

### Previous story intelligence (6.1)

- TOCTOU guards on Draft mutate/submit — mobile should refresh claim before submit if user waited on form a long time; handle **422** gracefully (*"Receipt image is required…"* and non-Draft status).
- Presign/confirm **422** when `status != Draft` — read-only detail must not offer receipt upload.
- `ThenByDescending(c.Id)` list order — display as API returns; no client re-sort needed.
- Monthly totals / supervisor endpoints — **not used** on mobile field worker.
- Integration tests require Docker/Testcontainers for API suite; mobile uses Jest.

### Testing requirements

- Run `npm test -w apps/mobile` for new screen/sync tests.
- Run `dotnet test tests/api.integration --filter SyncPushTravelClaim` after API sync extension.
- Regenerate api-client: `EXPORT_OPENAPI_PATH` + `npm run build -w @midi-kaval/api-client`.
- Manual: create Bus claim → attach receipt → submit; create offline draft → airplane mode → sync → attach receipt → submit.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 6 Story 6.2]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Empty travel drafts, sync chip voice]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.5 offline scope includes draft travel claims]
- [Source: `_bmad-output/implementation-artifacts/6-1-travel-claim-api-with-receipt-validation.md`]
- [Source: `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx` — attachment flow]
- [Source: `apps/mobile/src/screens/court/CourtScheduleScreen.tsx` — list UX]
- [Source: `apps/mobile/src/services/visits/VisitApiService.ts` — offline enqueue pattern]
- [Source: `apps/mobile/src/services/sync/mergeQueueWithVisits.ts` — list merge pattern]
- [Source: `tests/api.integration/SyncPushTests.cs` — idempotent replay pattern]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

- Docker Desktop not running — `SyncPushTravelClaimTests` require Testcontainers. Run locally: `dotnet test tests/api.integration --filter SyncPushTravelClaim` with Docker started.
- Mobile Jest: 34/34 suites pass, 130 tests (`npm test -w apps/mobile`).
- Code review patches applied (2026-06-20): receipt upload error surfacing, duplicate replay hardening, local-draft receipt display, date-only `claimDate`, post-sync navigation (decision A), duplicate `caseIds` validation, AC9 Jest guard.

### Completion Notes List

- **More → Travel** — list + form screens with field-role guard, empty state, pull-to-refresh, status chips.
- **TravelClaimApiService** — online CRUD/submit; offline create via `enqueueTravelClaimDraft`.
- **Offline sync** — `travel.claim.create` mutation; `ResultTravelClaimId` migration; deferred receipt upload after sync apply.
- **Sync queue** — travel row labels; generic empty copy.
- **Tests** — 6 new Jest suites + `SyncPushTravelClaimTests.cs`; regression mobile suite green.

### File List

- apps/api/Domain/Entities/SyncMutation.cs (modified)
- apps/api/Infrastructure/Sync/SyncApplyOutcome.cs (new)
- apps/api/Infrastructure/Sync/SyncPushService.cs (modified)
- apps/api/Models/Sync/SyncDtos.cs (modified)
- apps/api/Migrations/20260620005739_AddSyncMutationTravelClaimResult.cs (new)
- apps/api/Migrations/20260620005739_AddSyncMutationTravelClaimResult.Designer.cs (new)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (modified)
- apps/mobile/src/services/travel/TravelClaimApiService.ts (new)
- apps/mobile/src/services/travel/travel.models.ts (new)
- apps/mobile/src/screens/travel/TravelClaimsListScreen.tsx (new)
- apps/mobile/src/screens/travel/TravelClaimFormScreen.tsx (new)
- apps/mobile/src/services/sync/mergeQueueWithTravelClaims.ts (new)
- apps/mobile/src/services/sync/resolveSyncQueueLabel.ts (new)
- apps/mobile/src/services/sync/resolveTravelSyncChip.ts (new)
- apps/mobile/src/services/sync/syncMutationTypes.ts (modified)
- apps/mobile/src/services/sync/offlineQueue.ts (modified)
- apps/mobile/src/services/sync/mobileSyncPushService.ts (modified)
- apps/mobile/src/services/sync/useSyncOnForeground.ts (modified)
- apps/mobile/src/services/sync/mergeQueueWithVisits.ts (modified)
- apps/mobile/src/services/sync/resolveVisitSyncChip.ts (modified)
- apps/mobile/src/navigation/types.ts (modified)
- apps/mobile/src/navigation/MoreStackNavigator.tsx (modified)
- apps/mobile/src/screens/more/MoreScreen.tsx (modified)
- apps/mobile/src/screens/more/SyncQueueScreen.tsx (modified)
- apps/mobile/__tests__/TravelClaimsListScreen.test.tsx (new)
- apps/mobile/__tests__/TravelClaimFormScreen.test.tsx (new)
- apps/mobile/__tests__/mergeQueueWithTravelClaims.test.ts (new)
- apps/mobile/__tests__/resolveSyncQueueLabel.test.ts (new)
- apps/mobile/__tests__/offlineQueue.test.ts (modified)
- apps/mobile/__tests__/SyncQueueScreen.test.tsx (modified)
- tests/api.integration/SyncPushTravelClaimTests.cs (new)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (modified)
- README.md (modified)

## Change Log

- 2026-06-20: Story 6.2 created — mobile travel claim capture, More → Travel, receipt photos, offline draft sync via extended sync/push; approve/notifications deferred Epic 6.3–6.4.
- 2026-06-20: Validation — 14 fixes (discriminated queue union, ResultTravelClaimId migration, mobileSyncPush travel results, offline create-only scope, useSyncOnForeground, mergeQueueWithTravelClaims, SyncQueue labels, field-role copy, api-client regen).
- 2026-06-20: Implementation — mobile travel UI, offline draft sync, API sync extension, tests, README, api-client update.
- 2026-06-20: Code review — 7 patches applied; story marked done.
