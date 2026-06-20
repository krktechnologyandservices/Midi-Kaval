---
baseline_commit: 4ebf6e0
---

# Story 8.4: Dashboard Web UI

Status: done

## Story

As a **supervisor**,
I want a chart dashboard as secondary view,
So that I analyse trends after triage (FR-20, UX-DR11).

*Scope: **Angular PWA dashboard page only** — replace the existing placeholder `DashboardPageComponent` at `features/shell/pages/dashboard-page.component.ts` with a full widget dashboard. Each widget maps to one of the 9 properties in `DashboardResultDto` returned by `GET /api/v1/supervisor/dashboard`. No mobile UI, no report exports, no notification changes.*

## Acceptance Criteria

1. **Given** the Dashboard sidebar route (`/dashboard`)
   **When** the page loads
   **Then** skeleton widget placeholders render immediately (no blank flash)
   **And** real widget content populates from `GET /api/v1/supervisor/dashboard` once the API responds
   **And** the page auto-refreshes every 60 seconds (matching the Redis cache TTL)

2. **Given** the dashboard API returns widget data
   **When** each widget type is rendered
   **Then** the following 9 widgets are displayed in a responsive grid:
   - **Cases by Stage** — distribution per stage (`stage` + `count`)
   - **Cases by Offence Classification** — distribution (`offenceClassification` + `count`)
   - **Cases by Domicile** — distribution (`domicile` + `count`)
   - **Cases by Staff** — worker list (`workerName` + `caseCount`)
   - **Overdue Visits** — metric card (`totalOverdue` + `uniqueCasesAffected`)
   - **Interventions Gauge** — metric card (`inProgress`, `overdue`, `completedThisMonth`)
   - **Court This Week** — metric card (`totalUpcoming`, `attendedSoFar`, `totalCasesWithSittings`)
   - **Pending Claims** — metric card (`pendingCount`, `totalAmountPending`, `oldestPendingDays`)
   - **Intake Trend** — 12-month trend chart (`month` YYYY-MM + `count`)

3. **Given** the API returns data for distribution widgets (CasesByStage, CasesByOffenceClassification, CasesByDomicile)
   **Then** each category is displayed as a horizontal bar with label, count, and proportional fill
   **And** bars use the `status-info` color (`#175CD3`) as fill

4. **Given** the API returns CasesByStaff data
   **Then** each worker row shows `workerName`, `caseCount`, and a proportional bar
   **And** bars use the `primary` color (`#0D6E6E`) as fill

5. **Given** the API returns overdue visits, interventions gauge, court this week, and pending claims
   **Then** each is rendered as a compact metric card with bold number and label
   **And** metric cards use `surface-raised` background with hairline border

6. **Given** the API returns intake trend data
   **Then** a 12-month trend is rendered as a CSS bar chart (12 vertical bars, one per month)
   **And** bars use the `primary` color; height is proportional to count relative to the max month
   **And** months with zero count render as a thin hairline (visible but not zero-height)

7. **Given** the dashboard API call fails (network error, 4xx, 5xx)
   **Then** an error message is displayed with a Retry button
   **And** previously loaded data (if any) is NOT cleared until the retry succeeds

8. **Given** no data exists in the system (fresh deployment)
   **Then** all widgets render their zero/empty state:
   - Distribution widgets: "No data" placeholder inside each widget
   - Metric cards: show `0` or `—` as appropriate (InterventionsGauge sub-metrics each show `0` individually)
   - Intake trend: 12 zero-height bars shown with an empty-state label "No intake data for the past 12 months"

9. **Given** auto-refresh fires every 60 seconds
   **Then** a subtle "Updating…" indicator shows during refresh (not a full loading skeleton)
   **And** stale data remains visible until new data arrives

10. **Given** the responsive dashboard layout
    **Then** widgets display in a 3-column grid at viewport ≥1024px
    **And** 2-column grid at 768–1023px
    **And** single-column at <768px
    **And** no horizontal scroll at 200% browser zoom (UX-DR15)

11. **Given** any dashboard widget
    **Then** no gamification elements, leaderboards, or performance scores are shown (NFR-11, UX-DR16)
    **And** data is presented as factual distributions, not rankings

12. **Given** the existing Crisis Queue page
    **Then** the Crisis Queue empty-state link "Go to Dashboard" continues to navigate to `/dashboard` unchanged

## Tasks / Subtasks

