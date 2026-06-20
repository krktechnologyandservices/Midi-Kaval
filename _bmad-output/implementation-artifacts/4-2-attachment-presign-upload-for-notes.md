---
baseline_commit: NO_VCS
---

# Story 4.2: Attachment Presign Upload for Notes

Status: done

<!-- Validated: 2026-06-19 — see 4-2-attachment-presign-upload-for-notes-validation-report.md (9 fixes applied) -->

## Story

As a **staff member**,
I want to attach files to case notes securely,
so that evidence is stored safely (FR-13, NFR-5).

*Scope: **API only** — Azure Blob storage integration, `attachments` table, presign → client PUT → confirm flow, read SAS for download, RBAC via case-note access, audit. Extend `GET /cases/{id}/notes` with attachment metadata. No web/mobile UI (Story 4.3), no travel-claim attachments (Epic 6 — reuse same module with `ResourceType = TravelClaim` later), no offline attachment sync.*

## Acceptance Criteria

1. **Given** I am authenticated and can read a Case (same rules as Story 4.1 — `EnsureCanReadCase`)  
   **And** a `case_notes` row exists for that case in my organisation  
   **When** I `POST /api/v1/attachments/presign` with body:
   ```json
   {
     "resourceType": "CaseNote",
     "resourceId": "<case_note_id>",
     "fileName": "evidence.jpg",
     "contentType": "image/jpeg",
     "fileSizeBytes": 204800
   }
   ```
   **Then** response is **200 OK** with envelope `{ data: AttachmentPresignResultDto, meta: { requestId } }` containing:
   - `attachmentId` (new UUID)
   - `uploadUrl` — blob URL with **write** SAS, expiring **15 minutes** from issue (HTTPS in production; HTTP acceptable for local Azurite)
   - `requiredHeaders` — at minimum `{ "x-ms-blob-type": "BlockBlob", "Content-Type": "<contentType>" }` for Azurite/Azure PUT
   - `expiresAtUtc` — SAS expiry timestamp  
   **And** a row is inserted into **`attachments`** with `status = Pending`, `organisation_id`, `resource_type = CaseNote`, `resource_id = noteId`, `blob_name` (server-generated path — never trust client path), `original_file_name`, `content_type`, `file_size_bytes`, `uploaded_by_user_id` = current user, `created_at_utc`  
   **And** `audit_events` records `attachment.presign.issued` in the **same transaction** with metadata `{ attachmentId, resourceType, resourceId, contentType }` — **no file bytes or SAS token** in audit

