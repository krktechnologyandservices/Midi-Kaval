---
baseline_commit: 7a23426dd78b4cfca889db11a96ebac5b5ad40eb
---

# Story 23.1: Strengthen Import File Validation

Status: done

## Story

As a security engineer,
I want uploaded import files validated by their content (magic bytes),
So that renamed executables are rejected.

**FRs covered:** FR-19
**NFRs covered:** OWASP A05:2025 (Injection via file upload)

## Acceptance Criteria

**AC1: Renamed executable is rejected**
Given an upload of a renamed .exe file (saved as .xlsx)
When `POST /api/v1/migration/import` is called
Then the request is rejected with HTTP 400
And the error message is "Invalid file format. Please upload a .xlsx file."

**AC2: Genuine .xlsx is accepted**
Given a genuine .xlsx file
When `POST /api/v1/migration/import` is called
Then the file is accepted and processed normally

**AC3: Validate endpoint also checks magic bytes**
Given a renamed .exe file (saved as .xlsx)
When `POST /api/v1/migration/validate` is called
Then the request is rejected with HTTP 400 with the same error message

**AC4: Short/empty files are rejected**
Given a file smaller than 4 bytes
When POST to either migration endpoint is called
Then the request is rejected with HTTP 400

**AC5: Content-Type header check is preserved as secondary guard**
Given a genuine .xlsx file with an incorrect Content-Type header
When POST to either migration endpoint is called
Then the request still succeeds (magic bytes take precedence)

## Tasks / Subtasks

- [x] Task 1: Add magic byte validation to MigrationImportService
  - [x] 1.1 Add `using MidiKaval.Api.Infrastructure.Cases;` import to `MigrationImportService.cs`
  - [x] 1.2 Add `ValidateFileFormat(Stream stream)` static method — takes a MemoryStream (after CopyToAsync)
  - [x] 1.3 Define `XlsxMagicBytes` constant: `[0x50, 0x4B, 0x03, 0x04]` (PK\x03\x04)
  - [x] 1.4 Check `stream.Length < 4` → throw `CaseValidationException`
  - [x] 1.5 Use `Span<byte>` + `stream.Read(header)` to read first 4 bytes (avoid BinaryReader — it disposes the stream)
  - [x] 1.6 Reset `stream.Position = 0` after reading
  - [x] 1.7 Throw `CaseValidationException("Invalid file format. Please upload a .xlsx file.")` on mismatch

- [x] Task 2: Wire magic byte validation into MigrationController
  - [x] 2.1 In the `Import` endpoint, call `MigrationImportService.ValidateFileFormat(stream)` after `stream.Position = 0` (post-CopyToAsync) and before `XLWorkbook` construction
  - [x] 2.2 In the `Validate` endpoint, call `MigrationImportService.ValidateFileFormat(stream)` in the same position (after CopyToAsync, before XLWorkbook)

- [x] Task 3: Add integration tests
  - [x] 3.1 Test: Import endpoint rejects renamed .exe with 400 (AC1)
  - [x] 3.2 Test: Import endpoint accepts genuine .xlsx (AC2)
  - [x] 3.3 Test: Validate endpoint rejects renamed .exe with 400 (AC3)
  - [x] 3.4 Test: Short file (< 4 bytes) rejected with 400 (AC4)
  - [x] 3.5 Test: Genuine .xlsx with incorrect Content-Type header is accepted (AC5)

## Developer Notes

### Architecture Context

This story implements `architecture-security.md#2.8` (FR-19). It adds server-side MIME type validation via magic byte inspection before the import/validate endpoints process the file.

### Architecture Decision (AD-08)

Per `architecture-security.md` §2.8:

> **Decision:** Add server-side MIME-type validation via magic byte inspection in `MigrationImportService` before processing the file.

> **Rationale:** Magic byte check at stream start for import validation — minimal overhead (~0.1ms), no external dependency.

### Existing Patterns

**Current validation in MigrationController** (both `Validate` and `Import` endpoints):

```csharp
if (file is null || file.Length == 0)
    return BadRequestProblem("A non-empty Excel file (.xlsx) is required.");

if (!file.ContentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)
    && !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
{
    return BadRequestProblem("File must be an .xlsx Excel file.");
}

if (file.Length > 10 * 1024 * 1024)
    return BadRequestProblem("File size must not exceed 10 MB.");
```

The current check relies on `ContentType` header and file extension — both are attacker-controlled. Magic bytes provide content-based verification.

### Architecture Reference Code (Corrected for IFormFile)