- [x] **Create `DashboardApiService`** — API service for `GET /api/v1/supervisor/dashboard`
  - [ ] Inject `HttpClient`, read `environment.apiBaseUrl`
  - [ ] Method `get(): Promise<DashboardResultDto>` using `firstValueFrom` + `ApiEnvelope<DashboardResultDto>`
  - [ ] Error wrapping with custom `DashboardApiError` class (pattern: `CrisisQueueApiService`)
  - [ ] `extractErrorMessage(error: unknown): string` method following crisis queue pattern
  - [ ] Place at `apps/web/src/app/features/shell/services/dashboard-api.service.ts`

- [x] **Create `shell.models.ts`** — TypeScript interfaces for dashboard widget DTOs
  - [ ] `DashboardResultDto` with all 9 widget properties (interface, not class)
  - [ ] `CasesByStageDto` — `stage: string`, `count: number`
  - [ ] `CasesByOffenceClassificationDto` — `offenceClassification: string`, `count: number`
  - [ ] `CasesByDomicileDto` — `domicile: string`, `count: number`
  - [ ] `CasesByStaffDto` — `workerName: string`, `workerId: string`, `caseCount: number`
  - [ ] `OverdueVisitsDto` — `totalOverdue: number`, `uniqueCasesAffected: number`
  - [ ] `InterventionsGaugeDto` — `inProgress: number`, `overdue: number`, `completedThisMonth: number`
  - [ ] `CourtThisWeekDto` — `totalUpcoming: number`, `attendedSoFar: number`, `totalCasesWithSittings: number`
  - [ ] `PendingClaimsDto` — `pendingCount: number`, `totalAmountPending: number`, `oldestPendingDays: number`
  - [ ] `IntakeTrendPointDto` — `month: string`, `count: number`
  - [ ] Place at `apps/web/src/app/features/shell/shell.models.ts`

- [x] **Replace `DashboardPageComponent`** — full widget dashboard at `features/shell/pages/dashboard-page.component.ts`
  - [ ] Convert from placeholder to full dashboard with:
    - [ ] Skeleton loading state (9 skeleton widget cards)
    - [ ] Error state with retry button
    - [ ] Real widget grid populated from API
    - [ ] Auto-refresh every 60s via `setInterval` in `ngOnInit`
    - [ ] Cleanup `ngOnDestroy` (clear interval)
    - [ ] `loading()`, `refreshing()`, `errorMessage()`, `data()` signals
    - [ ] `loadingGuard` to prevent concurrent loads (same pattern as crisis queue)
  - [ ] Template: heading "Dashboard", widget grid, error handling
  - [ ] Styles: responsive grid (3→2→1 cols), widget card styling, proportional bars, metric cards, skeleton animation
  - [ ] Use `MatCardModule` for widget containers

- [x] **Implement widget display logic** within `DashboardPageComponent`
  - [ ] **Distribution widgets** (CasesByStage, CasesByOffenceClassification, CasesByDomicile — use a shared sub-template):
    - [ ] Render widget title (e.g., "Cases by Stage")
    - [ ] For each entry: label + count + proportional horizontal bar
    - [ ] Empty: show "No data" placeholder
  - [ ] **CasesByStaff widget:**
    - [ ] Render worker rows with name, case count, and proportional bar
    - [ ] Empty: show "No data"
  - [ ] **Metric card widgets** (OverdueVisits, CourtThisWeek, PendingClaims):
    - [ ] Bold primary metric number
    - [ ] Supporting metrics below
    - [ ] Zero values: show `0` or `—`
  - [ ] **InterventionsGauge widget:**
    - [ ] Three metrics: In Progress, Overdue (status-critical color `#B42318`), Completed This Month
    - [ ] Each sub-metric shows `0` in its zero state (overdue shows `0` in `status-critical` color)
  - [ ] **IntakeTrend widget:**
    - [ ] 12 vertical CSS bars, one per month
    - [ ] Month labels (abbreviated) below
    - [ ] Height proportional to max month
    - [ ] Zero-count months: thin hairline bar
    - [ ] Y-axis labels (optional, can use implicit scale)

