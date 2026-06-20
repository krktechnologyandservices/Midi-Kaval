---
baseline_commit: NO_VCS
---

# Story 4.1: Case Notes API and Timeline

Status: done

<!-- Validated: 2026-06-19 — see 4-1-case-notes-api-and-timeline-validation-report.md (7 fixes applied) -->

## Story

As a **staff member**,
I want typed notes on Cases,
so that activity is documented (FR-13, CAP-5).

*Scope: **API only** — `case_notes` schema, `POST` create, `GET` chronological timeline list, audit. No attachments (Story 4.2), no web/mobile UI (Story 4.3), no interventions (Stories 4.4–4.5). Do **not** conflate `visit_notes` (visit-completion bridge from Story 3.3) with `case_notes`; do **not** auto-emit `case_notes` on visit complete in this story.*

## Acceptance Criteria

1. **Given** I am authenticated and can read a Case (`EnsureCanReadCase` — Coordinator/Director: any org case; SocialWorker/CaseWorker: assigned only)  
   **When** I `POST /api/v1/cases/{id}/notes` with body:
   ```json
   {
     "noteType": "Visit|Court|Intervention|General",
     "bodyText": "<non-empty text>",
     "actionRequired": false,
     "actionDueAtUtc": null
   }
   ```
   **Then** response is **201 Created** with envelope `{ data: CaseNoteDto, meta: { requestId } }` (auto-wrapped by `ApiEnvelopeFilter`)  
   **And** a row is inserted into **`case_notes`** with `organisation_id`, `case_id`, `author_user_id` = current user, `note_type`, trimmed `body_text` (max **4000** chars), `action_required`, optional `action_due_at_utc`, `created_at_utc` = `DateTime.UtcNow`  
   **And** `audit_events` records `case.note.created` in the **same transaction** with metadata `{ caseId, noteId, noteType }` — **no note body text** in audit (PII pattern from Story 3.3)  
   **And** `Location` header / `Created` route: `/api/v1/cases/{caseId}/notes` (collection URI — no per-note GET in v1)

2. **Given** I `POST /api/v1/cases/{id}/notes` with missing body, null `bodyText`, or whitespace-only `bodyText`  
   **When** validation runs  
   **Then** **400** Problem Details; no row or audit

3. **Given** I `POST /api/v1/cases/{id}/notes` with `bodyText` longer than 4000 characters  
   **When** validation runs  
   **Then** **400** Problem Details

4. **Given** I `POST /api/v1/cases/{id}/notes` with missing, null, empty, or invalid `noteType`  
   **When** validation runs  
   **Then** **400** Problem Details with allowed values: `Visit`, `Court`, `Intervention`, `General` (PascalCase strings in JSON — match existing enum-as-string pattern e.g. `CaseStage`)

5. **Given** I `POST /api/v1/cases/{id}/notes` with `actionDueAtUtc` supplied  
   **When** the value is not a valid ISO 8601 UTC timestamp  
   **Then** **400** Problem Details  
   **And** when `actionDueAtUtc` is in the past (strictly before `DateTime.UtcNow`) → **422** Problem Details ("Action due date must be in the future.")  
   **And** when `actionRequired` is true and `actionDueAtUtc` is null → **400** Problem Details  
   **And** when `actionRequired` is false/omitted and `actionDueAtUtc` is set → persist with `action_required = true` (implicit — due date implies action required)

6. **Given** I can read a Case  
   **When** I `GET /api/v1/cases/{id}/notes`  
   **Then** response is **200 OK** with `{ data: { items: CaseNoteDto[] }, meta: { requestId } }`  
   **And** items are **`case_notes` only**, ordered **`created_at_utc` ascending** (oldest first — chronological timeline feed)  
   **And** each `CaseNoteDto` includes: `id`, `caseId`, `noteType`, `bodyText`, `actionRequired`, `actionDueAtUtc`, `authorUserId`, `authorEmail`, `createdAtUtc`  
   **And** `authorEmail` is resolved from `users` join (active author in same org) — omit/null only if user row missing (defensive)  
   **And** read is **not** audited (same as `GET /cases/{id}`)

6b. **Given** a Case I can read with **no** `case_notes` rows  
    **When** I `GET /api/v1/cases/{id}/notes`  
    **Then** **200 OK** with `{ data: { items: [] }, meta: { requestId } }` (not 404)