2. **Given** presign validation  
   **When** request is invalid  
   **Then**:
   - missing/null body → **400** `"Request body is required."`
   - `resourceType` missing, null, empty, numeric (`"0"`), or not exactly `"CaseNote"` (v1 whitelist — match `noteType` explicit-name pattern from Story 4.1) → **400** with allowed value
   - missing/empty `fileName` → **400**
   - `fileName` contains path separators (`/` or `\`) or `..` → **400**
   - missing/invalid `contentType` (not in allow-list) → **400** — allow-list: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`
   - `fileSizeBytes` ≤ 0 or > `BlobStorage:MaxUploadBytes` (default **10_485_760** = 10 MiB) → **400**
   - `resourceId` note not found in org → **404**
   - note's case not readable by actor → **403** with `Policies.ForbiddenByRoleMessage`
   - field worker on **unassigned** case (`assigned_worker_id` null) → **403** (Story 2.8 parity)
   - unauthenticated → **401**; deactivated → **403** with `AuthService.DeactivatedMessage`

3. **Given** a **Pending** attachment I created (or any user with case read access for that note's case)  
   **And** the client has PUT the blob bytes to `uploadUrl` successfully  
   **When** I `POST /api/v1/attachments/confirm` with body `{ "attachmentId": "<uuid>" }`  
   **Then** response is **200 OK** with `{ data: AttachmentDto, meta: { requestId } }` where `AttachmentDto` includes:
   - `id`, `resourceType`, `resourceId`, `originalFileName`, `contentType`, `fileSizeBytes`, `status = Confirmed`, `uploadedByUserId`, `createdAtUtc`, `confirmedAtUtc`
   - `downloadUrl` — HTTPS URL with **read** SAS, expiring **15 minutes**
   - `downloadExpiresAtUtc`  
   **And** row updates to `status = Confirmed`, `confirmed_at_utc = DateTime.UtcNow`  
   **And** server verifies blob **exists** in storage at `blob_name` before confirming  
   **And** if stored blob size exceeds presigned `file_size_bytes` or `MaxUploadBytes` → **422** (reject oversize upload)  
   **And** `audit_events` records `attachment.confirmed` in the **same transaction** as status update with metadata `{ attachmentId, resourceType, resourceId }`

4. **Given** confirm validation  
   **When**:
   - `attachmentId` not found in org → **404**
   - attachment already `Confirmed` → **409** Problem Details ("Attachment already confirmed.")
   - blob not present at storage path (client never uploaded or upload failed) → **422** Problem Details ("Upload not found. Complete the blob PUT before confirming.")
   - actor cannot read the note's case → **403**
   **Then** no status change and no confirm audit

5. **Given** a **Confirmed** attachment on a note I can read  
   **When** I `GET /api/v1/attachments/{id}/download-url`  
   **Then** **200 OK** with `{ data: { downloadUrl, downloadExpiresAtUtc }, meta: { requestId } }` — fresh **read** SAS (**15 min**)  
   **And** unauthorized role / wrong assignee → **403**; attachment not found in org → **404**; `Pending` attachment → **422** ("Attachment upload not confirmed.")

6. **Given** I `GET /api/v1/cases/{id}/notes` (Story 4.1 timeline)  
   **When** notes have confirmed attachments  
   **Then** each `CaseNoteDto` includes `attachments: AttachmentSummaryDto[]` (may be empty array)  
   **And** `AttachmentSummaryDto` has `id`, `originalFileName`, `contentType`, `fileSizeBytes`, `confirmedAtUtc` — **no** embedded download URL (client calls `GET /attachments/{id}/download-url` when preview needed — Story 4.3)  
   **And** pending/unconfirmed attachments are **omitted** from timeline list  
   **And** confirmed attachments per note are ordered **`confirmed_at_utc` ascending, then `id`** (stable ordering)

7. **Given** architecture NFR-5  
   **When** SAS URLs are issued (presign upload or download)  
   **Then** expiry is **15 minutes** from issue (`BlobStorage:SasExpiryMinutes` default **15**)  
   **And** role check runs **before** every SAS issue (presign, confirm downloadUrl, GET download-url)  
   **And** blob container is **private** (no anonymous public access)

8. **Given** local development  
   **When** developer runs `docker compose -f infra/docker-compose.yml up`  
   **Then** **Azurite** blob service is available (port **10000** default)  
   **And** `appsettings.Development.json` includes `BlobStorage` section with connection string pointing at Azurite  
   **And** API ensures container exists on startup (create-if-not-exists)

9. **Given** no UI in this story  
   **When** this story ships  
   **Then** **no** web/mobile changes  
   **And** **no** travel-claim attachment wiring (Epic 6 reuses `attachments` table + service with new `ResourceType`)  
   **And** **no** offline/mobile sync of attachments  
   **And** **no** attachment delete endpoint in v1

10. **Given** OpenAPI and client contract  
    **When** this story ships  
    **Then** OpenAPI documents `POST /attachments/presign`, `POST /attachments/confirm`, `GET /attachments/{id}/download-url`  
    **And** `CaseNoteDto` schema includes `attachments` array  
    **And** `packages/api-client` regenerated from snapshot  
    **And** README documents presign → PUT → confirm flow, allowed types, size limit, Azurite local setup

11. **Given** test baseline after Story 4.1  
    **When** I run `dotnet test Midi-Kaval.slnx`  
    **Then** all existing tests pass (except known pre-existing `VisitGroupingTests` failures — do not regress further)  
    **And** new integration tests in `AttachmentPresignTests.cs` cover at minimum:
    - happy path: create note → presign → HTTP PUT to `uploadUrl` → confirm → DB `Confirmed` + audits `attachment.presign.issued` + `attachment.confirmed`
    - `GET /cases/{id}/notes` includes attachment summary after confirm
    - `GET /attachments/{id}/download-url` returns 200 with fetchable URL (HEAD or GET blob via `HttpClient`)
    - coordinator presign/confirm on any org case note → success
    - SocialWorker on assigned case → success; other worker's note → **403** on presign, confirm, and `GET download-url`
    - **CaseWorker parity** — same RBAC as SocialWorker on presign/confirm
    - unassigned case + field worker → **403** on presign (Story 2.8 parity)
    - deactivated user → **403** on presign
    - invalid `contentType` / oversize `fileSizeBytes` / numeric `resourceType` (`"0"`) → **400**
    - confirm without blob PUT → **422**
    - double confirm → **409**
    - presign for missing note → **404**; unauthenticated → **401**
    - existing `CaseNotesTests` still pass — `CaseNoteDto.attachments` defaults to **empty array** on notes without attachments
    - `SwaggerEndpointTests` — new attachment paths present
    - `UsersSchemaTests` — `attachments` table in migration list (alphabetically after `audit_events`); TRUNCATE includes `attachments`

## Tasks / Subtasks

- [x] **Infrastructure — blob storage** (AC: 1, 7, 8)
  - [x] Add NuGet `Azure.Storage.Blobs` (12.x, compatible with .NET 8)
  - [x] `Infrastructure/Storage/BlobStorageOptions.cs` — `SectionName = "BlobStorage"`, `ConnectionString`, `ContainerName` (default `attachments`), `SasExpiryMinutes` (15), `MaxUploadBytes` (10485760)
  - [x] `Infrastructure/Storage/IBlobStorageService.cs` — `EnsureContainerAsync`, `GenerateUploadSasUri(blobName, contentType, expiry)`, `GenerateReadSasUri(blobName, expiry)`, `BlobExistsAsync`, `GetBlobSizeAsync`
  - [x] `Infrastructure/Storage/AzureBlobStorageService.cs` — implement with `BlobSasBuilder`; blob path `{organisationId}/case-note/{caseNoteId}/{attachmentId}/{sanitizedFileName}` (lowercase segment; store original name in DB)
  - [x] `Infrastructure/Storage/StorageServiceCollectionExtensions.cs` — `AddBlobStorage(IServiceCollection, IConfiguration)` register options + scoped service
  - [x] `Program.cs` — bind options; register blob service inside `if (!builder.Environment.IsTesting())` block (same as other infra)
  - [x] Extend `DatabaseInitializer.ApplyMigrationsAndSeedAsync` (Development only) — after migrate, resolve `IBlobStorageService` and call `EnsureContainerAsync` (mirror migration/seed gate — **not** in `Testing` env used by `TestingWebApplicationFactory`)
  - [x] **Note:** `AuthWebApplicationFactory` uses `Development` (not `Testing`) — blob services **do** register; factory must start Azurite and set `BlobStorage__*` env vars before host creation
  - [x] `appsettings.Development.json` — Azurite connection string `UseDevelopmentStorage=true` or explicit `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=...;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;`
  - [x] `infra/docker-compose.yml` — add `azurite` service (`mcr.microsoft.com/azure-storage/azurite:3.34.0`), port 10000, volume for persistence optional

- [x] **Domain — attachment model** (AC: 1, 3, 6)
  - [x] `Domain/Enums/AttachmentResourceType.cs` — `CaseNote` (v1); comment Epic 6 `TravelClaim`
  - [x] `Domain/Enums/AttachmentStatus.cs` — `Pending`, `Confirmed`
  - [x] `Domain/Entities/Attachment.cs` — fields per AC1
  - [x] `Infrastructure/Persistence/AttachmentConfiguration.cs` — table `attachments`, snake_case, index `(resource_type, resource_id)`, index `(organisation_id, status)`, FK not required to `case_notes` (generic resource — enforce in service)
  - [x] `DbSet<Attachment>` in `AppDbContext.cs`
  - [x] EF migration `AddAttachments`

- [x] **API — DTOs** (AC: 1, 3, 5, 6)
  - [x] `Models/Attachments/AttachmentDtos.cs` — `AttachmentPresignRequest`, `AttachmentPresignResultDto`, `AttachmentConfirmRequest`, `AttachmentDto`, `AttachmentSummaryDto`, `AttachmentDownloadUrlDto`
  - [x] Extend `CaseNoteDto` with `IReadOnlyList<AttachmentSummaryDto> Attachments` (default empty)
  - [x] camelCase JSON property names

- [x] **API — AttachmentService** (AC: 1–7)
  - [x] `Infrastructure/Storage/AttachmentService.cs` — inject `AppDbContext`, `IHttpContextAccessor`, `IBlobStorageService`, `IOptions<BlobStorageOptions>`
  - [x] Duplicate private `ResolveActorContext`, `ResolveActorRole`, `EnsureCanReadCase`, `IsSupervisorRole` from `CaseNoteService.cs` (still private in `CaseService`)
  - [x] `PresignForCaseNoteAsync` — load note by `resourceId` + org; load case; `EnsureCanReadCase`; validate request; insert Pending row + audit; return SAS upload URL
  - [x] `ConfirmAsync` — load attachment; resolve case via note; auth; verify Pending; verify blob exists + size; update Confirmed + audit; return DTO with read SAS
  - [x] `GetDownloadUrlAsync` — Confirmed only; auth via note/case chain; return read SAS
  - [x] `AuditEventTypes.AttachmentPresignIssued = "attachment.presign.issued"`, `AttachmentConfirmed = "attachment.confirmed"`
  - [x] Reuse `CaseNotFoundException`, `CaseForbiddenException`, `CaseValidationException`, `CaseBusinessRuleException`, `CaseConflictException` (double confirm → **409**); add `AttachmentNotFoundException` in `Infrastructure/Cases` namespace (same file pattern as `CaseNotFoundException`)

- [x] **API — extend CaseNoteService list** (AC: 6)
  - [x] `ListAsync` — after loading notes, batch-load confirmed attachments where `resource_type = CaseNote` and `resource_id IN noteIds`; map to `AttachmentSummaryDto` per note; preserve order `CreatedAtUtc, Id`

- [x] **API — AttachmentsController** (AC: 1, 3, 5, 10)
  - [x] `Controllers/V1/AttachmentsController.cs` — route prefix `api/v1/attachments`
  - [x] `POST presign`, `POST confirm`, `GET {id:guid}/download-url` — all `[Authorize]`; null body on POST → **400** `"Request body is required."` at controller (mirror `CasesController` notes routes)
  - [x] Exception mapping consistent with `CasesController` — reuse `ConflictProblem` / `ForbiddenProblem` helpers (400/403/404/409/422)
  - [x] XML doc comments + `[ProducesResponseType]`
  - [x] Register `AddScoped<AttachmentService>()` in `Program.cs`

- [x] **Integration tests** (AC: 11)
  - [x] Add `Testcontainers.Azurite` to test project (align version with existing Testcontainers 4.1.0+)
  - [x] Extend `AuthWebApplicationFactory` — start Azurite container; set `BlobStorage__ConnectionString` from container; ensure container created
  - [x] `tests/api.integration/AttachmentPresignTests.cs` — `[Collection("AuthIntegration")]`
  - [x] Helpers in `CaseTestData` or new `AttachmentTestData` — `PresignAsync`, `ConfirmAsync`, `PutBlobAsync` (raw HttpClient PUT to SAS URL)
  - [x] Update `SwaggerEndpointTests.cs`, `UsersSchemaTests.cs` (expected tables list + TRUNCATE — add `attachments`; no FK to `case_notes`, any TRUNCATE position OK)
  - [x] Update `README.md` Quick start — Azurite port **10000** alongside Postgres/Redis

- [x] **OpenAPI + api-client + README** (AC: 10)
  - [x] Export snapshot; `npm run generate:api-client`
  - [x] README — attachment flow, Azurite compose, limits

### Review Findings

- [x] [Review][Patch] Missing test: confirm when uploaded blob exceeds presigned `fileSizeBytes` → **422** [`AttachmentPresignTests.cs`] — fixed: `Confirm_OversizeBlobPut_Returns422`
- [x] [Review][Patch] Missing test: `GET /attachments/{id}/download-url` on **Pending** attachment → **422** [`AttachmentPresignTests.cs`] — fixed: `GetDownloadUrl_PendingAttachment_Returns422`
- [x] [Review][Patch] `fileName` max length not validated before DB insert [`AttachmentService.cs:315`] — fixed: 255-char validation + `Presign_FileNameTooLong_Returns400` test
- [x] [Review][Defer] Blob container bootstrap only in Development startup [`DatabaseInitializer.cs`] — deferred, story scoped local/Azurite; production ops must pre-create container
- [x] [Review][Defer] Orphan `Pending` attachment rows / no presign rate limit [`AttachmentService.cs`] — deferred, v1 story out of scope
- [x] [Review][Defer] Concurrent double-confirm race (no row version) [`AttachmentService.cs:157`] — deferred, rare v1 edge case

## Dev Notes

### READ FIRST (implementation guardrails)

1. **Note must exist before presign** — flow is `POST /cases/{id}/notes` (4.1) → presign with `resourceId = noteId`. Do not presign against a case alone.
2. **`EnsureCanReadCase` is private** — duplicate auth helpers in `AttachmentService` (same as `CaseNoteService`).
3. **Unassigned case → 403** for field workers (Story 2.8) — applies to presign, confirm, and download-url.
4. **Generic `attachments` table** — `resource_type` + `resource_id` enables Epic 6 travel receipts without schema rewrite. v1 presign whitelist: `"CaseNote"` only (explicit string — reject numeric enum strings).
5. **Never trust client blob paths** — server generates `blob_name`; sanitize `fileName` to basename only.
6. **SAS 15 min** — configure via `BlobStorage:SasExpiryMinutes`; applies to upload and download SAS. Local Azurite URLs are HTTP — do not force HTTPS in dev.
7. **Private container** — no public blob access; all access via role-checked SAS issue.
8. **Pending attachments omitted from timeline** — avoids showing broken uploads; orphan Pending rows acceptable in v1 (no cleanup job).
9. **`CaseNoteDto.attachments`** — always serialize as array (empty when none); do not break existing `CaseNotesTests`.
10. **Confirm + audit** — single `SaveChangesAsync` transaction (mirror Story 4.1 note create).
11. **API-only** — no web/mobile/UI; Story 4.3 consumes api-client + download-url endpoint.
12. **Integration tests use `Development` env** — `AuthWebApplicationFactory` is not `Testing`; Azurite Testcontainer required alongside Postgres/Redis.

### Epic 4 context

| Story | Delivers |
|-------|----------|
| 4.1 (done) | `case_notes`, POST/GET timeline, audit |
| **4.2 (this)** | Blob presign/confirm, `attachments`, timeline metadata |
| 4.3 | Web + mobile timeline UI with attachment preview |
| 4.4 | Interventions CRUD API |
| 4.5 | Interventions UI + overdue job |

Epic 6 will add `ResourceType.TravelClaim` and presign for claim receipts — **do not** implement claim wiring now; design service with switch/extensibility point.

### Architecture compliance

[Source: `_bmad-output/planning-artifacts/architecture.md` §5.2, §5.3, §6]

- **Flow:** `POST /attachments/presign` → client PUT blob → `POST /attachments/confirm` [architecture §6 rule 5]
- **Storage:** Azure Blob private container; SAS 15 min; role check before issue [§5.2]
- **Envelope / errors:** same as rest of API
- **Audit:** append-only; no SAS tokens or file content in metadata
- **Infrastructure folder:** `Infrastructure/Storage/` per project-context

### Authorization chain for attachments

```
Attachment → resource_id (case_note.id) → CaseNote → Case → EnsureCanReadCase
```

Supervisor roles (Coordinator, Director): any org case. Field workers: assigned case only. Unassigned case → **403**.

### Blob path convention

```
{organisationId}/case-note/{caseNoteId}/{attachmentId}/{sanitizedFileName}
```

Use lowercase enum segment in path; store original filename in DB column.

### Presign / confirm sequence (integration test reference)

```
1. POST /cases/{caseId}/notes        → noteId
2. POST /attachments/presign         → attachmentId, uploadUrl, requiredHeaders
3. PUT uploadUrl (HttpClient)        → 201 Created (Azurite)
4. POST /attachments/confirm         → status Confirmed, downloadUrl
5. GET /cases/{caseId}/notes         → note.attachments[0].id present
6. GET /attachments/{id}/download-url → fresh read SAS
```

### CaseNoteService list change

Extend `ListAsync` only — do not change create behavior. Load attachments in one query:

```csharp
var noteIds = notes.Select(n => n.Id).ToList();
var attachments = await db.Attachments
    .Where(a => a.OrganisationId == organisationId
        && a.ResourceType == AttachmentResourceType.CaseNote
        && noteIds.Contains(a.ResourceId)
        && a.Status == AttachmentStatus.Confirmed)
    .ToListAsync(cancellationToken);
```

Group by `ResourceId` when building DTOs; order each note's attachments by `ConfirmedAtUtc`, then `Id`.

Initialize `CaseNoteDto.Attachments = Array.Empty<AttachmentSummaryDto>()` in `MapToDto` before overlaying loaded summaries.

### Configuration (`appsettings.Development.json`)

```json
"BlobStorage": {
  "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPH0JgJoR19l870eXw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
  "ContainerName": "attachments",
  "SasExpiryMinutes": 15,
  "MaxUploadBytes": 10485760
}
```

### docker-compose Azurite snippet

```yaml
  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:3.34.0
    container_name: midi-kaval-azurite
    ports:
      - "10000:10000"
    command: azurite-blob --blobHost 0.0.0.0 --blobPort 10000
```

### AuthWebApplicationFactory extension

`AuthWebApplicationFactory` uses **`Development`** (see line 62) — blob DI registers normally. Mirror postgres/redis startup:

```csharp
private readonly AzuriteContainer _azurite = new AzuriteBuilder().Build();
// StartContainersAsync: await _azurite.StartAsync(); (before postgres or in parallel)
// ApplyTestConfiguration:
Environment.SetEnvironmentVariable("BlobStorage__ConnectionString", _azurite.GetConnectionString());
Environment.SetEnvironmentVariable("BlobStorage__ContainerName", "attachments");
Environment.SetEnvironmentVariable("BlobStorage__SasExpiryMinutes", "15");
Environment.SetEnvironmentVariable("BlobStorage__MaxUploadBytes", "10485760");
```

Package: `Testcontainers.Azurite` (match existing `Testcontainers.PostgreSql` **4.1.0**). Use separate `HttpClient` for raw PUT to presigned `uploadUrl` with `requiredHeaders` (not the API test client — SAS URL is absolute).

### DatabaseInitializer blob bootstrap

After `MigrateAsync()` in `ApplyMigrationsAndSeedAsync`, call `IBlobStorageService.EnsureContainerAsync()` so local dev and integration tests (Development env) have the container before first presign.

### Audit events

```csharp
public const string AttachmentPresignIssued = "attachment.presign.issued";
public const string AttachmentConfirmed = "attachment.confirmed";
```

Metadata excludes SAS URLs and file bytes. `SubjectUserId = actorUserId`.

### Testing requirements

| Layer | Location | Pattern |
|-------|----------|---------|
| Integration | `AttachmentPresignTests.cs` | AuthWebApplicationFactory + Azurite; HTTP presign → external PUT → confirm |
| Schema | `UsersSchemaTests.cs` | Add `attachments` to expected tables; TRUNCATE list |

**Pre-existing failures:** `VisitGroupingTests` (2) — POCSO seed; do not block story on fixing these.

### OpenAPI + api-client regeneration (Windows)

```bat
set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json
dotnet test tests/api.integration/MidiKaval.Api.IntegrationTests.csproj --filter "FullyQualifiedName~SwaggerEndpointTests.Export_Swagger_WhenRequested"
set API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json
npm run generate:api-client
```

### File structure (new + modified)

| Action | Path |
|--------|------|
| NEW | `apps/api/Infrastructure/Storage/BlobStorageOptions.cs` |
| NEW | `apps/api/Infrastructure/Storage/IBlobStorageService.cs` |
| NEW | `apps/api/Infrastructure/Storage/AzureBlobStorageService.cs` |
| NEW | `apps/api/Infrastructure/Storage/StorageServiceCollectionExtensions.cs` |
| NEW | `apps/api/Infrastructure/Storage/AttachmentService.cs` |
| NEW | `apps/api/Domain/Enums/AttachmentResourceType.cs` |
| NEW | `apps/api/Domain/Enums/AttachmentStatus.cs` |
| NEW | `apps/api/Domain/Entities/Attachment.cs` |
| NEW | `apps/api/Infrastructure/Persistence/AttachmentConfiguration.cs` |
| NEW | `apps/api/Models/Attachments/AttachmentDtos.cs` |
| NEW | `apps/api/Controllers/V1/AttachmentsController.cs` |
| NEW | `apps/api/Migrations/*_AddAttachments.cs` |
| NEW | `tests/api.integration/AttachmentPresignTests.cs` |
| UPDATE | `apps/api/MidiKaval.Api.csproj` (Azure.Storage.Blobs) |
| UPDATE | `apps/api/Infrastructure/Persistence/AppDbContext.cs` |
| UPDATE | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Cases/CaseNoteService.cs` (list attachments) |
| UPDATE | `apps/api/Models/Cases/CaseNoteDtos.cs` |
| UPDATE | `apps/api/Program.cs` |
| UPDATE | `apps/api/Infrastructure/Seed/DatabaseInitializer.cs` |
| UPDATE | `apps/api/appsettings.Development.json` |
| UPDATE | `infra/docker-compose.yml` |
| UPDATE | `tests/api.integration/AuthWebApplicationFactory.cs` |
| UPDATE | `tests/api.integration/MidiKaval.Api.IntegrationTests.csproj` |
| UPDATE | `tests/api.integration/SwaggerEndpointTests.cs` |
| UPDATE | `tests/api.integration/UsersSchemaTests.cs` |
| UPDATE | `README.md` |
| UPDATE | `packages/api-client/` (generated) |

### Previous story intelligence (4.1)

- Duplicate `EnsureCanReadCase` — do not refactor `CaseService` in this story.
- `CaseNoteService.ListAsync` uses `orderby note.CreatedAtUtc, note.Id` — preserve when extending.
- Deactivated author email filtered with `author.IsActive` — unrelated but do not regress.
- `noteType` whitelist pattern — use similar explicit allow-list for `contentType` and `resourceType`.
- Code review added GET 403 wrong-assignee test — mirror for attachment presign/confirm.
- Audit never stores PII/note body — same for attachment bytes.

### Out of scope (explicit)

- Web/mobile attachment UI (4.3)
- Travel claim attachments (Epic 6)
- Attachment delete/replace
- Pending attachment TTL cleanup job
- Virus scanning / content inspection
- Unified server-side merge of `visit_notes` + `case_notes` timeline

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 4.2]
- [Source: `_bmad-output/planning-artifacts/architecture.md` §5.2, §6]
- [Source: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-13.md` — NFR-5]
- [Source: `_bmad-output/implementation-artifacts/4-1-case-notes-api-and-timeline.md`]
- [Source: `_bmad-output/project-context.md` — Attachments: presign → PUT → confirm]
- [Source: `apps/api/Infrastructure/Cases/CaseNoteService.cs`]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — EnsureCanReadCase]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

