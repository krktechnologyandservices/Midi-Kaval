---
baseline_commit: 20146278a5c82080b95cba06539c23c64d429fa6
---

# Story 15.1: Socio-Demographic Profile Report

Status: done

## Story

As a **Project Coordinator**,
I want to generate the monthly socio-demographic profile report matching the KavalSample.xlsx format,
so that DCPU receives the required reporting.

## Acceptance Criteria

**AC 1 — Report Generation:**
Given Cases exist with the new socio-demographic fields
When I generate the report for a given month/year
Then the Excel output has two sections matching the KavalSample.xlsx format:

- **Section 1 — List of children** with columns: Sl No, Name, Age, Contact, Case Committed, Crime Number, ST Number, Status, Present Stage
- **Section 2 — Cross-tabulation count table** with rows for each dimension and columns per category value:

| Dimension | Source Field on Case | Categories |
|-----------|---------------------|------------|
| Gender | `Case.Gender` | Male, Female, Transgender |
| Age Group | `Case.BeneficiaryAge` | 0–6, 7–11, 12–15, 16–18 (computed) |
| Occupation | `Case.Occupation.Name` (via `Occupations` legends table) | Values from legends |
| Domicile | `Case.Domicile` | Urban, Rural, Coastal, Tribal, Slum |
| Education | `Case.EducationLevel.Name` (via `EducationLevels` legends table) | Values from legends |
| Family Type | `Case.FamilyType` | Joint, Nuclear, SingleParent, Others |
| Economic Status | `Case.EconomicStatus` | APL, BPL |
| Frequency | `Case.IsFirstTimeOffender` | First time, Repeat |
| Family History | `Case.FamilyHistoryOfCrime` | Yes, No |
| Recidivism | `Case.RecidivismBeforeCount`, `Case.RecidivismAfterCount` | Count distributions |
| Nature of Offence | `Case.TypeOfOffence` | Values from cases |

Each cell = count of cases matching that category, filtered to the selected month/year by `Case.CreatedAtUtc`.

**AC 2 — Month/Year Selection:**
Given the report generation page
When the Coordinator selects a month and year
Then the report data is filtered to cases created in that month/year

**AC 3 — Empty Data:**
Given no Cases exist for the selected month/year
When the report is generated
Then the Excel contains "No data available for the selected period" in both sections

**AC 4 — Error Handling:**
Given the API returns an error
When the export fails
Then an error message is displayed clearly

**AC 5 — Loading State:**
Given the report is being generated
When data is loading
Then a loading indicator is shown while the report is being generated

**AC 6 — Coordinator Access:**
Given the Coordinator (or above) role
When viewing the reports page
Then the "Generate Socio-Demographic Profile" button is visible

## Tasks / Subtasks

- [x] **Create SocioDemographicProfileService** (AC: 1, 2, 3)
  - [x] Create `apps/api/Infrastructure/Reports/SocioDemographicProfileService.cs`
  - [x] Inject `AppDbContext` (existing pattern from `ReportGenerationService`)
  - [x] Query Cases for the given month/year (filter by `Case.CreatedAtUtc`)
  - [x] Include `Occupation` and `EducationLevel` navigation properties
  - [x] Build Section 1 data: list of children with Sl No, Name (`BeneficiaryName`), Age (`BeneficiaryAge`), Contact (`BeneficiaryContact`), Case Committed (`CreatedAtUtc` formatted), Crime Number (`CrimeNumber`), Status (derive from `CurrentStage`), Present Stage (`CurrentStage.ToString()`)
  - [x] Build Section 2 data: cross-tabulation counts grouped by each dimension
  - [x] Handle empty data (no cases found → return empty state)

- [x] **Create SocioDemographicProfileExcelService** (AC: 1, 2, 3)
  - [x] Create `apps/api/Infrastructure/Reports/SocioDemographicProfileExcelService.cs`
  - [x] Use ClosedXML (same library as `BudgetReportExcelService`)
  - [x] Sheet 1 (or Section 1 in single sheet): "List of Children" — table with header row, data rows, auto-fit columns
  - [x] Sheet 2 (or Section 2): "Cross-Tabulation" — each dimension as a sub-table with category columns and count cells
  - [x] Empty data: "No data available for the selected period" message
  - [x] Use bold headers, alternating row colors for readability, freeze panes on headers
  - [x] Filename: `Socio-Demographic-Profile-{Month}-{Year}.xlsx`

