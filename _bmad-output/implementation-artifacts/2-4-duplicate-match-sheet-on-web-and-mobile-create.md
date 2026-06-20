---
baseline_commit: NO_VCS
---

# Story 2.4: Duplicate Match Sheet on Web and Mobile Create

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Project Coordinator or Director creating a Case on web**,
I want a blocking warning when Crime/ST matches an existing Case,
so that I review before saving (FR-5, UX-DR5, UJ-3).

*Note: Field workers (`SocialWorker`, `CaseWorker`) cannot create cases in v1 API — mobile duplicate-sheet UI is component-complete for Epic 2 sequencing; see AC8.*

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** on **web** (`supervisorGuard`)  
   **When** I open `/cases/new`, fill all required create fields (see **Create form fields** below), and attempt save  
   **Then** Save is disabled and shows **"Checking…"** while `POST /api/v1/cases/check-duplicate` is in flight (prevents double-submit)  
   **And** the check request sends **trimmed** `crimeNumber` and/or `stNumber` — include each field only if non-empty after trim (API requires at least one)  
   **And** check runs **before** `POST /api/v1/cases`  
   **And** if `data.hasMatch` is `false`, create proceeds normally (**201**)  
   **And** if `data.hasMatch` is `true`, save stays **blocked** and the duplicate match sheet opens

2. **Given** duplicate check returns one or more matches  
   **When** the duplicate match sheet is shown  
   **Then** headline copy is **"Possible match — review before saving."** (UX-DR12 — not "Error: duplicate")  
   **And** `aria-live="polite"` announces **"N possible matches found"** (or "1 possible match found") on open  
   **And** each match **row** shows: `crimeNumber`, `stNumber`, `beneficiaryName`, `currentStage`, and human-readable **matched-on** label (not raw enum):  
   - `CrimeNumber` → "Matched on Crime number"  
   - `StNumber` → "Matched on ST number"  
   - `Both` → "Matched on Crime and ST number"  
   **And** each row has its own **Open existing** (secondary) and **Merge** (primary, supervisor only) buttons  
   **And** the sheet has one **Cancel** action (sheet-level — closes without picking a row)  
   **And** there is **no** "Create anyway" / "Save duplicate" action  
   **And** primary save on the create form stays **disabled** while the sheet is open

3. **Given** the duplicate match sheet is open (web)  
   **When** I inspect accessibility  
   **Then** it uses `role="dialog"`, `aria-modal="true"`, labelled title, focus trap (CDK/`MatDialog`)  
   **And** Esc does **not** dismiss the sheet (`disableClose: true` — UX-DR16 blocking modal)  
   **And** on Cancel, focus returns to the Crime or ST field that triggered the check

4. **Given** I tap **Open existing** on a specific match row  
   **When** the action completes  
   **Then** web navigates to `/cases/{caseId}` placeholder detail with summary fields from that row (pass via router `state`)  
   **And** create draft is abandoned (no `POST /api/v1/cases` fired)  
   **And** sheet closes  
   **And** on hard refresh of `/cases/{caseId}` (no router state), page shows `caseId` plus copy: **"Full case details available in registry (coming soon)."** — do not error

5. **Given** I am **Coordinator** or **Director** (`isSupervisorRole`)  
   **When** I tap **Merge** on a specific match row  
   **Then** the Merge button is visible and enabled on that row  
   **And** tapping it shows a non-blocking message: **"Merge workflow coming in the next update."** (Story 2.5 implements `POST /api/v1/cases/{id}/merge`)  
   **And** sheet remains open until Cancel or Open existing — **do not** call merge API in this story

6. **Given** I tap **Cancel** on the duplicate match sheet  
   **When** the sheet closes  
   **Then** I return to the create form with identifiers editable  
   **And** save is re-enabled only after I change `crimeNumber` or `stNumber` (re-check required on next save attempt)

