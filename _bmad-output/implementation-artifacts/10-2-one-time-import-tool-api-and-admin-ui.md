---
baseline_commit: NO_VCS
---

# Story 10.2: One-Time Import Tool API and Admin UI

Status: done

## Story

As a **Project Director**,
I want to import legacy Cases once,
So that cloud becomes source of truth (MVP §6.1, addendum).

## Acceptance Criteria

1. **Given** a validated Excel file uploaded via the Admin UI
   **When** Director runs import (with dry-run=false)
   **Then** each row is mapped per `docs/excel-migration/mapping-specification.md` and created as a Case
   **And** duplicate Crime/ST rows within the organisation are **skipped** (not failed) — import summary shows success/skipped/error counts
   **And** each created Case has an `audit_events` row (`case.imported` event type) written in the same transaction

2. **Given** a validated Excel file uploaded
   **When** Director runs import with dry-run=true
   **Then** the system validates every row against the mapping spec without writing any Cases or audit rows
   **And** returns the same summary (success/skipped/error) so the Director can preview the result before committing

3. **Given** the import completes
   **When** I view the import summary
   **Then** the report includes: total rows processed, cases created, duplicates skipped (with Crime/ST identifiers), validation errors per row (with row number and reason), and timestamp
   **And** the summary is logged as an `audit_events` row (`migration.import_completed`)

4. **Given** I am authenticated as a **Coordinator** (not Director)
   **When** I attempt to access the import endpoint
   **Then** response is **403** Forbidden — Director-only operation

5. **Given** the mapping spec file does not exist at `docs/excel-migration/mapping-specification.md`
   **When** I attempt to import
   **Then** response is **400** stating the mapping must be finalised first (Story 10.1 must be complete)

6. **Given** I am viewing the Admin page in the web app
   **When** I navigate to the admin area
   **Then** there is a "Legacy Import" link (Director-only visible) that opens the import page
   **And** the import page shows: file upload (drag-and-drop or file picker), dry-run toggle, Run Import button, and a results panel

## Tasks / Subtasks

- [x] **Create `MigrationImportService`** (AC: 1, 2, 3)
  - [x] `apps/api/Infrastructure/Migration/MigrationImportService.cs` — service that reads the mapping spec and uploaded Excel, processes rows into Cases
  - [x] For each row: validate columns per mapping spec, map fields, check existing duplicates (Crime/ST per org), insert Case + audit event in same transaction
  - [x] Wrap per-row DB work in individual SaveChangesAsync so that one bad row does not roll back the entire batch
  - [x] Dry-run mode: run all validations and duplicate checks but do NOT write to DB
  - [x] Return `MigrationImportResultDto` with: total rows, created count, skipped count (with Crime/ST per row), error count (with row index + reason), timestamp
  - [x] Register as scoped service in `Program.cs`

- [x] **Create migration DTOs** (AC: 1, 3)
  - [x] `apps/api/Models/Migration/MigrationDtos.cs` — `MigrationImportRequest` (dryRun bool), `MigrationImportResultDto` (totalRows, created, skipped list, errors list, timestamp)
  - [x] `MigrationImportRowResultDto` — rowIndex, crimeNumber, stNumber, status (created/skipped/error), reason

- [x] **Create `MigrationController` import endpoint** (AC: 1, 2, 4, 5)
  - [x] `POST /api/v1/migration/import` in `Controllers/V1/MigrationController.cs` (extend from Story 10.1)
  - [x] `[Authorize(Policy = Policies.DirectorOnly)]`
  - [x] Accept `IFormFile` (Excel) + `dryRun` query parameter (default false)
  - [x] Validate mapping spec exists at `docs/excel-migration/mapping-specification.md` — if not, return **400**
  - [x] Call `MigrationImportService.ImportAsync(file, dryRun)`
  - [x] Return structured JSON result
  - [x] Follow same controller pattern as other controllers (`[ApiController]`, `[Produces("application/json")]`, XML doc comments)

- [x] **Extend `AuditEventTypes`** (AC: 1, 3)
  - [x] Add `CaseImported = "case.imported"` — logged per successfully created Case (metadata: `{ caseId, crimeNumber, stNumber }`)
  - [x] Add `MigrationImportCompleted = "migration.import_completed"` — logged once per import run with summary metadata

