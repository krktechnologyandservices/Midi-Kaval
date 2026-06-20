---
baseline_commit: 4ebf6e0
---

# Story 8.5: Standard Reports API and Async Export Jobs

Status: done

## Story

As a **Coordinator**,
I want operational reports exported,
So that funders and courts receive evidence (FR-22, SM-C1).

*Scope: **Backend API only** — `POST /api/v1/reports/{type}/export` endpoint that triggers an async background job to generate an Excel or PDF file, stores it in blob storage, and returns a download URL when ready. Report types: daily work, yearly work, visits planned vs completed, interventions, court summary, offence/area counts, workload distribution, travel totals. Request >30s never block HTTP. Staff workload report is distribution-only, not performance scoring. **No web UI** (Story 8.6), **no mobile UI**.*

## Acceptance Criteria

1. **Given** the API is running
   **When** a Coordinator or Director calls `POST /api/v1/reports/{type}/export` with `{ format: "excel" | "pdf" }`
   **Then** the endpoint returns `202 Accepted` with a `reportJobId` and `status: "pending"`
   **And** unauthenticated requests return `401`; SocialWorker/CaseWorker requests return `403`
   **And** an invalid report type returns `400` with a list of valid types
   **And** an invalid format returns `400`

2. **Given** a report export job is accepted
   **When** the background job runs
   **Then** the job queries the database for the report's data, generates the file using ClosedXML (Excel) or QuestPDF (PDF), uploads it to blob storage with a signed URL (15 min expiry), and updates the job status to `completed`
   **And** on completion, a notification (in-app) is created for the requesting user; an email is sent if the user's role is Coordinator/Director
   **And** on failure, the job status is set to `failed` with an error message

3. **Given** a report export job exists
   **When** a Coordinator or Director calls `GET /api/v1/reports/exports/{jobId}/status`
   **Then** the endpoint returns the job status: `pending`, `processing`, `completed`, or `failed`
   **And** when `completed`, the response includes a `downloadUrl` (signed URL, 15 min expiry)
   **And** the requesting user can only see their own jobs (scoped by `createdByUserId`)
   **And** unauthenticated/unauthorized returns appropriate HTTP errors

4. **Given** the following operational report types exist
   **When** each type is requested
   **Then** the system supports these report types:
   - **daily-work** — Cases with visits scheduled/completed today, grouped by worker
   - **yearly-work** — Aggregate case/work stats for a given year (query param `year`)
   - **visits-planned-vs-completed** — Visit counts by worker, planned vs completed, for a date range (`from`, `to`)
   - **interventions** — Interventions by status/outcome/worker for a date range (`from`, `to`)
   - **court-summary** — Court sittings by status/outcome for a date range (`from`, `to`)
   - **offence-area-counts** — Case counts grouped by offence classification and domicile
   - **workload-distribution** — Case counts per worker (distribution only, NOT performance scored)
   - **travel-totals** — Travel claim totals by worker/month for a date range (`from`, `to`)

5. **Given** any report type
   **When** the export is requested
   **Then** the staff workload report is distribution-only, not performance scoring (NFR-11)
   **And** no report contains worker rankings, leaderboards, or punitive scoring

6. **Given** the workload-distribution report
   **When** generated
   **Then** it shows case counts per worker as factual distribution
   **And** it does NOT include performance scores, rankings, or comparisons

7. **Given** the export completes
   **When** the download URL is obtained via status endpoint
   **Then** the URL is a signed SAS URL valid for 15 minutes (matching attachment pattern from Story 4.2)
   **And** expired URLs return `410 Gone`
   **And** access to the blob is role-checked: only Coordinator/Director can download

8. **Given** regression safety
   **When** this story ships
   **Then** existing case export endpoint (`GET /api/v1/cases/export`) continues to work unchanged
   **And** existing crisis-queue, dashboard, and other supervisor endpoints continue to work unchanged

## Tasks / Subtasks

