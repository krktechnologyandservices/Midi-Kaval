---
baseline_commit: NO_VCS
---

# Story 2.2: Six-Stage Lifecycle Transitions

Status: done

<!-- Run validate-create-story before dev-story for quality pass -->

## Story

As a **Coordinator**,
I want to advance Cases through six lifecycle Stages,
so that progress is structured and auditable (FR-3).

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `PATCH /api/v1/cases/{id}/stage` with a valid forward transition (see rules below)  
   **Then** `cases.current_stage` updates to the target stage, `cases.updated_at_utc` is refreshed  
   **And** a row is inserted in `case_stages` recording `from_stage`, `to_stage`, `notes` (optional sub-step text), `created_by_user_id`, `created_at_utc`  
   **And** response is **200 OK** with envelope `{ data: CaseDto }` reflecting the new `currentStage`  
   **And** `audit_events` records `case.stage.changed` in the **same database transaction** as the case update and `case_stages` insert (metadata: `{ caseId, fromStage, toStage }` — no beneficiary PII)

2. **Given** a Case is at stage N (not `TerminationExclusion`)  
   **When** I request transition to stage N+1 (exactly one step forward in the ordered lifecycle)  
   **Then** transition succeeds  
   **When** I request the same `targetStage` as current, skip a stage, or move backward  
   **Then** response is **422 Unprocessable Entity** Problem Details (business rule — not 400 validation)

3. **Given** a Case is at `TerminationExclusion`  
   **When** I `PATCH /api/v1/cases/{id}/stage` with any target  
   **Then** response is **422** — terminal stage; no further transitions (soft-close path per architecture; no hard delete in v1)

4. **Given** required request fields are missing or invalid (`targetStage` empty/unknown, `notes` over max length)  
   **When** I `PATCH /api/v1/cases/{id}/stage`  
   **Then** response is **400** Problem Details  
   **And** no `case_stages` row, case stage change, or audit row is persisted

5. **Given** the Case `id` does not exist or belongs to another organisation  
   **When** I `PATCH /api/v1/cases/{id}/stage`  
   **Then** response is **404** Not Found

6. **Given** I am authenticated as **SocialWorker** or **CaseWorker**  
   **When** I `PATCH /api/v1/cases/{id}/stage`  
   **Then** response is **403** with `Policies.ForbiddenByRoleMessage`

6b. **Given** I am Coordinator/Director but **deactivated** (`IsActive = false`)  
   **When** I `PATCH /api/v1/cases/{id}/stage`  
   **Then** response is **403** with `AuthService.DeactivatedMessage` — not the RBAC role message

7. **Given** I am unauthenticated  
   **When** I `PATCH /api/v1/cases/{id}/stage`  
   **Then** response is **401** Unauthorized

8. **Given** EF Core migrations run on PostgreSQL  
   **When** migration from this story is applied  
   **Then** only the **`case_stages`** table is added (no `visits`, `notes`, `legend_*`, or other child tables)  
   **And** columns use snake_case; index on `(case_id, created_at_utc)` for timeline reads

9. **Given** Story 2.1 test baseline (**66** .NET: 1 unit + 65 integration; **19** web; **17** mobile)  
   **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
   **Then** all existing tests pass  
   **And** new integration tests cover: happy-path forward transition + DB + audit; full chain to `TerminationExclusion`; invalid skip/backward 422; terminal 422; 404 wrong org; RBAC 403 field worker; deactivated 403; 401  
   **And** OpenAPI documents `PATCH /api/v1/cases/{id}/stage`; `packages/api-client` regenerated

10. **Given** Stories 2.3–2.9 are not yet implemented  
    **When** this story ships  
    **Then** **no web/mobile stage UI**, no `GET /cases/{id}`, no sub-step field matrix from PRD §5.2 — API + `case_stages` history only; optional `notes` text is the pilot sub-step payload

## Tasks / Subtasks

- [x] **Domain — stage history entity** (AC: 1, 8)
  - [x] `Domain/Entities/CaseStageTransition.cs` — `Id`, `CaseId`, `OrganisationId`, `FromStage`, `ToStage`, `Notes` (nullable), `CreatedByUserId`, `CreatedAtUtc`
  - [x] Reuse `Domain/Enums/CaseStage.cs` (six values, unchanged order)

- [x] **Persistence — EF configuration + migration** (AC: 8)
  - [x] `Infrastructure/Persistence/CaseStageTransitionConfiguration.cs` — table `case_stages`, snake_case, `from_stage`/`to_stage` string enums, `notes` max 2000 nullable
  - [x] Register `DbSet<CaseStageTransition>` on `AppDbContext`
  - [x] Add migration `AddCaseStages` — **only** `case_stages` table
  - [x] Update `AppDbContextModelSnapshot`