7. **Given** per-resource authorization (Story 2.8)  
   **When** I call `POST` or `GET` notes on a case I cannot read  
   **Then** **403** with `Policies.ForbiddenByRoleMessage` for wrong assignee or **unassigned** case (`assigned_worker_id` is null — field worker has no read access)  
   **And** case not found in org → **404**  
   **And** unauthenticated → **401**; deactivated → **403** with `AuthService.DeactivatedMessage`

8. **Given** no note mutations besides create in v1  
   **When** this story ships  
   **Then** **no** `PATCH`/`DELETE` note endpoints  
   **And** **no** attachment fields or presign flow (Story 4.2)  
   **And** **no** web/mobile timeline UI changes (Story 4.3 — placeholders remain)  
   **And** **no** auto-sync of `visit_notes` into `case_notes` or unified timeline merge (visit completion notes stay in `visit_notes`; UI may compose both in 4.3)

9. **Given** OpenAPI and client contract  
   **When** this story ships  
   **Then** OpenAPI documents `POST /api/v1/cases/{id}/notes` and `GET /api/v1/cases/{id}/notes`  
   **And** `packages/api-client` regenerated from snapshot (Windows flow in Dev Notes)  
   **And** README documents note types, action-due semantics, and `visit_notes` vs `case_notes` boundary

10. **Given** test baseline after Epic 3 (Stories 3.1–3.8 complete)  
    **When** I run `dotnet test Midi-Kaval.slnx`  
    **Then** all existing tests pass  
    **And** new integration tests in `CaseNotesTests.cs` cover at minimum:
    - assignee `POST` note → **201** + DB row + audit `case.note.created` (metadata has caseId, noteId, noteType; no bodyText)
    - assignee `GET` notes → **200** chronological order (two notes; backdate second row `created_at_utc` via DB — AC10 backdate bullet)
    - coordinator `POST`/`GET` on any org case → **201**/**200**
    - SocialWorker assigned case → **201**; other worker's case → **403**
    - **CaseWorker parity** — same as SocialWorker
    - missing body / whitespace body / body > 4000 → **400**
    - invalid `noteType` → **400**
    - `actionRequired: true` without due date → **400**; past due date → **422**
    - case not found → **404**; unauthenticated → **401**; deactivated user → **403**
    - unassigned case (`assigned_worker_id` null) + field worker → **403** (AC7)
    - `GET` on case with zero notes → **200** empty `items` (AC6b)
    - director `POST`/`GET` → **201**/**200** (supervisor parity with coordinator)
    - chronological order via two notes with backdated `created_at_utc` on second row (DB update in test — mirror Story 2.8 whisper backdate pattern)
    - `SwaggerEndpointTests` — new paths present

## Tasks / Subtasks

- [x] **Domain — case note model** (AC: 1, 6)
  - [x] `Domain/Enums/CaseNoteType.cs` — `Visit`, `Court`, `Intervention`, `General`
  - [x] `Domain/Entities/CaseNote.cs` — `Id`, `OrganisationId`, `CaseId`, `AuthorUserId`, `NoteType`, `BodyText`, `ActionRequired`, `ActionDueAtUtc?`, `CreatedAtUtc`
  - [x] `Infrastructure/Persistence/CaseNoteConfiguration.cs` — table `case_notes`, snake_case, `body_text` max 4000, `note_type` string max 64, index `(case_id, created_at_utc)` for timeline reads (mirror `CaseStageTransitionConfiguration`); FK `case_id` → `cases` and `author_user_id` → `users` with `OnDelete(DeleteBehavior.Restrict)` (mirror `CaseConfiguration` assignee FK)
  - [x] Add `DbSet<CaseNote>` to `AppDbContext.cs`
  - [x] EF migration `AddCaseNotes`

- [x] **API — DTOs** (AC: 1, 6)
  - [x] `Models/Cases/CaseNoteDtos.cs` — `CreateCaseNoteRequest`, `CaseNoteDto`, `CaseNoteListResultDto` (`Items` array)
  - [x] JSON property names camelCase: `noteType`, `bodyText`, `actionRequired`, `actionDueAtUtc`, `authorUserId`, `authorEmail`, `createdAtUtc`