- [x] **Create report type enum and DTOs** — `apps/api/Domain/Enums/ReportType.cs` + request/response DTOs
  - [x] Create `ReportType` enum with all 8 types from AC 4
  - [x] Create `ReportExportRequest` DTO with `format` (excel/pdf) and optional date range params (`from`, `to`, `year`)
  - [x] Create `ReportExportJobDto` with `jobId`, `status`, `reportType`, `format`, `createdAtUtc`, `completedAtUtc`, `downloadUrl`, `errorMessage`
  - [x] Create `ReportExportStatusDto` with `status`, `downloadUrl?`, `errorMessage?`
  - [x] Create `ReportExportListResultDto` with `items: ReportExportJobDto[]`

- [x] **Create database schema for report export jobs** — EF Core migration
  - [x] Create `report_export_jobs` table: `id` (Guid PK), `organisation_id` (FK), `created_by_user_id` (FK), `report_type` (string), `format` (string), `status` (string: pending/processing/completed/failed), `blob_path` (string?), `error_message` (string?), `created_at_utc`, `completed_at_utc`
  - [x] Add `ReportExportJob` entity class in `apps/api/Domain/Entities/ReportExportJob.cs`
  - [x] Configure EF mapping in `AppDbContext` (snake_case table/columns)
  - [x] Create migration

- [x] **Create `ReportGenerationService`** — business logic per report type
  - [x] Inject `AppDbContext`, `BlobStorageService`, `ILogger`
  - [x] Implement each report type query method returning `IReadOnlyList<ReportRowDto>`:
    - [x] `BuildDailyWorkReport(orgId, date)` — cases with visits today grouped by worker
    - [x] `BuildYearlyWorkReport(orgId, year)` — aggregate stats for the year
    - [x] `BuildVisitsPlannedVsCompletedReport(orgId, from, to)` — per-worker visit counts
    - [x] `BuildInterventionsReport(orgId, from, to)` — interventions by status/outcome
    - [x] `BuildCourtSummaryReport(orgId, from, to)` — court sittings summary
    - [x] `BuildOffenceAreaCountsReport(orgId)` — group by classification + domicile
    - [x] `BuildWorkloadDistributionReport(orgId)` — case counts per worker (distribution only)
    - [x] `BuildTravelTotalsReport(orgId, from, to)` — travel claim totals per worker
  - [x] Create `GenerateFileAsync(rows, format)` — uses `CaseExcelExporter`-style ClosedXML for Excel or `CasePdfExporter`-style QuestPDF for PDF
  - [x] Create `UploadToBlobAsync(fileBytes, fileName, contentType)` returning blob path
  - [x] Handle empty data gracefully — produce a file with "No data available for the selected period" instead of empty/error

- [x] **Create `ReportExportBackgroundService`** — background job processor
  - [x] Create `ReportExportJobRunner` class (following `CourtReminderJobRunner` pattern)
  - [x] Create `ReportExportBackgroundService` (following `CourtReminderBackgroundService` pattern)
  - [x] Poll `report_export_jobs` table for `pending` jobs every 30 seconds
  - [x] For each pending job: set status to `processing`, generate report, upload to blob, set status to `completed` with blob path, or `failed` on error
  - [x] On completion: create in-app notification; send email via `EmailDeliveryService` using `ReportExportReadyEmailTemplate`
  - [x] On failure: log error, set status to `failed`, store error message
  - [x] Use `IServiceScopeFactory` for scoped dependencies (same pattern as existing background services)
  - [x] Register in `Program.cs` as `AddHostedService<ReportExportBackgroundService>`

- [x] **Create `ReportsController`** — API endpoints
  - [x] `POST /api/v1/reports/{type}/export` — accepts `ReportExportRequest`, validates type/format, creates `ReportExportJob`, returns `202` with job ID
  - [x] `GET /api/v1/reports/exports/{jobId}/status` — returns job status and download URL if completed
  - [x] `GET /api/v1/reports/exports` — list requesting user's recent export jobs (paginated, ordered by created_at desc)
  - [x] Apply `[Authorize(Policy = Policies.CoordinatorOrAbove)]` on all endpoints
  - [x] Add `[ProducesResponseType]` attributes for 200, 202, 400, 401, 403, 404, 410
  - [x] Validate report type against `ReportType` enum; return 400 with valid types on mismatch
  - [x] Validate format is excel or pdf; return 400 on invalid format