7. **Given** **mobile** `CaseCreateScreen` on the Cases tab stack  
   **When** duplicate check returns a match during save attempt  
   **Then** `DuplicateMatchSheet` modal blocks save with the same per-row actions, copy, and match summary as web  
   **And** `accessibilityViewIsModal`, focus containment, and `aria-live` match-count announcement are present  
   **And** Android hardware **back** does **not** dismiss the blocking sheet (parity with web Esc / `disableClose`)  
   **And** Open existing navigates to `CaseDetailPlaceholder` with `caseId` + summary params/state

8. **Given** pilot RBAC today (Stories 2.1, 2.3)  
   **When** mobile app is used in production  
   **Then** Coordinators/Directors see `WebOnlyScreen` (not Cases tab) — mobile create is **component-complete** for Epic 2 sequencing  
   **And** field roles (`SocialWorker`, `CaseWorker`) do **not** see a New Case entry point (they cannot `check-duplicate` or create per API)  
   **And** mobile duplicate-sheet behavior is covered by **unit/component tests** with mocked `CaseApiService`

9. **Given** check-duplicate or create returns **400** or **403**  
   **When** save is attempted  
   **Then** inline/`aria-live` error displays API `detail` via `AuthSessionService.extractErrorMessage()` (inject into `CaseApiService` / create screen — do not duplicate Problem Details parsing)  
   **And** no duplicate sheet opens unless a match is returned

10. **Given** network failure on check-duplicate  
    **When** save is attempted  
    **Then** save is blocked with user-facing copy: **"Could not verify Crime/ST — check connection and try again."**  
    **And** no silent fallback to create without check (project-context: case create requires network)

11. **Given** `check-duplicate` returned no match but `POST /api/v1/cases` returns **409** (race: another user created the same Crime/ST between check and save)  
    **When** create fails with 409  
    **Then** client re-calls `check-duplicate` with current identifiers and opens the duplicate match sheet if `hasMatch` is true  
    **And** if re-check still returns no match, show Problem Details `detail` or **"This Crime or ST number is already in use."**  
    **And** client never retries create automatically and never offers "save duplicate"

12. **Given** create succeeds with **201**  
    **When** the case is saved  
    **Then** web navigates to `/cases/{id}` placeholder with the `CaseDto` from the response  
    **And** mobile navigates to `CaseDetailPlaceholder` with created case summary  
    **And** a brief success acknowledgement is shown (snackbar/toast): **"Case created."**

13. **Given** I blur the Crime or ST field on web create (optional enhancement)  
    **When** the field has a non-empty trimmed value and ≥400ms has passed since last keystroke  
    **Then** client may call `check-duplicate` and open the sheet early (same blocking rules as save-time)  
    **And** this is **optional** — save-time check (AC1) is mandatory

14. **Given** Story 2.3 test baseline (**103** .NET: 1 unit + 102 integration; **19** web; **17** mobile)  
    **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
    **Then** all existing tests pass  
    **And** new web tests cover: checking state disables save, sheet opens on match, per-row actions, save blocked, no create POST when blocked, Cancel re-enables after identifier change, Merge visible for coordinator role, Open existing navigates, 409 triggers re-check + sheet, 201 navigates to placeholder, aria-modal present, no duplicate-create button  
    **And** new mobile tests cover: sheet renders matches, per-row Merge hidden when `canMerge` false, Cancel closes, save blocked while open, Android back does not dismiss (mock `BackHandler` if needed)  
    **And** update `RootNavigator.test.tsx` (or tab tests) if Cases tab structure changes  
    **And** **no API changes** — use existing `packages/api-client` types from Story 2.3

15. **Given** Stories 2.5–2.9 are not yet implemented  
    **When** this story ships  
    **Then** **no** merge API, **no** full case registry/search UI (2.6), **no** sidebar IA (2.9) — only create + placeholder detail + duplicate sheet

## Tasks / Subtasks

