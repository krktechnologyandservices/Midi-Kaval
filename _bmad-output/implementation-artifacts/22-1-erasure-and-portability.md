---
baseline_commit: 7a23426dd78b4cfca889db11a96ebac5b5ad40eb
---

# Story 22.1: Implement Right to Erasure and Data Portability

Status: done

## Story

As a Director,
I want to erase personal data for a specific case or export it in machine-readable format,
So that GDPR Article 17 (right to erasure) and Article 20 (data portability) requests can be fulfilled.

**FRs covered:** FR-14 (PII erasure endpoint), FR-15 (erasure notification in audit log), FR-20 (personal data export endpoint)
**NFRs covered:** NFR-COMP-02 (GDPR Article 17 — right to erasure), NFR-COMP-03 (GDPR Article 20 — data portability)

## Acceptance Criteria

**AC1: Director-only erasure endpoint**
Given an authenticated non-Director user
When they call `DELETE /api/v1/cases/{id}/personal-data`
Then they receive HTTP 403

**AC2: Successful erasure returns nullified fields**
Given an authenticated Director user
When they call `DELETE /api/v1/cases/{id}/personal-data` on a valid case
Then PII fields are nullified (`beneficiaryName`, `beneficiaryContact`, `latitude`, `longitude`, `landmark`, `beneficiaryAge`)
And the response is 200 with `{ "nullifiedFields": ["beneficiaryName", "beneficiaryContact", "beneficiaryAge", "latitude", "longitude", "landmark"] }`
And an audit event (`case.personal_data_erased`) records the operation
And the case record is preserved (crimeNumber, stNumber, visitCount, currentStage, etc.)

**AC3: Idempotent erasure**
Given the erasure endpoint is called twice
When on the same case
Then the second call returns the same result (idempotent, same nullified fields list)

**AC4: Case not found (404)**
Given an authenticated Director user
When they call `DELETE /api/v1/cases/{id}/personal-data` on a non-existent case
Then they receive HTTP 404

**AC5: Director-only export endpoint**
Given a non-Director user
When they call `GET /api/v1/cases/{id}/personal-data`
Then they receive HTTP 403

**AC6: Successful export returns PII as JSON**
Given an authenticated Director user
When they call `GET /api/v1/cases/{id}/personal-data` on a valid case
Then the response contains all PII fields as JSON (`beneficiaryName`, `beneficiaryContact`, `beneficiaryAge`, `latitude`, `longitude`, `landmark`)
And the response includes `Content-Disposition: attachment` header
And the response status is 200

**AC7: Export on erased case returns null/default values**
Given a case that has been erased
When a Director calls `GET /api/v1/cases/{id}/personal-data`
Then the response contains the PII fields with null/default values

**AC8: Export on non-existent case returns 404**
Given an authenticated Director user
When they call `GET /api/v1/cases/{id}/personal-data` on a non-existent case
Then they receive HTTP 404

**AC9: Audit event metadata**
Given a successful erasure
Then the `case.personal_data_erased` audit event includes:
  - `ActorUserId` set to the Director who performed the erase
  - `SubjectUserId` = null (not user-targeted)
  - `MetadataJson` contains `{ "nullifiedFields": ["beneficiaryName", ...] }`

## Tasks / Subtasks

- [x] Task 1: Add `CasePersonalDataErased` audit event type constant
  - [x] 1.1 Add `public const string CasePersonalDataErased = "case.personal_data_erased";` to `AuditEventTypes.cs`

- [x] Task 2: Add erasure service method to CaseService
  - [x] 2.1 Add `ErasePersonalDataAsync(Guid caseId, Guid organisationId, Guid actorUserId, CancellationToken ct)` method
  - [x] 2.2 Load case by ID + organisationId (org-scoped)
  - [x] 2.3 Return null if case not found
  - [x] 2.4 Nullify all PII fields: `BeneficiaryName`, `BeneficiaryContact`, `BeneficiaryAge`, `Latitude`, `Longitude`, `Landmark`
  - [x] 2.5 Track which fields were actually non-null (for response list)
  - [x] 2.6 Write `case.personal_data_erased` audit event with `ActorUserId` set to actor
  - [x] 2.7 Save and return list of nullified fields
  - [x] 2.8 Ensure idempotency — second call with same case returns same shape (nullified fields list)

