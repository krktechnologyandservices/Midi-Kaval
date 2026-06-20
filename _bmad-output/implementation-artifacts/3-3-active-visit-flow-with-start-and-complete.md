---
baseline_commit: NO_VCS
---

# Story 3.3: Active Visit Flow with Start and Complete

Status: done

<!-- Validated: 2026-06-16 — see 3-3-active-visit-flow-with-start-and-complete-validation-report.md (10 fixes applied) -->

## Story

As a **Social Worker** (or **Case Worker**),
I want to start and complete visits with notes,
so that supervisors see progress (FR-8; online visit capture — full FR-11 offline in Story 3.6).

## Acceptance Criteria

1. **Given** I am authenticated as **SocialWorker** or **CaseWorker** (`Policies.FieldWorker`)  
   **When** I `POST /api/v1/visits/{id}/start` for a visit assigned to me with `status = Scheduled`  
   **Then** visit `status` becomes `InProgress`, `started_at_utc` is set to `DateTime.UtcNow`, `updated_at_utc` refreshes  
   **And** response is **200 OK** with envelope `{ data: VisitListItemDto, meta: { requestId } }` (auto-wrapped by `ApiEnvelopeFilter`)  
   **And** `audit_events` records `visit.started` in the **same transaction** (metadata: `{ visitId, caseId }` — no note text, no beneficiary PII)

2. **Given** I attempt to start a visit that is already `InProgress`, `Completed`, assigned to another worker, wrong org, or missing  
   **When** I `POST /api/v1/visits/{id}/start`  
   **Then** **422** for already `InProgress` or `Completed`; **403** for wrong assignee; **404** for missing/wrong org

3. **Given** I am the assigned field worker and visit is `Scheduled` or `InProgress`  
   **When** I `POST /api/v1/visits/{id}/complete` with body `{ "note": "<non-empty text>" }`  
   **Then** visit `status` becomes `Completed`, `completed_at_utc` is set  
   **And** a row is inserted into **`visit_notes`** linked to the visit and case (author = current user, `body_text` = trimmed note, max **4000** chars)  
   **And** parent `cases.visit_count` increments by 1, `cases.next_visit_due_at_utc` is set to **null**, `cases.updated_at_utc` refreshes (same as Story 3.1)  
   **And** response **200 OK** `VisitListItemDto` includes `completionNote` with the saved text  
   **And** visit update, `visit_notes` insert, case counter update, and `audit_events` `visit.completed` occur in a **single** `SaveChangesAsync` (same transaction — no partial complete without note)

3b. **Given** I attempt to complete a visit that is already `Completed`  
    **When** I `POST /api/v1/visits/{id}/complete` with a valid note  
    **Then** **422** Problem Details; no second `visit_notes` row (unique `visit_id` index is a safety net only — business rule rejects first)

4. **Given** I `POST /api/v1/visits/{id}/complete` with missing body, null `note`, or whitespace-only `note`  
   **When** validation runs  
   **Then** **400** Problem Details; no visit/case/note/audit changes

5. **Given** I `POST /api/v1/visits/{id}/complete` with `note` longer than 4000 characters  
   **When** validation runs  
   **Then** **400** Problem Details

6. **Given** I am **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I call `GET /api/v1/cases/{caseId}/visits` after a field worker completes a visit with a note  
   **Then** the visit row shows `status = Completed`, `startedAtUtc` / `completedAtUtc` when set, and `completionNote` with the saved text  
   **And** an `InProgress` visit (started but not completed) shows `status = InProgress` and `startedAtUtc`  
   **And** `lastRescheduleReason` continues to surface when set (Story 3.1 — unchanged)

7. **Given** I tap **Start visit** on a Command Strip card (`TodayScreen`)  
   **When** the visit `status` is `Scheduled`  
   **Then** app calls `POST /api/v1/visits/{id}/start`, then navigates to **Active Visit** screen with the returned visit payload  
   **And** the Start/Continue button shows a loading state and is not double-tappable while the start request is in flight  
   **When** the visit `status` is already `InProgress`  
   **Then** navigate directly to **Active Visit** (no duplicate start call)

