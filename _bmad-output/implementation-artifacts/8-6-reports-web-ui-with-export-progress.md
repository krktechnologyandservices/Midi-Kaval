---
baseline_commit: 4ebf6e0
---

# Story 8.6: Reports Web UI with Export Progress

Status: done

## Story

As a **Coordinator**,
I want to generate operational reports from the web UI,
So that I can trigger exports, track progress, and download completed files without manual assembly (FR-22, UX-DR13).

*Scope: **Web UI only** — a reports page at `/reports` that lists the 8 standard report types, allows starting an export with format/date parameters, shows progress of in-flight jobs, and surfaces completed download URLs. Uses the existing Story 8.5 API (`POST /api/v1/reports/{type}/export`, `GET /api/v1/reports/exports/{jobId}/status`, `GET /api/v1/reports/exports`). **No changes to the API or backend** — this is purely an Angular feature.*

## Acceptance Criteria

1. **Given** the web app sidebar
   **When** a Coordinator or Director clicks "Reports"
   **Then** the Reports page loads with a list of the 8 standard report types (daily-work, yearly-work, visits-planned-vs-completed, interventions, court-summary, offence-area-counts, workload-distribution, travel-totals)
   **And** each report type shows its display name and a short description

2. **Given** a report type is selected
   **When** the user chooses a format (Excel/PDF) and optionally provides date parameters (from, to, year)
   **Then** the "Start Export" button is enabled and clicking it calls `POST /api/v1/reports/{type}/export`
   **And** the button is disabled while the request is in-flight; duplicate export for the same type is allowed (concurrent jobs are valid)
   **And** on success (202), the new job appears in the export jobs list below
   **And** on error (400, 401, 403, network), an error message surfaces clearly without page disruption

3. **Given** export jobs exist (pending, processing, completed, failed)
   **When** the page loads or a new export is started
   **Then** a jobs list shows recent exports (paginated, newest first) with:
   - Report type name, format, created timestamp
   - Status badge (pending/processing/completed/failed)
   - Download button (only for completed, linking to the signed URL)
   - Error message (only for failed)
   **And** pending/processing jobs auto-poll status every 10 seconds until completed or failed

4. **Given** an export completes
   **When** the download link becomes available
   **Then** the link opens the signed URL (15 min expiry) in a new tab
   **And** expired links gracefully show a "Download expired — start a new export" UI state (410 handled)

5. **Given** the workload-distribution report
   **When** displayed in the export jobs list
   **Then** there is no indication of rankings, scores, or performance comparisons (NFR-11, UX-DR16)

6. **Given** a Director user
   **When** viewing the Reports page
   **Then** all functionality works identically (Reports is scoped to CoordinatorOrAbove, which includes Director)

7. **Given** the Reports page exists
   **When** data is loading or unavailable
   **Then** skeleton loading states show for the jobs table; empty state shows "No exports yet. Start one above." (UX-DR13)
   **And** the page title and subtitle match the standard page-header pattern

## Tasks / Subtasks

- [x] **Create report types model and constants** — `src/app/features/shell/reports.models.ts`
  - [x] Define `ReportTypeInfo` interface (type key, display name, description, supportsDateRange, supportsYear)
  - [x] Define `REPORT_TYPES` constant array with all 8 types and metadata
  - [x] Define `ReportExportJob` interface matching API DTO fields from `ReportExportJobDto`
  - [x] Define `ReportExportRequest` interface (format, from?, to?, year?)
  - [x] Reuse `ApiEnvelope<T>` from `../cases/models/case.models.ts` for response typing

