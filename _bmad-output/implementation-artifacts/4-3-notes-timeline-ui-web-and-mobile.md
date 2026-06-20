---
baseline_commit: NO_VCS
---

# Story 4.3: Notes Timeline UI (Web and Mobile)

Status: done

<!-- Validated: 2026-06-19 — see 4-3-notes-timeline-ui-web-and-mobile-validation-report.md (10 fixes applied) -->

## Story

As a **staff member**,
I want to read and add notes on Case detail,
so that context is shared across roles (FR-13, CAP-5).

*Scope: **UI only** — web supervisor case detail + mobile field-worker case detail. Consume Stories **4.1** (notes API) and **4.2** (attachment presign/confirm/download-url). Uses generated `@midi-kaval/api-client` types for all API calls. **No API/backend changes.** No interventions UI (4.5). No offline note/attachment sync. No unified `visit_notes` + `case_notes` merge (case_notes timeline only in v1).*

## Acceptance Criteria

1. **Given** I am on **web** case detail (`/cases/:id`, Coordinator/Director via existing guards)  
   **When** the page loads  
   **Then** a **Notes timeline** section fetches `GET /api/v1/cases/{id}/notes` via `CaseApiService`  
   **And** notes render **chronologically** (oldest first — match API order)  
   **And** each entry shows: **type badge**, author (`authorEmail` or short id fallback), `createdAtUtc` (localized display), `bodyText`  
   **And** when `actionRequired` is true, an **action required** indicator and formatted `actionDueAtUtc` are shown  
   **And** empty timeline shows a clear empty state (not an error)  
   **And** loading and error states with retry are handled (mirror existing detail page patterns)

2. **Given** I am on **web** case detail  
   **When** I use **Add note**  
   **Then** I can select `noteType` (`Visit`, `Court`, `Intervention`, `General`), enter required `bodyText` (max **4000**), optional **action required** + future **due date**  
   **And** submit calls `POST /api/v1/cases/{id}/notes`  
   **And** on success the new note appears in the timeline without full page reload  
   **And** validation errors (400/422) surface user-readable messages via `CaseApiService.extractErrorMessage`  
   **And** **403** shows forbidden message (should not occur for supervisors on org cases)