- [x] **Unit tests — `dashboard-page.component.spec.ts`** (follow crisis queue test pattern)
  - [ ] Loading state: shows 9 skeleton widgets
  - [ ] Error state: shows error message with Retry button
  - [ ] Retry clears error and reloads
  - [ ] Data loaded: renders all 9 widget sections
  - [ ] Auto-refresh 60s interval setup on init
  - [ ] Interval cleared on destroy
  - [ ] Refreshing shows indicator without full skeleton
  - [ ] Empty API data shows zero-state in each widget
  - [ ] Responsive grid: verify CSS grid classes
  - [ ] No gamification text present

- [x] **Verify crisis queue link still works** (AC 12)
  - [ ] Crisis queue empty state "Go to Dashboard" link navigation unchanged

## Dev Notes

### File locations (all paths under `apps/web/src/app/`)

| File | Purpose | Pattern |
|------|---------|---------|
| `features/shell/services/dashboard-api.service.ts` | API service for dashboard endpoint | Follows `features/travel/services/crisis-queue-api.service.ts` |
| `features/shell/shell.models.ts` | Dashboard DTO interfaces | Follows `features/travel/travel.models.ts` |
| `features/shell/pages/dashboard-page.component.ts` | Dashboard page component (replace placeholder) | Follows `features/shell/pages/crisis-queue-page.component.ts` |
| `features/shell/pages/dashboard-page.component.html` | Dashboard template | Follows crisis queue template pattern |
| `features/shell/pages/dashboard-page.component.scss` | Dashboard styles | New — responsive grid + widget styles |
| `features/shell/pages/dashboard-page.component.spec.ts` | Dashboard unit tests | Follows `crisis-queue-page.component.spec.ts` |

### Existing infrastructure (do not create)

- **Route** `/dashboard` already registered in `app.routes.ts` (line 80) — no route changes needed
- **Directory** `features/shell/services/` does NOT exist yet — create it when placing `dashboard-api.service.ts`
- **Sidebar nav** "Dashboard" → `/dashboard` already in `supervisor-shell.component.ts` (line 22) — no nav changes needed
- **Crisis queue empty state** link to `/dashboard` already wired — verify it still works
- **`ApiEnvelope<T>`** already defined in `features/cases/models/case.models.ts`
- **`environment.apiBaseUrl`** already configured — use as-is

### API contract

Endpoint: `GET /api/v1/supervisor/dashboard`

```typescript
// Envelope wrapper (existing)
interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string; totalCount?: number };
}

// Response shape (from Story 8.3)
interface DashboardResultDto {
  casesByStage: CasesByStageDto[];
  casesByOffenceClassification: CasesByOffenceClassificationDto[];
  casesByDomicile: CasesByDomicileDto[];
  casesByStaff: CasesByStaffDto[];
  overdueVisits: OverdueVisitsDto;
  interventionsGauge: InterventionsGaugeDto;
  courtThisWeek: CourtThisWeekDto;
  pendingClaims: PendingClaimsDto;
  intakeTrend: IntakeTrendPointDto[];
}
```

### Component pattern (follow crisis queue)

```typescript
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { DashboardApiService } from '../services/dashboard-api.service';

@Component({
  selector: 'app-dashboard-page',
  imports: [MatCardModule, CommonModule],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(DashboardApiService);
  // ... signals, load(), autoRefresh(), error handling
}
```

### Widget rendering approach

Use `@for` blocks in the template. Distribution widgets share a common structure — use a macro-like `@for` loop pattern:

```html
<!-- Distribution widget (repeated for each distribution type) -->
<section class="widget" aria-label="Widget title">
  <h2 class="widget-title">Cases by Stage</h2>
  @if (data()?.casesByStage; as items) {
    @for (item of items; track item.stage) {
      <div class="bar-row">
        <span class="bar-label">{{ item.stage }}</span>
        <span class="bar-track">
          <span class="bar-fill" [style.width.%]="pct(item.count, maxStageCount())"></span>
        </span>
        <span class="bar-count">{{ item.count }}</span>
      </div>
    }
  } @else {
    <p class="widget-empty">No data</p>
  }
</section>
```

### Metric card pattern

```html
<section class="widget metric-card" aria-label="Overdue Visits">
  <h2 class="widget-title">Overdue Visits</h2>
  <p class="metric-primary">{{ data()?.overdueVisits?.totalOverdue ?? '—' }}</p>
  <p class="metric-sub">Cases affected: {{ data()?.overdueVisits?.uniqueCasesAffected ?? '—' }}</p>
</section>

<section class="widget metric-card" aria-label="Pending Claims">
  <h2 class="widget-title">Pending Claims</h2>
  <p class="metric-primary">{{ data()?.pendingClaims?.pendingCount ?? '—' }}</p>
  <p class="metric-sub">
    Amount: ₹{{ formatAmount(data()?.pendingClaims?.totalAmountPending) }}
    &middot; Oldest: {{ oldestDays(data()?.pendingClaims?.oldestPendingDays) }}
  </p>
</section>
```

