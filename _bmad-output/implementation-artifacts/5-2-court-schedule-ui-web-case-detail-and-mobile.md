---
baseline_commit: NO_VCS
---

# Story 5.2: Court Schedule UI (Web Case Detail and Mobile)

<!-- Validated: 2026-06-19 — see 5-2-court-schedule-ui-web-case-detail-and-mobile-validation-report.md (10 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Case Worker**,
I want to view and update court sittings on web case detail and mobile Today / case detail,
so that I prepare for court appearances (FR-15, UX-DR9, UX-DR13).

*Scope: **UI only** — consume Story **5.1** APIs. Wire **`CourtCountdownBanner`** + mobile **Court schedule** surface. **No** reminder job (Story 5.3), **no** miss escalation / Crisis Queue rows (Story 5.4), **no** new API endpoints. **Do not** conflate `CaseNoteType.Court` timeline notes with structured `court_sittings`. Online-only (no offline queue) like interventions.*

## Acceptance Criteria

1. **Given** web case detail (`/cases/:id`)  
   **When** the page loads  
   **Then** a **Court sittings** section lists `GET /api/v1/cases/{id}/court-sittings`  
   **And** each row shows `scheduledAtUtc`, `courtName`, `purpose`, status chip (`Upcoming` | `Attended` | `Postponed`), optional notes/outcome  
   **And** past-due styling when `status === 'Upcoming'` and `scheduledAtUtc < now` — **compute client-side** on case detail (`CourtSittingDto` has no `isPastDue`; mirror `CaseInterventionsComponent.isOverdue()`)  
   **And** status chips show visible text labels (`Upcoming`, `Attended`, `Postponed`); past-due adds `Overdue` badge text (UX-DR9 — not color alone)  
   **And** empty state `"No court sittings yet."`, loading, error+retry mirror `CaseInterventionsComponent`

2. **Given** web case detail with case read access  
   **When** I add a court sitting  
   **Then** form captures `scheduledAtUtc`, `courtName`, `purpose`, optional `status` (default `Upcoming`), optional `notes`  
   **And** when `status=Attended` on create, `outcome` is required  
   **And** when `status=Postponed`, past `scheduledAtUtc` is allowed (backfill); when `status=Upcoming`, `scheduledAtUtc` must be in the future (client + API 422)  
   **And** create on `TerminationExclusion` case surfaces API **422** via `extractErrorMessage`  
   **And** submit calls `POST /api/v1/cases/{id}/court-sittings`  
   **And** list refreshes on success; API validation/422 surfaced via `extractErrorMessage`

3. **Given** a court sitting row on web  
   **When** I update fields (`status`, `scheduledAtUtc`, `courtName`, `purpose`, `notes`, `outcome`, `nextCourtAtUtc`)  
   **Then** `PATCH /api/v1/cases/{id}/court-sittings/{sittingId}` is called  
   **And** `Attended` requires non-empty `outcome`; `nextCourtAtUtc` optional  
   **And** past-due critical styling clears when status leaves `Upcoming` or date moves forward

4. **Given** mobile case detail (`CaseDetailPlaceholderScreen`)  
   **When** the screen loads  
   **Then** court sittings list + add/update flows mirror web capabilities (compact layout, status chips)  
   **And** pull-to-refresh reloads court sittings with notes/interventions  
   **And** past-due `Upcoming` rows use critical accent — compute client-side on case detail (`status === 'Upcoming' && scheduledAtUtc < now`); use API `isPastDue` only on upcoming/Court schedule lists

5. **Given** mobile **Today** tab (field worker)  
   **When** upcoming sittings exist (`GET /api/v1/court-sittings/upcoming`)  
   **Then** `CourtCountdownBanner` shows a label for the **nearest** sitting (first item in API order — includes past-due when present), e.g. `"Court sitting Thursday — 2 days"` or `"Court sitting overdue — {courtName}"` when `isPastDue`  
   **And** label refreshes when Today pull-to-refresh runs (same cycle as visit list)  
   **And** when no upcoming sittings, banner hidden (`label={null}`)

6. **Given** mobile **Court schedule** surface (reached from Today header / filter control)  
   **When** I open it  
   **Then** it lists upcoming sittings from `GET /api/v1/court-sittings/upcoming` **filtered client-side to current UTC week** (Mon 00:00 – Sun 23:59:59.999, same week boundary as `GET /visits/weekly`)  
   **And** each row shows date/time, court name, status chip (+ `Overdue` text when `isPastDue`); critical accent styling (UX-DR9)  
   **And** nested `case` summary respects POCSO redaction from API (`beneficiaryName` initials only)  
   **And** tap row navigates to case detail for that `caseId`  
   **And** empty state **"No sittings this week."** when no items in current UTC week (UX-DR13)  
   **And** loading, error + Retry, and pull-to-refresh re-fetch upcoming list  
   **And** non-field-worker roles see explanatory message (upcoming API is field-worker only; mirror Today Command Strip guard)

7. **Given** RBAC (Story 2.8 / 5.1)  
   **When** field worker opens unassigned case court UI  
   **Then** API **403** surfaces as user-visible error (no silent empty list)  
   **And** coordinators/directors on web can manage sittings on any org case via nested endpoints

8. **Given** regression safety  
   **When** story ships  
   **Then** existing visit Command Strip, interventions UI, and notes timeline behave unchanged  
   **And** web + mobile unit tests cover court components/hooks; no API or OpenAPI changes unless a contract bug is found

## Tasks / Subtasks

- [x] **Shared models + API clients** (AC: 1–6)
  - [x] Web `case.models.ts` — `CourtSittingDto`, list/create/update request types, `COURT_SITTING_STATUSES` const from api-client
  - [x] Web `case-api.service.ts` — `listCourtSittings`, `createCourtSitting`, `updateCourtSitting` (mirror intervention methods + envelope unwrap)
  - [x] Mobile `case.models.ts` + `CaseApiService` — same nested CRUD methods
  - [x] Mobile `CourtApiService` (or `services/court/`) — `listUpcomingCourtSittings()` → `GET /api/v1/court-sittings/upcoming`, unwrap `{ data: { items }, meta: { totalCount } }`

- [x] **Web case detail** (AC: 1–3, 7)
  - [x] `case-court-sittings.component.ts/html/scss` — mirror `CaseInterventionsComponent` structure (list, add form, inline update, chips, overdue class)
  - [x] Embed `<app-case-court-sittings [caseId]="caseId" />` in `case-detail-placeholder.component.html` after interventions
  - [x] `case-court-sittings.component.spec.ts` — list/load error, create, patch, overdue styling  
  - [x] Update `case-detail-placeholder.component.spec.ts` — court sittings section renders

- [x] **Mobile case detail** (AC: 4, 7)
  - [x] `CaseDetailPlaceholderScreen.tsx` — court sittings state, load on mount + refresh, add/update forms (status picker, datetime, outcome when Attended, nextCourtAtUtc optional)
  - [x] Reuse chip/status styling patterns from interventions section

- [x] **Mobile Today + court schedule** (AC: 5–6)
  - [x] `useCourtCountdown.ts` — fetch upcoming once on Today mount/refresh; derive banner label from first item
  - [x] `TodayScreen.tsx` — pass label to `CourtCountdownBanner`; add header control "Court this week" → navigate to `CourtSchedule`
  - [x] `CourtScheduleScreen.tsx` — week filter helper, list, empty state, refresh, row tap → case detail
  - [x] `TodayStackNavigator.tsx` + `types.ts` — add `CourtSchedule` route

- [x] **Tests + docs** (AC: 8)
  - [x] Mobile Jest: `useCourtCountdown.test.tsx`, `CourtScheduleScreen.test.tsx`, extend `CaseDetailPlaceholderScreen.test.tsx`, `courtSittingUtils.test.ts`
  - [x] Run `npm test` (web + mobile); document manual smoke in story completion notes

### Review Findings

- [x] [Review][Patch] Court schedule row omits nested case `beneficiaryName` [apps/mobile/src/screens/court/CourtScheduleScreen.tsx:132] — AC 6 requires POCSO-redacted case summary on each row
- [x] [Review][Patch] Mobile case detail past-due uses intervention red `#B42318` not court accent `#B54708` [apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx:1036] — AC 4 / UX-DR9
- [x] [Review][Patch] Mobile Postponed update always sends `nextCourtAtUtc` seeded from `defaultDueDate()` when absent [CaseDetailPlaceholderScreen.tsx:513-553] — AC 3/4 optional next court date
- [x] [Review][Patch] iOS Postponed update cannot edit `nextCourtAtUtc` (no picker affordance) [CaseDetailPlaceholderScreen.tsx Postponed update UI]
- [x] [Review][Patch] Countdown label shows "1 day" for same-calendar-day sittings [apps/mobile/src/utils/courtSittingUtils.ts:25-26] — AC 5 "today" wording
- [x] [Review][Patch] Web spec missing list/load error + retry test [case-court-sittings.component.spec.ts] — AC 8 story task checklist
- [x] [Review][Patch] Court schedule non-field-worker guard untested [CourtScheduleScreen.test.tsx] — AC 6/8
- [x] [Review][Defer] API PATCH cannot clear `nextCourtAtUtc` when field omitted or null (`HasValue` guard in CourtSittingService) — pre-existing 5.1 contract; web `null` send also ineffective
- [x] [Review][Defer] `useCourtCountdown` hides banner on API failure (no error UI) — AC 5 only requires hidden when no sittings; matches lightweight banner pattern

## Dev Notes

### READ FIRST

1. **Story 5.1 API is complete** — nested CRUD on cases + `GET /api/v1/court-sittings/upcoming`. Do **not** add API code unless you discover a contract bug.
2. **Mirror Story 4.5 interventions UI** — web standalone component + mobile inline section; same loading/error/empty/retry patterns.
3. **Story 3.2 placeholder** — `CourtCountdownBanner` already exists; `TodayScreen` currently passes `label={null}`. This story wires `useCourtCountdown()`.
4. **UX-DR9** — status chips: `Upcoming`, `Attended`, `Postponed`; past-due `Upcoming` uses critical accent (match `CommandStripCard` overdue / `interventionOverdue` styles: left border `#B54708` or equivalent).
5. **UX-DR13** — mobile court schedule empty copy is exactly **"No sittings this week."** (not on web case detail list).
6. **Separate from case notes** — `CaseNoteType.Court` is timeline logging; this UI is structured sittings table (FR-15).
7. **No DELETE** — create + update only (API v1).
8. **Jobs deferred** — Story 5.3 reminders, Story 5.4 Crisis Queue court-miss rows.
9. **Online-only** — no sync queue entries for court sittings.

10. **Datetime → UTC** — local datetime pickers convert to ISO UTC strings on API calls (`toISOString()`), same as interventions `dueAtLocal` pattern.

### `isPastDue` — two sources (do not confuse)

| Surface | DTO | Past-due detection |
|---------|-----|-------------------|
| Web/mobile **case detail** nested list | `CourtSittingDto` | Client: `isCourtSittingPastDue(item)` → `status === 'Upcoming' && scheduledAtUtc < now` |
| Mobile **upcoming** / **Court schedule** | `CourtSittingScheduleItemDto` | Use API `isPastDue` field |

### API contracts (Story 5.1 — do not change)

| Endpoint | Use in this story |
|----------|-------------------|
| `GET /api/v1/cases/{id}/court-sittings` | Web + mobile case detail list |
| `POST /api/v1/cases/{id}/court-sittings` | Add sitting forms |
| `PATCH /api/v1/cases/{id}/court-sittings/{sittingId}` | Update sitting |
| `GET /api/v1/court-sittings/upcoming` | Today banner + Court schedule screen (field worker only) |

**Create validation reminders:** `Upcoming` + past `scheduledAtUtc` → 422; `Attended` without `outcome` → 400; terminal case → 422 on create.

**Upcoming item shape:** `CourtSittingScheduleItemDto` includes `isPastDue`, nested `CaseSummaryDto` (POCSO redacted server-side).

### Web implementation sketch

| Action | Path |
|--------|------|
| NEW | `apps/web/src/app/features/cases/court-sittings/case-court-sittings.component.{ts,html,scss,spec.ts}` |
| UPDATE | `apps/web/.../models/case.models.ts`, `services/case-api.service.ts`, `detail/case-detail-placeholder.component.{ts,html}` |

**Reference:** `CaseInterventionsComponent` — signals, `extractErrorMessage`, Material form fields, `formatTimestamp` helper.

**Status chip CSS:** add `.chip-upcoming`, `.chip-attended`, `.chip-postponed`, `.past-due` in component SCSS (mirror intervention `.overdue`).

### Mobile implementation sketch

| Action | Path |
|--------|------|
| NEW | `apps/mobile/src/hooks/useCourtCountdown.ts` |
| NEW | `apps/mobile/src/screens/court/CourtScheduleScreen.tsx` |
| NEW | `apps/mobile/src/services/court/CourtApiService.ts` (upcoming list) |
| UPDATE | `CaseApiService.ts`, `case.models.ts`, `CaseDetailPlaceholderScreen.tsx`, `TodayScreen.tsx`, `TodayStackNavigator.tsx`, `navigation/types.ts` |

**Week filter helper** — align with `VisitService.GetUtcWeekBounds` (Mon 00:00 UTC through Sun 23:59:59.999 UTC):

```typescript
// apps/mobile/src/utils/utcWeekBounds.ts (recommended shared util)
export function getUtcWeekBounds(utcNow = new Date()): { start: Date; end: Date } {
  const day = utcNow.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const start = new Date(Date.UTC(utcNow.getUTCFullYear(), utcNow.getUTCMonth(), utcNow.getUTCDate() + mondayOffset));
  const end = new Date(start.getTime() + 7 * 24 * 60 * 60 * 1000 - 1);
  return { start, end };
}
```

**Navigation:** Court schedule row tap → `navigation.navigate('Cases', { screen: 'CaseDetailPlaceholder', params: { caseId } })` (same cross-tab pattern as `CommandStripCard`).

**`useCourtCountdown` label logic:**

- No items → `null`
- First item `isPastDue` → `"Court sitting overdue — {courtName}"`
- Else format scheduled day + relative days: `"Court sitting {weekday} — {n} days"` (use existing date formatting utilities)

### Previous story intelligence (5.1)

- Integration tests use Testcontainers; UI tests are unit/Jest only.
- Past-due `Upcoming` on **upcoming API** — use `isPastDue` from `CourtSittingScheduleItemDto`.
- Case detail nested list — compute past-due client-side (`CourtSittingDto` has no `isPastDue`).
- Invalid status on create must **400** from API — form should not invent statuses.
- Code review fixed past-due test pattern: create future sitting, backdate in DB — UI does not need DB hacks; rely on API `isPastDue`.
- OpenAPI + api-client already include court types — import from `@midi-kaval/api-client` / `components['schemas']`.

### Previous story intelligence (4.5 interventions UI)

- Web embeds feature component in `case-detail-placeholder`; mobile extends monolithic screen (acceptable — match interventions, do not refactor whole screen).
- Mobile `listFieldWorkers` / envelope: use `envelope.data ?? []` or typed unwrap consistently.
- `extractErrorMessage` / `getApi` error handling on mobile — copy intervention patterns.

### Testing requirements

- **Web:** `case-court-sittings.component.spec.ts` with mocked `CaseApiService` (copy interventions spec structure).
- **Mobile:** render tests for court schedule empty state copy, banner label present/absent, case detail create court sitting API call.
- **Do not** require Docker for UI story completion.
- Manual smoke: coordinator adds sitting on web; field worker sees it on mobile upcoming + case detail after transfer.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 5, Story 5.2]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-15, FR-12]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Court schedule IA, UX-DR9, UX-DR13]
- [Source: `_bmad-output/implementation-artifacts/5-1-court-sitting-crud-api.md` — API contracts]
- [Source: `_bmad-output/implementation-artifacts/4-5-interventions-ui-and-overdue-job.md` — UI patterns]
- [Source: `_bmad-output/implementation-artifacts/3-2-morning-command-strip-mobile-home.md` — `CourtCountdownBanner` + `useCourtCountdown` deferral]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Web `CaseCourtSittingsComponent` on case detail: list/create/update, client-side past-due, status chips with Overdue badge.
- Mobile case detail court sittings section mirrors interventions (load/refresh, add/update, past-due styling).
- `useCourtCountdown` wires `CourtCountdownBanner` on Today; pull-to-refresh re-fetches upcoming.
- `CourtScheduleScreen` filters upcoming API by UTC week bounds; empty copy "No sittings this week."; row tap opens case detail.
- Tests: web 72 passed; mobile 117 passed (`npm run test:web`, `npm run test:mobile`).
- Manual smoke: coordinator adds sitting on web → field worker sees upcoming banner + case detail after API sync.

