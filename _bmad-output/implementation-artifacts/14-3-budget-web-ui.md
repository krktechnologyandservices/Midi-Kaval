---
baseline_commit: 20146278a5c82080b95cba06539c23c64d429fa6
---

# Story 14.3: Budget Web UI

Status: done

## Story

As a **Project Director or Coordinator**,
I want a web page to manage budgets and view utilization,
So that I can track finances without Excel.

## Acceptance Criteria

1. **Given** the web app sidebar
   **When** the Coordinator or Director navigates
   **Then** a "Budgets" nav item is visible between "Reports" and "Legends"
   **And** clicking it opens the budget list page

2. **Given** the budget list page
   **When** the page loads
   **Then** a paginated Material table shows budgets with columns: Source, Financial Year, Approval Status, Allocated, Utilized, Balance
   **And** 404 errors from the API are displayed gracefully (no blank page)

3. **Given** the budget list page
   **When** the Director clicks "Create Budget"
   **Then** a dialog or inline form opens with fields for:
   - Source (select: DCPU / NPO (Dale View)) — required
   - Financial Year Start (date picker) — required
   - Financial Year End (date picker) — required
   - Notes (textarea, optional)
   - At least one line item with Budget Head (select: Honorarium, Travel expenses, Parent Management Training, Life Skills Training, Psychosocial Support, Administrative Expenses, Stationery Expenses) and Amount Allocated (number, required)
   **And** the user can add/remove line item rows
   **And** clicking Save calls `POST /api/v1/budgets` and refreshes the list
   **And** validation errors from the API are shown inline

4. **Given** the budget list page
   **When** the Director clicks "Edit" on a budget
   **Then** a dialog or inline form opens pre-populated with the budget's line items
   **And** clicking Save calls `PUT /api/v1/budgets/{id}` and refreshes the list
   **And** 422 errors (e.g. for invalid state transitions) are displayed

5. **Given** the budget list or detail page
   **When** the Director clicks "Approve" or "Return" on a budget with status "Proposed"
   **Then** a confirmation dialog appears with optional comment field
   **And** on confirm, calls `POST /api/v1/budgets/{id}/approve` or `POST /api/v1/budgets/{id}/return`
   **And** the list refreshes showing the updated status (no response body — re-fetch the budget)

6. **Given** the budget list or detail page
   **When** an Accountant clicks "Propose" on a budget with status "Draft"
   **Then** the budget transitions from Draft to Proposed (calls `POST /api/v1/budgets/{id}/propose`)
   **And** the view refreshes to show the updated status

7. **Given** the budget detail page
   **When** an Accountant clicks "Execute" on a budget with status "Approved"
   **Then** the budget transitions from Approved to Executed (calls `POST /api/v1/budgets/{id}/execute`)
   **And** the view refreshes to show the updated status

8. **Given** the budget list or detail page
   **When** any user clicks on a budget row
   **Then** a detail view shows:
   - Header: Source, Financial Year, Status, Created by
   - Line items table: Head, Allocated, Utilized, Balance, %
   - Utilization summary section: per-head totals with overall percentage
   - Recent utilization entries list (last 20)
   **And** approval actions (Propose/Approve/Return/Execute) are shown conditionally:
     - "Propose" button for Draft budgets when role is AccountantOrAbove
     - "Approve"/"Return" buttons for Proposed budgets when role is Director
     - "Execute" button for Approved budgets when role is AccountantOrAbove
   **And** utilization entries table includes columns: Date, Budget Head, Amount, Description, Case, Created At
   **And** the detail page makes three API calls in parallel: budget detail, utilization summary, recent utilizations

9. **Given** the budget detail page
   **When** loading fails with 404
   **Then** an appropriate "not found" message is displayed with a link back to the budget list

### Status Color Coding

Apply color coding to approval status badges:
- **Draft** → gray (default)
- **Proposed** → blue (`#1976D2` or `primary`)
- **Returned** → orange/warning (`#F57C00` or `warn`)
- **Approved** → green/success (`#388E3C`)
- **Executed** → teal (`#00897B`)