The architecture document suggests `BinaryReader + file.Position`, but `IFormFile.OpenReadStream()` returns a non-positionable stream and disposing the reader disposes the underlying stream. The correct approach validates ***after*** the file is copied to a MemoryStream — which both endpoints already do:

```csharp
private static readonly byte[] XlsxMagicBytes = [0x50, 0x4B, 0x03, 0x04]; // PK\x03\x04

private static void ValidateFileFormat(Stream stream)
{
    // Call after CopyToAsync — stream is at position 0, fully buffered in memory.
    if (stream.Length < 4)
        throw new CaseValidationException("Invalid file format.");

    Span<byte> header = stackalloc byte[4];
    var read = stream.Read(header);

    if (read < 4 || !header.SequenceEqual(XlsxMagicBytes))
        throw new CaseValidationException("Invalid file format. Please upload a .xlsx file.");

    stream.Position = 0; // Reset for downstream processing
}
```

**Why this approach:**
- Takes a `Stream` (MemoryStream after CopyToAsync) — no IFormFile stream lifecycle issues
- Uses `Span<byte>` + `Read` instead of `BinaryReader` — no disposal concern, no `leaveOpen` needed
- `stream.Position = 0` compiles because MemoryStream has a settable Position

### What to Preserve

- Both endpoints must continue to function identically for genuine .xlsx files
- The existing `ContentType` + extension checks should remain as secondary guards
- The 10 MB size limit must be preserved
- The existing try-catch for `CaseValidationException` in both endpoints already handles this — the exception will be caught by the existing `catch (CaseValidationException ex)` block which returns 400

### Where to Add the Validation

The correct approach validates the file format **after** `file.CopyToAsync(stream)` — both endpoints already copy the file into a MemoryStream. Call `ValidateFileFormat(stream)` after `stream.Position = 0` and before `XLWorkbook` construction. The MemoryStream is fully buffered in memory, so `stream.Position = 0` (reset after reading magic bytes) and `stream.Read()` work correctly.

`CaseValidationException` is already caught by both endpoints (`catch (CaseValidationException ex)` → 400), so the validation simply throws and propagates naturally.

**Mandatory:** Add `using MidiKaval.Api.Infrastructure.Cases;` to `MigrationImportService.cs` — `CaseValidationException` lives in the `Cases` namespace which is not currently imported. Without this, the build fails.

### Files to Modify

| Action | File |
|--------|------|
| MODIFY | `apps/api/Infrastructure/Migration/MigrationImportService.cs` |
| MODIFY | `apps/api/Controllers/V1/MigrationController.cs` |
| CREATE/tests | `tests/api.integration/MigrationFileValidationTests.cs` |

### Anti-Patterns to Avoid

- **Do NOT** rely solely on `Content-Type` header — it's client-controlled and trivially spoofed
- **Do NOT** validate the `IFormFile` stream directly — `BinaryReader` disposes it, `file.Position` doesn't compile. Validate the MemoryStream *after* `CopyToAsync`
- **Do NOT** use `BinaryReader` — .NET 8 `Stream.Read(Span<byte>)` is simpler, zero disposal concerns
- **Do NOT** forget to reset `stream.Position = 0` after reading — the downstream `XLWorkbook(stream)` expects the stream at position 0
- **Do NOT** add a new NuGet dependency — `Span<byte>` and `SequenceEqual` are in the BCL
- **Do NOT** validate on the validate-only path differently from the import path — both must be consistent
- **Do NOT** forget `using MidiKaval.Api.Infrastructure.Cases;` — build fails without it

### Testing Approach

- Use `AuthWebApplicationFactory` (same pattern as `MigrationImportTests` and `MigrationValidationTests`)
- Test Director access (both endpoints are Director-only)
- For the renamed .exe test: create a `byte[]` with `[0x4D, 0x5A]` (MZ — PE executable magic) and send as `multipart/form-data` with `.xlsx` filename
- For the genuine .xlsx test: reuse `BuildValidExcel()` from `MigrationImportTests` (or duplicate a minimal version)
- For the short file test: send a 3-byte `byte[]`
- For the Content-Type test: send a valid .xlsx with `Content-Type: application/octet-stream`
- Tests should go in a new file `MigrationFileValidationTests.cs` following the existing test class pattern

### Cross-References

- **Story 10-2** (`_bmad-output/implementation-artifacts/10-2-one-time-import-tool.md`) — established the import patterns and `MigrationImportService` with `ProcessRowAsync`, row-by-row creation, batch saves, and audit events. The magic byte check sits before all this logic.
- **Architecture** `architecture-security.md §2.8` — the implementation pattern with the corrected stream approach above