### File List

- apps/web/src/app/features/cases/models/case.models.ts
- apps/web/src/app/features/cases/services/case-api.service.ts
- apps/web/src/app/features/cases/court-sittings/case-court-sittings.component.ts
- apps/web/src/app/features/cases/court-sittings/case-court-sittings.component.html
- apps/web/src/app/features/cases/court-sittings/case-court-sittings.component.scss
- apps/web/src/app/features/cases/court-sittings/case-court-sittings.component.spec.ts
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.ts
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.html
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.spec.ts
- apps/mobile/src/services/cases/case.models.ts
- apps/mobile/src/services/cases/CaseApiService.ts
- apps/mobile/src/services/court/CourtApiService.ts
- apps/mobile/src/utils/utcWeekBounds.ts
- apps/mobile/src/utils/courtSittingUtils.ts
- apps/mobile/src/hooks/useCourtCountdown.ts
- apps/mobile/src/screens/court/CourtScheduleScreen.tsx
- apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx
- apps/mobile/src/screens/today/TodayScreen.tsx
- apps/mobile/src/navigation/types.ts
- apps/mobile/src/navigation/TodayStackNavigator.tsx
- apps/mobile/__tests__/useCourtCountdown.test.tsx
- apps/mobile/__tests__/CourtScheduleScreen.test.tsx
- apps/mobile/__tests__/courtSittingUtils.test.ts
- apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx
- apps/mobile/__tests__/TodayScreen.test.tsx

## Change Log

- 2026-06-19: Story 5.2 created — court schedule UI (web case detail, mobile Today/case detail, CourtCountdownBanner wiring).
- 2026-06-19: Validation — 10 fixes (`isPastDue` client vs API, terminal/Postponed create UX, Court schedule states, UTC week bounds, accessibility).
- 2026-06-19: Story 5.2 implemented — court schedule UI (web component, mobile case detail, Today banner, Court schedule screen); tests green.
- 2026-06-19: Code review — 7 patches applied (beneficiary summary, court past-due accent, optional nextCourtAtUtc, iOS picker, today label, tests); story done.