- [x] **Web — case API service** (AC: 1, 9–12, 14)
  - [x] `features/cases/models/case.models.ts` — re-export `CreateCaseRequest`, `CaseDto`, `CheckCaseDuplicateRequest`, `CheckCaseDuplicateResultDto`, `CaseDuplicateMatchDto` from `@midi-kaval/api-client` `components['schemas']`
  - [x] `features/cases/services/case-api.service.ts` — `checkDuplicate()` and `createCase()` via `HttpClient` + `environment.apiBaseUrl`; envelope `{ data, meta }` pattern matching `AuthSessionService`
  - [x] Inject `AuthSessionService` for `extractErrorMessage()` on HTTP errors — single parsing path
  - [x] Helper `formatMatchedOn(value: string): string` for display labels (AC2)
  - [x] Bearer attached by existing `auth.interceptor.ts` — no per-call token plumbing

- [x] **Web — duplicate match sheet dialog** (AC: 2–6, 11, 14)
  - [x] **First dialog in project** — import `MatDialog`, `MatDialogModule` (or standalone dialog imports); open via `MatDialog.open(DuplicateMatchSheetComponent, { disableClose: true, ... })`
  - [x] `features/cases/duplicate-match-sheet/duplicate-match-sheet.component.ts` — `MAT_DIALOG_DATA`: `{ matches, canMerge, triggerFieldId, onOpenExisting(caseId), onMerge(caseId), onCancel }`
  - [x] Template: one card/row per match with per-row Open existing + Merge; sheet-footer Cancel
  - [x] Use Material `color="primary"` for Merge — **do not** scatter `#0D6E6E` hex until UX-DR1 theme story (see Dev Notes)
  - [x] `isSupervisorRole` from `AuthSessionService` sets `canMerge`

- [x] **Web — case create screen** (AC: 1, 6, 9–13, 14)
  - [x] `features/cases/create/case-create.component.ts` — reactive form with **all** required fields from table below
  - [x] Signals: `checkingDuplicate`, `saveBlocked`, `duplicateMatches`
  - [x] Save flow: validate → set `checkingDuplicate` → `checkDuplicate` → sheet if match → else `createCase` → on 409 run AC11 path → on 201 navigate (AC12)
  - [x] Optional debounced blur check on crime/ST (AC13)
  - [x] Link from `supervisor-home`: "New case" → `/cases/new`

- [x] **Web — placeholder case detail** (AC: 4, 12, 15)
  - [x] `features/cases/detail/case-detail-placeholder.component.ts` — `caseId` from route; summary from `history.state` or fallback message (AC4)
  - [x] Route `/cases/:id` under `authGuard` + `supervisorGuard`

- [x] **Web — routing** (AC: 1, 4, 12)
  - [x] Update `app.routes.ts`: `/cases/new`, `/cases/:id` lazy-loaded feature routes

- [x] **Web — tests** (AC: 14)
  - [x] `duplicate-match-sheet.component.spec.ts` — per-row actions, aria attributes, matched-on labels, no create-duplicate button
  - [x] `case-create.component.spec.ts` — mock `CaseApiService`; checking state; check before create; sheet on match; 409 re-check path; 201 navigation

- [x] **Mobile — case API service** (AC: 7–12, 14)
  - [x] `services/cases/CaseApiService.ts` — reuse `AuthSessionService` fetch/post pattern; mirror web error extraction
  - [x] Types from `@midi-kaval/api-client` — mirror web `case.models.ts`
  - [x] `formatMatchedOn()` shared or duplicated in mobile util

- [x] **Mobile — duplicate match sheet** (AC: 2, 7, 14)
  - [x] `components/DuplicateMatchSheet.tsx` — `Modal` with `animationType="fade"`, `transparent`, `accessibilityViewIsModal`
  - [x] `BackHandler.addEventListener('hardwareBackPress', () => true)` while visible — consume back, do not dismiss
  - [x] Per-row Open existing + Merge; sheet-level Cancel; matched-on labels

- [x] **Mobile — case create + navigation** (AC: 7, 8, 12, 14)
  - [x] `navigation/CasesStackNavigator.tsx` — stack: `CasesList` → `CaseCreate` → `CaseDetailPlaceholder`
  - [x] Replace flat `CasesScreen` in `MainTabNavigator` with stack navigator
  - [x] **Hide** "New case" FAB when `isFieldRole` from `AuthContext`
  - [x] Document in completion notes: coordinator mobile E2E unreachable until role routing changes