- [x] **Create SocioDemographicReportsController** (AC: 1, 2, 4)
  - [x] Create `apps/api/Controllers/V1/SocioDemographicReportsController.cs`
  - [x] Add `GET /api/v1/reports/socio-demographic-profile?month={1-12}&year={yyyy}` endpoint
  - [x] Authorize with `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] Validate month (1–12) and year (2000–2100)
  - [x] Return `FileContentResult` with Excel bytes
  - [x] Return 404 if no data, 400 for invalid parameters

- [x] **Create SocioDemographicProfileModels** (AC: 1)
  - [x] Create `apps/api/Models/Reports/SocioDemographicDtos.cs`
  - [x] `SocioDemographicProfileDto` — Sections list, Month, Year
  - [x] `ChildListItemDto` — SlNo, Name, Age, Contact, CaseCommittedDate, CrimeNumber, Status, PresentStage
  - [x] `CrossTabulationSectionDto` — DimensionName, Categories list with CategoryName + Count
  - [x] `CrossTabulationCategoryDto` — CategoryName, Count
  - [x] `ProfileReportRequestDto` — Month (int 1–12), Year (int)

- [x] **Register services in DI** (AC: 1)
  - [x] Add `builder.Services.AddScoped<SocioDemographicProfileService>()` in `Program.cs`
  - [x] Add `builder.Services.AddScoped<SocioDemographicProfileExcelService>()` in `Program.cs`

- [x] **Create report generation dialog** (AC: 2, 4, 5)
  - [x] Create `apps/web/src/app/features/reports/socio-demographic-report-dialog.component.ts`
  - [x] Standalone component with inline template, signals pattern
  - [x] Month select (1–12, display as month names) + Year input (default current year)
  - [x] "Generate" button triggers download via API service
  - [x] Loading state while generating
  - [x] Error display on failure

- [x] **Build and verify**
  - [x] Run `dotnet build` on API — 0 errors
  - [x] Run `ng build` on web — 0 new errors

## Dev Notes

### Existing Case Entity — Socio-Demographic Fields

The `Case` entity at `apps/api/Domain/Entities/Case.cs` already has all required fields from Epic 11:

```csharp
public sealed class Case
{
    // Core identity
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public int? BeneficiaryAge { get; set; }
    public string? BeneficiaryContact { get; set; }

    // Offence data
    public string TypeOfOffence { get; set; } = string.Empty;
    public OffenceClassification OffenceClassification { get; set; }
    public Domicile Domicile { get; set; }

    // Socio-demographic (from Epic 11)
    public Gender? Gender { get; set; }
    public FamilyType? FamilyType { get; set; }
    public EconomicStatus? EconomicStatus { get; set; }
    public Guid? OccupationId { get; set; }
    public Guid? EducationLevelId { get; set; }
    public Occupation? Occupation { get; set; }        // nav property
    public EducationLevel? EducationLevel { get; set; } // nav property
    public bool FamilyHistoryOfCrime { get; set; } = false;
    public int? RecidivismBeforeCount { get; set; }
    public int? RecidivismAfterCount { get; set; }
    public bool IsFirstTimeOffender { get; set; } = true;

