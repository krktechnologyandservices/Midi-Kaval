---
baseline_commit: 20146278a5c82080b95cba06539c23c64d429fa6
---

# Story 14.4: Budget Expenditure Report

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Project Director**,
I want to export a budget vs utilization report as Excel,
so that funders receive financial reports.

## Acceptance Criteria

1. **Given** the budget list or detail page
   **When** the Director clicks "Export Report"
   **Then** a dialog opens allowing selection of:
   - Report frequency: Monthly, Quarterly, Half-Yearly, Annually (required)
   - Year: numeric input defaulting to current year (required)
   **And** clicking "Generate" calls `GET /api/v1/budgets/report/export?frequency={freq}&year={year}`
   **And** the report data is rendered as an Excel file with allocation, utilization, balance, and utilization percentage columns

2. **Given** the budget report is generated
   **When** the data loads successfully
   **Then** an Excel file is downloaded with the filename `Budget-Report-{Frequency}-{Year}.xlsx`
   **And** the spreadsheet contains columns: Budget Head, Allocated, Utilized, Balance, Utilization %
   **And** a summary row shows Total Allocated, Total Utilized, Total Balance
   **And** the period and frequency are displayed in the spreadsheet header

3. **Given** the API returns an error
   **When** the export fails
   **Then** an error message is displayed clearly (invalid frequency, network failure, etc.)

4. **Given** the budget report page
   **When** data is loading
   **Then** a loading indicator is shown while the report is being generated

5. **Given** the Director role
   **When** viewing the budget list or detail for any budget
   **Then** the "Export Report" button is visible (Director only, matching API auth policy)

## Tasks / Subtasks

- [x] **Add Excel export endpoint to BudgetsController** (AC: 1, 2)
  - [x] Add `GET /api/v1/budgets/report/export?frequency={freq}&year={year}` endpoint (Director only)
  - [x] Return Excel file as `FileContentResult` with `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
  - [x] Filename: `Budget-Report-{Frequency}-{Year}.xlsx`
  - [x] Handle invalid frequency → 400 with descriptive error

- [x] **Create BudgetReportExcelService or extend BudgetService** (AC: 1, 2)
  - [x] Create `apps/api/Infrastructure/Budgets/BudgetReportExcelService.cs`
  - [x] Reuse existing `BudgetReportDto` + `BudgetReportLineDto` from `BudgetDtos.cs`
  - [x] Use ClosedXML (same library as `ReportGenerationService`) to generate Excel
  - [x] Spreadsheet structure:
    - Header row: period + frequency info
    - Column headers: Budget Head, Allocated (₹), Utilized (₹), Balance (₹), Utilization %
    - Data rows per budget head
    - Summary row: Total Allocated, Total Utilized, Total Balance
  - [x] Handle empty data with "No data available for the selected period" message row
  - [x] Register in DI via `Program.cs`

- [x] **Create BudgetReportDialogComponent** (AC: 1, 3, 4, 5)
  - [x] Create `apps/web/src/app/features/budgets/budget-report-dialog.component.ts`
  - [x] Inline template, standalone component, signals pattern
  - [x] Form fields: frequency select (Monthly/Quarterly/HalfYearly/Annually), year input
  - [x] "Generate" button triggers download via API service
  - [x] Loading state while generating
  - [x] Error display on failure

- [x] **Add exportReport method to BudgetsApiService** (AC: 1, 3)
  - [x] Add `exportReport(frequency: string, year: number): Promise<Blob>` method
  - [x] Use `responseType: 'blob'` for Excel binary download
  - [x] Trigger browser download from the blob
  - [x] Wrap errors with `extractErrorMessage`

- [x] **Add "Export Report" button to budget list page** (AC: 1, 5)
  - [x] Add button in page header area (visible when role is Director — matches API auth policy)
  - [x] Click opens `BudgetReportDialogComponent`

- [x] **Add "Export Report" button to budget detail page** (AC: 1, 5)
  - [x] Add button in action buttons area (visible when role is Director)

- [x] **Build and verify**
  - [x] Run `dotnet build` on API — 0 errors
  - [x] Run `ng build` on web — 0 new errors

## Dev Notes

### API — Existing Budget Report Infrastructure

The budget report backend is already partially built:

| Artifact | Path | Status |
|----------|------|--------|
| `BudgetReportDto` | `apps/api/Models/Budgets/BudgetDtos.cs` | ✅ Exists |
| `BudgetReportLineDto` | `apps/api/Models/Budgets/BudgetDtos.cs` | ✅ Exists |
| `BudgetReportFrequency` enum | `apps/api/Domain/Enums/BudgetReportFrequency.cs` | ✅ Exists (Monthly, Quarterly, HalfYearly, Annually) |
| `GET /api/v1/budgets/report` | `apps/api/Controllers/V1/BudgetsController.cs` | ✅ Exists (returns JSON) |
| `BudgetService.GetReportAsync()` | `apps/api/Infrastructure/Budgets/BudgetService.cs` | ✅ Exists (returns BudgetReportDto) |
| **Excel generation** | NOT YET BUILT | ❌ Needs new service |
| **Excel download endpoint** | NOT YET BUILT | ❌ Needs new endpoint |

The existing `GET /api/v1/budgets/report` endpoint returns JSON. For this story, add a **new** endpoint `GET /api/v1/budgets/report/export` that returns the same data as an Excel file download. Do NOT modify the existing JSON endpoint.
The `ApiEnvelopeFilter` automatically skips `FileContentResult` responses, so no special handling is needed for envelope wrapping.

### Excel Generation Pattern

Follow the existing `ReportGenerationService` pattern in `apps/api/Infrastructure/Reports/ReportGenerationService.cs`:

```csharp
// Reference: ReportGenerationService.GenerateExcel()
using ClosedXML.Excel;