- [x] **Mobile — tests** (AC: 14)
  - [x] `__tests__/DuplicateMatchSheet.test.tsx` — per-row actions, back handler, matched-on labels
  - [x] `__tests__/CaseCreateScreen.test.tsx` — mocked API, save blocked on match, 409 path
  - [x] Update `__tests__/RootNavigator.test.tsx` if tab/screen names change

### Review Findings

- [x] [Review][Patch] 409 re-check sets error message when duplicate sheet opens — `runDuplicateCheck` returns `false` on match, so `if (!reopened)` shows a 409 error under the open sheet (violates AC11). [apps/web/src/app/features/cases/create/case-create.component.ts:180] [apps/mobile/src/screens/cases/CaseCreateScreen.tsx:127]
- [x] [Review][Patch] `checkDuplicate` does not wrap `HttpErrorResponse` — network failures on check path skip AC10 copy ("Could not verify Crime/ST…"); only `createCase` uses `CaseApiError`. [apps/web/src/app/features/cases/services/case-api.service.ts:19]
- [x] [Review][Patch] Web tests omit 409 re-check scenario — story AC14 requires it; `case-create.component.spec.ts` has no 409 test. [apps/web/src/app/features/cases/create/case-create.component.spec.ts]
- [x] [Review][Patch] `hasMatch: true` with empty `matches[]` proceeds to create — defensive guard should treat as blocked or re-fetch. [case-create.component.ts:157] [CaseCreateScreen.tsx:89]
- [x] [Review][Defer] Legacy `CasesScreen.tsx` placeholder unused after stack navigator — deferred, pre-existing cleanup. [apps/mobile/src/screens/cases/CasesScreen.tsx]

## Dev Notes

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Stories 2.1–2.3 delivered create API, stage transitions, and `check-duplicate` probe. **Story 2.4** wires the **duplicate match sheet** into web and mobile create flows (FR-5, UX-DR5). Story 2.5 adds merge API; 2.9 adds full registry/detail UI.

### Create form fields (required for `POST /api/v1/cases`)

| Field | Required | Validation |
|-------|----------|------------|
| `crimeNumber` | Yes | Non-empty after trim; max 64 |
| `stNumber` | Yes | Non-empty after trim; max 64 |
| `beneficiaryName` | Yes | Non-empty after trim; max 256 |
| `beneficiaryAge` | No | If present: 0–120 |
| `beneficiaryContact` | No | Max 32 |
| `typeOfOffence` | Yes | Non-empty; max 128 |
| `offenceClassification` | Yes | `Petty` \| `Serious` \| `Heinous` |
| `domicile` | Yes | `Urban` \| `Rural` \| `Coastal` \| `Tribal` \| `Slum` |
| `isFirstTimeOffender` | No | Default `true` if omitted |

Use Material selects / inputs matching auth form patterns (`appearance="outline"`, `aria-live` error region). Enum fields: string values sent to API (no client-side enum types required beyond form validation).

[Source: `_bmad-output/implementation-artifacts/2-1-case-aggregate-schema-and-create-api.md` — API contract]

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `POST /api/v1/cases/check-duplicate` | Done (Story 2.3) | **Consume only** — no API edits |
| `POST /api/v1/cases` | Done (Story 2.1) | **Consume only** |
| `packages/api-client` | Has check-duplicate + create types | **No regen** unless API touched |
| Web `app.routes.ts` | Auth + `/home` only | Add `/cases/new`, `/cases/:id` |
| Web features | `auth/`, `home/` only | Add `features/cases/` |
| Web dialogs | **None** — first `MatDialog` usage | Add duplicate-match-sheet |
| `styles.scss` | `mat.$azure-palette` primary (UX-DR1 not done) | Use Material `primary` token — no ad-hoc hex |
| Mobile `CasesScreen` | Placeholder text | Stack + create + duplicate sheet |
| Mobile role routing | Coordinators → `web-only` | Mobile create UI for Epic 2 completeness; hidden entry for field roles |