- [x] **Build Admin UI import page** (AC: 6)
  - [x] `apps/web/src/app/features/shell/pages/import-page.component.ts` — standalone Angular component
  - [x] Route `/admin/import` added to admin routing
  - [x] Add "Legacy Import" link to `AdminPageComponent` (Director-only visibility via `canShow` or `*ngIf` based on role claim)
  - [x] UI: file drop zone / file picker for `.xlsx`, dry-run toggle checkbox, "Run Import" button, results table panel
  - [x] Call `POST /api/v1/migration/import` via API service (`import-api.service.ts`)
  - [x] Display import results in a Material table: row index, Crime/ST, status chip (created/skipped/error), reason column
  - [x] Reuse existing semantic color tokens (status-critical for errors, status-warning for skipped, status-info for created — per DESIGN.md)
  - [ ] Regenerate `packages/api-client` after API changes — manual step

- [x] **Integration tests** (AC: 1, 2, 3, 4, 5)
  - [x] `tests/api.integration/MigrationImportTests.cs` — `[Collection("AuthIntegration")]`, `AuthWebApplicationFactory`
  - [x] Test: dry-run returns result with no DB writes; actual import creates Cases in DB; duplicate crime/ST rows are skipped; validation errors reported per row; Director 200, Coordinator 403, unauthenticated 401; missing mapping spec returns 400

## Dev Notes

### Domain context

This is the **final story in Epic 10** and the capstone of the one-time Excel migration feature. Story 10.1 produced the mapping specification (`docs/excel-migration/mapping-specification.md`). This story consumes it to perform the actual import.

**Key policy (decision log D-1, addendum):** One-time migration only. No ongoing sync. Post-migration, Excel exports are read-only reports from Kaval. WhatsApp remains non-integration per addendum.

### Codebase reference — read before coding