Currency formatting: use `toLocaleString('en-IN')` on the amount value and prepend `₹`. Oldest days: display as `"X days pending"` using a helper or property access.

### Intake trend (CSS bar chart)

```html
<section class="widget" aria-label="12-month Intake Trend">
  <h2 class="widget-title">Intake Trend (12 months)</h2>
  <div class="trend-chart">
    @for (point of data()?.intakeTrend; track point.month) {
      <div class="trend-bar-container">
        <span
          class="trend-bar"
          [style.height.%]="trendHeight(point.count, maxIntakeCount())"
          [class.zero]="point.count === 0"
        ></span>
        <span class="trend-label">{{ point.month | slice:5:7 }}</span>
      </div>
    }
  </div>
</section>
```

### Auto-refresh interval (60s, matching Redis TTL)

```typescript
ngOnInit(): void {
  this.load();
  this.refreshTimer = setInterval(() => this.autoRefresh(), 60000);
}

ngOnDestroy(): void {
  if (this.refreshTimer !== null) {
    clearInterval(this.refreshTimer);
    this.refreshTimer = null;
  }
}
```

### CSS layout

```scss
.widget-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;

  @media (max-width: 1023px) {
    grid-template-columns: repeat(2, 1fr);
  }
  @media (max-width: 767px) {
    grid-template-columns: 1fr;
  }
}

.widget {
  background: #FFFFFF;   /* surface-raised */
  border: 1px solid #EAECF0;  /* border-hairline */
  border-radius: 8px;    /* rounded/md */
  padding: 16px;
}
```

### Dark mode

Add `prefers-color-scheme: dark` media query with DESIGN.md dark tokens:
- surface-raised: `#1D2939`
- border-hairline: `#344054`

### Testing pattern

Follow `crisis-queue-page.component.spec.ts` precisely:
- `jasmine.createSpyObj('DashboardApiService', ['get', 'extractErrorMessage'])`
- Stub `AuthSessionService` with a `currentUser` signal (not needed for dashboard but keeps DI happy)
- `ComponentFixture`, `provideRouter([])`
- `async function settle()` helper
- Test skeleton, error, empty, and data-loaded states

### Skeleton widget

Show 9 skeleton cards matching the grid layout — each skeleton is a placeholder card with animated pulse. Use CSS animation (not a library):

```scss
@keyframes pulse {
  0%, 100% { opacity: 0.4; }
  50% { opacity: 0.8; }
}
.skeleton-widget {
  animation: pulse 1.5s ease-in-out infinite;
  height: 160px;
}
```

### No chart library needed

All visualizations use **pure CSS** — no ngx-echarts, Chart.js, D3, or similar dependencies. This keeps the bundle small, avoids licensing concerns, and matches the DESIGN.md aesthetic of minimal decoration.

### Critical don'ts

- **No gamification** — do NOT add rankings, leaderboard styling, or performance scores
- **No ranking** — CasesByStaff shows case counts as factual distribution, not a ranking
- **No hardcoded hex** — use DESIGN.md color tokens. The component should reference CSS custom properties or shadcn design tokens. For the initial implementation, use the hex values directly with a comment pointing to the DESIGN.md token name. Story 9.4 (Angular Material theme from tokens) will formalize the token system.
- **No chart library** — CSS-only visualizations
- **Do not modify** `app.routes.ts`, `supervisor-shell.component.ts`, or `crisis-queue-page.component.ts` — routing, nav, and crisis queue are already wired
- **Do not hand-edit** `packages/api-client` — create DTOs locally in `shell.models.ts`

### Regressions to prevent

- Crisis queue "Go to Dashboard" link must still work
- Dashboard route must still be guarded by `authGuard` + `supervisorGuard`
- Page title and sidebar highlight should show "Dashboard" as active when on `/dashboard`

## References