- [x] **API — CaseNoteService** (AC: 1–7)
  - [x] `Infrastructure/Cases/CaseNoteService.cs` — inject `AppDbContext`, `IHttpContextAccessor`; **duplicate** private `ResolveActorContext`, `ResolveActorRole`, `EnsureCanReadCase`, `IsSupervisorRole` from `CaseService.cs` (`EnsureCanReadCase` is **private** — cannot call across services)
  - [x] `CreateAsync(caseId, request)` — load case with `organisation_id` scope; `EnsureCanReadCase`; validate fields; insert note + audit; return DTO
  - [x] `ListAsync(caseId)` — load case; `EnsureCanReadCase`; query `case_notes` where `organisation_id` matches actor org, ordered by `CreatedAtUtc` asc; join author email
  - [x] Reuse exception types from `CaseService.cs` namespace only (`CaseNotFoundException`, `CaseForbiddenException`, `CaseValidationException`, `CaseBusinessRuleException`) — do not duplicate exception **classes**
  - [x] `AuditEventTypes.CaseNoteCreated = "case.note.created"`

- [x] **API — controller routes** (AC: 1, 6, 7, 9)
  - [x] Inject `CaseNoteService` into `CasesController`
  - [x] `POST {id:guid}/notes` — `[Authorize]` (not `CoordinatorOrAbove` — field workers add notes on assigned cases); null body → **400** `"Request body is required."` at controller (mirror `ScheduleVisit`)
  - [x] `GET {id:guid}/notes` — `[Authorize]`
  - [x] XML doc comments on both actions (OpenAPI — project-context rule)
  - [x] Map exceptions: validation → 400, business rule → 422, forbidden → 403, not found → 404
  - [x] `[ProducesResponseType]` for 200/201, 400, 401, 403, 404, 422
  - [x] Register `AddScoped<CaseNoteService>()` in `Program.cs`

- [x] **API — integration tests** (AC: 10)
  - [x] `tests/api.integration/CaseNotesTests.cs` — `[Collection("AuthIntegration")]`
  - [x] Extend `CaseTestData` (in `CaseCreateTests.cs`) — `CreateCaseNoteAsync`, `ListCaseNotesAsync` helpers; reuse `TransferCaseAsync` for assignee setup
  - [x] Update `SwaggerEndpointTests.cs`

- [x] **OpenAPI + api-client** (AC: 9)
  - [x] Export snapshot; `npm run generate:api-client`
  - [x] README — note endpoints, types, `visit_notes` boundary

### Review Findings

- [x] [Review][Patch] Numeric `noteType` strings bypass PascalCase validation [`CaseNoteService.cs:151-166`] — fixed: explicit allowed-name whitelist
- [x] [Review][Patch] Deactivated author email exposed in timeline list [`CaseNoteService.cs:105-116`] — fixed: `author.IsActive` filter in list join
- [x] [Review][Patch] Timeline order unstable when `created_at_utc` ties [`CaseNoteService.cs:110`] — fixed: secondary `orderby note.Id`
- [x] [Review][Patch] Missing GET 403 test for wrong assignee [`CaseNotesTests.cs`] — fixed: `SocialWorker_OtherWorkersCase_Get_Returns403`
- [x] [Review][Defer] `VisitGroupingTests` failures (2/250) — deferred, pre-existing POCSO seed pollutes today visit counts
- [x] [Review][Defer] Unbounded timeline GET without pagination — deferred, v1 scope; revisit if cases accumulate thousands of notes
- [x] [Review][Defer] Concurrent case delete → FK `DbUpdateException` may 500 — deferred, rare race; no v1 requirement

## Dev Notes

### READ FIRST (implementation guardrails)

1. **`EnsureCanReadCase` is private** in `CaseService.cs` — duplicate the helper (+ `IsSupervisorRole`, `ResolveActorContext`, `ResolveActorRole`) inside `CaseNoteService`; do not try to call `CaseService` for auth.
2. **Field worker on unassigned case** (`assigned_worker_id == null`) → **403** (same as Story 2.8 `GET /cases/{id}`).
3. **Empty timeline** → `GET` returns **200** with `items: []`, not 404.
4. **Audit `SubjectUserId`** = `actorUserId` (author) — mirror `visit.completed` / `case.gps.verified`; metadata excludes `bodyText`.
5. **Scope queries** by `organisation_id` on both case load and `case_notes` list (defense in depth).
6. **API-only** — no web/mobile/UI changes; no attachment columns; no `visit_notes` bridge changes.