## Tasks / Subtasks

- [x] Add "Budgets" nav item to `supervisor-shell.component.ts` between Reports and Legends
- [x] Create `apps/web/src/app/features/budgets/` module folder structure
- [x] Create `BudgetModels` (typescript interfaces matching API DTOs for BudgetListDto, BudgetDetailDto, CreateBudgetRequest, etc.)
- [x] Create `BudgetsApiService` in `apps/web/src/app/features/budgets/services/` with methods: list, getById, create, update, propose, approve, return, execute, listUtilizations, getUtilizationSummary
- [x] Create `BudgetsListPageComponent` with paginated Material table (AC: 2, 5, 6)
- [x] Create `BudgetCreateDialogComponent` with form for creating budgets with line items (AC: 3)
- [x] Create `BudgetEditDialogComponent` with form for editing budgets (AC: 4)
- [x] Create `BudgetDetailPageComponent` with line items table, utilization summary, utilization entries, conditional action buttons (AC: 8, 9)
- [x] Create `BudgetApproveDialogComponent` for approve/return confirmation (AC: 5)
- [x] Add `/budgets` and `/budgets/:id` routes to `app.routes.ts`
- [x] Run `dotnet build` on API and `ng build` on web to verify everything compiles

## Review Findings

### Patches

- [x] [Review][Patch] **"Create Budget" button not role-gated** (`budgets-list-page.component.ts`) — The header "Create Budget" button has no role gating, while all other state-changing action buttons are role-gated. The API enforces AccountantOrAbove. Resolved decision: add role gating to hide for Coordinators; only show for Director/Accountant.

### Patches

- [x] [Review][Patch] **extractErrorMessage always returns "Budgets API error" — actual error detail lost** (`budgets-api.service.ts:128-146,302-307`) — `wrapError` wraps all errors into `BudgetsApiError` with hardcoded `.message = "Budgets API error"`. `extractErrorMessage` checks `BudgetsApiError` first and returns that hardcoded message. The entire HttpErrorResponse inspection block is dead code. Fix: have `extractErrorMessage` recurse into `error.sourceError` for `BudgetsApiError`, or store the original message.
- [x] [Review][Patch] **404 detection in BudgetDetailPageComponent is dead code + fragile string matching** (`budget-detail-page.component.ts:1532-1534`) — `errMsg.includes('404')` never matches because the error message is always "Budgets API error" due to the above issue. Also, string-based 404 detection is fragile. Fix: check HTTP status code via the source error instead.
- [x] [Review][Patch] **formatFinancialYear and formatAmount duplicated across two components** (`budgets-list-page.component.ts`, `budget-detail-page.component.ts`) — These pure functions are defined identically in both files. Extract to a shared utility module.
- [x] [Review][Patch] **No validation that financial year start precedes financial year end** (`budget-create-dialog.component.ts`) — The form accepts any two dates. Nothing prevents start > end, zero-length, or excessive gaps. Fix: add validation in `isValid()`.
- [x] [Review][Patch] **Edit dialog shows empty form with enabled Save when load fails — data loss risk** (`budget-edit-dialog.component.ts:1016-1028`) — If `getById` fails, `loading` is set to `false` but `lineItems` is empty. The Save button is enabled and would overwrite the server budget with empty payload. Fix: disable Save when error occurred, or keep loading true on error.
- [x] [Review][Patch] **Non-null assertions `!` on amountAllocated bypass compile-time safety** (`budget-create-dialog.component.ts:869`, `budget-edit-dialog.component.ts:1057`) — `li.amountAllocated!` passes `null` to API if validation is bypassed. Fix: use explicit guard with fallback.
- [x] [Review][Patch] **No validation against duplicate budget heads within same budget** (`budget-create-dialog.component.ts`, `budget-edit-dialog.component.ts`) — Two line items can both have the same Budget Head. Fix: add uniqueness check in `isValid()`.
- [x] [Review][Patch] **Dialog close return types inconsistent** (`budget-create-dialog.component.ts`, `budget-approve-dialog.component.ts`) — Create/Edit dialogs close with `false` on cancel, Approve dialog closes with `undefined`. Works by coincidence but signals sloppy typing.
- [x] [Review][Patch] **No empty/whitespace id validation in API service** (`budgets-api.service.ts:41-113`) — None of the id-based methods guard against empty or whitespace-only id. Fix: add guard at the start of each id-based method.
- [x] [Review][Patch] **No zero/negative pagination guard in list/listUtilizations** (`budgets-api.service.ts:18,97`) — Zero or negative page/pageSize would be sent to the API. Fix: clamp with `Math.max()`.
- [x] [Review][Patch] **Missing "Created by" in detail page header (AC 8)** (`budget-detail-page.component.ts`) — AC 8 specifies the header includes "Created by" but the implementation only shows Source, Financial Year, Status, and Notes. Fix: add a "Created by" field referencing `createdByUserId`.