- Epic 8: Supervisor Crisis Queue, Dashboard & Reports → `_bmad-output/planning-artifacts/epics.md` (FR-20)
- Architecture: Dashboard endpoint `GET /supervisor/dashboard` → `_bmad-output/planning-artifacts/architecture.md`
- UX: Dashboard sidebar placement (UX-DR11), responsive layout (UX-DR15), anti-patterns (UX-DR16), theme (UX-DR17) → `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md`
- Dashboard API patterns (dto contracts, cache TTL 60s, service pattern) → `_bmad-output/implementation-artifacts/8-3-dashboard-api-and-redis-cached-widgets.md`
- Crisis queue page pattern → `apps/web/src/app/features/shell/pages/crisis-queue-page.component.ts`
- Crisis queue test pattern → `apps/web/src/app/features/shell/pages/crisis-queue-page.component.spec.ts`
- Crisis queue API service pattern → `apps/web/src/app/features/travel/services/crisis-queue-api.service.ts`
- API envelope pattern → `apps/web/src/app/features/cases/models/case.models.ts` (`ApiEnvelope`)
- Environment config → `apps/web/src/environments/environment.ts` (`apiBaseUrl`)
- Route config → `apps/web/src/app/app.routes.ts` (line 80)
- Sidebar nav config → `apps/web/src/app/features/shell/supervisor-shell.component.ts` (line 22)

## Previous Story Intelligence (8.3 — Dashboard API)

### Implementation summary

Story 8.3 created the backend dashboard API with 9 widget queries, Redis caching (60s TTL), DTOs in `DashboardDtos.cs`, `DashboardService.cs` with `InvalidateCacheAsync`, and comprehensive integration tests.

### Key patterns

- Dashboard relies on Redis cache — first request hits DB, subsequent requests within 60s return cached data. The web UI auto-refresh at 60s ensures the cache is always warm.
- `GET /api/v1/supervisor/dashboard` returns an `ApiResponse<DashboardResultDto>` envelope — unwrap via `response.data`.
- The API is at the **same base URL** as all other endpoints (`environment.apiBaseUrl`).
- Auth enforcement: Coordinator or Director only (SocialWorker/CaseWorker get 403).

### Relevant learnings

- The `DashboardResultDto` uses `IReadOnlyList<T>` for collection properties — when the TypeScript API client is regenerated, these become arrays. For now, define interfaces manually in `shell.models.ts`.
- The `TotalCount` meta field represents non-widget count — not needed for the UI but available if useful.
- `InvalidateCacheAsync` exists on `DashboardService` but is not wired to any mutation endpoint yet — that's expected; the dashboard will simply show data within 60s of field sync.

### Files from 8.3 (for reference only — do not modify)

- `apps/api/Models/Supervisor/DashboardDtos.cs`
- `apps/api/Infrastructure/Supervisor/DashboardService.cs`
- `apps/api/Controllers/V1/SupervisorController.cs` (`GetDashboard` action)
- `tests/api.integration/DashboardApiTests.cs`

## Git Intelligence

Recent commits for this repository:
- `4ebf6e0` — Initial commit: Midi-Kaval project (all existing code, including the dashboard placeholder)

The dashboard page component is currently a placeholder — this story replaces it with the full implementation.

## File List

### Modified files
- `apps/web/src/app/features/shell/pages/dashboard-page.component.ts` — Replaced placeholder with full dashboard component
- `apps/web/src/app/features/shell/pages/dashboard-page.component.html` — New template with all 9 widgets, skeleton loading, error state
- `apps/web/src/app/features/shell/pages/dashboard-page.component.scss` — New styles (responsive grid, bars, metric cards, skeleton, dark mode)

### New files
- `apps/web/src/app/features/shell/services/dashboard-api.service.ts` — Dashboard API service
- `apps/web/src/app/features/shell/shell.models.ts` — Dashboard DTO interfaces (10 interfaces)
- `apps/web/src/app/features/shell/pages/dashboard-page.component.spec.ts` — Unit tests (16 test cases)

## Dev Agent Record

### Implementation Plan
1. Created `shell.models.ts` with 10 TypeScript interfaces matching the `DashboardResultDto` API contract (DashboardResultDto, 9 widget DTOs)
2. Created `dashboard-api.service.ts` following `CrisisQueueApiService` pattern — `get()` method, `DashboardApiError` class, `extractErrorMessage()`
3. Replaced placeholder `DashboardPageComponent` with full implementation:
   - 9 widget types rendered in a responsive CSS grid (3→2→1 columns)
   - Distribution widgets: CasesByStage, CasesByOffenceClassification, CasesByDomicile, CasesByStaff with proportional horizontal bars
   - Metric card widgets: OverdueVisits, InterventionsGauge, CourtThisWeek, PendingClaims
   - Intake trend: 12-month CSS bar chart with month labels
   - Skeleton loading (9 animated cards), error state with retry, "Updating…" indicator on auto-refresh
   - Auto-refresh every 60s matching Redis cache TTL
   - Empty state handling: "No data" placeholders, zero-value metric cards, empty intake trend label
   - Dark mode support via `prefers-color-scheme: dark`