### Epic 4 context

Epic 4 delivers FR-13 (notes timeline + attachments) and FR-14 (interventions). This story is the **foundation API** for typed case notes. Stories 4.2–4.5 build attachments, UI, and interventions on top.

| Story | Delivers |
|-------|----------|
| **4.1 (this)** | `case_notes` table, POST create, GET timeline list, audit |
| 4.2 | Presign → PUT blob → confirm for note attachments |
| 4.3 | Web + mobile timeline UI (uses generated api-client) |
| 4.4 | Interventions CRUD API |
| 4.5 | Interventions UI + overdue background job |

Future consumers: Crisis Queue (Epic 8) may flag "court within 48h without prep note" — `Court` notes with `actionRequired`/`actionDueAtUtc` support that pattern. Interventions (4.4) are a **separate** entity; `Intervention` **note type** is free-text activity logging, not the interventions table.

### `visit_notes` vs `case_notes` — critical boundary

| Table | Purpose | Created by | Story |
|-------|---------|------------|-------|
| `visit_notes` | Visit-completion capture (1:1 per visit) | `POST /visits/{id}/complete` | 3.3 |
| `case_notes` | Staff-typed timeline entries on a case | `POST /cases/{id}/notes` | **4.1** |

- Story 3.3 explicitly deferred `POST /cases/{id}/notes` to Epic 4.
- `visit_notes` sync merge (`VisitSyncNoteMerge`) applies only to offline visit completion — **not** to `case_notes`.
- `GET /cases/{id}/notes` returns **`case_notes` only** in 4.1. Story 4.3 UI may fetch visits (with `completionNote`) separately and compose a unified feed client-side, or a later story adds server-side merge.
- Do **not** delete or alter `visit_notes` behavior.

### Architecture compliance

[Source: `_bmad-output/planning-artifacts/architecture.md` §5.1, §5.3]

- **Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL 16+, JWT + policy RBAC
- **Routes:** `/api/v1/cases/{id}/notes` nested under case aggregate (same as `{id}/visits`, `{id}/transfer`)
- **Envelope:** `{ data, meta: { requestId } }` via `ApiEnvelopeFilter`
- **Errors:** RFC 7807 Problem Details
- **IDs:** UUID v4; timestamps ISO 8601 UTC
- **Audit:** append-only `audit_events` in same `SaveChangesAsync` as mutation
- **Tenant:** `organisation_id` on `case_notes` (required on all tenant-scoped tables)
- **No attachments** in 4.1 — architecture §5.2 presign flow is Story 4.2

### Authorization pattern (reuse — do not reinvent)

Copy `EnsureCanReadCase` from `CaseService.cs`:

```1126:1137:apps/api/Infrastructure/Cases/CaseService.cs
    private static void EnsureCanReadCase(Case entity, Guid actorUserId, string actorRole)
    {
        if (IsSupervisorRole(actorRole))
        {
            return;
        }

        if (entity.AssignedWorkerId != actorUserId)
        {
            throw new CaseForbiddenException();
        }
    }
```

- Both `POST` and `GET` notes use `[Authorize]` + `EnsureCanReadCase` — **not** `CoordinatorOrAbove`.
- Any staff member with case read access may add notes (FR-13 roles: Social Worker, Case Worker, Coordinator, Director per `roles-and-access.md`).

### Controller pattern (mirror existing nested routes)

Reference `CasesController` nested routes:

```172:218:apps/api/Controllers/V1/CasesController.cs
    [HttpGet("{id:guid}/visits")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    ...
    [HttpPost("{id:guid}/visits")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
```

Notes endpoints differ: `[Authorize]` only (field workers need access). Exception mapping matches `GetById`:

```234:255:apps/api/Controllers/V1/CasesController.cs
    [HttpGet("{id:guid}")]
    [Authorize]
    ...
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
```

Inject `CaseNoteService` alongside existing `CaseService`, `VisitService`.

### Schema design (`case_notes`)