### Project-context References

- `project-context.md` §Language-Specific Rules: "No `[AllowAnonymous]` on data mutations"
- `project-context.md` §Testing Rules: "Integration = HTTP→DB with WebApplicationFactory"
- `Policies.DirectorOnly` — both migration endpoints require Director role
- `CaseValidationException` — defined in existing codebase, converts to 400 in catch handlers
- Magic byte signature for .xlsx: `PK\x03\x04` (PK = `0x50, 0x4B`), which is the ZIP file magic number (`.xlsx` files are ZIP archives)

### Review Findings

- [x] [Review][Patch] Null stream guard — ValidateFileFormat should throw ArgumentNullException for null stream [MigrationImportService.cs:450]
- [x] [Review][Patch] Non-seekable stream guard — ValidateFileFormat should check CanSeek before accessing Length/Position [MigrationImportService.cs:452]
- [x] [Review][Patch] Stream position not reset on failure path — Position = 0 is only set on success; exceptions leave stream at byte 4 [MigrationImportService.cs:460-461]
- [x] [Review][Patch] Inconsistent error messages — Short-stream path says "Invalid file format." vs magic-mismatch path says "Invalid file format. Please upload a .xlsx file." [MigrationImportService.cs:453,458]
- [x] [Review][Patch] Misleading log message — CaseValidationException from file format logs as "failed due to actor context", sending operators on wrong diagnosis path [MigrationController.cs:105,174]
- [x] [Review][Defer] ContentType null guard — file.ContentType can be null; existing guard throws NullReferenceException [MigrationController.cs:49-50] — deferred, pre-existing
- [x] [Review][Defer] Magic byte check only validates ZIP header, not Excel structure — a .zip/.docx file passes; checking ZIP internals for Excel structure is beyond AC scope [MigrationImportService.cs]
- [x] [Review][Defer] No CancellationToken support — ValidateFileFormat uses synchronous Read; method is called from async controller context [MigrationImportService.cs:455]
- [x] [Review][Defer] No concurrency protection in doc comment — static method shared across requests not documented as non-thread-safe [MigrationImportService.cs:448-462]
- [x] [Review][Defer] No test for 4+ byte non-ZIP garbage — only short file (.Length < 4) and renamed .exe (MZ) are tested; random bytes >= 4 bytes is untested [MigrationFileValidationTests.cs]

### Review Completion

All 5 `patch` findings have been applied and verified in build (2026-06-23):

1. **Null stream guard** — Added `ArgumentNullException` check at the top of `ValidateFileFormat`.
2. **Non-seekable stream guard** — Added `ArgumentException` guard for `!stream.CanSeek`.
3. **Stream position reset on failure path** — Wrapped validation in `try/finally` so `stream.Position = 0` executes even on exception paths.
4. **Inconsistent error messages** — Short-stream path now says "Invalid file format. Please upload a .xlsx file." matching the magic-mismatch message.
5. **Misleading log message** — Changed both catch blocks from "failed due to actor context" to "failed: {Message}" in Validate and Import endpoints.

5 defer findings logged to `deferred-work.md`.

Status: **review → done**

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Completion Notes List

- Story created from epics-security.md §Epic 23, architecture-security.md §2.8, prd-Midi-Kaval-2026-06-22 §4.7
- Story validated: corrected `file.Position` + `BinaryReader` approach → `Span<byte>` on MemoryStream after `CopyToAsync`
- Added `ValidateFileFormat(Stream)` static method to `MigrationImportService` with `XlsxMagicBytes` constant
- Added `using MidiKaval.Api.Infrastructure.Cases;` to `MigrationImportService.cs`
- Wired validation into both `POST /api/v1/migration/validate` and `POST /api/v1/migration/import` endpoints
- Created `MigrationFileValidationTests.cs` with 5 integration tests covering all ACs (exe rejection, xlsx acceptance, short file, Content-Type override)
- Build: 0 errors in API and test projects
- File: `23-1-import-file-validation.md`

### File List

| Action | File |
|--------|------|
| MODIFY | `apps/api/Infrastructure/Migration/MigrationImportService.cs` |
| MODIFY | `apps/api/Controllers/V1/MigrationController.cs` |
| CREATE | `tests/api.integration/MigrationFileValidationTests.cs` |

### Change Log

- Story created, status → ready-for-dev (Date: 2026-06-23)
- Story validated — corrected stream handling approach (Span<byte> instead of BinaryReader, MemoryStream after CopyToAsync) (Date: 2026-06-23)
- Implementation complete, status → review (Date: 2026-06-23)