### Deferred

- [x] [Review][Defer] **Role cast `as AppRole | undefined` hides upstream type mismatch** (`budgets-list-page.component.ts:570`, `budget-detail-page.component.ts:1545`) — `currentUser()?.role` has a wider type than `AppRole`. The cast silences the compiler rather than fixing upstream. Deferred: pre-existing upstream type issue.
- [x] [Review][Defer] **Hardcoded status colors break theme/dark-mode support** (`budget.models.ts:131-137`) — Status colors are hardcoded hex values. Dark-mode theming would require duplicating overrides. Deferred: design system concern beyond this feature; per spec requirements.
- [x] [Review][Defer] **No tests across ~1400 new lines** — Six new files (models, service, 4 components) with zero tests. Deferred: testing is a separate activity; tests to be written in a follow-up story.
- [x] [Review][Defer] **Create/Edit dialogs use plain properties instead of signals** (`budget-create-dialog.component.ts`, `budget-edit-dialog.component.ts`) — Spec recommends signals for local state, but dialogs use plain properties. Deferred: style convention, not functional; no runtime impact.

## Dev Notes

### Critical: This is the frontend for the Budget module

Story 14.3 builds the Angular web UI that consumes the Budget CRUD API (Story 14.1) and Budget Utilization API (Story 14.2). The backend APIs are already implemented and deployed.

### Existing Nav Structure

The sidebar in `supervisor-shell.component.ts` currently lists:
1. Crisis queue
2. Dashboard
3. Cases
4. Reports
5. **Legends** ← Insert "Budgets" here
6. Admin (Director only)

Insert a new `NavItem` between Reports and Legends. No role-based gating is needed — the "Budgets" nav item is visible to all users who can access the supervisor shell (Coordinator, Director, Accountant).

```typescript
{ label: 'Budgets', path: '/budgets' },
```

### Route Structure

Add lazy-loaded routes under the supervisor shell children:
```typescript
{
  path: 'budgets',
  loadComponent: () => import('./features/budgets/budgets-list-page.component').then(m => m.BudgetsListPageComponent),
},
{
  path: 'budgets/:id',
  loadComponent: () => import('./features/budgets/budget-detail-page.component').then(m => m.BudgetDetailPageComponent),
},
```

### Module Structure

```
apps/web/src/app/features/budgets/
├── budget.models.ts
├── budgets-list-page.component.ts
├── budget-detail-page.component.ts
├── budget-create-dialog.component.ts
├── budget-edit-dialog.component.ts
├── budget-approve-dialog.component.ts
└── services/
    └── budgets-api.service.ts
```

### API Surface