var workbook = new XLWorkbook();
var ws = workbook.Worksheets.Add("Budget Report");
// ... build rows, apply formatting ...
using var stream = new MemoryStream();
workbook.SaveAs(stream);
return stream.ToArray();
```

Use:
- **ClosedXML** (same library as existing report generation)
- Bold header row with a distinct fill color
- Currency formatting for amount columns (Indian Rupee ₹)
- Percentage formatting for Utilization % column
- Summary row with `=SUM()` formulas
- Use `AddScoped<BudgetReportExcelService>()` (service pattern, not static — for testability). The existing `CaseExcelExporter` is static, but the budget report service needs DI for `AppDbContext`.

### API Endpoint Pattern

```csharp
[HttpGet("report/export")]
[Authorize(Policy = Policies.Director)]
[Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<IActionResult> ExportReport(
    [FromQuery] string frequency = "Quarterly",
    [FromQuery] int year = 2026,
    CancellationToken ct = default)
```

The endpoint should catch `BudgetNotFoundException` → return 404 (matching the existing `Report` endpoint), and `BudgetBusinessRuleException` → return 422.

### Error Handling for Blob Responses

With `responseType: 'blob'`, Angular's `HttpClient` parses the response body as a Blob. When the API returns 4xx/5xx errors, the error body is a JSON string wrapped as a Blob. Extract it:

```typescript
async exportReport(frequency: string, year: number): Promise<Blob> {
  const params = new HttpParams().set('frequency', frequency).set('year', year);
  try {
    return await firstValueFrom(
      this.http.get(`${this.baseUrl}/report/export`, {
        params,
        responseType: 'blob',
      }),
    );
  } catch (error) {
    if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
      const text = await error.error.text();
      const parsed = JSON.parse(text);
      throw new BudgetsApiError(parsed.error ?? text);
    }
    throw this.wrapError(error);
  }
}
```

### Register the new service in DI

In `apps/api/Program.cs`, add:
```csharp
builder.Services.AddScoped<BudgetReportExcelService>();
```

### Angular — BudgetReportDialogComponent Pattern

Create a new component in `apps/web/src/app/features/budgets/budget-report-dialog.component.ts`:

- Standalone component with inline template importing `MatDialogModule` (for `mat-dialog-title`, `mat-dialog-content`, `mat-dialog-actions`)
- Inject `BudgetsApiService`, `MatDialogRef`
- Form fields: frequency select (4 options), year number input
- On "Generate": call `api.exportReport(frequency, year)` which returns a Blob
- Trigger download using:
  ```typescript
  const blob = await this.api.exportReport(frequency, year);
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `Budget-Report-${frequency}-${year}.xlsx`;
  a.click();
  window.URL.revokeObjectURL(url);
  ```
- Loading spinner while generating
- Error banner on failure
- Close dialog after successful download

### BudgetsApiService — exportReport Method

```typescript
async exportReport(frequency: string, year: number): Promise<Blob> {
  const params = new HttpParams().set('frequency', frequency).set('year', year);
  return await firstValueFrom(
    this.http.get(`${this.baseUrl}/report/export`, {
      params,
      responseType: 'blob',
    }),
  );
}
```

### Button Placement

**Budget list page** (`budgets-list-page.component.ts`):
- Add "Export Report" button next to "Create Budget" in the header
- Visible when `role === AppRole.Director` (Director-only per API auth policy `Policies.Director`)
- Use `mat-stroked-button` with `description` icon for distinction from Create
- Click opens `BudgetReportDialogComponent`

**Budget detail page** (`budget-detail-page.component.ts`):
- Add "Export Report" button in the action buttons area
- Visible when Director, alongside Approve/Return
- Use `mat-stroked-button` styling

### Existing File Structure (for reference)

```
apps/api/
├── Controllers/V1/BudgetsController.cs    — Existing, add new endpoint
├── Infrastructure/Budgets/
│   ├── BudgetService.cs                   — Existing, GetReportAsync()
│   └── BudgetUtilizationService.cs        — Existing
├── Infrastructure/Reports/
│   └── ReportGenerationService.cs          — Reference for Excel pattern
├── Models/Budgets/BudgetDtos.cs           — Existing DTOs
├── Domain/Enums/BudgetReportFrequency.cs  — Existing enum
└── Program.cs                              — Register new service