    // Lifecycle
    public CaseStage CurrentStage { get; set; } = CaseStage.ProcessInitiation;
    public DateTime CreatedAtUtc { get; set; }
    // ... other fields
}
```

### Enum Definitions

| Enum | File | Values |
|------|------|--------|
| `Gender` | `Domain/Enums/Gender.cs` | Male, Female, Transgender |
| `FamilyType` | `Domain/Enums/FamilyType.cs` | Joint, Nuclear, SingleParent, Others |
| `EconomicStatus` | `Domain/Enums/EconomicStatus.cs` | APL, BPL |
| `Domicile` | `Domain/Enums/Domicile.cs` | Urban, Rural, Coastal, Tribal, Slum |
| `OffenceClassification` | `Domain/Enums/OffenceClassification.cs` | Petty, Serious, Heinous |
| `CaseStage` | `Domain/Enums/CaseStage.cs` | ProcessInitiation, MaintainAndDevelopment, InterSectoralApproach, Rehabilitation, Reintegration, TerminationExclusion |

### Legend Tables

| Entity | File | DbSet | FK from Case |
|--------|------|-------|-------------|
| `Occupation` | `Domain/Entities/Legends/Occupation.cs` | `db.Occupations` | `Case.OccupationId` |
| `EducationLevel` | `Domain/Entities/Legends/EducationLevel.cs` | `db.EducationLevels` | `Case.EducationLevelId` |

Both legends have: `Id (Guid)`, `OrganisationId (Guid)`, `Name (string)`, `IsActive (bool)`.

### Existing DTOs with Socio-Demographic Fields

The `CaseExportRowDto` at `apps/api/Models/Cases/CaseExportRowDto.cs` already includes socio-demographic fields (Gender, FamilyType, EconomicStatus, Occupation, EducationLevel, FamilyHistoryOfCrime, RecidivismBefore/After) — can reference for data shape, but do NOT modify.

### PII Sensitivity

`BeneficiaryName`, `BeneficiaryAge`, and `BeneficiaryContact` are PII-protected fields. In the existing API, they are **excluded** from default DTOs (`CaseDto`, `CaseSummaryDto`) and only exposed via a dedicated `RevealCasePiiResponse` endpoint. Since the socio-demographic report is Coordinator+ only (matching the access level of the PII endpoint), exposing these fields in the Excel report is acceptable — but be mindful of the sensitivity.

### Age Groups (Section 2)

Compute age group from `BeneficiaryAge`:
- 0–6 → "0–6 years"
- 7–11 → "7–11 years"
- 12–15 → "12–15 years"
- 16–18 → "16–18 years"
- null/missing → "Unknown"

### Section 1 Columns

| Column | Source | Notes |
|--------|--------|-------|
| Sl No | Row index (1-based) | Computed |
| Name | `Case.BeneficiaryName` | String |
| Age | `Case.BeneficiaryAge` | Int (nullable — show "—" if null) |
| Contact | `Case.BeneficiaryContact` | String (nullable — show "—" if null) |
| Case Committed | `Case.CreatedAtUtc` | Format as dd-MMM-yyyy |
| Crime Number | `Case.CrimeNumber` | String |
| ST Number | `Case.StNumber` | String |
| Status | `Case.CurrentStage` | Map to human-readable: ProcessInitiation→"Process Initiation", MaintainAndDevelopment→"Maintain & Development", InterSectoralApproach→"Inter-Sectoral Approach", Rehabilitation→"Rehabilitation", Reintegration→"Reintegration", TerminationExclusion→"Termination/Exclusion" |
| Present Stage | `Case.CurrentStage.ToString()` | Same data; KavalSample format includes both Status and Present Stage columns |

### Section 2 — Cross-Tabulation Pattern

Each dimension is a sub-table with:
- **Header row**: Dimension name + each category column name
- **Data row**: Category → count of cases matching that category for the selected month/year

Example structure for Gender:
```
| Gender | Male | Female | Transgender |
|--------|------|--------|-------------|
| Count  | 12   | 8      | 1           |
```

For Occupation and EducationLevel, categories come from the `Occupations` and `EducationLevels` legend tables (not the enum). Query them dynamically.

For Recidivism: just show "Recidivism Before" and "Recidivism After" with average counts or count distributions.

Domicile and OffenceClassification can be included as additional dimensions.

### Age Group Query Pattern

```csharp
var cases = await db.Cases
    .Include(c => c.Occupation)
    .Include(c => c.EducationLevel)
    .Where(c => c.OrganisationId == organisationId
        && c.CreatedAtUtc.Year == year
        && c.CreatedAtUtc.Month == month)
    .ToListAsync(ct);