- [x] **Domain rules — transition service** (AC: 1–5)
  - [x] `Infrastructure/Cases/CaseStageTransitionRules.cs` — ordered stages array; `TryGetNextStage(current)`; `IsValidForwardTransition(from, to)` (exactly +1 index, not from terminal)
  - [x] `Infrastructure/Cases/CaseService.cs` (extend) or `CaseStageService.cs` — `TransitionStageAsync(caseId, request)`:
    - Load case by `id` **and** JWT `organisation_id` (tenant scope) → 404 if missing
    - Parse `targetStage` string case-insensitively with `Enum.IsDefined` → 400 if invalid
    - Validate forward rule → throw `CaseBusinessRuleException` → 422
    - Trim `notes`; max 2000 → 400
    - Single `SaveChangesAsync`: update `Case.CurrentStage` + `UpdatedAtUtc`; insert `CaseStageTransition`; insert `AuditEvent` (`case.stage.changed`)
    - Resolve actor from `IHttpContextAccessor` (same as create)
  - [x] `CaseBusinessRuleException` for 422 (mirror validation vs business rule split per `project-context.md`)

- [x] **API — DTOs, controller** (AC: 1–7, 9)
  - [x] `Models/Cases/TransitionCaseStageRequest.cs` — `TargetStage` (string), `Notes` (optional string)
  - [x] Extend `CasesController` — `PATCH {id}/stage`; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`; return **200** with `CaseDto`
  - [x] `[ProducesResponseType]` (200, 400, 401, 403, 404, 422)
  - [x] Extend `AuditEventTypes` with `CaseStageChanged = "case.stage.changed"`

- [x] **Integration tests** (AC: 1–7, 9)
  - [x] `tests/api.integration/CaseStageTransitionTests.cs` — `[Collection("AuthIntegration")]`, reuse `AuthWebApplicationFactory`, `CaseTestData`, `RbacTestData`
  - [x] Helper: `CreateCaseAsync(session)` → case id at `ProcessInitiation`
  - [x] Tests: coordinator `ProcessInitiation` → `MaintainAndDevelopment` 200 + `case_stages` row + audit; director can transition; step through all six stages to `TerminationExclusion`; skip stage 422; backward 422; same stage 422; terminal case 422; unknown case 404; social worker + case worker 403; deactivated coordinator 403; unauthenticated 401; invalid `targetStage` 400

- [x] **OpenAPI + api-client** (AC: 9)
  - [x] Export + regenerate `packages/api-client`; extend `SwaggerEndpointTests` with `PATCH` path for cases stage

- [x] **Documentation** (AC: 9)
  - [x] README — stage transition endpoint, forward-only rules, 422 semantics, terminal stage

## Dev Notes

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Story 2.1 delivered `cases` + `POST` create. Story 2.2 adds **stage progression API + history table**. Story 2.3 duplicate-check; 2.4+ UI.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `cases` table | Exists; `current_stage` defaults `ProcessInitiation` on create | UPDATE on transition |
| `CasesController` | `POST` only | Add `PATCH {id}/stage` |
| `CaseService` | `CreateAsync` only | Add `TransitionStageAsync` (or sibling service) |
| `CaseDto` | id, identifiers, beneficiary summary, stage, visitCount, createdAtUtc | Unchanged shape (stage updates in response) |
| `AuditEventTypes` | `case.created` | Add `case.stage.changed` |
| Web/mobile | Auth shells only | **No UI changes** |

**Do not break:**
- **66** .NET tests (1 unit + 65 integration) — add transition tests on top
- **19** web + **17** mobile tests (unchanged)
- Story 2.1 create behavior, RBAC, audit same-transaction pattern, enum string parsing with `Enum.IsDefined`

### Stage order (canonical — must match `CaseStage` enum progression)

| Order | Stage | Enum value |
|-------|-------|------------|
| 1 | Process Initiation | `ProcessInitiation` |
| 2 | Maintain and Development | `MaintainAndDevelopment` |
| 3 | Inter-sectoral Approach | `InterSectoralApproach` |
| 4 | Rehabilitation | `Rehabilitation` |
| 5 | Reintegration | `Reintegration` |
| 6 | Termination/Exclusion | `TerminationExclusion` |

**Transition rules (pilot):**
- Forward **exactly one** stage per request
- No backward transitions
- No skipping
- `TerminationExclusion` is **terminal** (soft-close — architecture §5.1; no `deleted_at`, no hard delete)
- Cases created in 2.1 start at `ProcessInitiation` with **no** `case_stages` row until first successful PATCH (create audit remains `case.created` only)

### API contract

**Transition stage** `PATCH /api/v1/cases/{id}/stage`  
`Authorization: Bearer` required — `CoordinatorOrAbove`

```json
// Request
{
  "targetStage": "MaintainAndDevelopment",
  "notes": "ICP draft completed; moving to maintain phase."
}
```

| Field | Required | Validation |
|-------|----------|------------|
| `targetStage` | Yes | Must be valid `CaseStage` name; must be exactly next stage in order |
| `notes` | No | Max 2000 chars; pilot sub-step summary (full PRD sub-step matrix deferred) |

```json
// Response 200
{
  "data": {
    "id": "uuid",
    "crimeNumber": "CR-...",
    "stNumber": "ST-...",
    "beneficiaryName": "...",
    "currentStage": "MaintainAndDevelopment",
    "visitCount": 0,
    "createdAtUtc": "..."
  },
  "meta": { "requestId": "..." }
}
```

| Status | When |
|--------|------|
| 200 | Stage advanced |
| 400 | Validation (`targetStage` missing/invalid enum, `notes` too long) |
| 401 | No/invalid JWT |
| 403 | Field worker (`ForbiddenByRoleMessage`) or deactivated (`DeactivatedMessage`) |
| 404 | Case not found in caller's organisation |
| 422 | Business rule (same stage, skip, backward, terminal case) |

**Tenant scoping:** Load case with `organisation_id` from JWT — never from request body.

**DTO binding:** `targetStage` as **string** on request (no `JsonStringEnumConverter`); parse in service like Story 2.1.

### Security & RBAC

- `[Authorize(Policy = Policies.CoordinatorOrAbove)]` — Director included
- Field workers cannot transition stages (coordinator/supervisor action)
- Deactivated users: `DeactivatedMessage` before role denial (Story 1.8 order)

### Audit (mandatory)

- Event: `case.stage.changed`
- `organisation_id`, `actor_user_id` = transition actor, `subject_user_id` = null
- Metadata: `{ "caseId", "fromStage", "toStage" }` — no notes/PII in audit metadata (notes live in `case_stages` row)
- **Same `SaveChangesAsync`** as case update + `case_stages` insert — do **not** call `AuditService.RecordAsync`

### Suggested file structure

```
apps/api/
├── Domain/Entities/CaseStageTransition.cs          # NEW
├── Infrastructure/
│   ├── Persistence/CaseStageTransitionConfiguration.cs  # NEW
│   └── Cases/
│       ├── CaseStageTransitionRules.cs             # NEW
│       └── CaseService.cs                          # UPDATE (TransitionStageAsync)
├── Controllers/V1/CasesController.cs               # UPDATE
├── Models/Cases/CaseDtos.cs                        # UPDATE (TransitionCaseStageRequest)
├── Infrastructure/Audit/AuditEventTypes.cs         # UPDATE
└── Migrations/*_AddCaseStages.cs                   # NEW

tests/api.integration/
└── CaseStageTransitionTests.cs                     # NEW
```

### Previous story intelligence (2.1 + review)

- **Audit:** Direct `db.AuditEvents.Add` + single `SaveChangesAsync` — never `AuditService.RecordAsync` for coupled writes
- **Enums:** String request properties + `Enum.TryParse` + `Enum.IsDefined`
- **Postgres 23505:** Walk full `InnerException` chain if needed (not applicable here unless new unique indexes)
- **Null body:** Return 400 on null request DTO
- **Tests:** `CaseTestData.BuildValidRequest()` + `BuildCoordinatorSessionAsync()`; unique crime/ST per test
- **OpenAPI:** `EXPORT_OPENAPI_PATH` + `API_OPENAPI_FILE` on Windows
- **UsersSchemaTests:** Update expected table list to include `case_stages` after migration

### Architecture compliance

- REST `PATCH /api/v1/cases/{id}/stage` [Source: `epics.md` Story 2.2, `architecture.md` §5.3]
- Envelope `{ data, meta }` via `ApiEnvelopeFilter`
- snake_case columns, `organisation_id` on tenant tables
- Soft-close via `TerminationExclusion` only — no hard delete [Source: `architecture.md` §5.1]
- **422** for business rules [Source: `project-context.md`]

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 66 existing + new transition tests pass |
| Web | `npm run test:web` | 19 unchanged |
| Mobile | `npm run test:mobile` | 17 unchanged |

```csharp
[Collection("AuthIntegration")]
public class CaseStageTransitionTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    // Create case via POST, then PATCH stage
}
```

### Scope boundaries

| In scope (2.2) | Out of scope |
|----------------|--------------|
| `case_stages` history + PATCH transition | Full PRD §5.2 sub-step field matrix |
| Forward-only sequential rules | Backward/skip transitions |
| Optional `notes` text on transition | Web/mobile stage UI (2.9) |
| `case.stage.changed` audit | `GET /cases/{id}`, search, duplicate check (2.3) |
| OpenAPI + api-client | Auto-create `case_stages` row on POST create |

### Definition of Done

- [x] `case_stages` migration applies cleanly
- [x] Forward transitions work for Coordinator and Director through full lifecycle
- [x] 422/400/404/RBAC/audit covered by integration tests
- [x] 66+ .NET tests green; web/mobile unchanged
- [x] api-client regenerated; Swagger lists stage PATCH
- [x] README updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.2, FR-3]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.1 Data, §5.3 API, 422 errors]
- [Source: `_bmad-output/specs/spec-kaval-online/case-and-lifecycle.md` — six stages]
- [Source: `_bmad-output/project-context.md` — audit same transaction, 422 business rules]
- [Source: `_bmad-output/implementation-artifacts/2-1-case-aggregate-schema-and-create-api.md` — patterns, test baseline]
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-06-15.md` — scoped migrations, Testcontainers]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Implemented `case_stages` migration (`AddCaseStages`), `CaseStageTransition` entity, `CaseStageTransitionRules` (forward-only +1), and `CaseService.TransitionStageAsync` with same-transaction audit (`case.stage.changed`).
- Added `PATCH /api/v1/cases/{id}/stage` on `CasesController` (CoordinatorOrAbove); `TransitionCaseStageRequest` DTO; `CaseBusinessRuleException` → 422 for business-rule violations.
- Added 13 integration tests in `CaseStageTransitionTests.cs` covering happy path, full lifecycle chain, skip/backward/same/terminal 422, 404, RBAC 403, deactivated 403, 401, invalid target 400, and audit row.
- Extracted `CaseTestData.CreateCaseAsync()` helper for reuse across case integration tests.
- Regenerated OpenAPI snapshot and `@midi-kaval/api-client`; updated README, `UsersSchemaTests`, and `SwaggerEndpointTests`.
- **Test results:** 84 .NET (1 unit + 83 integration), 19 web, 17 mobile — all passing.
- Code review (2026-06-15): strict enum name parsing, null-body/empty/numeric targetStage/notes-too-long tests, cross-org 404, 422/400 no-persist assertions, `UpdatedAtUtc` assertion.

### Code Review Findings (2026-06-15)

| Severity | Finding | Resolution |
|----------|---------|------------|
| MEDIUM | Numeric `targetStage` (e.g. `"1"`) accepted via `Enum.TryParse` | `TryParseEnum` now requires parsed name to match input (case-insensitive); test `NumericTargetStage_Returns400` |
| MEDIUM | Null PATCH body untested (NRE risk) | Controller/service already guard; added `NullBody_Transition_Returns400` |
| MEDIUM | AC5 cross-organisation 404 untested | Added `CrossOrganisationCase_Returns404` |
| MEDIUM | AC4 notes-too-long and no-persist on 400 untested | Added `NotesTooLong_Returns400_AndDoesNotPersist` |
| LOW | 422 skip-stage did not prove no DB mutation | `SkipStage_Returns422` asserts stage unchanged and zero history rows |
| LOW | AC1 `updated_at_utc` refresh untested | Happy-path asserts `UpdatedAtUtc >= CreatedAtUtc` |
| LOW | Empty `targetStage` untested | Added `EmptyTargetStage_Returns400` |
| LOW | Invalid target 400 did not prove no history row | `InvalidTargetStage_Returns400` asserts zero `case_stages` rows |
| DEFER | No FK from `case_stages.case_id` to `cases` | Architecture — matches `cases` table pattern |
| DEFER | Stale JWT role/org not re-read from DB | Pre-existing Epic 1 auth design |
| DISMISS | No web/mobile UI for stages | Intentional per AC10 |

### File List

- apps/api/Domain/Entities/CaseStageTransition.cs (new)
- apps/api/Infrastructure/Persistence/CaseStageTransitionConfiguration.cs (new)
- apps/api/Infrastructure/Cases/CaseStageTransitionRules.cs (new)
- apps/api/Migrations/20260615060716_AddCaseStages.cs (new)
- apps/api/Migrations/20260615060716_AddCaseStages.Designer.cs (new)
- apps/api/Infrastructure/Cases/CaseService.cs (modified)
- apps/api/Controllers/V1/CasesController.cs (modified)
- apps/api/Models/Cases/CaseDtos.cs (modified)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (modified)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (modified)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (modified)
- tests/api.integration/CaseStageTransitionTests.cs (new)
- tests/api.integration/CaseCreateTests.cs (modified)
- tests/api.integration/UsersSchemaTests.cs (modified)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (modified)
- README.md (modified)

### Change Log

- 2026-06-15: Story 2.2 created — six-stage lifecycle transitions API + `case_stages` history.
- 2026-06-15: Implementation complete — PATCH stage transition API, `case_stages` migration, 13 integration tests, OpenAPI/api-client regen; status review.
- 2026-06-15: Code review patches applied — 84 .NET tests (18 stage transition), status done.