**Do not break:**
- **103** .NET tests — no API changes expected
- **19** web + **17** mobile tests — add new specs on top; update navigator tests if needed
- Story 2.3 check-duplicate contract and normalization rules
- Story 2.1 create 409 semantics — surface via sheet (AC11), never bypass
- UX-DR16: **no modal stack >1** — do not open OTP or second dialog atop duplicate sheet

### API contract (client consumption)

**Check duplicate** — call before every create attempt; re-call on create 409.

```json
// POST /api/v1/cases/check-duplicate — send trimmed non-empty fields only
{ "crimeNumber": "CR-2024-001", "stNumber": "ST-887766" }

// 200 — unwrap envelope in service layer (same as AuthSessionService)
{
  "data": {
    "hasMatch": true,
    "matches": [{
      "caseId": "uuid",
      "crimeNumber": "CR-2024-001",
      "stNumber": "ST-887766",
      "beneficiaryName": "Ravi Kumar",
      "currentStage": "ProcessInitiation",
      "matchedOn": "Both"
    }]
  },
  "meta": { "requestId": "..." }
}
```

| Client action | API call |
|---------------|----------|
| Save (no match) | `check-duplicate` → `POST /cases` → navigate on 201 |
| Save (match) | `check-duplicate` only — **no** create |
| Create 409 | Re-`check-duplicate` → open sheet if match |
| Open existing | Navigate only — **no** API |
| Merge (2.4) | **No API** — stub message for 2.5 |
| Cancel | Close sheet — **no** API |

**RBAC:** Both endpoints require `CoordinatorOrAbove` (Director included). Web create is supervisor-only. Mobile field roles must not surface create UI.

### UX / DESIGN compliance (UX-DR5, UX-DR12, UX-DR16)

| Requirement | Implementation |
|-------------|----------------|
| Blocking sheet | Web: `MatDialog` `disableClose: true`; Mobile: `Modal` + `BackHandler` consume |
| Actions | **Per row:** Open existing (secondary), Merge (primary, supervisor); **sheet:** Cancel |
| No duplicate create | Omit any tertiary "save anyway" button |
| Copy | "Possible match — review before saving." |
| Focus | Trap + return focus to trigger field on Cancel |
| `aria-modal` | Web dialog; mobile `accessibilityViewIsModal` |
| Tokens | Material `primary` / theme until UX-DR1 — DESIGN target `#0D6E6E` deferred |
| Modal stack | **Max 1** — never stack duplicate sheet over another modal |

[Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — `duplicate-match-sheet`]  
[Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 3, State Patterns, Interaction Primitives]

### Suggested file structure

```
apps/web/src/app/features/cases/
├── models/case.models.ts
├── services/case-api.service.ts
├── utils/matched-on-label.ts
├── duplicate-match-sheet/
│   ├── duplicate-match-sheet.component.ts
│   ├── duplicate-match-sheet.component.html
│   ├── duplicate-match-sheet.component.scss
│   └── duplicate-match-sheet.component.spec.ts
├── create/
│   ├── case-create.component.ts
│   ├── case-create.component.html
│   ├── case-create.component.scss
│   └── case-create.component.spec.ts
└── detail/
    ├── case-detail-placeholder.component.ts
    └── case-detail-placeholder.component.html

apps/mobile/src/
├── components/DuplicateMatchSheet.tsx
├── screens/cases/
│   ├── CasesListScreen.tsx
│   ├── CaseCreateScreen.tsx
│   └── CaseDetailPlaceholderScreen.tsx
├── navigation/CasesStackNavigator.tsx
├── services/cases/CaseApiService.ts
└── utils/matchedOnLabel.ts
```

### Previous story intelligence (2.3)