- [x] Task 3: Add export service method to CaseService
  - [x] 3.1 Add `GetPersonalDataAsync(Guid caseId, Guid organisationId, CancellationToken ct)` method
  - [x] 3.2 Load case by ID + organisationId
  - [x] 3.3 Return null if case not found
  - [x] 3.4 Return anonymous/DTO object with PII fields: `beneficiaryName`, `beneficiaryContact`, `beneficiaryAge`, `latitude`, `longitude`, `landmark`
  - [x] 3.5 Do NOT log an audit event for data export (reading PII is already tracked by existing PII reveal audit)

- [x] Task 4: Add endpoints to CasesController
  - [x] 4.1 Add `DELETE /api/v1/cases/{caseId:guid}/personal-data` endpoint with `[Authorize(Policy = Policies.DirectorOnly)]`
  - [x] 4.2 Resolve organisationId from the authenticated user
  - [x] 4.3 Resolve userId from the authenticated user (for audit ActorUserId)
  - [x] 4.4 Call `caseService.ErasePersonalDataAsync`
  - [x] 4.5 Return 404 if null, 200 with `{ nullifiedFields }` on success
  - [x] 4.6 Add `GET /api/v1/cases/{caseId:guid}/personal-data` endpoint with `[Authorize(Policy = Policies.DirectorOnly)]`
  - [x] 4.7 Call `caseService.GetPersonalDataAsync`
  - [x] 4.8 Return 404 if null, 200 with PII data + `Content-Disposition: attachment` header
  - [x] 4.9 Apply `[EnableRateLimiting("data-write")]` to DELETE and `[EnableRateLimiting("data-read")]` to GET
  - [x] 4.10 Add `[ProducesResponseType]` attributes for 200, 401, 403, 404