Mirror `visit_notes` text limits and `case_stages` timeline index:

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid PK | `Guid.NewGuid()` |
| `organisation_id` | uuid | From case |
| `case_id` | uuid FK | → `cases.id` |
| `author_user_id` | uuid FK | → `users.id` |
| `note_type` | varchar(64) | Enum string: Visit, Court, Intervention, General |
| `body_text` | varchar(4000) | Required, trimmed |
| `action_required` | bool | Default false |
| `action_due_at_utc` | timestamptz nullable | Future-only when set |
| `created_at_utc` | timestamptz | Set on create |

**Index:** `ix_case_notes_case_id_created_at_utc` on `(case_id, created_at_utc)` — same pattern as `CaseStageTransitionConfiguration` line 31.

**No soft-delete** — notes are append-only in v1.

### Validation rules (service layer — no FluentValidation)

Match existing `CaseService` / `VisitService` manual validation:

| Rule | Status |
|------|--------|
| Null body | 400 "Request body is required." |
| Empty/whitespace `bodyText` | 400 |
| `bodyText` > 4000 | 400 |
| Invalid/missing `noteType` | 400 with allowed list |
| `actionRequired: true` + null `actionDueAtUtc` | 400 |
| `actionDueAtUtc` in past | 422 |
| `actionDueAtUtc` set, `actionRequired` false | Set `action_required = true` on persist |

Parse `noteType` with `Enum.TryParse<CaseNoteType>` (case-sensitive PascalCase to match JSON contract).

### Audit event

Add to `AuditEventTypes.cs`:

```csharp
public const string CaseNoteCreated = "case.note.created";
```

Metadata JSON: `{ "caseId": "...", "noteId": "...", "noteType": "Visit" }` — **never** include `bodyText` (POCSO/PII). Set `SubjectUserId = actorUserId` on the audit row (author).

### DTO shape (`CaseNoteDto`)

```json
{
  "id": "uuid",
  "caseId": "uuid",
  "noteType": "Court",
  "bodyText": "Prepared witness statement.",
  "actionRequired": true,
  "actionDueAtUtc": "2026-06-25T09:00:00Z",
  "authorUserId": "uuid",
  "authorEmail": "worker@org.example",
  "createdAtUtc": "2026-06-19T14:30:00Z"
}
```

List wrapper: `CaseNoteListResultDto { Items: CaseNoteDto[] }` inside envelope `data`.

### Testing requirements

[Source: `_bmad-output/project-context.md` Testing Rules]

| Layer | Location | Pattern |
|-------|----------|---------|
| Integration | `tests/api.integration/CaseNotesTests.cs` | `AuthWebApplicationFactory`, `RbacTestData.EnsureRoleUsersAsync`, HTTP → assert status → `ApiEnvelope<T>` → verify DB + audit |
| Unit | Optional | Only if extracting validation helpers |

**Must test:** RBAC denial (403), audit write, chronological ordering, validation 400/422.

**Test setup pattern** (from `CaseStageTransitionTests.cs`):
1. Coordinator creates case via `CaseTestData.CreateCaseAsync`
2. `CaseTestData.TransferCaseAsync` to field worker for assignee tests
3. Assert `db.CaseNotes` row + `db.AuditEvents` with `case.note.created`

**Mirror test:** `VisitSchedulerTests.CompleteVisit_WithNote_PersistsVisitNote_ReturnsCompletionNote` for note persistence assertions.

### OpenAPI + api-client regeneration (Windows)

```bat
set EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json
dotnet test tests/api.integration/MidiKaval.Api.IntegrationTests.csproj --filter "FullyQualifiedName~SwaggerEndpointTests"
set API_OPENAPI_FILE=packages/api-client/openapi-snapshot.json
npm run generate:api-client
```

Never hand-edit `packages/api-client/`.

### File structure (new + modified)