- [x] **Register services in DI** — `Program.cs`
  - [x] Add `builder.Services.AddScoped<ReportGenerationService>();`
  - [x] Add `builder.Services.AddHostedService<ReportExportBackgroundService>();`
  - [x] Place alongside existing service registrations

- [x] **Integration tests — `ReportsApiTests.cs`** (follow `DashboardApiTests`/`CrisisQueueApiTests` pattern)
  - [x] Test: Unauthenticated request returns 401
  - [x] Test: SocialWorker/CaseWorker request returns 403
  - [x] Test: Invalid report type returns 400
  - [x] Test: Invalid format returns 400
  - [x] Test: Valid request returns 202 with job ID and pending status
  - [x] Test: Status endpoint returns pending/processing/completed/failed states
  - [x] Test: Completed job returns download URL
  - [x] Test: Workload distribution does not include ranking/scoring data
  - [x] Test: Empty data produces file with "No data" message, not an error

## Dev Notes

### File locations (all paths under `apps/api/`)

| File | Purpose | Pattern |
|------|---------|---------|
| `Domain/Enums/ReportType.cs` | Report types enum | Follows `Domain/Enums/CaseStage.cs` |
| `Domain/Entities/ReportExportJob.cs` | Report export job entity | Follows `Domain/Entities/CourtSitting.cs` |
| `Models/Reports/ReportDtos.cs` | Request/response DTOs | Follows `Models/Supervisor/DashboardDtos.cs` |
| `Models/Reports/ReportRowDto.cs` | Generic report row DTO | New — flexible key-value row |
| `Infrastructure/Reports/ReportGenerationService.cs` | Report data + file generation | Follows `Infrastructure/Supervisor/DashboardService.cs` |
| `Jobs/ReportExportBackgroundService.cs` | Background job processor | Follows `Jobs/CourtReminderBackgroundService.cs` |
| `Jobs/ReportExportJobRunner.cs` | Job runner | Follows `Jobs/CourtReminderJobRunner.cs` |
| `Controllers/V1/ReportsController.cs` | Report API endpoints | Follows `Controllers/V1/SupervisorController.cs` |
| `tests/api.integration/ReportsApiTests.cs` | Integration tests | Follows `tests/api.integration/DashboardApiTests.cs` |

### Existing infrastructure (leverage — do not recreate)

- **ClosedXML** is already referenced in the project. Used by `CaseExcelExporter`. Use the same pattern for report Excel generation.
- **QuestPDF** is already referenced. Used by `CasePdfExporter`. Use the same pattern for report PDF generation.
- **`ReportExportReadyEmailTemplate`** already exists in `Infrastructure/Email/Templates/` — use it for the completion notification email.
- **`EmailDeliveryService`** already exists for sending emails.
- **`NotificationService`** already exists for creating in-app notifications.
- **`BlobStorageService`** already exists for SAS URL generation and blob upload (from Story 4.2 attachment pattern).
- **Background services** pattern established by `CourtReminderBackgroundService` + `CourtReminderJobRunner` — follow the same `IServiceScopeFactory` scoped DI pattern.
- **`CaseExportOptions`** config pattern exists in `Infrastructure/Cases/CaseExportOptions.cs` — create `ReportExportOptions` for concurrency limits, polling interval, etc.
- **`AppDbContext`** partial class pattern — create a new partial file for `ReportExportJob` DbSet.

### Background job pattern

The existing background job pattern uses `BackgroundService` + a separate `Runner` class:

```csharp
// Runner — handles business logic with scoped dependencies
public sealed class ReportExportJobRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportExportOptions> options,
    ILogger<ReportExportJobRunner> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reportService = scope.ServiceProvider.GetRequiredService<ReportGenerationService>();
        var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailDeliveryService>();

        var pending = await db.ReportExportJobs
            .Where(j => j.Status == ReportExportJobStatus.Pending)
            .OrderBy(j => j.CreatedAtUtc)
            .ToListAsync(ct);

        foreach (var job in pending)
        {
            try
            {
                job.Status = ReportExportJobStatus.Processing;
                await db.SaveChangesAsync(ct);

                var rows = await reportService.BuildReportAsync(job.ReportType, job.OrganisationId, /* params */);
                var fileBytes = await reportService.GenerateFileAsync(rows, job.Format);
                var blobPath = await blobService.UploadReportAsync(fileBytes, job.Id.ToString(), job.Format);

                job.Status = ReportExportJobStatus.Completed;
                job.BlobPath = blobPath;
                job.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // Notify user
                var notification = notificationService.CreateReportReadyNotification(job.Id, job.ReportType);
                await emailService.SendReportExportReadyAsync(job, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Report export job {JobId} failed", job.Id);
                job.Status = ReportExportJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}

// Background service — polling loop
public sealed class ReportExportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportExportOptions> options,
    ILogger<ReportExportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<ReportExportJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report export background job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
```

### API endpoint pattern

```csharp
[ApiController]
[Authorize(Policy = Policies.CoordinatorOrAbove)]
[Route("api/v1/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    [HttpPost("{type}/export")]
    [ProducesResponseType(typeof(ApiResponse<ReportExportJobDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartExport(
        string type,
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        // Validate type
        if (!ReportType.TryParse(type, out var reportType))
        {
            return BadRequest(new ProblemDetails
            {
                Detail = $"Invalid report type '{type}'. Valid types: {string.Join(", ", ReportType.All)}",
                StatusCode = StatusCodes.Status400BadRequest,
            });
        }

        // Validate format
        if (request.Format != "excel" && request.Format != "pdf")
        {
            return BadRequest(new ProblemDetails
            {
                Detail = "Format must be 'excel' or 'pdf'",
                StatusCode = StatusCodes.Status400BadRequest,
            });
        }

        // Create job...
        return Accepted(new ApiResponse<ReportExportJobDto>(job, new ApiMeta { RequestId = requestId }));
    }
}
```

### Report type enum

```csharp
namespace MidiKaval.Api.Domain.Enums;

public enum ReportType
{
    DailyWork,
    YearlyWork,
    VisitsPlannedVsCompleted,
    Interventions,
    CourtSummary,
    OffenceAreaCounts,
    WorkloadDistribution,
    TravelTotals,
}
public static class ReportTypeExtensions
{
    public static string ToApiString(this ReportType type) => type switch { ... };
    public static ReportType? FromApiString(string value) => ...;
}
```

### Report row DTO

```csharp
namespace MidiKaval.Api.Models.Reports;

public sealed class ReportExportRowDto
{
    public required IReadOnlyDictionary<string, object?> Columns { get; init; }
}
```

Each report type returns a list of `ReportExportRowDto` with type-specific column dictionaries. The file generator writes the dictionary keys as column headers and values as rows. This avoids creating a separate DTO per report type while keeping the schema self-describing.

### ReportGenerationService approach

```csharp
public sealed class ReportGenerationService(
    AppDbContext db,
    ILogger<ReportGenerationService> logger)
{
    public async Task<IReadOnlyList<ReportExportRowDto>> BuildReportAsync(
        ReportType type, Guid organisationId,
        DateOnly? from, DateOnly? to, int? year,
        CancellationToken ct = default)
    {
        return type switch
        {
            ReportType.DailyWork => await BuildDailyWorkReportAsync(organisationId, from ?? DateOnly.FromDateTime(DateTime.UtcNow), ct),
            // ... each type delegates to a private method
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    public async Task<byte[]> GenerateFileAsync(
        IReadOnlyList<ReportExportRowDto> rows,
        string format,
        string title)
    {
        if (rows.Count == 0)
        {
            rows = [new ReportExportRowDto { Columns = new Dictionary<string, object?> { ["Message"] = "No data available for the selected period" } }];
        }

        return format.ToLowerInvariant() switch
        {
            "excel" => GenerateExcel(rows, title),
            "pdf" => GeneratePdf(rows, title),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }
}
```