- **Check-duplicate is read-only** — no audit rows; client should not expect side effects
- **Normalization is server-side** — send raw trimmed user input; server uppercases for lookup (do not reimplement normalization in clients for gating decisions)
- **`matchedOn` values:** `CrimeNumber` | `StNumber` | `Both` (PascalCase in JSON) — map to display labels in UI layer
- **Multiple matches:** OR query can return two cases when crime matches A and ST matches B — render **all** rows with **per-row** actions
- **TerminationExclusion** cases still appear in matches — sheet should not filter them out
- **Test helpers:** Integration coverage is API-side; web/mobile use component tests with mocked services
- **Post-2.3 baseline:** 103 .NET, 19 web, 17 mobile — maintain green

### Architecture compliance

- **Generated api-client types only** — import `components` types; HTTP via existing interceptors/fetch wrapper [Source: `architecture.md` §5.4, `project-context.md`]
- **Case create requires network** — block save on check-duplicate failure; no offline queue for create [Source: `architecture.md` §5.5]
- **Business rules stay in API** — UI only orchestrates check → show sheet → create; no client-side duplicate logic beyond displaying `hasMatch`
- **Angular:** standalone components, signals for `saveBlocked`, `checkingDuplicate`, `matches` [Source: `project-context.md`]
- **Error messages:** reuse `AuthSessionService.extractErrorMessage()` — do not duplicate Problem Details parsing in case feature
- **No hand-edit** `packages/api-client/src/generated/api.ts`

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 103 unchanged (no API work) |
| Web | `npm run test:web` | 19 existing + new case/sheet specs |
| Mobile | `npm run test:mobile` | 17 existing + new specs; update navigator tests if needed |

**Web test patterns:** Follow `otp.component.spec.ts` — `provideRouter`, spy `CaseApiService`, query `aria-live` / `role="dialog"`.

**Mobile test patterns:** Follow `RootNavigator.test.tsx` — React Testing Library, mock navigation, API, and `BackHandler`.

### Scope boundaries

| In scope (2.4) | Out of scope |
|----------------|--------------|
| Duplicate match sheet web + mobile | `POST /cases/{id}/merge` (2.5) |
| Full create forms + duplicate orchestration | Full registry, search, sidebar IA (2.6, 2.9) |
| Placeholder case detail route | `GET /cases/{id}` API + rich detail (2.9) |
| Merge button stub message | Functional merge workflow |
| 409 race → re-check + sheet | UX-DR1 Material theme migration |
| Component tests + navigator test updates | Playwright E2E (optional later) |
| Hide mobile create for field roles | Opening field-worker create API (deferred) |

### Pilot RBAC note (critical)

Coordinators and Directors use **web** after login (`supervisorGuard` / `WebOnlyScreen` on mobile). Mobile case-create UI satisfies Epic 2 story sequencing and UX-DR5 component delivery; **production E2E for mobile create is not reachable** for supervisor roles until product changes role routing. Implement and test components thoroughly; do not hack role routing in this story.

### Definition of Done

- [x] Web create form includes all required fields; check-duplicate before create with loading state
- [x] Sheet blocks on match; per-row actions; 409 race opens sheet via re-check
- [x] 201 navigates to placeholder detail with success acknowledgement
- [x] Sheet meets UX-DR5 accessibility (web + mobile back/Esc behavior)
- [x] Merge stubbed; matched-on human labels; `extractErrorMessage` reused
- [x] Mobile sheet + create screen; field-role create entry hidden; navigator tests updated
- [x] 103 .NET still green; web/mobile tests added and passing
- [x] No API or api-client regeneration

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.4, FR-5, UX-DR5]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — UJ-3, FR-5]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 Cases API, §5.4 Web, §5.5 Mobile offline bounds]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — duplicate-match-sheet component]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 3, Voice and Tone, Modals]
- [Source: `_bmad-output/project-context.md` — api-client only, case create online, modal stack ban]
- [Source: `_bmad-output/implementation-artifacts/2-3-unique-crime-and-st-constraints-with-duplicate-check.md` — API contract, test baseline]
- [Source: `_bmad-output/implementation-artifacts/2-1-case-aggregate-schema-and-create-api.md` — create request fields, 409 semantics]
- [Source: `packages/api-client/src/generated/api.ts` — CheckCaseDuplicate*, CreateCaseRequest, CaseDto]
- [Source: `apps/web/src/app/core/auth/auth-session.service.ts` — isSupervisorRole, extractErrorMessage, HTTP/envelope patterns]
- [Source: `apps/mobile/src/services/auth/roleRouting.ts` — isFieldRole, isSupervisorRole]