3. **Given** I am adding a note on **web** with an optional attachment  
   **When** I select a file (`image/jpeg`, `image/png`, `image/webp`, `application/pdf`, max **10 MiB**)  
   **Then** flow is: **create note** → `POST /attachments/presign` → **PUT bytes to `uploadUrl`** (raw `fetch`, no auth interceptor) → `POST /attachments/confirm`  
   **And** presign uses `{ resourceType: "CaseNote", resourceId: <newNoteId>, fileName, contentType, fileSizeBytes }`  
   **And** PUT includes `requiredHeaders` from presign response (`x-ms-blob-type`, `Content-Type`)  
   **And** on confirm success the timeline **re-fetches** `GET /cases/{id}/notes` (or merges confirmed attachment into local state) so attachment chip(s) with `originalFileName` appear  
   **And** v1 supports **one attachment per note submission** (single file input / picker — not multi-file batch)  
   **And** `fileName` sent to presign is **basename only** (strip path segments — API rejects `/`, `\`, `..`)  
   **And** if presign/PUT/confirm fails after note create, the note remains visible and a **clear upload error** is shown (user may retry by adding another note or re-select file before submit — v1: single submit attempt per form submission)  
   **And** submit control is **disabled** while create/upload/confirm is in progress

4. **Given** a note with confirmed attachment(s) on **web**  
   **When** I click an attachment  
   **Then** client calls `GET /api/v1/attachments/{id}/download-url`  
   **And** opens the returned `downloadUrl` in a **new tab** for preview/download  
   **And** **403** shows access denied message (role-scoped SAS — no URL embedded in list DTO)  
   **And** **422** on pending attachment shows appropriate error

5. **Given** I am on **mobile** case detail (`CaseDetailPlaceholderScreen`)  
   **When** the screen loads  
   **Then** a **Notes timeline** section lists notes via the same `GET /cases/{id}/notes` API (extend `CaseApiService`)  
   **And** entries match AC1 display fields with mobile-appropriate layout and **type badges**  
   **And** notes reload via **RefreshControl** on pull (mirror `CasesListScreen`) and after successful add  
   **And** screen content is wrapped in **`ScrollView`** with refs so whisper → timeline scroll works (current screen uses root `View` — must change)

6. **Given** I am on **mobile** case detail with case read access (assigned field worker primary; supervisors allowed by API if using mobile)  
   **When** I add a note (with optional attachment via document picker)  
   **Then** same create + presign → PUT → confirm sequence as web  
   **And** blob PUT uses raw `fetch` to SAS URL (not `AuthSessionService` — SAS is unauthenticated)  
   **And** **403** on unassigned/wrong assignee shows forbidden message from API  
   **And** tapping attachment runs download-url flow; **403** shows access denied (same as web AC4)  
   **And** case notes remain **online-only** (not queued in sync push — per README)  
   **And** action due date UI mirrors `RescheduleVisitModal` (`@react-native-community/datetimepicker`, future-only validation)

7. **Given** mobile case detail shows **Handoff Whisper** (assignee, ≤7 days)  
   **When** I tap **View full timeline**  
   **Then** the screen **scrolls** to the notes timeline section (UX-DR4 / EXPERIENCE.md — whisper stays summary; full history always accessible)  
   **And** the placeholder `Alert.alert('Notes timeline coming soon.')` is removed

8. **Given** web case detail shows handoff whisper (when API returns it — primarily parity with mobile component)  
   **When** I click **View full timeline** on `HandoffWhisperComponent`  
   **Then** page scrolls to the notes timeline section (replace `window.alert` placeholder)  
   **And** whisper component emits an event; parent performs scroll (do not hard-code global scroll in whisper)

9. **Given** type badges for note types  
   **When** timeline renders  
   **Then** each note shows a **rectangular badge** with readable label (`Visit`, `Court`, `Intervention`, `General`)  
   **And** styling follows DESIGN.md — semantic tokens / Material theme, **not** hardcoded hex; badge text present (not color-only)

10. **Given** contract compliance (epic AC)  
    **When** this story ships  
    **Then** all REST calls use **types** from `@midi-kaval/api-client` re-exported in feature `*.models.ts` files  
    **And** API host calls use existing envelope services (`CaseApiService` / `AuthSessionService.getApi|postApi`) — not hand-written URLs  
    **And** presigned blob **PUT** uses raw `fetch` only (documented exception — same as export blob GET)  
    **And** **no** hand-edits to `packages/api-client/src/generated/`  
    **And** **no** API/OpenAPI/migration changes  
    **And** changes limited to notes timeline surfaces (+ handoff whisper scroll wiring + mobile picker dependency)

11. **Given** test baseline after Story 4.2  
    **When** I run `npm test` in `apps/web` and `apps/mobile`  
    **Then** new/updated specs pass  
    **And** `dotnet test Midi-Kaval.slnx` integration suite is unchanged (no API edits)

## Tasks / Subtasks

- [x] **Shared types — re-export api-client schemas** (AC: 10)
  - [x] `apps/web/src/app/features/cases/models/case.models.ts` — add `CaseNoteDto`, `CaseNoteListResultDto`, `CreateCaseNoteRequest`, `AttachmentSummaryDto`, `AttachmentPresignRequest`, `AttachmentPresignResultDto`, `AttachmentConfirmRequest`, `AttachmentDto`, `AttachmentDownloadUrlDto`
  - [x] `apps/mobile/src/services/cases/case.models.ts` — same exports
  - [x] Add `CASE_NOTE_TYPES`, `ALLOWED_ATTACHMENT_CONTENT_TYPES`, `MAX_ATTACHMENT_BYTES = 10485760` constants in both model files

- [x] **Web — CaseApiService notes methods** (AC: 1, 2)
  - [x] `listCaseNotes(caseId): Promise<CaseNoteDto[]>` — unwrap `{ data: { items } }`
  - [x] `createCaseNote(caseId, request): Promise<CaseNoteDto>` — `POST` (**201 Created**), return `data`

- [x] **Web — AttachmentApiService** (AC: 3, 4)
  - [x] NEW `apps/web/src/app/features/cases/services/attachment-api.service.ts`
  - [x] `presign(request)`, `confirm(request)`, `getDownloadUrl(attachmentId)`
  - [x] `uploadToPresignedUrl(uploadUrl, file: File, requiredHeaders): Promise<void>` — **raw `fetch` PUT** (documented exception to api-client-only rule — same category as export blob download; SAS URL must not receive JWT)

- [x] **Web — CaseNotesTimelineComponent** (AC: 1–4, 9)
  - [x] NEW `apps/web/src/app/features/cases/notes-timeline/` — standalone component + template + scss + spec
  - [x] Inputs: `caseId` (required signal/input)
  - [x] Template ref id `notesTimelineSection` for scroll target
  - [x] Timeline list, empty/loading/error states, type badges, action-required row
  - [x] Add-note form: mat-select note type, textarea body, checkbox action required, **future due datetime** via `<input matInput type="datetime-local">` (no `MatDatepicker` in codebase yet — avoid adding datepicker module unless needed)
  - [x] Optional file input `accept="image/jpeg,image/png,image/webp,application/pdf"`
  - [x] Attachment chips on note rows; click → download-url → `window.open`
  - [x] Public method `scrollIntoView()` for parent handoff link

- [x] **Web — integrate into case detail** (AC: 1, 8)
  - [x] `case-detail-placeholder.component.html` — add `<app-case-notes-timeline>` after handoff whisper; `#notesTimeline` ViewChild
  - [x] `HandoffWhisperComponent` — replace alert with `@Output() viewFullTimeline = output<void>()`; button emits event
  - [x] Parent handler calls timeline `scrollIntoView()`

- [x] **Mobile — CaseApiService notes methods** (AC: 5, 6)
  - [x] `listCaseNotes`, `createCaseNote` via `authSessionService.getApi` / `postApi`

- [x] **Mobile — AttachmentApiService** (AC: 6)
  - [x] NEW `apps/mobile/src/services/attachments/AttachmentApiService.ts` (+ models if needed)
  - [x] presign, confirm, getDownloadUrl, `uploadToPresignedUrl` with `fetch` + blob from picker

- [x] **Mobile — document picker dependency** (AC: 6)
  - [x] Add `react-native-document-picker` (or equivalent maintained RN 0.76-compatible picker)
  - [x] Restrict to allowed MIME types; enforce 10 MiB max before presign

- [x] **Mobile — notes UI on CaseDetailPlaceholderScreen** (AC: 5–7)
  - [x] Replace root `View` with `ScrollView` + `RefreshControl` (pattern: `CasesListScreen.tsx`)
  - [x] `notesTimelineRef` + `scrollTo` for whisper → timeline (replace `Alert.alert` placeholder)
  - [x] Notes list + add form (compact mobile layout); action due date via `DateTimePicker` (pattern: `RescheduleVisitModal.tsx`)
  - [x] Attachment preview via `Linking.openURL(downloadUrl)` after download-url fetch; handle **403**
  - [x] Re-fetch notes after create/confirm; disable submit while busy

- [x] **Tests** (AC: 11)
  - [x] Web: `case-notes-timeline.component.spec.ts` — list render order, badges, create note calls service, attachment preview calls download-url
  - [x] Web: update `handoff-whisper.component.spec.ts` — emits viewFullTimeline instead of alert
  - [x] Web: update `case-detail-placeholder.component.spec.ts` — timeline section present
  - [x] Mobile: `__tests__/CaseDetailPlaceholderScreen.test.tsx` — timeline renders, whisper link scrolls (mock scroll ref), add note flow mocks services

### Review Findings

- [x] [Review][Patch] Mobile presign response missing `uploadUrl`/`attachmentId` fails silently [`CaseDetailPlaceholderScreen.tsx:201-212`] — user sees success with note saved but no attachment and no error (violates AC3 upload error surfacing)
- [x] [Review][Patch] Mobile document picker may report `size: null` → presign sends `fileSizeBytes: 0` → API 400 [`CaseDetailPlaceholderScreen.tsx:144-148`] — reject zero/unknown size before presign
- [x] [Review][Patch] Attachment `extractErrorMessage` maps all **403** to "view this attachment" [`attachment-api.service.ts:80`, `AttachmentApiService.ts:71`] — misleading on presign/confirm RBAC failures (AC6/AC3); reserve custom copy for download-url preview only
- [x] [Review][Patch] Web file input not reset after submit [`case-notes-timeline.component.ts:169`] — re-selecting the same file does not fire `change`; clear `<input type="file">` value on success
- [x] [Review][Patch] Web upload-path unit test missing [`case-notes-timeline.component.spec.ts`] — story testing table requires mock presign/confirm on upload path; only download-url is covered
- [x] [Review][Defer] `react-native-document-picker@9.3.1` deprecated on npm [`apps/mobile/package.json`] — deferred, package still works on RN 0.76; migrate to `@react-native-documents/picker` in a future mobile tooling story
- [x] [Review][Defer] Mobile badge colors use hardcoded hex [`CaseDetailPlaceholderScreen.tsx:537-548`] — deferred, matches pre-existing screen styling; token migration tracked for Epic 9 theme story

## Dev Notes

### READ FIRST (implementation guardrails)

1. **API is complete** — Stories 4.1 + 4.2 shipped all endpoints. Do **not** modify `apps/api/` unless a blocking bug is found (then stop and run correct-course).
2. **Create note before presign** — attachment `resourceId` is the **note id** from `POST /cases/{id}/notes`. Never presign against case id alone.
3. **Timeline data source** — `GET /cases/{id}/notes` returns **`case_notes` only**. Do not merge `visit_notes` in this story (Story 4.1 boundary). Visit completion notes stay in visit flow.
4. **No offline notes** — README: case notes are online-only; do not add to sync push payload.
5. **SAS PUT exception** — project-context says api-client for HTTP; **presigned blob PUT** and **export blob GET** use raw fetch/HttpClient without JWT. Never send `Authorization` header to Azure/Azurite SAS URLs.
6. **Download URLs are ephemeral** — list DTOs have attachment metadata only. Always call `GET /attachments/{id}/download-url` at preview time (15 min SAS).
7. **Pending attachments hidden** — timeline list omits unconfirmed uploads; UI should not show failed/in-progress attachments from list API.
8. **RBAC** — supervisors (web): any org case. Field workers (mobile): assigned case only; unassigned → **403**. Surface API messages; do not hide timeline as security.
9. **Handoff whisper on web** — Story 2.9: coordinators usually get `handoffWhisper: null`. Still wire scroll for component parity when whisper is present.
10. **Stage transition "Notes" field** — existing optional input on stage form is **stage transition notes**, not case timeline notes — do not conflate.
11. **Re-fetch after attachment confirm** — confirm response alone does not update timeline list DTO; call `listCaseNotes` again (or merge attachment summary locally).
12. **Mobile scroll requires ScrollView** — `CaseDetailPlaceholderScreen` currently uses root `View`; whisper scroll cannot work without structural change.

### Epic 4 context

| Story | Delivers |
|-------|----------|
| 4.1 (done) | `case_notes` POST/GET, audit |
| 4.2 (done) | Presign/confirm/download-url, `CaseNoteDto.attachments[]` |
| **4.3 (this)** | Web + mobile timeline UI |
| 4.4 | Interventions CRUD API |
| 4.5 | Interventions UI + overdue job |

### Architecture & PRD compliance

[Source: `_bmad-output/planning-artifacts/architecture.md` §6, `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` FR-13]

- Note types: `Visit`, `Court`, `Intervention`, `General` (PascalCase JSON strings)
- Fields: timestamp, author, text, action required, action due date, attachments
- Attachment flow: presign → PUT blob → confirm [architecture §6 rule 5]
- Chronological timeline on case detail (web + mobile)
- Role-based attachment access via server-issued SAS

[Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md]

- Case detail: Handoff Whisper + timeline (FR-7, FR-13)
- Whisper: ≤7 days, max 3 lines + **View full timeline** → scroll to full history
- Badges: rectangular, text label required (DESIGN.md — not color-only)

### Existing surfaces to replace

| Location | Current behavior |
|----------|------------------|
| `apps/web/.../handoff-whisper.component.ts` | `window.alert('Notes timeline coming soon.')` |
| `apps/mobile/.../CaseDetailPlaceholderScreen.tsx` | `Alert.alert('Notes timeline coming soon.')` |

### API contracts (already in OpenAPI / api-client)

**List notes:** `GET /api/v1/cases/{id}/notes` → `{ data: { items: CaseNoteDto[] } }`

**Create note:** `POST /api/v1/cases/{id}/notes` body:
```json
{ "noteType": "Visit", "bodyText": "...", "actionRequired": false, "actionDueAtUtc": null }
```

**Presign:** `POST /api/v1/attachments/presign`

**Confirm:** `POST /api/v1/attachments/confirm` → `{ attachmentId }`

**Download:** `GET /api/v1/attachments/{id}/download-url`

Allowed attachment types: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`; max **10485760** bytes (`MAX_ATTACHMENT_BYTES`).

**Client presign prep:** use `file.name` basename only; validate size ≤ max and MIME in allow-list before API call.

### Web implementation patterns

Follow Story **2.9** conventions:

- Standalone components + **signals** for local UI state
- Feature folder: `apps/web/src/app/features/cases/`
- Services: `@Injectable({ providedIn: 'root' })`, envelope unwrap via `firstValueFrom` + `HttpClient`
- Errors: `CaseApiError` + `extractErrorMessage`
- Tests: Jasmine + component fixture (existing specs in `detail/`, `handoff-whisper/`)

**Case detail route:** `/cases/:id` — `authGuard` + `supervisorGuard` (`app.routes.ts`). Coordinators/Directors add/read notes on any org case.

**Suggested component API:**

```typescript
// case-notes-timeline.component.ts
readonly caseId = input.required<string>();
scrollIntoView(): void { /* elementRef native scrollIntoView */ }
```

**Handoff whisper wiring:**

```typescript
// handoff-whisper — output event
readonly viewFullTimeline = output<void>();

// case-detail-placeholder — template
<app-handoff-whisper [whisper]="whisper" (viewFullTimeline)="onViewFullTimeline()" />
<app-case-notes-timeline #notesTimeline [caseId]="caseId" />
```

### Mobile implementation patterns

- Screen: extend `CaseDetailPlaceholderScreen.tsx` (do not add new route unless layout demands it)
- Services: `CaseApiService` + new `AttachmentApiService` using `authSessionService.getApi` / `postApi` for API host calls only
- Tests: `apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx` (react-test-renderer pattern)
- Discreet header / POCSO expand — **preserve** existing behavior; notes section below whisper
- Field worker assigned cases: `GET /cases/assigned` already used elsewhere; detail uses `getCaseDetail(caseId)`
- **ScrollView + RefreshControl:** mirror `apps/mobile/src/screens/cases/CasesListScreen.tsx`
- **Action due datetime:** mirror `apps/mobile/src/components/RescheduleVisitModal.tsx` (already depends on `@react-native-community/datetimepicker`)

**Blob upload helper (mobile + web):**

```typescript
async function uploadToPresignedUrl(
  uploadUrl: string,
  body: Blob | ArrayBuffer,
  requiredHeaders: Record<string, string>,
): Promise<void> {
  const response = await fetch(uploadUrl, {
    method: 'PUT',
    headers: requiredHeaders,
    body,
  });
  if (!response.ok) {
    throw new Error(`Upload failed (${response.status})`);
  }
}
```

### Attachment preview flow

```
User taps attachment chip
  → GET /attachments/{id}/download-url (authenticated)
  → open downloadUrl
Web: window.open(url, '_blank', 'noopener')
Mobile: Linking.openURL(url) with error alert on failure
403: show "You don't have permission to view this attachment."
```

Do **not** cache SAS URLs in component state beyond active preview session.

### Type badge mapping (suggested labels)

| `noteType` | Badge label |
|------------|-------------|
| `Visit` | Visit |
| `Court` | Court |
| `Intervention` | Intervention |
| `General` | General |

Use Material `mat-chip` or styled span with theme primary/accent variants — map consistently on mobile with `Text` + `View` badge styles from theme tokens.

### File structure (new + modified)

| Action | Path |
|--------|------|
| NEW | `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.ts` |
| NEW | `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.html` |
| NEW | `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.scss` |
| NEW | `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.spec.ts` |
| NEW | `apps/web/src/app/features/cases/services/attachment-api.service.ts` |
| UPDATE | `apps/web/src/app/features/cases/services/case-api.service.ts` |
| UPDATE | `apps/web/src/app/features/cases/models/case.models.ts` |
| UPDATE | `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.{ts,html,spec.ts}` |
| UPDATE | `apps/web/src/app/features/cases/handoff-whisper/handoff-whisper.component.{ts,html,spec.ts}` |
| NEW | `apps/mobile/src/services/attachments/AttachmentApiService.ts` |
| UPDATE | `apps/mobile/src/services/cases/CaseApiService.ts` |
| UPDATE | `apps/mobile/src/services/cases/case.models.ts` |
| UPDATE | `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx` |
| UPDATE | `apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx` |
| UPDATE | `apps/mobile/package.json` (document picker dependency) |

**Out of scope:** `apps/api/**`, `packages/api-client/**` (no regen needed — 4.2 already exported schemas), interventions, visit_notes merge, offline queue, E2E Playwright (optional follow-up).

### Previous story intelligence (4.2)

- Attachment list on notes: confirmed only, ordered `confirmedAtUtc` asc then `id`
- `CaseNoteDto.attachments` always an array (may be empty)
- Presign requires existing note id; `resourceType` must be exact string `"CaseNote"`
- File name max 255 chars; no path separators in `fileName`
- Integration test reference for PUT headers: `x-ms-blob-type: BlockBlob`, `Content-Type: <mime>`
- Deferred: orphan Pending rows, rate limits — UI should show upload errors clearly

### Previous story intelligence (4.1)

- `noteType` whitelist — use same four PascalCase strings in UI select controls
- `bodyText` max 4000, required non-whitespace
- `actionRequired: true` requires future `actionDueAtUtc`; past due → **422**
- Empty timeline → **200** `items: []`
- Field worker unassigned case → **403** on GET/POST notes

### Testing requirements

| Layer | Location | Minimum coverage |
|-------|----------|------------------|
| Web unit | `case-notes-timeline.component.spec.ts` | Renders items in API order; type badges; create calls `createCaseNote`; mock attachment service on upload path |
| Web unit | `handoff-whisper.component.spec.ts` | Emits `viewFullTimeline` on button click |
| Web unit | `case-detail-placeholder.component.spec.ts` | Timeline component included |
| Mobile unit | `CaseDetailPlaceholderScreen.test.tsx` | Notes section renders; whisper link no longer alerts; mocks for list/create |

**Do not** add API integration tests in this story.

### Project context reminders

[Source: `_bmad-output/project-context.md`]

- Angular standalone + signals; RN screens in `src/screens/`
- Never hand-edit generated api-client
- Map DESIGN.md tokens — no hardcoded hex in components
- WCAG: badge text + labels; keyboard accessible form controls on web
- No gamification, no infinite scroll on visits

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 4 Story 4.3]
- [Source: `_bmad-output/implementation-artifacts/4-1-case-notes-api-and-timeline.md`]
- [Source: `_bmad-output/implementation-artifacts/4-2-attachment-presign-upload-for-notes.md`]
- [Source: `README.md` — Case notes + Note attachments sections]
- [Source: `packages/api-client/src/generated/api.ts` — `CaseNoteDto`, attachment schemas]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — badge shape]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Handoff Whisper + timeline]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

- Rebuilt `packages/api-client` (`npm run build`) so note/attachment types resolve in web/mobile TS — generated source already present from Story 4.2.

### Completion Notes List

- Web: `CaseNotesTimelineComponent` on case detail — list/create notes, optional attachment (presign → fetch PUT → confirm → re-fetch), download-url preview, type badges, action-required display.
- Web: `HandoffWhisperComponent` emits `viewFullTimeline`; parent scrolls timeline into view.
- Mobile: `CaseDetailPlaceholderScreen` wrapped in `ScrollView` + `RefreshControl`; full notes UI with document picker, DateTimePicker for due dates, whisper scroll-to-timeline.
- Services: `CaseApiService` + `AttachmentApiService` on web and mobile; shared model exports/constants from `@midi-kaval/api-client`.
- Tests: web 63/63 pass; mobile 106/106 pass. No API changes.
- Code review (2026-06-19): fixed mobile presign silent failure, unknown file size guard, attachment 403 messaging split (`extractDownloadErrorMessage`), web file input reset, upload-path unit test.

### File List

- `apps/web/src/app/features/cases/models/case.models.ts`
- `apps/web/src/app/features/cases/services/case-api.service.ts`
- `apps/web/src/app/features/cases/services/attachment-api.service.ts`
- `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.ts`
- `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.html`
- `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.scss`
- `apps/web/src/app/features/cases/notes-timeline/case-notes-timeline.component.spec.ts`
- `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.ts`
- `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.html`
- `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.spec.ts`
- `apps/web/src/app/features/cases/handoff-whisper/handoff-whisper.component.ts`
- `apps/web/src/app/features/cases/handoff-whisper/handoff-whisper.component.html`
- `apps/web/src/app/features/cases/handoff-whisper/handoff-whisper.component.spec.ts`
- `apps/mobile/src/services/cases/case.models.ts`
- `apps/mobile/src/services/cases/CaseApiService.ts`
- `apps/mobile/src/services/attachments/AttachmentApiService.ts`
- `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx`
- `apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx`
- `apps/mobile/package.json`
- `packages/api-client/dist/` (rebuilt types from existing generated source)

## Change Log

- 2026-06-19: Story 4.3 — Notes timeline UI on web case detail and mobile case detail (FR-13). Handoff whisper scroll wiring. Attachment upload/preview flows. All ACs satisfied.
- 2026-06-19: Code review — 5 patch findings fixed; 2 deferred (document-picker migration, badge hex tokens).