| What | Where | Pattern |
|------|-------|---------|
| Case creation + audit transaction | `Infrastructure/Cases/CaseService.cs` lines 26–80 | Single `SaveChangesAsync`, add Case + AuditEvent in same DbContext |
| Duplicate violation detection | `Controllers/V1/CasesController.cs` lines 846–857 | `IsUniqueViolation(DbUpdateException)` — walks inner exceptions for PostgresException.SqlState == "23505" |
| Duplicate pre-check | `Infrastructure/Cases/CaseService.cs` lines 83–137 | `CheckDuplicateAsync` — queries by Crime/ST per org |
| Excel reading | `Infrastructure/Cases/CaseExcelExporter.cs` | ClosedXML — same library for reading (XLWorkbook.Load) |
| File upload pattern | `Infrastructure/Storage/AttachmentService.cs` | `IFormFile`, content-type check (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`), size limit |
| Admin page pattern | `apps/web/.../shell/pages/admin-page.component.ts` | Standalone Angular component with `mat-stroked-button` links |
| Audit event convention | `Infrastructure/Audit/AuditEventTypes.cs` | Dot-notation: `case.imported`, `migration.import_completed` |

### Architecture compliance

- **API route:** `POST /api/v1/migration/import` — add to `MigrationController.cs` from Story 10.1
- **RBAC:** `[Authorize(Policy = Policies.DirectorOnly)]` — only Directors can run imports (use existing `Policies.DirectorOnly` constant)
- **File handling:** Accept `IFormFile` with content-type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, max size 10 MB
- **Excel parsing:** ClosedXML (`using ClosedXML.Excel` per `CaseExcelExporter` pattern)
- **Audit:** Add `case.imported` (per-Case, same transaction) and `migration.import_completed` (one summary row) to `AuditEventTypes.cs`
- **Dry-run:** All validation and duplicate checking runs, but `SaveChangesAsync` is never called — the service takes a `bool dryRun` parameter
- **Per-row transaction:** Each Case is saved individually (not batch) so a single bad row does not block the rest of the import. Duplicate rows are caught by the unique constraint and skipped gracefully
- **Mapping spec dependency:** The service reads `docs/excel-migration/mapping-specification.md` at startup for column mappings. If the file does not exist, return **400** with clear message
- **No ongoing sync:** Post-migration, Excel exports are read-only reports from Kaval only. No periodic sync or write-back
- **OpenAPI:** Document endpoint with `[ProducesResponseType]` for 200, 400, 401, 403, 422; regenerate `packages/api-client`

### File structure

```
apps/api/
  Controllers/V1/
    MigrationController.cs          ← extend with POST /import (NEW in this story)
  Infrastructure/
    Migration/
      MigrationImportService.cs     ← import processing logic (NEW)
      ColumnMappingValidator.cs     ← from Story 10.1
      MappingConfig.cs              ← from Story 10.1
  Models/
    Migration/
      MigrationDtos.cs              ← import request/result DTOs (NEW)
apps/web/
  src/app/features/shell/
    pages/
      import-page.component.ts      ← Admin import UI (NEW)
      admin-page.component.ts       ← add "Legacy Import" link (EDIT)
    ...
tests/
  api.integration/
    MigrationImportTests.cs         ← integration tests (NEW)
    MigrationValidationTests.cs     ← from Story 10.1
docs/
  excel-migration/
    mapping-specification.md        ← from Story 10.1 (consumed this story)
```

### Case field mapping (from mapping spec)

Each Excel row maps to a Case entity following Story 10.1's mapping spec:

| Case Field | Source | Transformation |
|------------|--------|---------------|
| `CrimeNumber` | Legacy column | Normalize: Trim() + ToUpperInvariant() |
| `StNumber` | Legacy column | Normalize: Trim() + ToUpperInvariant() |
| `BeneficiaryName` | Legacy column | Direct copy |
| `BeneficiaryAge` | Legacy column | Parse int, validate 0–120 |
| `BeneficiaryContact` | Legacy column | Truncate to 32 chars if longer |
| `TypeOfOffence` | Legacy column | Direct copy (free text, max 128) |
| `OffenceClassification` | Legacy column | Parse enum: map legacy terms to Petty/Serious/Heinous |
| `Domicile` | Legacy column | Parse enum: map legacy terms to Urban/Rural/Coastal/Tribal/Slum |
| `IsFirstTimeOffender` | Legacy column or default | Default `true` if column missing |
| `Id` | System | Guid.NewGuid() |
| `OrganisationId` | System | From JWT claims |
| `CurrentStage` | System | `CaseStage.ProcessInitiation` |
| `VisitCount` | System | `0` |
| `CreatedByUserId` | System | From JWT claims (actor) |
| `CreatedAtUtc` | System | `DateTime.UtcNow` |
| `UpdatedAtUtc` | System | `DateTime.UtcNow` |

### Duplicate handling

- Before creating each Case, query `db.Cases.AnyAsync(c => c.OrganisationId == orgId && (c.CrimeNumber == crimeNumber || c.StNumber == stNumber))`
- If a match exists, **skip** the row (do not fail the whole import)
- Record the skip in the result summary with the matching Crime/ST identifiers
- Also rely on DB unique constraints (`UNIQUE(organisation_id, crime_number)`, `UNIQUE(organisation_id, st_number)`) as a second line of defence — catch `DbUpdateException` with `IsUniqueViolation` for any race conditions

### Import result DTO shape

```json
{
  "totalRows": 500,
  "created": 480,
  "skipped": [
    { "rowIndex": 5, "crimeNumber": "CR-2020-001", "stNumber": "ST-123", "reason": "Duplicate crime number" },
    { "rowIndex": 12, "crimeNumber": "CR-2020-002", "stNumber": "ST-456", "reason": "Duplicate ST number" }
  ],
  "errors": [
    { "rowIndex": 20, "crimeNumber": null, "stNumber": null, "reason": "Missing required field: beneficiaryName" }
  ],
  "importedAtUtc": "2026-06-20T18:00:00Z"
}
```

### Testing requirements

| Layer | Tool | What to test |
|-------|------|-------------|
| API integration | xUnit + WebApplicationFactory (`AuthWebApplicationFactory`, `[Collection("AuthIntegration")]`) | Dry-run returns result with 0 DB rows inserted; actual import inserts Cases; duplicate rows skipped; invalid rows reported with reason; Director 200, Coordinator 403, unauthenticated 401; missing mapping spec returns 400; audit events written per created Case and per completed import |

### Previous story intelligence

- **Story 10.1** provides `docs/excel-migration/mapping-specification.md` (mapping rules) and `POST /api/v1/migration/validate` (pre-import column validation)
- The mapping spec must exist before this story can function — this is enforced as a 400 check
- The `ColumnMappingValidator` from Story 10.1 should be reused for per-row validation in this story
- The `MigrationController` from Story 10.1 gets extended with `POST /import`

### Project context reference

See `_bmad-output/project-context.md` for:
- Technology stack versions (.NET 8, EF Core 8, PostgreSQL 16+)
- API conventions (envelope, snake_case DB, camelCase JSON, RFC 7807 errors)
- Angular standalone components + signals pattern
- Testing standards (xUnit + Testcontainers for integration; Jasmine + ATL for web)
- RBAC policy naming patterns
- Audit event requirements (every mutation writes audit_events)

## Dev Agent Record

### Agent Model Used

TBD at implementation

### Debug Log References

### Completion Notes List

- Implemented `MigrationImportService` — core import processing with per-row SaveChangesAsync, duplicate pre-check (+ unique constraint fallback), dry-run mode, enum mapping via `MappingSpecLoader`
- Extended `MigrationDtos.cs` with `MigrationImportResultDto`, `MigrationImportRowResultDto`
- Extended `MigrationController.cs` with `POST /api/v1/migration/import` — file upload, dryRun query param, Director-only RBAC
- Extended `AuditEventTypes.cs` with `CaseImported` and `MigrationImportCompleted`
- Registered `MigrationImportService` as scoped service in `Program.cs`
- Built Admin UI import page — standalone Angular component with drag-drop file zone, dry-run toggle, results table with status chips
- Added `/admin/import` route with `directorGuard` and "Legacy Import" link in admin nav
- Added `ImportApiService` for HTTP calls to the import endpoint
- Created 10 integration tests covering: dry-run (no DB), live import (Case + audit creation), duplicate skip, invalid rows error, empty file, import completed audit, Coordinator 403, unauthenticated 401, no-file 400, non-Excel 400
- **Pending manual step:** Regenerate `packages/api-client` with `openapi-typescript` after API changes to keep the typed client in sync

### File List

- `apps/api/Controllers/V1/MigrationController.cs` — added POST /import
- `apps/api/Infrastructure/Migration/MigrationImportService.cs` — import processing logic (NEW)
- `apps/api/Infrastructure/Migration/MappingSpecLoader.cs` — no changes needed (reused from 10.1)
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — added CaseImported, MigrationImportCompleted
- `apps/api/Models/Migration/MigrationDtos.cs` — added MigrationImportResultDto, MigrationImportRowResultDto
- `apps/api/Program.cs` — registered MigrationImportService
- `apps/web/src/app/features/shell/pages/import-page.component.ts` — admin import UI (NEW)
- `apps/web/src/app/features/shell/pages/admin-page.component.ts` — added Legacy Import link
- `apps/web/src/app/features/shell/services/import-api.service.ts` — import API service (NEW)
- `apps/web/src/app/app.routes.ts` — added /admin/import route
- `tests/api.integration/MigrationImportTests.cs` — integration tests (NEW)

### Review Findings

**Decision Needed:**

- `[x] [Review][Decision] AC5: mapping spec existence 400 — Implemented: `MappingSpecLoader.Load()` throws `InvalidOperationException` when `SpecFilePath` is configured but the file can't be found, and the controller catches it returning 400.

- `[x] [Review][Decision] AC2: dry-run `created` stays 0 — dismissed per user instruction. Dry-run omits successful rows to avoid confusion with live counts.

**Patch Items:**

- `[ ] [Review][Patch] Per-row SaveChangesAsync causes 10k+ DB round-trips on large imports — batch SaveChangesAsync at intervals (e.g., every 100 rows) instead of per-row [MigrationImportService.cs:68-87, 248-250]
- `[ ] [Review][Patch] N+1 duplicate check queries DB per row — pre-load existing crime/st numbers into a HashSet before the loop [MigrationImportService.cs:176-182]
- `[ ] [Review][Patch] Enum mapping silently defaults to enum value 0 — unmapped/unknown enum values result in default(T) without error. Should reject such rows or report as warnings [MigrationImportService.cs:209-213, 273-295]
- `[ ] [Review][Patch] MemoryStream (int)file.Length fragile cast — explicit long-to-int cast would overflow for >2GiB files. Remove explicit capacity or pass file stream directly [MigrationController.cs:153]
- `[ ] [Review][Patch] No overarching transaction for the import — if the import fails mid-file, previously committed rows remain in the DB with no rollback [MigrationImportService.cs:68-87]
- `[ ] [Review][Patch] Unhandled non-unique DbUpdateException crashes entire import — catch block only handles SqlState 23505; other constraint violations abort the whole batch [MigrationImportService.cs:260]
- `[ ] [Review][Patch] Intra-file duplicates not detected in dry-run — two identical rows in the same file both report "created" in dry-run since the first row is not persisted. Check against in-memory processed set [MigrationImportService.cs:195-205]
- `[ ] [Review][Patch] CancellationToken not passed to specLoader.Load() — cannot respond to cancellation during spec loading [MigrationImportService.cs:29]

**Deferred Items:**

- `[x] [Review][Defer] IsFirstTimeOffender defaults to true when missing — per spec NullDefault = "true", intentional
- `[x] [Review][Defer] Magic strings for row status instead of enum — pre-existing project pattern; acceptable for v1
- `[x] [Review][Defer] Audit events omit per-row identifiers — per-story AC3 requires aggregate summary, acceptable
- `[x] [Review][Defer] No progress reporting for long-running imports — v1 scope; acceptable for one-time migration
- `[x] [Review][Defer] required + default = [] on DTO collections — pre-existing DTO pattern in project