- [x] Task 5: Integration tests (AC: #1–#9)
  - [x] 5.1 Create `PersonalDataTests.cs` with `IClassFixture<AuthWebApplicationFactory>`
  - [x] 5.2 Test: Director can erase personal data (AC2)
  - [x] 5.3 Test: Non-Director receives 403 on erase (AC1)
  - [x] 5.4 Test: Erasure is idempotent (AC3)
  - [x] 5.5 Test: Erase on non-existent case returns 404 (AC4)
  - [x] 5.6 Test: Director can export personal data (AC6)
  - [x] 5.7 Test: Non-Director receives 403 on export (AC5)
  - [x] 5.8 Test: Export has Content-Disposition header (AC6)
  - [x] 5.9 Test: Export on non-existent case returns 404 (AC8)
  - [x] 5.10 Test: Export returns null fields after erasure (AC7)
  - [x] 5.11 Test: Audit event written with correct metadata (AC9)
  - [x] 5.12 Test: Operational fields preserved after erasure (AC2)

## Developer Notes

### Architecture Context

This story implements `architecture-security.md#2.7` (FR-14, FR-15, FR-20). It adds two endpoints to the existing `CasesController` (or a new controller, per architecture — choose CasesController to keep it simple since the endpoint is case-scoped).

### Existing Patterns

**Director-only endpoints** follow this pattern:
```csharp
[Authorize(Policy = Policies.DirectorOnly)]
```
Already used in: `MigrationController`, `AuditLogController`, `StaffController`, `DirectorTravelClaimsController`. All at class level.

**OrganisationId resolution** in existing controllers:
```csharp
var organisationId = HttpContext.GetOrganisationId();
var actorUserId = HttpContext.GetUserId();
```
These are extension methods on HttpContext. Every data-changing endpoint resolves these.

**Content-Disposition response** — not used in existing project controllers yet, but the standard ASP.NET Core pattern:
```csharp
var cd = new ContentDispositionHeaderValue("attachment") { FileName = $"case-{caseId}-personal-data.json" };
Response.Headers.ContentDisposition = cd.ToString();
```

**Audit event pattern** (from existing CaseService methods, similar to anonymization job):
```csharp
db.AuditEvents.Add(new AuditEvent
{
    Id = Guid.NewGuid(),
    OrganisationId = organisationId,
    ActorUserId = actorUserId,
    SubjectUserId = null,
    EventType = AuditEventTypes.CasePersonalDataErased,
    MetadataJson = JsonSerializer.Serialize(new { nullifiedFields }),
    CreatedAtUtc = DateTime.UtcNow,
});
```

### Files to Create

| Action | File |
|--------|------|
| MODIFY | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| MODIFY | `apps/api/Controllers/V1/CasesController.cs` |
| MODIFY | `apps/api/Infrastructure/Cases/CaseService.cs` |
| CREATE | `tests/api.integration/PersonalDataTests.cs` |

### Files Modified — Current State & What Changes

**`AuditEventTypes.cs`** — Currently has `CaseAnonymized = "case.anonymized"` as the last entry. Add `CasePersonalDataErased = "case.personal_data_erased"` after it.

**`CasesController.cs`** — Full controller with `CaseService`, `CaseSearchPresetService`, `VisitService`, `CaseNoteService`, `InterventionService`, `CourtSittingService`. Has endpoints for GET/POST/PATCH on cases, notes, interventions, court sittings under `/api/v1/cases/`. The controller itself does NOT have a class-level `[Authorize]` — individual endpoints use `[Authorize(Policy = Policies.CoordinatorOrAbove)]` or similar. The two new endpoints will be the first `[Authorize(Policy = Policies.DirectorOnly)]` within this controller.

**`CaseService.cs`** — Scoped service injected into CasesController. Contains methods like `CreateCaseAsync`, `TransitionStageAsync`, `MergeCasesAsync`, `TransferCaseAsync`, etc. Follows a pattern of: load entity from DbContext, validate, mutate, save audit event, save changes. New methods should follow this pattern.

### What to Nullify (Erasure)

| Case Property | DB Column | Nullify? |
|---------------|-----------|----------|
| `BeneficiaryName` | `beneficiary_name` | ✅ Yes (string?) |
| `BeneficiaryContact` | `beneficiary_contact` | ✅ Yes |
| `BeneficiaryAge` | `beneficiary_age` | ✅ Yes |
| `Latitude` | `latitude` | ✅ Yes |
| `Longitude` | `longitude` | ✅ Yes |
| `Landmark` | `landmark` | ✅ Yes |
| All other fields | — | ❌ No (operational) |

### What to Preserve (All operational fields)

Same rule as anonymization: CrimeNumber, StNumber, TypeOfOffence, CurrentStage, VisitCount, AssignedWorkerId, all stage data, all dates, all operational fields.

### Response Shapes

**Erasure response (200):**
```json
{
  "nullifiedFields": ["beneficiaryName", "beneficiaryContact", "beneficiaryAge", "latitude", "longitude", "landmark"]
}
```
Note: this is NOT wrapped in the API envelope `{ data: ... }` — it's a plain response body. Check existing controller patterns for `return Ok(new { nullifiedFields })`.

**Export response (200):**
```json
{
  "beneficiaryName": "...",
  "beneficiaryContact": "...",
  "beneficiaryAge": 25,
  "latitude": 12.345,
  "longitude": 67.890,
  "landmark": "..."
}
```
With `Content-Disposition: attachment; filename="case-{caseId}-personal-data.json"` header. This also is NOT wrapped in an envelope.

### Encryption Considerations

PII fields use value converters (`SearchablePiiEncryptionConverter` for `BeneficiaryName`, `PiiEncryptionConverter` for `BeneficiaryContact`/`Landmark`, `GpsEncryptionConverter` for `Latitude`/`Longitude`). These converters handle null transparently (null in → null out), so setting entity properties to null and saving via EF Core will correctly store DB NULL.

The export endpoint reads entity properties normally — the value converters transparently decrypt on read. No special handling needed.

### Testing Approach

- Use `AuthWebApplicationFactory` (same as all integration tests)
- Test Director access via login with director@pilot.example
- Test 403 via coordinator token (non-Director role)
- Create a real case via CaseTestData helper pattern
- For erasure: call DELETE endpoint, then verify DB values are null and audit written
- For export: call GET endpoint, verify JSON structure and Content-Disposition header
- Follow same test helper patterns as `CaseAnonymizationTests.cs`

### Anti-Patterns to Avoid

- **Do NOT** hard-delete the case record — cases are never deleted (project-context.md rule)
- **Do NOT** write one audit event per nullified field — single event per erasure is sufficient
- **Do NOT** wrap personal-data responses in the API envelope `{ data: ... }` — they return plain bodies
- **Do NOT** log an audit event for the GET export endpoint — reading is not a mutation
- **Do NOT** add caching headers on the DELETE endpoint (it's a mutation)
- **Do NOT** expose these endpoints in the mobile web API client (Director-only, admin functionality)

### Project-context References

- `project-context.md` establishes the anti-pattern: "Never hard-delete cases"
- `AuditEvent` entity requires: `Id`, `OrganisationId`, `ActorUserId` (nullable), `SubjectUserId` (nullable), `EventType`, `MetadataJson`, `CreatedAtUtc`
- `Policies.DirectorOnly` is defined in `Infrastructure/Auth/Policies.cs`
- `HttpContext.GetOrganisationId()` and `HttpContext.GetUserId()` are extension methods used across all controllers
- Rate limiting uses `[EnableRateLimiting("data-write")]` for mutations and `[EnableRateLimiting("data-read")]` for reads

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Completion Notes List

- Story created from epics-security.md §Epic 22, architecture-security.md §2.7, prd-Midi-Kaval-2026-06-22 §4.5 and §4.8
- Previous story 21.1 patterns noted for integration tests and audit events
- Service methods follow existing CaseService pattern (org-scoped, audit-aware)
- Both endpoints placed in existing CasesController (per architecture decision option)
- No migration needed — all PII fields are already nullable (BeneficiaryName made nullable in Story 21.1 migration)
- Added `CasePersonalDataErased` audit event constant
- Implemented `ErasePersonalDataAsync` — nullifies 6 PII fields, tracks which were non-null, writes audit event, returns nullified list
- Implemented `GetPersonalDataAsync` — loads case, returns PII fields as anonymous object, no audit event for reads
- Added `DELETE /api/v1/cases/{caseId:guid}/personal-data` — Director-only, idempotent, returns `{ nullifiedFields: [...] }`
- Added `GET /api/v1/cases/{caseId:guid}/personal-data` — Director-only, returns PII JSON with Content-Disposition attachment header
- Created `PersonalDataTests.cs` with 10 integration tests covering all 9 ACs
- Build: 0 errors in API and test projects

### Review Findings

- [x] [Review][Patch] Idempotent erasure returns empty list on second call [CaseService.cs:1522-1553]
- [x] [Review][Patch] Audit event written on no-op/idempotent calls [CaseService.cs:1540-1549]
- [x] [Review][Patch] UpdatedAtUtc not updated after PII nullification [CaseService.cs:1522-1553]
- [x] [Review][Patch] ErasePersonalData endpoint lacks try-catch [CasesController.cs:884-889]
- [x] [Review][Patch] Content-Disposition header set without response-has-started guard [CasesController.cs:904]
- [x] [Review][Patch] CasePersonalDataErased not cataloged in PiiAuditEventTypes [PiiAuditEventTypes.cs]
- [x] [Review][Defer] No "before" snapshot in erasure audit event — enhancement
- [x] [Review][Defer] Landmark missing from audit snapshot exclusion — pre-existing
- [x] [Review][Defer] BeneficiaryAge null JSON serialization — project-wide pattern
- [x] [Review][Defer] No concurrency protection on erasure — pre-existing pattern
- [x] [Review][Defer] Export content-level protection — broader architectural concern
- [x] [Review][Defer] Test data never cleaned up — pre-existing pattern
- [x] [Review][Defer] Rate-limit distinction for sensitive endpoints — broader security policy

### File List

| Action | File |
|--------|------|
| MODIFY | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| MODIFY | `apps/api/Controllers/V1/CasesController.cs` |
| MODIFY | `apps/api/Infrastructure/Cases/CaseService.cs` |
| CREATE | `tests/api.integration/PersonalDataTests.cs` |

### Change Log

- Story created, status → ready-for-dev (Date: 2026-06-23)
- Added `CasePersonalDataErased` audit event constant, implemented service methods, added controller endpoints, created integration tests (Date: 2026-06-23)
- Implementation complete, status → review (Date: 2026-06-23)

### Review Completion

All 6 `patch` findings have been applied and verified in build (2026-06-23):

1. **Idempotent erasure returns all field names** — Changed `ErasePersonalDataAsync` to always return all 6 field names regardless of null state. No longer conditionally includes only non-null fields.
2. **Audit event only on actual mutation** — Audit event creation gated behind `if (anyChanged)`, so no-op/idempotent calls no longer write misleading events.
3. **UpdatedAtUtc set on erasure** — Added `entity.UpdatedAtUtc = DateTime.UtcNow` when fields are actually changed.
4. **Try-catch on ErasePersonalData endpoint** — Added `catch (DbUpdateException ex) when (IsUniqueViolation(ex))` per existing mutation endpoint pattern.
5. **HasStarted guard on Content-Disposition** — Header assignment wrapped in `if (!HttpContext.Response.HasStarted)`.
6. **CasePersonalDataErased cataloged** — Added to `PiiAuditEventTypes.VerifiedCleanTypes` with explanatory comment.

Status: **review → done**