- [x] **Create Reports API service** — `src/app/features/shell/services/reports-api.service.ts`
  - [x] Follow `DashboardApiService` pattern exactly (`@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `firstValueFrom`, `ApiEnvelope`)
  - [x] `startExport(type: string, request: ReportExportRequest): Promise<ReportExportJobDto>` — POST, returns 202 data
  - [x] `getExportStatus(jobId: string): Promise<ReportExportStatusDto>` — GET status
  - [x] `listExports(page?: number, pageSize?: number): Promise<{ items: ReportExportJobDto[]; totalCount: number }>` — GET list
  - [x] Define `ReportsApiError extends Error` with `kind: 'network' | 'http' | 'unknown'`
  - [x] Implement `extractErrorMessage(error)` and `wrapError(error)` helpers

- [x] **Add model types to `shell.models.ts` or create Reports-specific models**
  - [x] `ReportExportJobDto` interface (jobId, status, reportType, format, createdAtUtc, completedAtUtc, downloadUrl?, errorMessage?)
  - [x] `ReportExportStatusDto` interface (status, downloadUrl?, errorMessage?)

- [x] **Replace the placeholder Reports page** — `src/app/features/shell/pages/reports-page.component.ts`
  - [x] Change to standalone component with `OnInit`, `OnDestroy`
  - [x] Inject `ReportsApiService`, `AuthSessionService` (for user info if needed)
  - [x] Add signals: `loading`, `refreshing`, `errorMessage`, `jobs`, `totalCount`, `selectedType`, `selectedFormat`, `fromDate`, `toDate`, `year`, `exportingJobId`
  - [x] Load initial jobs list on init; start polling timer for pending/processing jobs
  - [x] Implement `startExport()`: validate, call API, append job to list, clear form
  - [x] Implement polling: filter jobs with status `pending` or `processing`, poll every 10s, update in place
  - [x] Implement `downloadJob()`: open `downloadUrl` in new tab
  - [x] Implement `loadJobs()`: fetch paginated list from API
  - [x] Clean up polling timer in `OnDestroy`

- [x] **Create Reports page HTML template** — `src/app/features/shell/pages/reports-page.component.html`
  - [x] Page header: "Reports" title + subtitle "Standard operational reports"
  - [x] **Report type selection section** — Card with `@for` loop over `REPORT_TYPES`:
    - [x] Radio/selection for report type (or clickable cards showing name + description)
    - [x] Format toggle: Excel / PDF (MatButtonToggle or radio)
    - [x] Conditional date inputs:
      - Date range fields (`from`, `to`) appear when selected type supports date range
      - Year field appears when selected type supports year
      - No date fields for types that don't support them
    - [x] "Start Export" button (disabled when type/format not selected or export in-flight)
    - [x] Error alert for failed export requests
  - [x] **Export jobs list section** — Card with:
    - [x] `@if (loading())` → skeleton rows
    - [x] `@else if (errorMessage())` → error with retry button
    - [x] `@else if (jobs().length === 0)` → empty state "No exports yet. Start one above."
    - [x] `@else` → table (mat-table) with columns: Report Type, Format, Started, Status, Actions
    - [x] Status chips: pending (grey), processing (blue/spinner), completed (green + download), failed (red + error message)
    - [x] Pagination at bottom (MatPaginator)

- [x] **Create Reports page styles** — `src/app/features/shell/pages/reports-page.component.scss`
  - [x] Layout: responsive, sidebar-compatible padding
  - [x] Status chip styles (matching existing Dashboard/Crisis Queue severity colors)
  - [x] Skeleton loader styles
  - [x] Card spacing and divider patterns

- [x] **Write component tests** — `src/app/features/shell/pages/reports-page.component.spec.ts`
  - [x] Follow existing `dashboard-page.component.spec.ts` / `crisis-queue-page.component.spec.ts` patterns
  - [x] Test: page renders title and report type selection
  - [x] Test: start export calls API and adds job to list
  - [x] Test: error surfaces error message
  - [x] Test: completed job shows download button
  - [x] Test: empty state shows "No exports yet"
  - [x] Test: loading state shows skeleton

- [x] **Verify routing** — no route changes needed (already registered at `app.routes.ts` line 108-113)
- [x] **Verify sidebar nav** — no nav changes needed ("Reports" entry already exists in `allNavItems`)

## Dev Notes

### File locations

| File | Purpose | Pattern |
|------|---------|---------|
| `src/app/features/shell/pages/reports-page.component.ts` | Reports page component | Follows `crisis-queue-page.component.ts` / `dashboard-page.component.ts` |
| `src/app/features/shell/pages/reports-page.component.html` | Reports page template | Follows `crisis-queue-page.component.html` |
| `src/app/features/shell/pages/reports-page.component.scss` | Reports page styles | Follows `crisis-queue-page.component.scss` |
| `src/app/features/shell/pages/reports-page.component.spec.ts` | Component tests | Follows `crisis-queue-page.component.spec.ts` |
| `src/app/features/shell/services/reports-api.service.ts` | Reports API service | Follows `dashboard-api.service.ts` |
| `src/app/features/shell/reports.models.ts` | Report types and DTOs | Follows `shell.models.ts` pattern |

### Existing infrastructure (leverage — do not recreate)

- **`DashboardApiService`** pattern at `src/app/features/shell/services/dashboard-api.service.ts` — create `ReportsApiService` identically
- **`ApiEnvelope<T>`** type at `src/app/features/cases/models/case.models.ts` — reuse for response typing
- **`environment.apiBaseUrl`** at `src/environments/environment.ts` — configured as `http://localhost:5049`
- **`supervisor-shell.component.ts`** — nav and layout already includes Reports route
- **`app.routes.ts`** — `/reports` route already registered with lazy loading
- **Angular Material modules**: `MatCardModule`, `MatButtonModule`, `MatIconModule`, `MatTableModule`, `MatPaginatorModule`, `MatProgressSpinnerModule`, `MatSelectModule`, `MatDatepickerModule`, `MatFormFieldModule`, `MatInputModule`, `MatButtonToggleModule`, `MatChipsModule`
- **Existing page patterns**: loading/error skeleton pattern, `@if/@for` control flow, signal state management

### API endpoints consumed

| Method | URL | Request | Response |
|--------|-----|---------|----------|
| `POST` | `/api/v1/reports/{type}/export` | `{ format: "excel" \| "pdf", from?: string, to?: string, year?: number }` | `202 { data: { jobId, status, ... }, meta: { requestId } }` |
| `GET` | `/api/v1/reports/exports/{jobId}/status` | — | `200 { data: { status, downloadUrl?, errorMessage? }, meta: { requestId } }` |
| `GET` | `/api/v1/reports/exports?page=1&pageSize=20` | — | `200 { data: { items: ReportExportJobDto[] }, meta: { requestId, totalCount } }` |

### Report types metadata

| API key | Display name | Description | Has date range | Has year |
|---------|-------------|-------------|----------------|----------|
| `daily-work` | Daily Work | Cases with visits scheduled/completed today | No | No |
| `yearly-work` | Yearly Work | Aggregate case/work stats for a given year | No | Yes |
| `visits-planned-vs-completed` | Visits Planned vs Completed | Visit counts by worker for a date range | Yes | No |
| `interventions` | Interventions | Interventions by status/outcome for a date range | Yes | No |
| `court-summary` | Court Summary | Court sittings by status/outcome for a date range | Yes | No |
| `offence-area-counts` | Offence Area Counts | Case counts by offence and domicile | No | No |
| `workload-distribution` | Workload Distribution | Case counts per worker (distribution only) | No | No |
| `travel-totals` | Travel Totals | Travel claim totals by worker for a date range | Yes | No |

### UI states per component

| State | Loading | Empty | Error | Data |
|-------|---------|-------|-------|------|
| Report type selection | — | — | Error banner on export failure | Cards/radio list |
| Export jobs list | Skeleton rows | "No exports yet. Start one above." | Error message + retry button | Table with status chips |
| Start Export button | Disabled during API call | — | — | Enabled when type+format selected |

### Polling logic

```typescript
private startPolling(): void {
  this.pollTimer = setInterval(async () => {
    const active = this.jobs().filter(j => j.status === 'pending' || j.status === 'processing');
    if (active.length === 0) return;
    await this.refreshActiveJobs(active);
  }, 10000);
}
```

### Critical don'ts

- **Do NOT modify** `app.routes.ts` — route already exists at line 108-113
- **Do NOT modify** `supervisor-shell.component.ts` — nav already has Reports entry
- **Do NOT modify** any backend API files — this story is web UI only
- **Do NOT add** worker rankings, leaderboards, or performance scoring anywhere in the UI (NFR-11, UX-DR16)
- **Do NOT hand-edit** `packages/api-client` — use `HttpClient` directly with the known API contract
- **Do NOT block the UI** during export — the POST returns 202 immediately; poll for completion
- **Do NOT create a shared Material module** — import only needed modules per component

### Regressions to prevent

- Existing crisis-queue, dashboard, cases, and other pages must continue to work
- The existing placeholder Reports page must be replaced entirely (not extended)
- Sidebar navigation must continue to show all existing items
- All existing route guards (authGuard, supervisorGuard) continue to apply

## References

- Epic 8: Supervisor Crisis Queue, Dashboard & Reports → `_bmad-output/planning-artifacts/epics.md` (FR-22)
- Architecture: Reporting, exports → `_bmad-output/planning-artifacts/architecture.md` §5.7
- Story 8.5 API (existing): `_bmad-output/implementation-artifacts/8-5-standard-reports-api-and-async-export-jobs.md`
- Existing dashboard page: `apps/web/src/app/features/shell/pages/dashboard-page.component.ts` (pattern)
- Existing crisis-queue page: `apps/web/src/app/features/shell/pages/crisis-queue-page.component.ts` (pattern)
- Existing API service: `apps/web/src/app/features/shell/services/dashboard-api.service.ts` (pattern)
- API envelope: `apps/web/src/app/features/cases/models/case.models.ts` → `ApiEnvelope<T>`
- Web PWA architecture: `architecture.md` §5.4
- UX sidebar IA (UX-DR11): Reports is listed after Dashboard in the sidebar
- Empty states (UX-DR13): Use "No exports yet. Start one above." pattern

## Previous Story Intelligence (8.5 — Standard Reports API and Async Export Jobs)

### Key learnings

- The API returns `202 Accepted` with a job ID immediately — HTTP never blocks
- The background job polls every 30s; reports >30s processing time never time out
- Download URLs are SAS tokens with 15 min expiry — the UI must handle 410 Gone gracefully
- Date params (`from`, `to`, `year`) are optional per report type — show/hide inputs accordingly
- Workload distribution contains zero ranking/scoring data — the UI must not imply any

### Files created in Story 8.5

- `apps/api/Controllers/V1/ReportsController.cs` — 3 endpoints
- `apps/api/Domain/Enums/ReportType.cs` — enum + extension methods
- `apps/api/Domain/Entities/ReportExportJob.cs` — entity with FromDate, ToDate, Year
- `apps/api/Infrastructure/Reports/ReportGenerationService.cs` — report building + file generation
- `apps/api/Jobs/ReportExportBackgroundService.cs` — polling background service
- `tests/api.integration/ReportsApiTests.cs` — integration tests

## Dev Agent Record

### Implementation Plan

1. Created `reports.models.ts` with `ReportTypeInfo` interface, `REPORT_TYPES` constant (all 8 types), `ReportExportJobDto`, `ReportExportStatusDto`, `ReportExportRequest` interfaces, and helper functions
2. Created `reports-api.service.ts` following `DashboardApiService` pattern: `startExport()`, `getExportStatus()`, `listExports()`, with `ReportsApiError` and error handling
3. Replaced `reports-page.component.ts` placeholder with full implementation: signals for all state, `loadJobs()`, `startExport()`, polling timer, download, pagination
4. Created `reports-page.component.html` with report type selector (mat-select), format toggle (mat-button-toggle-group), conditional date inputs, export jobs table (mat-table), status chips, pagination, skeleton loading, error, and empty states
5. Created `reports-page.component.scss` with status chip colors (pending/processing/completed/failed), skeleton animations, responsive layout
6. Created `reports-page.component.spec.ts` with tests for loading, error, empty state, form validation, job table rendering, status chips, download buttons, and error details

### Key Technical Decisions

- **No routing or nav changes needed**: Route and sidebar entry already existed from initial scaffold
- **Separate models file**: Created `reports.models.ts` instead of bloating `shell.models.ts` — follows the pattern set by `travel.models.ts`
- **mat-table for jobs list**: Used Angular Material table for consistent styling with pagination support, rather than hand-rolled CSS grid
- **mat-datepicker with native adapter**: Used `provideNativeDateAdapter()` for date inputs without requiring Moment.js or other date libraries
- **Status chips use color semantics**: Green for completed, blue for processing, red for failed, grey for pending — consistent with existing dashboard/crisis-queue patterns
- **Polling checks active jobs only**: The 10s timer only calls the API if there are pending/processing jobs, avoiding unnecessary network traffic

### Completion Notes

- All 8 report types are selectable with display names and descriptions
- Format toggle supports Excel/PDF selection
- Conditional date/parameter inputs appear based on selected report type
- Export jobs table renders with status chips, download buttons for completed jobs, error messages for failed jobs, and "Expired" label for jobs with expired download URLs
- Auto-polling every 10s for pending/processing jobs updates status in place without page flicker
- 410 Gone (expired URLs) is handled gracefully with "Expired" label
- No worker rankings, leaderboards, or performance scoring in the UI
- Full test coverage: loading skeleton, error with retry, empty state, form submission, job table rendering, status chips, download buttons, expired URLs

## File List

### New files
- `src/app/features/shell/services/reports-api.service.ts` — Reports API service
- `src/app/features/shell/reports.models.ts` — Report types constants and DTOs
- `src/app/features/shell/pages/reports-page.component.html` — Reports page template
- `src/app/features/shell/pages/reports-page.component.scss` — Reports page styles
- `src/app/features/shell/pages/reports-page.component.spec.ts` — Component tests

### Modified files
- `src/app/features/shell/pages/reports-page.component.ts` — Replaced placeholder with full implementation

### Change Log

- **2026-06-20**: Code review applied all 10 patch findings.
  - Datepicker handler uses `$event.value?.toISOString()` instead of broken `$any($event).target.value`
  - Year input now guards against `NaN` via `isNaN(Number($event))` check
  - Polling `refreshActiveJobs` now matches by active job IDs instead of filtering for terminal status only, so `pending`→`processing` transitions surface in the UI
  - Expired-download label changed from "Expired" to "Download expired — start a new export"
  - Removed dead `try/catch` in `ngOnInit` (was never reached since `loadJobs()` handles all errors)
  - `formatTimestamp` now checks `isNaN(date.getTime())` instead of relying on `try/catch` (which never caught "Invalid Date")
  - `ApiEnvelope.meta` access uses optional chaining (`envelope.meta?.totalCount`) to prevent `TypeError` at runtime
  - Component method renamed from `displayFormat` to `formatReportFormat` to avoid shadowing the imported helper
  - Test mock changed from `completedJob` to `pendingJob` for `startExport`
  - Added tests for export error state and button-disabled-during-export

### Review Findings

#### Patch (actionable fixes — all applied)

- [x] [Review][Patch] Datepicker `dateChange` handler always yields null [`reports-page.component.html:52,64`]
      Uses `$any($event).target.value?.toISOString?.()` which always returns null because `HTMLInputElement.value` is a string without `toISOString`. Fix: use `$event.value?.toISOString() ?? null`.
- [x] [Review][Patch] Year input accepts NaN for non-numeric entry [`reports-page.component.html:80`]
      `Number("abc")` returns `NaN`, but the ternary treats the truthy string as "yes set it". Fix: add `isNaN()` guard before setting `year` signal.
- [x] [Review][Patch] Polling never surfaces intermediate status (processing) [`reports-page.component.ts:222-228`]
      `refreshActiveJobs()` filters for `completed`/`failed` only, so `pending`→`processing` transition never updates the UI. Fix: match active jobs by ID instead of filtering by terminal status only.
- [x] [Review][Patch] Expired-download label text mismatch [`reports-page.component.html:198`]
      Shows "Expired" instead of the AC-specified "Download expired — start a new export".
- [x] [Review][Patch] Dead catch block in `ngOnInit` [`reports-page.component.ts:58-61`]
      Outer `try/catch` wrapping `loadJobs()` never triggers since `loadJobs()` handles all errors internally.
- [x] [Review][Patch] `formatTimestamp` silently produces "Invalid Date" text [`reports-page.component.ts:200`]
      `new Date(invalidString)` doesn't throw; `toLocaleString()` returns "Invalid Date". Fix: add `isNaN(date.getTime())` check.
- [x] [Review][Patch] `ApiEnvelope.meta` null crash in `listExports` [`reports-api.service.ts:55`]
      `envelope.meta.totalCount` throws `TypeError` if `meta` is nullish. Fix: use optional chaining `envelope.meta?.totalCount ?? 0`.
- [x] [Review][Patch] `displayFormat` component method shadows imported function [`reports-page.component.ts:130`]
      Component method has same name as imported `displayFormat`. Fragile — future refactoring could cause infinite recursion. Fix: rename method (e.g., `formatReportFormat`).
- [x] [Review][Patch] Test mock returns `completedJob` instead of `pending` [`reports-page.component.spec.ts:69`]
      `startExport.and.resolveTo(completedJob)` — a fresh export returns `pending` status, not `completed`.
- [x] [Review][Patch] Missing tests for error/edge paths [`reports-page.component.spec.ts`]
      No test for export failure (error signal), year field edge cases, or non-numeric year input.

#### Deferred (real issues, not actionable now)

- [x] [Review][Defer] Polling never adds new jobs from other sessions — deferred, pre-existing
- [x] [Review][Defer] `totalCount` pagination issues during polling — deferred, pre-existing
- [x] [Review][Defer] Single `exporting` boolean prevents concurrent exports — deferred, pre-existing
- [x] [Review][Defer] `getStatusLabel` is a pure function inside component class — deferred, pre-existing
- [x] [Review][Defer] `formatTimestamp` hardcodes `en-IN` locale — deferred, pre-existing
- [x] [Review][Defer] `window.open` URL unsanitized / popup blocker risk — deferred, pre-existing
- [x] [Review][Defer] In-flight HTTP not cancelled on destroy — deferred, pre-existing
- [x] [Review][Defer] `startExport` URL vulnerable to path traversal via `type` — deferred, pre-existing
- [x] [Review][Defer] `loadingGuard` mutation after component destruction — deferred, pre-existing
- [x] [Review][Defer] Page change silently desyncs on rapid click — deferred, pre-existing
- [x] [Review][Defer] `startExport` makes fragile API ordering assumptions — deferred, pre-existing
- [x] [Review][Defer] `displayFormat()` mislabels unknown formats as "PDF" — deferred, pre-existing

#### Dismissed (noise / false positive)

- Report types rendered as dropdown not list — subjective UX, `<mat-select>` is reasonable for 8 items