| Method | Endpoint | Auth | Notes |
|--------|----------|------|-------|
| GET | `/api/v1/budgets` | CoordinatorOrAbove | Paginated list |
| GET | `/api/v1/budgets/{id}` | CoordinatorOrAbove | Detail with line items |
| POST | `/api/v1/budgets` | AccountantOrAbove | Create with line items |
| PUT | `/api/v1/budgets/{id}` | AccountantOrAbove | Update budget (Draft or Returned only) |
| POST | `/api/v1/budgets/{id}/propose` | AccountantOrAbove | Propose (Draft → Proposed). No response body — re-fetch. |
| POST | `/api/v1/budgets/{id}/approve` | Director | Approve (Proposed → Approved). No response body — re-fetch. |
| POST | `/api/v1/budgets/{id}/return` | Director | Return (Proposed → Returned). No response body — re-fetch. |
| POST | `/api/v1/budgets/{id}/execute` | AccountantOrAbove | Execute (Approved → Executed). No response body — re-fetch. |
| GET | `/api/v1/budgets/{budgetId}/utilizations` | CoordinatorOrAbove | Paginated, date-filtered |
| GET | `/api/v1/budgets/{budgetId}/utilizations/summary` | CoordinatorOrAbove | Utilization per head |

### TypeScript Models

Create `apps/web/src/app/features/budgets/budget.models.ts`:

```typescript
export interface BudgetListDto {
  id: string;
  source: string;
  financialYearStart: string;
  financialYearEnd: string;
  approvalStatus: string;
  totalAllocated: number;
  totalUtilized: number;
  createdAtUtc: string;
}

export interface BudgetDetailDto {
  id: string;
  source: string;
  financialYearStart: string;
  financialYearEnd: string;
  approvalStatus: string;
  notes?: string;
  lineItems: BudgetLineItemDto[];
  createdByUserId: string;
  approvedByUserId?: string;
  decisionComment?: string;
  decidedAtUtc?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface BudgetLineItemDto {
  id: string;
  budgetHead: string;
  amountAllocated: number;
  amountUtilized: number;
}

export interface CreateBudgetRequest {
  source: string;
  financialYearStart: string;
  financialYearEnd: string;
  notes?: string;
  lineItems: CreateBudgetLineItemRequest[];
}

export interface CreateBudgetLineItemRequest {
  budgetHead: string;
  amountAllocated: number;
}

export interface UpdateBudgetRequest {
  notes?: string;
  lineItems: UpdateBudgetLineItemRequest[];
}

export interface UpdateBudgetLineItemRequest {
  budgetHead: string;
  amountAllocated: number;
}

export interface ApproveBudgetRequest {
  decisionComment?: string;
}

export interface ReturnBudgetRequest {
  decisionComment: string;
}

// Paginated API response shape
export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BudgetUtilizationListDto {
  id: string;
  budgetLineItemId: string;
  budgetHead: string;
  caseId?: string;
  caseCrimeNumber?: string;
  amountUtilized: number;
  utilizationDate: string;
  description: string;
  createdAtUtc: string;
}

export interface BudgetUtilizationSummaryDto {
  budgetId: string;
  headSummaries: BudgetHeadSummaryDto[];
  totalAllocated: number;
  totalUtilized: number;
  totalBalance: number;
  overallUtilizationPercentage: number;
}

export interface BudgetHeadSummaryDto {
  budgetHead: string;
  allocated: number;
  utilized: number;
  balance: number;
  utilizationPercentage: number;
}
```

### Budget Source Options

```typescript
export const BUDGET_SOURCE_OPTIONS = [
  { value: 'DCPU', label: 'DCPU' },
  { value: 'NPO (Dale View)', label: 'NPO (Dale View)' },
] as const;
```

### Budget Head Options

```typescript
export const BUDGET_HEAD_OPTIONS = [
  { value: 'Honorarium', label: 'Honorarium' },
  { value: 'TravelExpenses', label: 'Travel Expenses' },
  { value: 'ParentManagementTraining', label: 'Parent Management Training' },
  { value: 'LifeSkillsTraining', label: 'Life Skills Training' },
  { value: 'PsychosocialSupport', label: 'Psychosocial Support' },
  { value: 'AdministrativeExpenses', label: 'Administrative Expenses' },
  { value: 'StationeryExpenses', label: 'Stationery Expenses' },
] as const;
```

### Approval Status Display