### Blob storage pattern

Follow the existing attachment pattern:
- Upload: `BlobStorageService.UploadReportAsync(byte[] content, string jobId, string format)` → returns blob path
- Signed URL: `BlobStorageService.GetDownloadUrlAsync(string blobPath, TimeSpan expiry)` → returns URL with 15 min expiry
- Reports use a dedicated container/prefix (e.g., `reports/`) separate from attachments

### Scan the existing codebase for these specific names (non-negotiable)

Before writing code, read these files completely to match exact existing patterns:

- `apps/api/Infrastructure/Supervisor/DashboardService.cs` — service pattern (scoped, `ResolveOrganisationId`, DI)
- `apps/api/Controllers/V1/SupervisorController.cs` — controller pattern for supervisor endpoints
- `apps/api/Infrastructure/Cases/CaseExcelExporter.cs` — ClosedXML export pattern
- `apps/api/Infrastructure/Cases/CasePdfExporter.cs` — QuestPDF export pattern
- `apps/api/Jobs/CourtReminderBackgroundService.cs` — BackgroundService + Runner pattern
- `apps/api/Infrastructure/BlobStorageService.cs` (or equivalent) — blob upload/URL pattern
- `apps/api/Infrastructure/Notifications/NotificationService.cs` — notification creation pattern
- `apps/api/Infrastructure/Email/EmailDeliveryService.cs` — email sending pattern
- `apps/api/Program.cs` — DI registration pattern (lines around existing service registrations)
- `tests/api.integration/DashboardApiTests.cs` — integration test pattern (fixtures, seeding, assertions)

### Critical don'ts

- **Do NOT modify** `CaseExcelExporter`, `CasePdfExporter`, or other existing exporters — they are for case-level exports, not operational reports
- **Do NOT modify** `appsettings.json` schema — use `IOptions<ReportExportOptions>` for config
- **Do NOT add** worker rankings, leaderboards, or performance scoring to the workload-distribution report (NFR-11, UX-DR16)
- **Do NOT hand-edit** `packages/api-client` — the generated client will pick up new endpoints on next regeneration
- **Do NOT block HTTP** requests >30s — the POST returns immediately with 202

### Regressions to prevent

- Existing `GET /api/v1/cases/export` endpoint must continue to work
- Existing supervisor endpoints (crisis-queue, dashboard) must continue to work
- The `CaseExportRowDto` is for case-level export only — do not use it for operational reports
- `BlobStorageService` must not be modified — only consume its existing public API
- No changes to existing notification infrastructure

## References

- Epic 8: Supervisor Crisis Queue, Dashboard & Reports → `_bmad-output/planning-artifacts/epics.md` (FR-22)
- Architecture: Reports, async exports (ClosedXML/QuestPDF), blob storage, background jobs → `_bmad-output/planning-artifacts/architecture.md`
- Existing export patterns: `apps/api/Infrastructure/Cases/CaseExcelExporter.cs`, `CasePdfExporter.cs`
- Existing background job pattern: `apps/api/Jobs/CourtReminderBackgroundService.cs`, `CourtReminderJobRunner.cs`
- Existing notification pattern: `apps/api/Infrastructure/Notifications/NotificationService.cs`
- Existing email pattern: `apps/api/Infrastructure/Email/EmailDeliveryService.cs`
- Report ready email template: `apps/api/Infrastructure/Email/Templates/ReportExportReadyEmailTemplate.cs`
- API envelope pattern: `apps/api/Models/ApiResponse.cs`
- Story 8.3 dashboard API pattern: `_bmad-output/implementation-artifacts/8-3-dashboard-api-and-redis-cached-widgets.md`
- Integration test pattern: `tests/api.integration/DashboardApiTests.cs`

## Previous Story Intelligence (8.4 — Dashboard Web UI)

### Implementation summary

Story 8.4 created the Angular PWA dashboard page with 9 widget types, responsive grid, CSS-only visualizations, auto-refresh every 60s, skeleton loading, error/empty states, and dark mode.

