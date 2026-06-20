---
baseline_commit: NO_VCS
---

# Story 2.1: Case Aggregate Schema and Create API

Status: done

<!-- Validated: 2026-06-15 — see 2-1-case-aggregate-schema-and-create-api-validation-report.md -->

## Story

As a **Project Coordinator**,
I want to create a Case with core beneficiary and offence fields,
so that field work is tracked in one record (FR-3).

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `POST /api/v1/cases` with required pilot fields (see API contract below)  
   **Then** a row is inserted in `cases` with `current_stage = ProcessInitiation`, `visit_count = 0`, `organisation_id` from JWT, and `created_by_user_id` from actor  
   **And** response is **201 Created** with envelope `{ data: CaseDto }` including `id`, identifiers, beneficiary summary, `currentStage`, `createdAtUtc`  
   **And** `audit_events` records `case.created` in the **same database transaction** as the insert

2. **Given** required fields are missing or invalid (empty crime/ST/beneficiary name, invalid enum values)  
   **When** I `POST /api/v1/cases`  
   **Then** response is **400** Problem Details with field-level or summary `detail`  
   **And** no `cases` row or audit row is persisted

3. **Given** a Case already exists with the same `crime_number` or `st_number` in my organisation  
   **When** I `POST /api/v1/cases` with a duplicate identifier  
   **Then** response is **409 Conflict** Problem Details (duplicate crime/ST)  
   **And** no partial row is left without audit (transaction rolled back)

4. **Given** I am authenticated as **SocialWorker** or **CaseWorker**  
   **When** I `POST /api/v1/cases`  
   **Then** response is **403** with `Policies.ForbiddenByRoleMessage` (field workers cannot create cases)

4b. **Given** I am authenticated as Coordinator/Director but my account is **deactivated** (`IsActive = false`)  
   **When** I `POST /api/v1/cases`  
   **Then** response is **403** with `AuthService.DeactivatedMessage` ("Contact your coordinator") — **not** the RBAC role message (inactive-user handler runs before role denial)

5. **Given** I am unauthenticated  
   **When** I `POST /api/v1/cases`  
   **Then** response is **401** Unauthorized

6. **Given** EF Core migrations run on PostgreSQL  
   **When** migration from this story is applied  
   **Then** only the **`cases`** table is added (no `case_stages`, `visits`, `notes`, `legend_*`, or other child tables)  
   **And** columns use snake_case; `UNIQUE(organisation_id, crime_number)` and `UNIQUE(organisation_id, st_number)` indexes exist per architecture

7. **Given** Epic 1 test baseline (53 .NET, 19 web, 17 mobile)  
   **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
   **Then** all existing tests pass  
   **And** new integration tests cover happy-path create, validation 400, duplicate crime **and** duplicate ST 409, RBAC 403 for field worker, deactivated 403, audit row  
   **And** OpenAPI documents `POST /api/v1/cases`; `packages/api-client` regenerated

8. **Given** Stories 2.2–2.9 are not yet implemented  
   **When** this story ships  
   **Then** **no web/mobile create UI**, no `GET /cases/search`, no `check-duplicate` endpoint, no stage `PATCH` — API + schema only

## Tasks / Subtasks

- [x] **Domain — Case entity + enums** (AC: 1, 6)
  - [x] `Domain/Entities/Case.cs` — `Id`, `OrganisationId`, `CrimeNumber`, `StNumber`, `BeneficiaryName`, optional `BeneficiaryAge`, `BeneficiaryContact`, `TypeOfOffence`, `OffenceClassification`, `Domicile`, `IsFirstTimeOffender`, `CurrentStage`, `VisitCount`, `CreatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`
  - [x] `Domain/Enums/CaseStage.cs` — six values matching spec (`ProcessInitiation` … `TerminationExclusion`); store as string in DB
  - [x] `Domain/Enums/OffenceClassification.cs` — `Petty`, `Serious`, `Heinous`
  - [x] `Domain/Enums/Domicile.cs` — `Urban`, `Rural`, `Coastal`, `Tribal`, `Slum`