apps/web/src/app/features/budgets/
├── budget.models.ts                       — Existing TS models
├── budget-report-dialog.component.ts       — NEW
├── budgets-list-page.component.ts          — Modify: add button
├── budget-detail-page.component.ts         — Modify: add button
└── services/budgets-api.service.ts         — Modify: add exportReport()
```

### Previous Story Intelligence

**Key learnings from Story 14.3:**
1. Budget endpoints use `CoordinatorOrAbove` (read), `AccountantOrAbove` (write), `Director` (approve/return)
2. Budget report endpoint is Director-only
3. API envelope wraps responses in `{ data: ..., meta: ... }`
4. The Excel endpoint returns raw binary, NOT the envelope — use `responseType: 'blob'`
5. Angular services use `firstValueFrom` + `HttpClient`, `@Injectable({ providedIn: 'root' })`
6. Components use standalone with inline templates, signals for state, `inject()` for DI
7. Error pattern: `extractErrorMessage()` exists on `BudgetsApiService`

### Testing Standards Summary

- **API:** Unit test for Excel generation with valid data, empty data, invalid frequency
- **Angular:** Test dialog opens/closes, form validation, export triggers correct API call, error state renders

## File List

### New Files
- `apps/api/Infrastructure/Budgets/BudgetReportExcelService.cs`
- `apps/web/src/app/features/budgets/budget-report-dialog.component.ts`

### Modified Files
- `apps/api/Controllers/V1/BudgetsController.cs` — add `/report/export` endpoint
- `apps/api/Program.cs` — register `BudgetReportExcelService`
- `apps/web/src/app/features/budgets/budgets-list-page.component.ts` — add "Export Report" button
- `apps/web/src/app/features/budgets/budget-detail-page.component.ts` — add "Export Report" button
- `apps/web/src/app/features/budgets/services/budgets-api.service.ts` — add `exportReport()` method

## Dev Agent Record

### Implementation Plan

Story 14.4 adds Excel export for the budget report. The API already has the JSON report endpoint and DTOs. This story:
1. Creates an Excel generation service (`BudgetReportExcelService`) using ClosedXML
2. Adds an `/export` endpoint to `BudgetsController` returning the Excel file
3. Adds an Angular dialog for frequency/year selection
4. Adds "Export Report" buttons to the budget list and detail pages
5. Builds and verifies both API and web

### Debug Log

### Completion Notes List

- Created `BudgetReportExcelService` with ClosedXML Excel generation (header, data rows, empty state, summary, formatting)
- Added `GET /api/v1/budgets/report/export` endpoint to `BudgetsController` (Director-only, catches `BudgetNotFoundException` → 404)
- Registered `BudgetReportExcelService` as transient in `Program.cs`
- Added `exportReport()` method to `BudgetsApiService` with Blob error handling
- Created `BudgetReportDialogComponent` (standalone, inline template, frequency/year form, loading/error states)
- Added "Export Report" button to budget list page header (Director only)
- Added "Export Report" button to budget detail page actions area (Director only)
- Both builds pass: dotnet=0 errors, ng=0 new errors

### File List

#### New Files
- `apps/api/Infrastructure/Budgets/BudgetReportExcelService.cs`
- `apps/web/src/app/features/budgets/budget-report-dialog.component.ts`

#### Modified Files
- `apps/api/Controllers/V1/BudgetsController.cs` — added `/report/export` endpoint
- `apps/api/Program.cs` — registered `BudgetReportExcelService`
- `apps/web/src/app/features/budgets/services/budgets-api.service.ts` — added `exportReport()` method
- `apps/web/src/app/features/budgets/budgets-list-page.component.ts` — added "Export Report" button
- `apps/web/src/app/features/budgets/budget-detail-page.component.ts` — added "Export Report" button

### Change Log

- Implemented budget expenditure report Excel export (API + Angular UI)
- Code review findings resolved: 5 decisions, 4 patches applied, 2 deferred

## Review Findings

### Decision Needed

- [x] [Review][Decision] Null guard on `report` parameter in `BudgetReportExcelService.Generate()` — Added `ArgumentNullException.ThrowIfNull(report)`.
- [x] [Review][Decision] Service interface abstraction — Extracted `IBudgetReportExportService`, controller and DI updated.
- [x] [Review][Decision] Empty-data orphaned total row — Summary row now only renders when `Lines.Count > 0`.
- [x] [Review][Decision] No generation timestamp in Excel — Added "(Generated: {UTC datetime})" to header.
- [x] [Review][Decision] No freeze panes on worksheet — Added `ws.SheetView.Freeze(4, 0)`.

### Patches

- [x] [Review][Patch] DI lifetime mismatch: `AddTransient` should be `AddScoped` per spec [Program.cs:129]
- [x] [Review][Patch] Missing `year` bounds validation in ExportReport endpoint — values < 1 or > 9998 crash with `ArgumentOutOfRangeException` [Controller:500]
- [x] [Review][Patch] Division by zero in summary utilization percentage — `TotalAllocated` can be 0 even with non-empty lines [BudgetReportExcelService.cs:66]
- [x] [Review][Patch] Missing `BudgetBusinessRuleException` → 422 catch block — spec explicitly requires this [Controller:485]

### Deferred

- [x] [Review][Defer] No column min/max width constraints — `AdjustToContents()` alone can produce extreme column widths [BudgetReportExcelService.cs:72] — deferred, nice-to-have formatting enhancement
- [x] [Review][Defer] No identity metadata in Excel (generated-by user, request ID) — beyond story scope, pre-existing pattern