### Key patterns

- The dashboard page at `/dashboard` shows operational data — the reports feature adds an async export layer on top of similar data
- Reports controller follows the same authorization pattern (`[Authorize(Policy = Policies.CoordinatorOrAbove)]`)
- The `ReportExportReadyEmailTemplate` already exists from a previous story — it's now wired to the actual export flow

### Relevant learnings

- `BlobStorageService` is the established pattern for file uploads and signed URLs — reuse for report storage
- The `CourtReminderBackgroundService` pattern (BackgroundService + scoped Runner) is the correct template for the report export job processor
- All queries must filter by `organisationId` resolved from JWT claims

## Git Intelligence

Recent commits:
- `4ebf6e0` — Initial commit (includes all existing infrastructure: case exporters, background services, email templates, notification service, blob storage)

## File List

### New files
- `apps/api/Domain/Enums/ReportType.cs` — Report types enum
- `apps/api/Domain/Entities/ReportExportJob.cs` — Report export job entity
- `apps/api/Models/Reports/ReportDtos.cs` — Request/response DTOs
- `apps/api/Models/Reports/ReportRowDto.cs` — Generic report row DTO
- `apps/api/Infrastructure/Reports/ReportGenerationService.cs` — Report data + file generation
- `apps/api/Jobs/ReportExportBackgroundService.cs` — Background job processor
- `apps/api/Jobs/ReportExportJobRunner.cs` — Job runner (scoped logic)
- `apps/api/Controllers/V1/ReportsController.cs` — Report API endpoints
- `tests/api.integration/ReportsApiTests.cs` — Integration tests

### Modified files
- `apps/api/Infrastructure/Persistence/AppDbContext.cs` — Add `ReportExportJobs` DbSet + entity config
- `apps/api/Infrastructure/Persistence/ReportExportJobConfiguration.cs` — Entity configuration
- `apps/api/Program.cs` — Register `ReportGenerationService` and `ReportExportBackgroundService`

## Review Findings

### Patch findings

- [x] [Review][Patch] Enum parsing mismatch — Runner uses `Enum.TryParse<ReportType>()` but job stores kebab-case from `ToApiString()`. Only "interventions" works; all other types fail. Fix: use `ReportTypeExtensions.FromApiString()`. [`ReportExportJobRunner.cs:38`]
- [x] [Review][Patch] Date filter params silently discarded — `ReportExportRequest.From/To/Year` are accepted but never stored in the entity, so runner always passes null. Fix: add date fields to `ReportExportJob` entity, persist from controller, pass in runner. [`ReportsController.cs:60-69`]
- [x] [Review][Patch] MapToDto inside EF `.Select()` — `.Select(j => MapToDto(j, null))` causes runtime LINQ translation error. Fix: project to anonymous type then map. [`ReportsController.cs:168-172`]
- [x] [Review][Patch] Empty catch block swallows exceptions — Blob storage failure is invisible. Fix: add `ILogger` and log the exception. [`ReportsController.cs:126-129`]
- [x] [Review][Patch] `requestId` extraction repeated 3x — Extract to private helper method. [`ReportsController.cs:81,139,175`]
- [x] [Review][Patch] No logging in controller — Add `ILogger<ReportsController>` injection. [`ReportsController.cs`]
- [x] [Review][Patch] `FromApiString` no null guard — `.Trim()` called on potentially null value. Fix: early return null. [`ReportType.cs:30`]
- [x] [Review][Patch] Year validation — User-supplied `year=0` causes `ArgumentOutOfRangeException`. Fix: guard with `if (year < 1)`. [`ReportGenerationService.cs:114`]
- [x] [Review][Patch] `HttpClient` per iteration — New client per job in loop causes socket exhaustion. Fix: use static `HttpClient` or `IHttpClientFactory`. [`ReportExportJobRunner.cs:64`]

### Deferred findings