### Completion Notes List

- Implemented presign → PUT → confirm attachment flow with Azure Blob SAS (15 min), generic `attachments` table, RBAC via case-note chain, audit events.
- Extended `GET /cases/{id}/notes` with confirmed attachment summaries; pending omitted from timeline.
- Added Azurite to docker-compose + Testcontainers for integration tests (16 new tests in `AttachmentPresignTests.cs`).
- Full integration suite: **267/269 pass**; 2 pre-existing `VisitGroupingTests` failures unchanged.
- Code review (2026-06-19): 3 patches applied — oversize confirm test, pending download-url test, fileName max 255 validation.

### File List

- apps/api/Domain/Enums/AttachmentResourceType.cs (NEW)
- apps/api/Domain/Enums/AttachmentStatus.cs (NEW)
- apps/api/Domain/Entities/Attachment.cs (NEW)
- apps/api/Infrastructure/Persistence/AttachmentConfiguration.cs (NEW)
- apps/api/Infrastructure/Storage/BlobStorageOptions.cs (NEW)
- apps/api/Infrastructure/Storage/IBlobStorageService.cs (NEW)
- apps/api/Infrastructure/Storage/AzureBlobStorageService.cs (NEW)
- apps/api/Infrastructure/Storage/StorageServiceCollectionExtensions.cs (NEW)
- apps/api/Infrastructure/Storage/AttachmentService.cs (NEW)
- apps/api/Models/Attachments/AttachmentDtos.cs (NEW)
- apps/api/Controllers/V1/AttachmentsController.cs (NEW)
- apps/api/Migrations/20260619024652_AddAttachments.cs (NEW)
- apps/api/Migrations/20260619024652_AddAttachments.Designer.cs (NEW)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (UPDATED)
- tests/api.integration/AttachmentPresignTests.cs (NEW)
- apps/api/MidiKaval.Api.csproj (UPDATED)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (UPDATED)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (UPDATED)
- apps/api/Infrastructure/Cases/CaseNoteService.cs (UPDATED)
- apps/api/Models/Cases/CaseNoteDtos.cs (UPDATED)
- apps/api/Program.cs (UPDATED)
- apps/api/Infrastructure/Seed/DatabaseInitializer.cs (UPDATED)
- apps/api/appsettings.Development.json (UPDATED)
- infra/docker-compose.yml (UPDATED)
- tests/api.integration/AuthWebApplicationFactory.cs (UPDATED)
- tests/api.integration/MidiKaval.Api.IntegrationTests.csproj (UPDATED)
- tests/api.integration/CaseCreateTests.cs (UPDATED)
- tests/api.integration/SwaggerEndpointTests.cs (UPDATED)
- tests/api.integration/UsersSchemaTests.cs (UPDATED)
- README.md (UPDATED)
- packages/api-client/openapi-snapshot.json (UPDATED)
- packages/api-client/src/generated/api.ts (UPDATED)