```

### Excel Generation Pattern

Follow the pattern from `BudgetReportExcelService` (ClosedXML):

```csharp
using var workbook = new XLWorkbook();
var ws = workbook.Worksheets.Add("List of Children");
// Section 1: header row + data rows
// Section 2 (new sheet): Cross-Tabulation
//   For each dimension, write a sub-table
// Freeze panes, bold headers, column widths
using var stream = new MemoryStream();
workbook.SaveAs(stream);
return stream.ToArray();
```

Consider using **two worksheets** in the same workbook:
- Sheet 1: "List of Children" — flat table with Sl No, Name, ...
- Sheet 2: "Cross-Tabulation" — sub-tables for each dimension

### API Endpoint Pattern

**Controller choice:** Create a dedicated `SocioDemographicReportsController.cs` (new controller) rather than adding to the existing `ReportsController` which uses an async job pattern. This endpoint is a direct download, so keep it cleanly separated.

```csharp
/// <summary>
/// Generate socio-demographic profile report.
/// </summary>
[HttpGet("socio-demographic-profile")]
[Authorize(Policy = Policies.CoordinatorOrAbove)]
[Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetSocioDemographicProfile(
    [FromQuery] int month,
    [FromQuery] int year,
    CancellationToken ct = default)
```

The `ApiEnvelopeFilter` automatically skips `FileContentResult` responses (same pattern as budget report).

### Error Handling

- Invalid month (not 1–12) or year (not 2000–2100) → 400 BadRequest
- No data found → 404 NotFound
- Database error → 500 (let global exception handler manage)
- Use pattern from existing endpoint: wrap in try/catch

### Frontend — Dialog Component

Create `apps/web/src/app/features/reports/socio-demographic-report-dialog.component.ts`:
- Standalone, inline template, signals
- Month select (dropdown with month names Jan–Dec) + Year input (default current year, min 2000, max 2100)
- "Generate" button calls API and triggers download
- Loading spinner + error banner pattern (see `BudgetReportDialogComponent` for reference)

### API Service Method

Since there's no existing reports API service, either:
- Create a new `ReportsApiService` at `apps/web/src/app/features/reports/services/reports-api.service.ts`

Pattern (same as BudgetsApiService.exportReport):
```typescript
async exportSocioDemographicProfile(month: number, year: number): Promise<Blob> {
  const params = new HttpParams().set('month', month).set('year', year);
  return await firstValueFrom(
    this.http.get(`${this.baseUrl}/reports/socio-demographic-profile`, {
      params,
      responseType: 'blob',
    }),
  );
}
```

### Button Placement

Since there's no existing reports page in the Angular web app (Story 8-6 was never built based on the empty `features/reports/` directory), the report access can be:
- Add a standalone report generation page at `/reports` route
- Or keep it simple with a button in the Budgets nav section or general Reports nav section

**Recommendation:** Add a "Socio-Demographic Profile" button on a simple reports list page or add route `/reports/socio-demographic-profile` that directly opens the dialog.

### Existing File Structure (for reference)

```
apps/api/
├── Controllers/V1/ReportsController.cs    — Existing (async export jobs pattern)
├── Infrastructure/Reports/
│   ├── ReportGenerationService.cs           — Reference for data query pattern
├── Domain/Entities/Case.cs                  — Existing Case entity with all fields
├── Domain/Entities/Legends/
│   ├── Occupation.cs                        — Legend entity (FK from Case)
│   └── EducationLevel.cs                    — Legend entity (FK from Case)
├── Domain/Enums/Gender.cs                   — Male, Female, Transgender
├── Domain/Enums/FamilyType.cs               — Joint, Nuclear, SingleParent, Others
├── Domain/Enums/EconomicStatus.cs           — APL, BPL
├── Domain/Enums/Domicile.cs                 — Urban, Rural, Coastal, Tribal, Slum
├── Domain/Enums/OffenceClassification.cs    — Petty, Serious, Heinous
├── Domain/Enums/CaseStage.cs                — 6 stages of case lifecycle
├── Models/Reports/ReportDtos.cs             — Existing report DTOs
└── Program.cs                               — Register new services

apps/web/src/app/features/reports/           — Currently empty directory
├── socio-demographic-report-dialog.component.ts  — NEW
└── services/reports-api.service.ts               — NEW
```

### Previous Story Intelligence

**Key learnings from Story 14.4 (Budget Expenditure Report):**
1. Excel generation uses ClosedXML with `XLWorkbook` + `MemoryStream` pattern
2. `FileContentResult` is automatically bypassed by `ApiEnvelopeFilter`
3. Use `responseType: 'blob'` for file download in Angular
4. Angular services use `firstValueFrom` + `HttpClient`, `@Injectable({ providedIn: 'root' })`
5. Components use standalone with inline templates, signals for state, `inject()` for DI
6. Error pattern: `extractErrorMessage()` or similar for user-friendly messages
7. DI registration: `builder.Services.AddScoped<TService>()` in `Program.cs`

### Testing Standards Summary

- **API:** Unit test Excel generation with valid data (2+ cases), empty data (no cases), invalid month/year parameters
- **Angular:** Test dialog opens/closes, form validation, export triggers correct API call, error state renders

## File List

### New Files
- `apps/api/Infrastructure/Reports/SocioDemographicProfileService.cs` — queries Cases, builds report DTOs
- `apps/api/Infrastructure/Reports/SocioDemographicProfileExcelService.cs` — ClosedXML Excel generation (2 sheets)
- `apps/api/Models/Reports/SocioDemographicDtos.cs` — report DTOs (ChildListItem, CrossTabulation, etc.)
- `apps/api/Controllers/V1/SocioDemographicReportsController.cs` — GET /api/v1/reports/socio-demographic-profile
- `apps/web/src/app/features/reports/socio-demographic-report-dialog.component.ts` — month/year dialog with download
- `apps/web/src/app/features/reports/services/reports-api.service.ts` — Angular API service for reports

### Modified Files
- `apps/api/Program.cs` — registered SocioDemographicProfileService and SocioDemographicProfileExcelService
- `apps/web/src/app/features/shell/pages/reports-page.component.ts` — added dialog import, button, method
- `apps/web/src/app/features/shell/pages/reports-page.component.html` — added "Socio-Demographic Profile" button in header
- `apps/web/src/app/features/shell/pages/reports-page.component.scss` — added header-actions style

## Review Findings

### Code Review — Story 15.1

Three parallel review subagents (Blind Hunter, Edge Case Hunter, Acceptance Auditor) reviewed the implementation. Findings were triaged into **patch** (applied), **defer**, and **dismiss**.

#### Applied Patches (10 items)

| # | Finding | File(s) |
|---|---------|---------|
| P1 | Empty data returned 404 instead of Excel with "No data" message — changed controller to stream the empty Excel | `SocioDemographicReportsController.cs` |
| P2 | `extractErrorMessage` recursion bug when `sourceError` is a string — added type guard | `reports-api.service.ts` |
| P3 | Nullable enums (Gender, FamilyType, EconomicStatus) silently excluded from cross-tabulation — added "Unknown" category for null counts | `SocioDemographicProfileService.cs` |
| P4 | Null Occupation/Education nav properties silently excluded — added "Not Specified" row for null values | `SocioDemographicProfileService.cs` |
| P5 | `JSON.parse` on non-JSON Blob bodies throws unhandled `SyntaxError` — wrapped in try/catch | `reports-api.service.ts` |
| P6 | Dead `catch (OperationCanceledException) { throw; }` block removed from controller | `SocioDemographicReportsController.cs` |
| P7 | `DateTime.UtcNow` in Excel title made output non-deterministic — passed as parameter from controller | `SocioDemographicProfileExcelService.cs`, `SocioDemographicReportsController.cs` |
| P8 | Recidivism average treated nulls as 0 (deflating avg) and used `(int)` truncation — filtered nulls, used `Math.Round` | `SocioDemographicProfileService.cs` |
| P9 | `revokeObjectURL` called immediately before download starts (Safari/mobile risk) — wrapped in `setTimeout(100ms)` | `socio-demographic-report-dialog.component.ts` |
| P10 | Client-side year validation bypassed by decimal input — added `Number.isInteger()` check | `socio-demographic-report-dialog.component.ts` |

#### Deferred (4 items)

- O(n×m) iteration in `CrossTabulationCategoryDto` construction — acceptable at current scale
- `AdjustToContents` performance for large datasets — pre-existing pattern
- Non-nullable enum defaults (`OffenceClassification`, `Domicile`) don't distinguish unset — pre-existing domain model constraint
- Missing `AbortController` for cancellation — out of scope, can be added later

#### Dismissed (8 items)

False positives or intentional design choices: logging patterns, null-forgiving operator usage, `HttpParams` setting style, `[Authorize]` double attribute, `ws.Cell` value coercion, enum name ordering, PII exposure scope, `ArgumentNullException.ThrowIfNull` style.

## Dev Agent Record

### Implementation Plan

Story 15.1 adds monthly socio-demographic profile report generation. The backend needs:
1. A service to query Cases and build the two-section report data
2. An Excel generation service to render the data using ClosedXML
3. An API endpoint returning the file
4. An Angular dialog for month/year selection and download

### Debug Log

### Completion Notes List

- Created `SocioDemographicDtos.cs` with ChildListItemDto, CrossTabulationSectionDto, CrossTabulationCategoryDto, SocioDemographicProfileDto
- Created `SocioDemographicProfileService.cs` — queries Cases by org/month/year, builds Section 1 (child list) and Section 2 (cross-tabulation for all 12 dimensions)
- Created `SocioDemographicProfileExcelService.cs` — ClosedXML with Sheet 1 ("List of Children") and Sheet 2 ("Cross-Tabulation"), alternating row colors, freeze panes, empty data handling
- Created `SocioDemographicReportsController.cs` with GET /api/v1/reports/socio-demographic-profile (Coordinator+)
- Registered both services in Program.cs DI
- Created Angular `ReportsApiService` with exportSocioDemographicProfile method and error handling
- Created Angular dialog component with month/year selection, loading state, error display, and download
- Added "Socio-Demographic Profile" button to Reports page header that opens the dialog
- Builds clean: API 0 errors, Web 0 new errors

### File List

### Change Log

- 2026-06-22: Implemented Story 15.1 — Socio-Demographic Profile Report generation. Created DTOs, data service, Excel service, controller, Angular dialog, and API service. Updated Reports page with button. All builds pass.
- 2026-06-22: Code review applied — 10 patch items fixed (empty data Excel flow, error handling, nullable enum support, null nav property fallback, JSON parse safety, timestamp determinism, recidivism math, download timing, client validation). Builds clean (0 errors). Status updated to `done`.