8. **Given** I am on the **Active Visit** screen  
   **When** rendered  
   **Then** show case headline `{crimeNumber} · {stNumber}`, domicile meta, visit status chip (`In progress`), and a multiline **Visit note** field (required before complete)  
   **And** primary action **Complete visit** (`#0D6E6E`) disabled until note has non-whitespace text  
   **And** secondary **Reschedule** opens a modal/sheet with future date-time picker + **reason** field (required, max 500) calling existing `POST /api/v1/visits/{id}/reschedule`  
   **And** **Navigate** remains a stub Alert (`"Navigation opens after GPS setup"`) — Story 3.4  
   **And** GPS capture, discreet POCSO header, offline queue, and real sync-chip states remain **out of scope** (Stories 3.4, 3.6–3.8, 3.7)

9. **Given** I tap **Complete visit** with a valid note  
   **When** `POST /api/v1/visits/{id}/complete` succeeds  
   **Then** navigate back to **Today** tab and refresh the Command Strip (`GET /api/v1/visits/today` + cache write)  
   **And** completed visit no longer appears on today's strip (server filters `Scheduled`/`InProgress` only)

9b. **Given** I reschedule successfully from **Active Visit**  
    **When** `POST /api/v1/visits/{id}/reschedule` returns **200**  
    **Then** navigate back to **Today** tab and refresh the Command Strip (visit may move off today's list if rescheduled to a future day)  
    **And** do not leave the user on Active Visit with stale `InProgress` state after status reset to `Scheduled`

10. **Given** start or complete API fails (network/401/422/5xx)  
    **When** on Active Visit  
    **Then** show inline error with retry; do not navigate away or clear the note draft

11. **Given** field-worker visit list DTOs after this story  
    **When** mapping `VisitListItemDto`  
    **Then** include optional `startedAtUtc`, `completedAtUtc`, `completionNote` (null unless applicable) — field-worker today list may omit `visit_notes` join on incomplete rows but **must** include `status` string for button logic (`Scheduled` vs `InProgress`)

11b. **Given** a field worker calls `GET /api/v1/visits/weekly`  
     **When** the list includes `Completed` visits from the current UTC week (Story 3.1 weekly semantics)  
     **Then** each completed row includes `startedAtUtc`, `completedAtUtc`, and `completionNote` when a note was saved  
     **And** incomplete rows (`Scheduled` / `InProgress`) include `startedAtUtc` when set but omit `completionNote`

12. **Given** a visit in `InProgress` is rescheduled via `POST /api/v1/visits/{id}/reschedule`  
    **When** status resets to `Scheduled`  
    **Then** clear `started_at_utc` to **null** (fix deferred 3.1 review item — InProgress start timestamp must not linger after reschedule)

13. **Given** Coordinator/Director attempts `POST /visits/{id}/start`, `POST /visits/{id}/complete`, or field worker attempts `GET /cases/{id}/visits`  
    **When** RBAC is evaluated  
    **Then** **403** with existing policy messages (unchanged from Story 3.1)

14. **Given** integration and mobile test baselines after Stories 3.1–3.2  
    **When** this story ships  
    **Then** new API integration tests in `VisitSchedulerTests.cs` cover at minimum:
    - start `Scheduled` → `InProgress` + `started_at_utc` + audit `visit.started`
    - start **422** when already `InProgress` or `Completed`
    - complete with note inserts `visit_notes`, returns `completionNote`, increments `visit_count`
    - complete **400** without note / whitespace note / note > 4000
    - coordinator `GET cases/{id}/visits` sees `completionNote` and `startedAtUtc` after start+complete
    - weekly list includes `completionNote` + timestamps on completed-this-week rows (AC11b)
    - director `POST /visits/{id}/start` returns **403** (AC13)
    - reschedule from `InProgress` clears `started_at_utc`
    **And** update existing complete tests to send `{ note: "..." }` body  
    **And** new mobile Jest tests cover: Start navigates to Active Visit; Complete disabled until note; successful complete returns to Today; InProgress card opens Active Visit without start call  
    **And** update `CommandStripCard.test.tsx` — replace stub-alert test with **Continue visit** label when `status === 'InProgress'`  
    **And** `TodayScreen.test.tsx` wraps screen in `NavigationContainer` + `TodayStackNavigator` (or test harness) so `useNavigation` / stack `navigate('ActiveVisit')` assertions work after Today tab stack refactor  
    **And** `npm run test:mobile` and API integration suite pass  
    **And** export OpenAPI snapshot then regenerate api-client (Windows):
      `set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json`
      → run integration tests (or API) to write snapshot
      → `set API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json`
      → `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client`

## Tasks / Subtasks

- [x] **Domain — VisitNote entity** (AC: 3, 6)
  - [x] `Domain/Entities/VisitNote.cs` — `Id`, `OrganisationId`, `VisitId`, `CaseId`, `AuthorUserId`, `BodyText`, `CreatedAtUtc`
  - [x] Register `DbSet<VisitNote>` on `AppDbContext`

- [x] **Persistence — EF + migration** (AC: 3, 12)
  - [x] `Infrastructure/Persistence/VisitNoteConfiguration.cs` — table `visit_notes`, snake_case, `body_text` max 4000
  - [x] Unique index on `visit_id` (pilot: one completion note per visit)
  - [x] Migration `AddVisitNotes` — **only** `visit_notes` table (no `case_notes` — Epic 4.1)
  - [x] Follow pilot no-FK pattern from `visits` / `cases`

- [x] **API — DTOs** (AC: 3, 6, 11, 11b)
  - [x] `Models/Visits/VisitDtos.cs` — add `CompleteVisitRequest { string? Note }`, extend `VisitListItemDto` with `DateTime? StartedAtUtc`, `DateTime? CompletedAtUtc`, `string? CompletionNote`
  - [x] Map timestamps and note from visit + optional `visit_notes` join in `ToListItemAsync`
  - [x] Weekly list (`ListWeeklyAsync`): join `visit_notes` for `Completed` rows; today list may skip join on incomplete rows only

- [x] **Service — VisitService** (AC: 1–6, 11b, 12)
  - [x] `StartAsync(Guid visitId)` — `Scheduled` → `InProgress`, set `StartedAtUtc`, audit `visit.started`
  - [x] Extend `CompleteAsync(Guid visitId, CompleteVisitRequest request)` — validate note; insert `VisitNote`; existing case counter logic unchanged; **single** `SaveChangesAsync`
  - [x] Fix `RescheduleAsync` — when resetting to `Scheduled`, set `StartedAtUtc = null`
  - [x] `ListForCaseAsync` / supervisor mapping includes note + timestamps via left join on `visit_notes`
  - [x] `ListWeeklyAsync` — include `visit_notes` join when mapping `Completed` items (AC11b)
  - [x] Add `AuditEventTypes.VisitStarted = "visit.started"`

- [x] **Controller — VisitsController** (AC: 1–5, 13)
  - [x] `POST {id:guid}/start` — `[Authorize(Policy = Policies.FieldWorker)]`
  - [x] `POST {id:guid}/complete` — accept optional `[FromBody] CompleteVisitRequest?` (required body with note per AC4)
  - [x] XML doc comments for OpenAPI

- [x] **Tests — API integration** (AC: 13, 14)
  - [x] Extend `VisitTestData` — `StartVisitAsync`, `SendStartVisitAsync`; update `CompleteVisitAsync` / `SendCompleteVisitAsync` to send `{ note }` body
  - [x] Extend `SeedVisitAsync` — optional `startedAtUtc` when `status = InProgress`
  - [x] Add tests listed in AC14; fix all existing `CompleteVisitAsync` call sites (5 call sites in `VisitSchedulerTests.cs`)
  - [x] `Director_StartVisit_Returns403` — mirror Story 3.1 coordinator 403 on field mutations

- [x] **API client + OpenAPI snapshot** (AC: 14)
  - [x] `EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json` during test run (see `SwaggerEndpointTests.cs`)
  - [x] `API_OPENAPI_FILE` + `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client`
  - [x] Commit `openapi-snapshot.json`, `src/generated/api.ts`, `dist/`

- [x] **Docs** (AC: 14)
  - [x] Update `README.md` visit table — add `POST /visits/{id}/start`, note body on `/complete`, `visit_notes` mention

- [x] **Mobile — Visit API service** (AC: 7, 9, 10)
  - [x] `VisitApiService.startVisit(id)` → `auth.postApi<VisitListItemDto>('/api/v1/visits/{id}/start')` (no body)
  - [x] `VisitApiService.completeVisit(id, note)` → `auth.postApi` with `{ note }` body (camelCase JSON)
  - [x] `VisitApiService.rescheduleVisit(id, scheduledAtUtc, reason)` → `auth.postApi` with `{ scheduledAtUtc, reason }`
  - [x] Extend `visit.models.ts` types from `@midi-kaval/api-client`; mirror `CaseApiService` envelope parsing (`envelope.data`)

- [x] **Mobile — Navigation** (AC: 7, 9, 9b)
  - [x] `TodayStackNavigator` — `TodayHome` + `ActiveVisit` screens; `screenOptions={{ headerShown: true }}` on stack, but `TodayHome` screen option `headerShown: false` (preserve Story 3.2 single title)
  - [x] Update `MainTabNavigator` — Today tab uses `TodayStackNavigator` instead of bare `TodayScreen`; tab-level `headerShown: false` unchanged
  - [x] `navigation/types.ts` — `TodayStackParamList` with `ActiveVisit: { visit: VisitListItemDto }`; update `MainTabParamList.Today` to `NavigatorScreenParams<TodayStackParamList>`

- [x] **Mobile — Active Visit screen** (AC: 8–10)
  - [x] `src/screens/today/ActiveVisitScreen.tsx` — note input, complete, reschedule modal, navigate stub
  - [x] `src/components/RescheduleVisitModal.tsx` (or inline modal) — reason required, future datetime
  - [x] Loading/error states on mutations

- [x] **Mobile — Today screen wiring** (AC: 7, 11)
  - [x] Replace Start stub in `TodayScreen` — call start or navigate for `InProgress`
  - [x] `CommandStripCard` — when `status === 'InProgress'`, primary button label **Continue visit** (same handler)

- [x] **Mobile — Tests** (AC: 14)
  - [x] `ActiveVisitScreen.test.tsx` — note gate, complete success mock, reschedule reason validation, reschedule success navigates back
  - [x] Extend `TodayScreen.test.tsx` — wrap in `NavigationContainer` + `TodayStackNavigator` test harness; mock `visitApiService.startVisit`; assert `navigate('ActiveVisit', { visit })` on Start
  - [x] Optional shared `apps/mobile/__tests__/testNavigation.tsx` — minimal stack wrapper if duplicated across screen tests
  - [x] Update `CommandStripCard.test.tsx` — **Continue visit** label for `InProgress`; remove stub-alert test

### Review Findings

- [x] [Review][Patch] Reschedule modal uses raw ISO text input instead of date-time picker — AC8 requires a future date-time picker; field workers must edit UTC ISO strings manually [`apps/mobile/src/components/RescheduleVisitModal.tsx:43-50`]

- [x] [Review][Patch] Visit note field has no client-side max length — users can type >4000 chars and only discover on API 400; add `maxLength={4000}` on Active Visit note input [`apps/mobile/src/screens/today/ActiveVisitScreen.tsx:90-97`]

- [x] [Review][Patch] Missing `Director_Complete_Returns403` integration test — AC13 requires coordinator/director field mutations return 403; `Director_StartVisit_Returns403` exists but complete is untested [`tests/api.integration/VisitSchedulerTests.cs`]

- [x] [Review][Patch] Missing Active Visit complete-error test — AC10/AC14 require inline error with retry on API failure; UI exists (`errorBlock` + Retry) but no Jest coverage [`apps/mobile/__tests__/ActiveVisitScreen.test.tsx`]

- [x] [Review][Patch] Reschedule modal retains stale reason/datetime when reopened — `useState` in modal is not reset on `visible` false; second open shows prior values [`apps/mobile/src/components/RescheduleVisitModal.tsx:33-34`]

- [x] [Review][Defer] N+1 handoff queries on field-worker list endpoints — `BuildHandoffWhisperAsync` per row; pre-existing from Story 3.1 review [`apps/api/Infrastructure/Visits/VisitService.cs:559-561`] — deferred, pilot list sizes

- [x] [Review][Defer] Transfer leaves stale visit assignee — case reassignment does not update active visits; pre-existing from Story 3.1 [`deferred-work.md`] — deferred, follow-up when transfer+visit lifecycle specified

- [x] [Review][Defer] No DB partial unique index for one active visit per case — app-layer enforcement only; pre-existing from Story 3.1 [`deferred-work.md`] — deferred, hardening pass

## Dev Notes

### Scope boundary (critical)

| In scope (3.3) | Deferred |
|----------------|----------|
| `POST /visits/{id}/start` API + mobile flow | Offline local queue (Story 3.6) |
| `visit_notes` table + required note on complete | Full `case_notes` timeline API (Story 4.1) |
| Active Visit screen (note + complete + reschedule) | GPS / Google Maps (Story 3.4) |
| Supervisor sees note via `GET /cases/{id}/visits` | Real sync chip states (Story 3.7) |
| Fix `started_at_utc` clear on reschedule | Discreet POCSO header (Story 3.8) |

`visit_notes` is a **bridge** table for visit-completion capture. Epic 4.1 will introduce typed case notes (`Visit`, `Court`, etc.) and timeline UI; do **not** build `POST /cases/{id}/notes` or web timeline in this story.

### API contract details

**Start**

```
POST /api/v1/visits/{id}/start
Authorization: Bearer <field worker>
Body: none
→ 200 { data: VisitListItemDto, meta: { requestId } }
```

**Complete** (breaking change from 3.1 — body now required)

```
POST /api/v1/visits/{id}/complete
Body: { "note": "Met family at home. Discussed school attendance." }
→ 200 VisitListItemDto with completionNote set
```

**Reschedule** — unchanged from Story 3.1; mobile must collect `reason` (AC8).

**VisitListItemDto additions**

```csharp
public DateTime? StartedAtUtc { get; set; }
public DateTime? CompletedAtUtc { get; set; }
public string? CompletionNote { get; set; }
```

Supervisor list (`GET /cases/{id}/visits`) loads `completionNote` via left join on `visit_notes`. Field-worker **today** list may skip the join for incomplete visits (performance); **weekly** list must include note + timestamps on `Completed` rows (AC11b). All lists **must** return accurate `status` for UI branching.

**Weekly completed row example**

```json
{
  "id": "...",
  "status": "Completed",
  "startedAtUtc": "2026-06-16T09:05:00Z",
  "completedAtUtc": "2026-06-16T09:45:00Z",
  "completionNote": "Met family at home. Discussed school attendance."
}
```

### Files to read before coding (UPDATE — do not skip)

| File | Current state | This story changes |
|------|---------------|-------------------|
| `apps/api/Infrastructure/Visits/VisitService.cs` | `CompleteAsync` no body; no `StartAsync`; reschedule leaves `StartedAtUtc` | Add start, note on complete, reschedule fix |
| `apps/api/Controllers/V1/VisitsController.cs` | No `/start`; complete has no body param | New route + complete body |
| `apps/api/Models/Visits/VisitDtos.cs` | No `CompleteVisitRequest`; no timestamp/note fields on DTO | Extend DTOs |
| `apps/mobile/src/screens/today/TodayScreen.tsx` | Start → Alert stub | Wire start + navigation |
| `apps/mobile/src/services/visits/VisitApiService.ts` | `listToday()` only | Add start/complete/reschedule |
| `apps/mobile/src/navigation/MainTabNavigator.tsx` | Today tab → `TodayScreen` | Today tab → `TodayStackNavigator` |
| `tests/api.integration/VisitSchedulerTests.cs` | Complete without note | Update + new start/note tests |
| `tests/api.integration/VisitTestData.cs` | `CompleteVisitAsync` no note | Add note param + `StartVisitAsync` |

**Preserve:** Story 3.1 list filters (today = `Scheduled`|`InProgress` only); `isOverdue` overlap; handoff whisper on field lists; coordinator `lastRescheduleReason`; audit-in-same-transaction pattern; `ApiEnvelopeFilter` wrapping.

### Mobile UX (Active Visit)

No dedicated mockup file — follow EXPERIENCE.md **Active visit** IA and Story 3.2 card styling:

- Screen background `#F8FAFC`, card white with `12px` radius, `1px` `#E4E7EC` border
- Headline crime/ST `fontWeight: '600'`, meta domicile `13px` `#475467`
- Note field: multiline `TextInput`, min height ~120px, placeholder `"What happened on this visit?"` (voice: plain language per EXPERIENCE.md)
- Complete button matches Command Strip primary `#0D6E6E`
- Reschedule modal: reason `TextInput` required (mirror API 500-char max); show API error on failure
- Header title: **Active visit** (stack header; Today home keeps `headerShown: false` on tab)

Navigation flow:

```
TodayScreen → (Start visit) → ActiveVisitScreen → (Complete) → TodayScreen + refresh
TodayScreen → (Continue visit, InProgress) → ActiveVisitScreen
```

Pass visit via route params; refresh visit from API on Active Visit mount if needed after reschedule.

### Architecture compliance

- REST `/api/v1`, UUID ids, ISO 8601 UTC JSON, RFC 7807 errors [Source: `architecture.md` §5.3]
- Policy `FieldWorker` on start/complete/reschedule; `CoordinatorOrAbove` on case visits list [Source: Story 3.1]
- `visit_notes` aligns with offline sync scope ("visits, visit notes") for Story 3.6 — design `visit_id` + `created_at_utc` for future idempotent replay
- Near-real-time supervisor visibility via existing `GET /cases/{id}/visits` — no new supervisor endpoint required [Source: FR-8, SM-4]

### Testing standards

**API:** `[Collection("AuthIntegration")]`, reuse `VisitTestData` / `CaseTestData` / `RbacTestData` patterns from Story 3.1. Assert `visit_notes` row in DB via scoped `AppDbContext` where needed.

**Mobile:** Jest + `react-test-renderer` (no Testing Library RN). Mock `visitApiService`, `useNavigation`, `useRoute`. Baseline was **36** tests after Story 3.2 — extend, do not break.

**Navigation test harness:** After `TodayStackNavigator`, `TodayScreen` uses stack navigation — tests that assert `navigate('ActiveVisit')` must wrap the screen in `NavigationContainer` + `TodayStackNavigator` (or a thin `renderTodayScreen()` helper). Without this, `useNavigation` hooks may throw or no-op. See `apps/mobile/__tests__/testNavigation.tsx` if extracted.

### Previous story intelligence (3.1)

- `VisitStatus`: `Scheduled`, `InProgress`, `Completed` — `InProgress` existed but unused until this story
- `CompleteAsync` already increments `cases.visit_count`, clears `next_visit_due_at_utc`
- Deferred review items to address here: **InProgress reschedule must clear `StartedAtUtc`**
- Deferred (still out of scope): transfer/visit assignee drift, DB partial unique index for one active visit, N+1 handoff on lists

### Previous story intelligence (3.2)

- `CommandStripCard` props: `onStartVisit` — replace Alert with navigation
- `VisitApiService` mirrors `CaseApiService` error wrapping (`VisitApiError`)
- Today uses `commandStripCache` — refresh after complete
- `MainTabNavigator` Today tab uses `headerShown: false` — preserve when introducing stack
- Mobile tests: `TodayScreen.test.tsx`, `CommandStripCard.test.tsx` — extend

### Project structure notes

- API visit code lives under `apps/api/Infrastructure/Visits/` and `Controllers/V1/VisitsController.cs`
- Mobile today flow under `apps/mobile/src/screens/today/`
- Do not add web UI for active visit (mobile-only per architecture §5.4)

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.3, FR-8, FR-11]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — visit endpoints, visit_notes offline scope]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Active visit IA, Flow 1]
- [Source: `_bmad-output/implementation-artifacts/3-1-visit-scheduler-api.md` — visit API baseline, deferred start]
- [Source: `_bmad-output/implementation-artifacts/3-2-morning-command-strip-mobile-home.md` — Command Strip, Start stub]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md` — reschedule StartedAtUtc fix]
- [Source: `apps/api/Infrastructure/Visits/VisitService.cs`]
- [Source: `apps/mobile/src/screens/today/TodayScreen.tsx`]
- [Source: `packages/api-client/src/generated/api.ts` — regenerate after OpenAPI]

## Dev Agent Record

### Agent Model Used

Auto (Cursor)

### Debug Log References

### Completion Notes List

- Implemented `POST /api/v1/visits/{id}/start`, required-note `POST /complete`, `visit_notes` table + migration, supervisor/weekly `completionNote` visibility
- Fixed deferred 3.1 reschedule `started_at_utc` clear on InProgress → Scheduled
- Mobile: `TodayStackNavigator`, `ActiveVisitScreen`, `RescheduleVisitModal`, Command Strip start/continue flow with loading guard
- API integration: 31 VisitScheduler tests pass; mobile: 44/44 Jest tests pass
- Code review patches: DateTimePicker reschedule modal, note maxLength, Director complete 403 test, complete-error Jest test, modal state reset
- OpenAPI snapshot + `@midi-kaval/api-client` regenerated

### File List

- apps/api/Domain/Entities/VisitNote.cs
- apps/api/Infrastructure/Persistence/VisitNoteConfiguration.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Migrations/20260616151443_AddVisitNotes.cs
- apps/api/Migrations/20260616151443_AddVisitNotes.Designer.cs
- apps/api/Migrations/AppDbContextModelSnapshot.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Models/Visits/VisitDtos.cs
- apps/api/Infrastructure/Visits/VisitService.cs
- apps/api/Controllers/V1/VisitsController.cs
- tests/api.integration/VisitTestData.cs
- tests/api.integration/VisitSchedulerTests.cs
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- packages/api-client/dist/generated/api.d.ts
- apps/mobile/src/navigation/TodayStackNavigator.tsx
- apps/mobile/src/navigation/MainTabNavigator.tsx
- apps/mobile/src/navigation/types.ts
- apps/mobile/src/services/visits/VisitApiService.ts
- apps/mobile/src/screens/today/TodayScreen.tsx
- apps/mobile/src/screens/today/ActiveVisitScreen.tsx
- apps/mobile/src/components/CommandStripCard.tsx
- apps/mobile/src/components/RescheduleVisitModal.tsx
- apps/mobile/__tests__/TodayScreen.test.tsx
- apps/mobile/__tests__/ActiveVisitScreen.test.tsx
- apps/mobile/__tests__/CommandStripCard.test.tsx
- apps/mobile/package.json
- apps/mobile/jest.setup.js
- README.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