4. Created `dashboard-page.component.spec.ts` with 16 test cases covering loading, error, empty, data-loaded, auto-refresh, responsive grid, and no-gamification states
5. Verified crisis queue link to `/dashboard` is preserved unchanged

### Key Technical Decisions
- CSS-only visualizations — no external chart library, keeping bundle small
- `pct()` and `trendHeight()` helper methods for proportional bar calculations; minimum 4% height on zero-count trend bars to keep them visible
- `formatAmount()` uses `toLocaleString('en-IN')` for Indian rupee formatting
- `oldestDays()` displays "X days pending" or "Today" for zero days
- `monthLabel()` converts YYYY-MM to abbreviated month names without relying on Angular DatePipe
- `allIntakeZero()` detects all-zero intake trend to show the empty-state label
- Used CSS custom properties (`var(--mat-sys-*)`) with hex fallbacks matching DESIGN.md tokens; dark mode uses DESIGN.md dark token values

### Completion Notes
Story 8.4 complete. Dashboard page at `/dashboard` renders 9 widget types from `GET /api/v1/supervisor/dashboard` in a responsive CSS grid. Widgets include distribution bars, metric cards, and a 12-month intake trend bar chart — all CSS-only, no external chart library. Auto-refresh every 60s matches the Redis cache TTL. Crisis queue link preserved unmodified.

## Review Findings

### Patch (all resolved)

- [x] [Review][Patch] `maxCount()` method missing — added private `maxCount()` helper
- [x] [Review][Patch] Empty intake trend broken — `allIntakeZero()` now handles empty/null arrays; template uses `@else if`/@else to avoid dual rendering
- [x] [Review][Patch] Dark mode primary-color tokens — added `bar-fill-info`, `bar-fill-primary`, `trend-bar:not(.zero)` dark-mode overrides using `#5CB8B8`
- [x] [Review][Patch] Negative value guards — `pct()`, `trendHeight()`, `oldestDays()` now reject negative inputs and NaN
- [x] [Review][Patch] `formatAmount()` NaN/Infinity — added `isNaN()` guard, returns '—'
- [x] [Review][Patch] `MatCardModule` dead import — removed; template uses native `<section>` only
- [x] [Review][Patch] Auto-refresh + retry race — `load()` guard now also checks `refreshing()`
- [x] [Review][Patch] "Updating…" indicator test — added DOM assertion test for `.refreshing-indicator`
- [x] [Review][Patch] Skeleton count magic number — extracted to `SKELETON_WIDGET_COUNT` constant

### Defer (pre-existing / not actionable now)

- [x] [Review][Defer] No backoff on auto-refresh — `setInterval` without stagger/jitter; same pattern as crisis queue page, system-wide concern
- [x] [Review][Defer] `settle()` test helper fragility — extra `Promise.resolve()` papering over zone.js timing; pre-existing pattern in crisis queue tests

### Dismissed (noise / handled)

- [x] [Review][Dismiss] Auto-refresh errors silently swallowed — deliberate per AC 7
- [x] [Review][Dismiss] `ngOnInit` async — standard Angular pattern; try-catch handles errors
- [x] [Review][Dismiss] `monthLabel` format assumptions — YYYY-MM is the API contract
- [x] [Review][Dismiss] No gamification test brittleness — deliberate constraint guard (UX-DR16, NFR-11)

## Change Log

- **2026-06-20**: Initial implementation — Dashboard models (`shell.models.ts`), API service, full page component with 9 widgets, responsive grid (3→2→1 cols), skeleton loading, error/empty states, auto-refresh, dark mode, and 16 unit tests. Status set to review.
- **2026-06-20**: Code review findings applied — `maxCount()` helper, empty intake trend fix, dark mode primary-color tokens, negative/NaN guards, `MatCardModule` cleanup, refresh race guard, "Updating…" test coverage, skeleton constant extraction. Status set to done.