```typescript
export const BUDGET_APPROVAL_STATUS_LABELS: Record<string, string> = {
  Draft: 'Draft',
  Proposed: 'Proposed',
  Returned: 'Returned',
  Approved: 'Approved',
  Executed: 'Executed',
};
```

### Inline Template Pattern

All components should use inline `template:` strings (not separate HTML files) matching the established `audit-log-page.component.ts` and `admin-page.component.ts` pattern. Use signals for local state.

### Error Handling Pattern

Follow the existing pattern from other Angular feature pages:
- Wrap API calls in try/catch
- Use `extractErrorMessage()` on the API service
- Show error via `errorMessage` signal with an `error-banner` div
- Skeleton loading states

### API Envelope Pattern

All API responses wrap data in `{ data: T, meta: { ... } }`. Use the existing `ApiEnvelope` type from the case models:
```typescript
import { ApiEnvelope } from '../cases/models/case.models';
```

For paginated responses the envelope is:
```typescript
{ data: { items: T[], page: number, pageSize: number, totalCount: number }, meta: { ... } }
```

### Existing Patterns to Follow

#### API Service Pattern (LegendsApiService as reference)
- `@Injectable({ providedIn: 'root' })`
- Methods return `Promise<T>` using `firstValueFrom` + `http.get/post/put/delete`
- `extractErrorMessage(error)` public method
- Private `wrapError(error)` method
- API base URL from `environment.apiBaseUrl`

#### Component Pattern (AuditLogPageComponent as reference)
- `@Component({ selector, imports, template: `...`, styleUrls })`
- `readonly` + `signal()` for reactive state
- `inject()` for DI
- `@if` / `@for` control flow in templates
- Material table with `mat-table`, `mat-paginator`
- Error banner with `role="alert"`, skeleton loading

### Budget List Page Columns

The budget list Material table should display:
| Column | Display |
|--------|---------|
| Source | Budget source (DCPU / NPO) |
| Financial Year | "2026-27" format from start/end dates |
| Status | Approval status with colored badge |
| Total Allocated | Formatted number |
| Total Utilized | Formatted number |
| Balance | Allocated - Utilized, formatted number |
| Actions | View, Edit (AccountantOrAbove, Draft/Returned only), Propose (AccountantOrAbove, Draft only), Approve/Return (Director, Proposed only), Execute (AccountantOrAbove, Approved only) |

### Budget Detail Page Sections

The detail page makes **three parallel API calls** on load (use `Promise.all` or similar):
1. `GET /api/v1/budgets/{id}` — budget detail + line items
2. `GET /api/v1/budgets/{budgetId}/utilizations/summary` — utilization per head
3. `GET /api/v1/budgets/{budgetId}/utilizations?page=1&pageSize=20` — recent utilization entries

Page structure:
1. **Header card** — Source, Financial Year (displayed as "2026-27"), Status badge, Created by
2. **Approval action buttons** conditionally shown based on status and role:
   - "Propose" button — visible when `status === 'Draft'` and user role is AccountantOrAbove
   - "Approve" / "Return" buttons — visible when `status === 'Proposed'` and role is Director
   - "Execute" button — visible when `status === 'Approved'` and role is AccountantOrAbove
   - All action endpoints return `200 OK` with no body — re-fetch the budget detail after each action

### Previous Story Intelligence

**Key learnings from Story 14.1 & 14.2:**
1. Budget enums use string-based `BudgetHead` and `BudgetSource` — the API stores them as strings via `HasConversion<string>()`
2. `ApprovalStatus` goes through Draft → Proposed → Approved → Executed (or Returned from Proposed)
3. The API envelope pattern wraps all responses in `{ data: ..., meta: ... }`
4. Paginated results come as `{ items, page, pageSize, totalCount }` (NOT the envelope — check at implement time)
5. Utilizations are accessed via `/api/v1/budgets/{budgetId}/utilizations` with query params `fromDate`, `toDate`, `page`, `pageSize`
6. The summary endpoint `GET /api/v1/budgets/{budgetId}/utilizations/summary` returns per-head totals with percentage
7. Budget endpoints require `CoordinatorOrAbove` for reads and `AccountantOrAbove` for writes; `Director` for approve/return