- [x] **Persistence — EF configuration + migration** (AC: 6)
  - [x] `Infrastructure/Persistence/CaseConfiguration.cs` — table `cases`, snake_case, required string max lengths, unique indexes on `(organisation_id, crime_number)` and `(organisation_id, st_number)`
  - [x] Register `DbSet<Case>` on `AppDbContext`
  - [x] Add migration `AddCases` — **only** `cases` table
  - [x] Update `AppDbContextModelSnapshot`

- [x] **API — DTOs, service, controller** (AC: 1–5)
  - [x] `Models/Cases/CreateCaseRequest.cs`, `CaseDto.cs` (response)
  - [x] `Infrastructure/Cases/CaseService.cs` — validate request; **normalize** `crimeNumber` and `stNumber` with `Trim()` + `ToUpperInvariant()` before persist (consistent unique index); trim `beneficiaryName`; parse enum strings case-insensitively (`Petty`/`petty` → `Petty`); set `ProcessInitiation`, `VisitCount = 0`; resolve `organisationId` + `actorUserId` from `IHttpContextAccessor` claims (`AuthClaimTypes.OrganisationId`, `ClaimTypes.NameIdentifier` / `sub`)
  - [x] Register `CaseService` as scoped in `Program.cs` (or extension)
  - [x] Single `SaveChangesAsync`: insert `Case` + `AuditEvent` (`case.created`, metadata `{ caseId, crimeNumber, stNumber }`) — **same transaction** (mirror `UserSessionService` direct `db.AuditEvents.Add`, do not call `AuditService.RecordAsync` — it calls `SaveChangesAsync` separately)
  - [x] `Controllers/V1/CasesController.cs` — `POST` only; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`; return **`Created($"/api/v1/cases/{id}", dto)`** (201 — `ApiEnvelopeFilter` wraps 2xx `ObjectResult`); catch `DbUpdateException` when inner `PostgresException.SqlState == "23505"` → **409**
  - [x] Extend `AuditEventTypes` with `CaseCreated = "case.created"`

- [x] **Integration tests** (AC: 1, 2, 3, 4, 5, 7)
  - [x] `tests/api.integration/CaseCreateTests.cs` — `[Collection("AuthIntegration")]`, reuse `AuthWebApplicationFactory` + `AuthTestHelpers` + `RbacTestData.EnsureRoleUsersAsync`
  - [x] Tests: coordinator create 201 + DB row + audit; director create 201 (`AuthTestData.Email`); missing `beneficiaryName` 400; invalid `offenceClassification` 400; duplicate crime 409; duplicate ST 409; social worker 403; deactivated coordinator 403 deactivated message; unauthenticated 401
  - [x] `CaseTestData` static helper — `BuildValidRequest()` with unique crime/ST per call (`Guid` suffix); `BuildCoordinatorSessionAsync()` wrapping `AuthTestHelpers.LoginAndVerifyAsync` with `RbacTestData.CoordinatorEmail`

- [x] **OpenAPI + api-client** (AC: 7)
  - [x] `[ProducesResponseType]` on controller (201, 400, 401, 403, 409)
  - [x] `EXPORT_OPENAPI_PATH` export + regenerate `packages/api-client`; extend `SwaggerEndpointTests` with `/api/v1/cases`

- [x] **Documentation** (AC: 7)
  - [x] README — `POST /api/v1/cases`, coordinator-only, pilot required fields, 409 duplicate semantics

## Dev Notes

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — first domain aggregate beyond auth. Story 2.1 delivers **schema + create API only**. Story 2.2 adds stage transitions; 2.3 duplicate-check API; 2.4+ web/mobile create UI.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `AppDbContext.cs` | `Users`, `AuditEvents` | Add `Cases` |
| `AuditEventTypes.cs` | auth.* events | Add `case.created` |
| `Controllers/V1/` | `AuthController`, `MetaController`, `RbacProbeController` | Add `CasesController` |
| `packages/api-client` | auth routes | Add cases types |
| Web/mobile | auth shells only | **No UI changes** |

**Do not break:**
- **53** .NET tests (1 unit + 52 integration)
- **19** web + **17** mobile tests (unchanged — no client edits)
- Auth flows, RBAC policies, deactivated-user 403 behavior
- `Testing` environment skips DB in `Program.cs` — case tests use `AuthWebApplicationFactory` (Testcontainers), not `TestingWebApplicationFactory`

### API contract

**Create case** `POST /api/v1/cases`  
`Authorization: Bearer` required — `CoordinatorOrAbove`

```json
// Request (pilot minimum required fields)
{
  "crimeNumber": "CR-2024-001",
  "stNumber": "ST-887766",
  "beneficiaryName": "Ravi Kumar",
  "beneficiaryAge": 17,
  "beneficiaryContact": "+919876543210",
  "typeOfOffence": "Theft",
  "offenceClassification": "Petty",
  "domicile": "Urban",
  "isFirstTimeOffender": true
}
```

| Field | Required | Validation |
|-------|----------|------------|
| `crimeNumber` | Yes | Non-empty after trim; max 64 |
| `stNumber` | Yes | Non-empty after trim; max 64 |
| `beneficiaryName` | Yes | Non-empty after trim; max 256 |
| `beneficiaryAge` | No | If present: 0–120 |
| `beneficiaryContact` | No | Max 32 |
| `typeOfOffence` | Yes | Non-empty; max 128 (free text until Epic 9 legends) |
| `offenceClassification` | Yes | `Petty` \| `Serious` \| `Heinous` |
| `domicile` | Yes | `Urban` \| `Rural` \| `Coastal` \| `Tribal` \| `Slum` |
| `isFirstTimeOffender` | No | Default `true` if omitted |

```json
// Response 201
{
  "data": {
    "id": "uuid",
    "crimeNumber": "CR-2024-001",
    "stNumber": "ST-887766",
    "beneficiaryName": "Ravi Kumar",
    "currentStage": "ProcessInitiation",
    "visitCount": 0,
    "createdAtUtc": "2026-06-15T10:00:00Z"
  },
  "meta": { "requestId": "..." }
}
```

| Status | When |
|--------|------|
| 201 | Created |
| 400 | Validation failure |
| 401 | No/invalid JWT |
| 403 | Field worker role (`ForbiddenByRoleMessage`) **or** deactivated account (`DeactivatedMessage`) |
| 409 | Duplicate `crime_number` or `st_number` in organisation (Postgres `23505`) |

**Identifier normalization:** Persist `crimeNumber` and `stNumber` as **uppercase** after trim so `CR-001` and `cr-001` collide under the unique index (Story 2.3 duplicate-check will use the same rule).

**Tenant scoping:** `organisation_id` from JWT claim `AuthClaimTypes.OrganisationId` (`organisation_id`) — never from request body.

**Actor:** `created_by_user_id` from `ClaimTypes.NameIdentifier` or `sub` (mirror `AuthController.Me`).

**DTO binding:** Request DTO uses **string** properties for enums (no `JsonStringEnumConverter` in API today) — parse in `CaseService`.

### Security & RBAC

- `[Authorize(Policy = Policies.CoordinatorOrAbove)]` on `CasesController` — **Director** included (epic persona is Coordinator; policy matches supervisor roles per Story 1.8)
- Field workers (**403** `ForbiddenByRoleMessage`) — case create is coordinator/supervisor action per PRD personas
- Deactivated users (**403** `DeactivatedMessage`) — `ActiveUserAuthorizationHandler` + `InactiveUserAuthorizationMiddlewareResultHandler` (Story 1.8 order)
- No `[AllowAnonymous]` on mutation
- PII (`beneficiaryContact`) stored plain text in v1 — encryption deferred per architecture assumption

### Audit (mandatory)

- Event: `case.created`
- `organisation_id`, `actor_user_id` = creator, `subject_user_id` = null (case is not a user)
- Metadata: `{ "caseId": "...", "crimeNumber": "...", "stNumber": "..." }` — no beneficiary PII in audit metadata
- **Same `SaveChangesAsync`** as case insert

### Suggested file structure

```
apps/api/
├── Domain/
│   ├── Entities/Case.cs                    # NEW
│   └── Enums/CaseStage.cs                  # NEW (+ OffenceClassification, Domicile)
├── Infrastructure/
│   ├── Persistence/CaseConfiguration.cs    # NEW
│   └── Cases/CaseService.cs                # NEW
├── Controllers/V1/CasesController.cs       # NEW
├── Models/Cases/CaseDtos.cs                # NEW
├── Infrastructure/Audit/AuditEventTypes.cs # UPDATE
├── Infrastructure/Persistence/AppDbContext.cs # UPDATE
└── Migrations/*_AddCases.cs                # NEW

tests/api.integration/
└── CaseCreateTests.cs                      # NEW
```

### Previous epic intelligence (Epic 1 + retro)

- **Testcontainers:** Reuse `AuthWebApplicationFactory` — Postgres + Redis; **Development** env applies migrations via `DatabaseInitializer` on startup
- **RBAC test users:** `RbacTestData.EnsureRoleUsersAsync` seeds `coordinator@rbac.test`; Director seed is `AuthTestData.Email` (`director@pilot.example`)
- **OpenAPI regen:** `EXPORT_OPENAPI_PATH=packages/api-client/openapi-snapshot.json` + `API_OPENAPI_FILE` for `generate.mjs`
- **Review workflow:** adversarial review after implementation; document findings in story file
- **Epic 1 retro:** scope migrations per story (`cases` only); duplicate enumeration policy matters in 2.3 — 2.1 uses honest **409** on duplicate POST (not ambiguous 400)

### Architecture compliance

- REST `/api/v1/cases`, plural kebab-case, UUID ids [Source: `architecture.md` §5.3]
- Envelope `{ data, meta }` via existing `ApiEnvelopeFilter` [Source: `project-context.md`]
- snake_case columns, `organisation_id` on tenant tables [Source: `architecture.md` §5.1]
- UNIQUE crime/ST per organisation [Source: `architecture.md` §5.1, `case-and-lifecycle.md`]
- Soft-delete only via Termination/Exclusion stage — no `deleted_at` column in 2.1 [Source: `architecture.md` §5.1]

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 53 existing + new case tests pass |
| Web | `npm run test:web` | 19 unchanged |
| Mobile | `npm run test:mobile` | 17 unchanged |

**Integration test pattern:**

```csharp
[Collection("AuthIntegration")]
public class CaseCreateTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    // InitializeAsync: EnsureRoleUsersAsync + coordinator session
    // POST /api/v1/cases with Bearer token
}
```

Use unique crime/ST per test (e.g. `$"CR-{Guid.NewGuid():N}"[..12]`) to avoid cross-test pollution.

### Scope boundaries

| In scope (2.1) | Out of scope |
|----------------|--------------|
| `cases` table + POST create | `GET /cases/{id}`, search, export |
| Pilot required fields | Full PRD §5.2 optional field matrix |
| DB unique constraints + 409 | `POST /cases/check-duplicate` (2.3) |
| Coordinator/Director create | Web/mobile create forms (2.4, 2.9) |
| Audit `case.created` | Stage history table (2.2) |
| OpenAPI + api-client | Legend FK tables (Epic 9) |

### Definition of Done

- [x] `cases` migration applies cleanly on empty and existing dev DB
- [x] POST create works for Coordinator and Director
- [x] Validation, RBAC, duplicate, and audit covered by integration tests
- [x] 53+ .NET tests green; web/mobile unchanged
- [x] api-client regenerated; Swagger lists `/api/v1/cases`
- [x] README updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.1, FR-3]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.1 Data, §5.3 API]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — stages, core fields]
- [Source: `_bmad-output/project-context.md` — audit same transaction, 409 duplicates, RBAC]
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-06-15.md` — Epic 2 prep]
- [Source: `_bmad-output/implementation-artifacts/1-8-rbac-policies-on-protected-endpoints.md` — `Policies.CoordinatorOrAbove`]
- [Source: `_bmad-output/implementation-artifacts/1-3-users-schema-and-seed-admin-account.md` — EF migration patterns]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Implemented `cases` table migration (`AddCases`), `Case` entity with enums, `CaseService` (validation, identifier normalization, same-transaction audit), and `CasesController` (`POST /api/v1/cases`, CoordinatorOrAbove).
- Added 9 integration tests in `CaseCreateTests.cs` covering happy path, validation, duplicates (crime + ST), RBAC 403, deactivated 403, 401, and audit row.
- Regenerated OpenAPI snapshot and `@midi-kaval/api-client`; updated README and `UsersSchemaTests` for `cases` table.
- **Test results:** 66 .NET (1 unit + 65 integration), 19 web, 17 mobile — all passing.
- Code review (2026-06-15): null-body 400, enum `IsDefined` guard, deep Postgres 23505 unwrap, CaseWorker RBAC test, case-insensitive duplicate 409, transaction rollback assertions, `createdAtUtc` assertion.

### Code Review Findings (2026-06-15)

| Severity | Finding | Resolution |
|----------|---------|------------|
| MEDIUM | `Enum.TryParse` accepted numeric strings (`"3"`) for offence/domicile | Added `Enum.IsDefined` check after parse; test `NumericOffenceClassification_Returns400` |
| MEDIUM | Null `CreateCaseRequest` could NRE → 500 | Nullable controller param + service guard → 400; test `NullBody_Create_Returns400` |
| MEDIUM | Postgres `23505` only checked direct `InnerException` | `IsUniqueViolation` walks full exception chain |
| MEDIUM | AC4 CaseWorker 403 untested | Added `CaseWorker_Create_Returns403_ForbiddenByRoleMessage` |
| MEDIUM | Case-insensitive duplicate (`CR-001` vs `cr-001`) untested | Added `DuplicateCrimeNumber_CaseInsensitive_Returns409` |
| LOW | Duplicate 409 tests did not prove no orphan row/audit | `DuplicateCrimeNumber_Returns409` asserts case/audit counts unchanged after failed insert |
| LOW | Coordinator test omitted `createdAtUtc` (AC1) | Assert `CreatedAtUtc` recent in happy-path test |
| DEFER | Stale JWT role/org not re-read from DB on each request | Pre-existing Epic 1 auth design; revisit in security hardening |
| DEFER | No FK on `organisation_id` / `created_by_user_id` | Architecture — organisations table deferred |
| DISMISS | `Location` header points at future GET endpoint | Intentional 201 contract per story |
| DISMISS | `CaseDto` omits persisted optional fields | Matches story API contract for 201 response |

### File List

- apps/api/Domain/Entities/Case.cs (new)
- apps/api/Domain/Enums/CaseStage.cs (new)
- apps/api/Domain/Enums/OffenceClassification.cs (new)
- apps/api/Domain/Enums/Domicile.cs (new)
- apps/api/Infrastructure/Persistence/CaseConfiguration.cs (new)
- apps/api/Infrastructure/Cases/CaseService.cs (new)
- apps/api/Controllers/V1/CasesController.cs (new)
- apps/api/Models/Cases/CaseDtos.cs (new)
- apps/api/Migrations/20260615025857_AddCases.cs (new)
- apps/api/Migrations/20260615025857_AddCases.Designer.cs (new)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (modified)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (modified)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (modified)
- apps/api/Program.cs (modified)
- tests/api.integration/CaseCreateTests.cs (new)
- tests/api.integration/UsersSchemaTests.cs (modified)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (modified)
- README.md (modified)

### Change Log

- 2026-06-15: Story 2.1 created — case schema + create API (Epic 2 kickoff).
- 2026-06-15: Validation pass — 403 semantics, identifier normalization, Postgres 23505, DI/claims, test matrix.
- 2026-06-15: Implementation complete — cases migration, POST create API, 9 integration tests, OpenAPI/api-client regen.
- 2026-06-15: Code review patches applied — 66 .NET tests (13 case create), status done.