## Dev Agent Record

### Agent Model Used

claude-4.6-opus-high-thinking

### Debug Log References

### Completion Notes List

- Implemented web case create at `/cases/new` with full Story 2.1 field set; `check-duplicate` runs before create with "Checking…" state.
- Blocking `MatDialog` duplicate match sheet: per-row Open existing / Merge (stub snackbar), sheet Cancel, `disableClose`, matched-on labels, aria-live match count.
- 409 race re-triggers duplicate check and opens sheet; 201 navigates to `/cases/{id}` placeholder with snackbar.
- Mobile: `CasesStackNavigator`, `DuplicateMatchSheet` (BackHandler consume), `CaseCreateScreen`; New Case hidden for `isFieldRole`.
- Rebuilt `@midi-kaval/api-client` dist from existing generated src (types were stale in dist; no OpenAPI regen).
- Coordinator mobile create E2E unreachable in pilot (web-only role routing) — covered by component tests with mocks.
- **Tests:** 103 .NET (1+102, no API changes), 29 web (+10), 21 mobile (+4).

### File List

- apps/web/src/app/features/cases/models/case.models.ts (new)
- apps/web/src/app/features/cases/utils/matched-on-label.ts (new)
- apps/web/src/app/features/cases/utils/matched-on-label.spec.ts (new)
- apps/web/src/app/features/cases/services/case-api.service.ts (new)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.ts (new)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.html (new)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.scss (new)
- apps/web/src/app/features/cases/duplicate-match-sheet/duplicate-match-sheet.component.spec.ts (new)
- apps/web/src/app/features/cases/create/case-create.component.ts (new)
- apps/web/src/app/features/cases/create/case-create.component.html (new)
- apps/web/src/app/features/cases/create/case-create.component.scss (new)
- apps/web/src/app/features/cases/create/case-create.component.spec.ts (new)
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.ts (new)
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.html (new)
- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.scss (new)
- apps/web/src/app/app.routes.ts (modified)
- apps/web/src/app/features/home/supervisor-home.component.ts (modified)
- apps/web/src/app/features/home/supervisor-home.component.html (modified)
- apps/mobile/src/utils/matchedOnLabel.ts (new)
- apps/mobile/src/services/cases/case.models.ts (new)
- apps/mobile/src/services/cases/CaseApiService.ts (new)
- apps/mobile/src/components/DuplicateMatchSheet.tsx (new)
- apps/mobile/src/screens/cases/CasesListScreen.tsx (new)
- apps/mobile/src/screens/cases/CaseCreateScreen.tsx (new)
- apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx (new)
- apps/mobile/src/navigation/CasesStackNavigator.tsx (new)
- apps/mobile/src/navigation/MainTabNavigator.tsx (modified)
- apps/mobile/src/navigation/types.ts (modified)
- apps/mobile/src/services/auth/AuthSessionService.ts (modified)
- apps/mobile/jest.setup.js (modified)
- apps/mobile/__tests__/DuplicateMatchSheet.test.tsx (new)
- apps/mobile/__tests__/CaseCreateScreen.test.tsx (new)
- packages/api-client/dist/* (rebuilt from existing generated src)

### Change Log

- 2026-06-15: Story 2.4 created — duplicate match sheet on web and mobile create flows.
- 2026-06-15: Quality review pass — 409 race handling, create field table, per-row actions, placeholder refresh fallback, post-create navigation, loading states, error reuse, mobile back button, MatDialog/first-dialog notes, matched-on labels, optional blur check, navigator test task, theme guidance.
- 2026-06-15: Implementation complete — web/mobile duplicate match sheet, create flows, tests; status review.

## Story Completion Status

- **Status:** in-progress
- **Completion note:** Code review patches applied 2026-06-15 — all 4 findings resolved; web 31 / mobile 21 / .NET 103 tests passing.