### Testing Standards Summary

- Unit tests using Jasmine + Angular Testing Library
- Test that nav item renders for Coordinator and Director
- Test budget list renders with mock data
- Test create budget dialog validates required fields
- Test approve/return dialog calls correct endpoint
- Test error states (404, 422, network failure)

## File List

### New Files
- `apps/web/src/app/features/budgets/budget.models.ts`
- `apps/web/src/app/features/budgets/services/budgets-api.service.ts`
- `apps/web/src/app/features/budgets/budgets-list-page.component.ts`
- `apps/web/src/app/features/budgets/budget-detail-page.component.ts`
- `apps/web/src/app/features/budgets/budget-create-dialog.component.ts`
- `apps/web/src/app/features/budgets/budget-edit-dialog.component.ts`
- `apps/web/src/app/features/budgets/budget-approve-dialog.component.ts`

### Modified Files
- `apps/web/src/app/features/shell/supervisor-shell.component.ts` — add "Budgets" nav item
- `apps/web/src/app/app.routes.ts` — add `/budgets` and `/budgets/:id` routes
- `packages/shared-types/src/index.ts` — added `Accountant` to `AppRole` enum

## Dev Agent Record

### Implementation Plan

Story 14.3 implements the Angular web UI for the Budget module, consuming the Budget CRUD API (Story 14.1) and Budget Utilization API (Story 14.2).

**Implementation Sequence:**
1. Added `Accountant` to `AppRole` enum in shared-types package (needed for policy checks)
2. Created `budget.models.ts` with all TypeScript interfaces matching API DTOs, including `PaginatedResult<T>` generic type, source/head/status constants
3. Created `BudgetsApiService` with all 9 API methods (list, getById, create, update, propose, approve, returnBudget, execute, listUtilizations, getUtilizationSummary) and error handling
4. Created `BudgetsListPageComponent` — paginated Material table with conditional action buttons per status/role, create/edit/approve/return/propose/execute dialogs, error and loading states
5. Created `BudgetCreateDialogComponent` — form with source select, financial year date pickers, notes, dynamic line items with add/remove
6. Created `BudgetEditDialogComponent` — loads existing budget via API, pre-populates form, allows editing notes and line items
7. Created `BudgetApproveDialogComponent` — confirm dialog with optional/required comment for approve or return actions
8. Created `BudgetDetailPageComponent` — header card, conditional action buttons, line items table, utilization summary, recent utilization entries (3 parallel API calls via Promise.all), 404 handling
9. Added "Budgets" nav item to supervisor shell sidebar between Reports and Legends
10. Added lazy-loaded routes for `/budgets` and `/budgets/:id`

### Debug Log

- Initial build failed: `AppRole.Accountant` did not exist in shared-types — added `Accountant` to the TypeScript enum and rebuilt the package
- `showActions` was defined as a getter but called with `()` in the template — fixed by removing `()`
- Role getter had type mismatch (`string | null | undefined` vs `AppRole | undefined`) — fixed with explicit cast

### Completion Notes

All 11 tasks completed:
- 7 new files created in `apps/web/src/app/features/budgets/`
- 3 existing files modified: supervisor-shell, routes, shared-types
- Full UI flow: list → create/edit → detail (with utilization summary) → approve/return/propose/execute
- Status color coding for all 5 budget approval states
- ApiEnvelopeFilter-compatible API service with error extraction
- `dotnet build` succeeds (0 errors), `ng build` succeeds (0 new errors)

## Change Log

- 2026-06-21: Created Story 14.3 with comprehensive dev notes
- 2026-06-21: Validation improvements applied — C1/C2/C3 (Propose/Execute endpoints, POST methods, no-body), E1/E2/E3/E4 (PaginatedResult, nav note, parallel calls, color coding)
- 2026-06-21: Implemented Story 14.3 — Created 7 Angular components, API service, models; added nav item and routes; built shared-types with Accountant role; dotnet build + ng build succeed