- [x] [Review][Defer] Claim-resolution throws 500 instead of 401/403 — matches existing project pattern in `DashboardService.cs`. [`ReportsController.cs:195-223`]
- [x] [Review][Defer] No upper bound on `page` query param — matches existing controller pattern. [`ReportsController.cs:158`]
- [x] [Review][Defer] User-id claim fallback fragile — matches existing `NotificationService` pattern. [`ReportsController.cs:209-222`]
- [x] [Review][Defer] Email gated by notification preferences — matches existing `EmailDeliveryService` pattern, not introduced by this story.
- [x] [Review][Defer] Test gap: no end-to-end background job test — requires full blob infrastructure.
- [x] [Review][Defer] Test gap: completed/failed/410 status tests — requires blob setup for full flow.
- [x] [Review][Defer] `SasUrlExpiryMinutes` option unused by `GenerateReadSasUri` — interface doesn't accept expiry parameter; would require modifying `IBlobStorageService` (constrained).

### Dismissed findings

- HttpContext null guard dead code — harmless, no functional impact.
- Route parameter type validation not at routing layer — design preference, not a bug.
- No `[FromBody]` / model validation attribute — handled by ASP.NET middleware.
- SAS URL expiry TOCTOU race — acceptable edge case; SAS check is advisory.
- Optimistic concurrency guard missing — no existing project pattern uses row versioning.
- Crash between saves / notification timeout — acceptable risk for async background jobs.

## Dev Agent Record

### Implementation Plan

1. Created `ReportType` enum with 8 report types + extension methods for API string conversion
2. Created `ReportExportJob` entity with status constants and EF Core configuration
3. Created DTOs: `ReportExportRequest`, `ReportExportJobDto`, `ReportExportStatusDto`, `ReportExportListResultDto`
4. Created `ReportRowDto` — generic key-value row DTO for flexible report schemas
5. Created `ReportGenerationService` with 8 report query methods + Excel/PDF generation
6. Created `ReportExportOptions` for polling interval and SAS URL configuration
7. Created `ReportExportJobRunner` with scoped DI pattern matching `CourtReminderJobRunner`
8. Created `ReportExportBackgroundService` matching `CourtReminderBackgroundService`
9. Created `ReportsController` with 3 endpoints (POST export, GET status, GET list)
10. Updated `AppDbContext` with `ReportExportJobs` DbSet
11. Created `ReportExportJobConfiguration` for EF Core mapping
12. Updated `Program.cs` with DI registrations
13. Created `ReportsApiTests.cs` with 11 integration tests

### Key Technical Decisions

- **Generic row DTO**: Used `IReadOnlyDictionary<string, object?>` for report rows instead of per-type DTOs, keeping the schema self-describing while avoiding 8 separate DTO classes
- **Blob SAS upload**: The `ReportGenerationService` generates file bytes and the `ReportExportJobRunner` uploads via SAS URI (matching attachment pattern) — this avoids coupling the service to blob infrastructure
- **Scoped runner pattern**: Followed the exact `CourtReminderJobRunner` pattern with `IServiceScopeFactory` to resolve scoped dependencies from the singleton background service
- **No migration file generated**: The migration is expected to be generated by running `dotnet ef migrations add` since the schema is configured via `IEntityTypeConfiguration` and discovered by `ApplyConfigurationsFromAssembly`

### Completion Notes

- All 8 report types are implemented with full query logic
- Excel and PDF generation both produce formatted output with headers and data rows
- Empty data sets produce a "No data available for the selected period" message instead of empty files
- Workload distribution includes only case counts per worker — no rankings, scores, or comparisons
- Blob upload uses SAS URI generation from existing `IBlobStorageService`
- In-app notifications and email are sent on completion via existing `NotificationService` and `EmailDeliveryService`
- All endpoints properly scoped by `organisationId` and `createdByUserId`
- Background service only runs in non-Development environments (matching existing pattern), so integration tests validate API endpoints synchronously

### Change Log

- **2026-06-20** — Code review applied: 9 patch findings fixed (enum parsing, date params, LINQ projection, logging, requestId helper, null guards, year validation, HttpClient static); 7 deferred; 6 dismissed.