| Action | Path |
|--------|------|
| NEW | `apps/api/Domain/Enums/CaseNoteType.cs` |
| NEW | `apps/api/Domain/Entities/CaseNote.cs` |
| NEW | `apps/api/Infrastructure/Persistence/CaseNoteConfiguration.cs` |
| NEW | `apps/api/Infrastructure/Cases/CaseNoteService.cs` |
| NEW | `apps/api/Models/Cases/CaseNoteDtos.cs` |
| NEW | `apps/api/Migrations/*_AddCaseNotes.cs` |
| NEW | `tests/api.integration/CaseNotesTests.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/AppDbContext.cs` |
| UPDATE | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| UPDATE | `apps/api/Controllers/V1/CasesController.cs` |
| UPDATE | `apps/api/Program.cs` |
| UPDATE | `tests/api.integration/CaseCreateTests.cs` (helpers) |
| UPDATE | `tests/api.integration/SwaggerEndpointTests.cs` |
| UPDATE | `README.md` |

**Out of scope — do not touch:**
- `apps/web/`, `apps/mobile/` (Story 4.3)
- `visit_notes` entity/service/sync
- Blob storage / presign (Story 4.2)
- `interventions` table (Story 4.4)

### Previous epic intelligence (Epic 3 handoff)

- **Story 3.3:** `visit_notes` bridge; `body_text` max 4000; audit excludes note text; visit complete is single transaction.
- **Story 3.6:** Offline sync is visits-only; `case_notes` are **online-only** in v1 (no `POST /sync/push` entry for case notes).
- **Story 3.8:** POCSO cases exist — note bodies may contain sensitive text; never log body in audit metadata.
- **Story 2.8:** `EnsureCanReadCase` + handoff whisper; "View full timeline" placeholder awaits this story's GET endpoint (UI still deferred to 4.3).

### Project context reference

[Source: `_bmad-output/project-context.md`]

- Business logic in `Domain/` / `Infrastructure/` — not controllers
- `[Authorize(Policy = Policies.*)]` on mutations; notes use `[Authorize]` + per-resource check
- Regenerate api-client after API changes
- Integration tests use Testcontainers PostgreSQL

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 4, Story 4.1]
- [Source: `_bmad-output/specs/spec-kaval-online/field-and-court-operations.md` — CAP-5 Notes]
- [Source: `_bmad-output/specs/spec-kaval-online/SPEC.md` — CAP-5 intent]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-13]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.1 aggregates, §5.3 API conventions]
- [Source: `_bmad-output/implementation-artifacts/3-3-active-visit-flow-with-start-and-complete.md` — visit_notes boundary]
- [Source: `_bmad-output/implementation-artifacts/2-8-case-assignment-transfer-and-handoff-whisper.md` — read authorization]

## Dev Agent Record

### Agent Model Used

Auto (Cursor)

### Debug Log References

### Completion Notes List

- Added `case_notes` table (migration `AddCaseNotes`) with FK to `cases`/`users`, timeline index `(case_id, created_at_utc)`.
- `CaseNoteService` with duplicated auth helpers, validation, audit `case.note.created` (no body in metadata).
- `POST`/`GET /api/v1/cases/{id}/notes` on `CasesController` with `[Authorize]` + per-resource checks.
- 17 integration tests in `CaseNotesTests.cs`; updated `UsersSchemaTests` expected table list.
- OpenAPI snapshot exported; `packages/api-client` regenerated.
- **Tests:** 12 unit pass; 248/250 integration pass. Two pre-existing `VisitGroupingTests` failures (POCSO seed visit pollutes today list counts) — unrelated to Story 4.1.
- **Code review (2026-06-19):** 4 patches applied — noteType whitelist, deactivated author email filter, stable timeline order, GET 403 test (+ numeric/deactivated tests). 20/20 `CaseNotesTests` pass.

### File List

- apps/api/Domain/Enums/CaseNoteType.cs
- apps/api/Domain/Entities/CaseNote.cs
- apps/api/Infrastructure/Persistence/CaseNoteConfiguration.cs
- apps/api/Infrastructure/Cases/CaseNoteService.cs
- apps/api/Models/Cases/CaseNoteDtos.cs
- apps/api/Migrations/20260619015339_AddCaseNotes.cs
- apps/api/Migrations/20260619015339_AddCaseNotes.Designer.cs
- apps/api/Migrations/AppDbContextModelSnapshot.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Controllers/V1/CasesController.cs
- apps/api/Program.cs
- tests/api.integration/CaseNotesTests.cs
- tests/api.integration/CaseCreateTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- tests/api.integration/UsersSchemaTests.cs
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- README.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-06-19 — Code review: 4 patches applied; story marked done.
