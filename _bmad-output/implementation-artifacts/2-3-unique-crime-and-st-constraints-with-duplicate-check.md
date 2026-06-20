---
baseline_commit: NO_VCS
---

# Story 2.3: Unique Crime and ST Constraints with Duplicate Check

Status: done

<!-- Validated: 2026-06-15 — see 2-3-unique-crime-and-st-constraints-with-duplicate-check-validation-report.md -->

## Story

As a **Coordinator**,
I want duplicate Crime/ST blocked at save,
so that one beneficiary has one active record (FR-4, FR-5).

## Acceptance Criteria

1. **Given** I am authenticated as **Coordinator** or **Director** (`Policies.CoordinatorOrAbove`)  
   **When** I `POST /api/v1/cases/check-duplicate` with `crimeNumber` and/or `stNumber`  
   **Then** response is **200 OK** with envelope `{ data: CheckCaseDuplicateResultDto }`  
   **And** identifiers are normalized (trim + uppercase) before lookup — same rule as `POST /api/v1/cases` create  
   **And** **no** case row is created; **no** audit event is written (read-only probe)

2. **Given** a Case exists in my organisation with Crime X (and/or ST Y)  
   **When** I `POST /api/v1/cases/check-duplicate` with matching normalized Crime and/or ST  
   **Then** `data.hasMatch` is `true`  
   **And** `data.matches` contains one entry per distinct matching Case (dedupe by `caseId` when OR matches same row) with: `caseId`, `crimeNumber`, `stNumber`, `beneficiaryName`, `currentStage`, `matchedOn` (string: `CrimeNumber` | `StNumber` | `Both` — PascalCase values in JSON, same convention as `currentStage`)
   **And** `data.hasMatch` is `true` iff `matches.length > 0`  
   **And** if Crime matches Case A and ST matches Case B, **both** cases appear in `matches`

3. **Given** no Case in my organisation matches the normalized Crime or ST  
   **When** I `POST /api/v1/cases/check-duplicate`  
   **Then** `data.hasMatch` is `false` and `data.matches` is an empty array

4. **Given** both `crimeNumber` and `stNumber` are missing or whitespace-only (including only one field sent but whitespace)  
   **When** I `POST /api/v1/cases/check-duplicate`  
   **Then** response is **400** Problem Details (`At least one of crimeNumber or stNumber is required.`)

4b. **Given** a provided identifier exceeds 64 characters after trim  
   **When** I `POST /api/v1/cases/check-duplicate`  
   **Then** response is **400** Problem Details naming the field

5. **Given** a Case exists in **another** organisation with the same Crime/ST  
   **When** I check duplicate as Coordinator in my org  
   **Then** `data.hasMatch` is `false` (tenant-scoped lookup via JWT `organisation_id`)

6. **Given** I `POST /api/v1/cases` with a duplicate Crime or ST (existing behaviour from Story 2.1)  
   **When** the unique index `ix_cases_organisation_id_crime_number` or `ix_cases_organisation_id_st_number` fires  
   **Then** response remains **409 Conflict** — this story must **not** regress create-time enforcement

7. **Given** I am authenticated as **SocialWorker** or **CaseWorker**  
   **When** I `POST /api/v1/cases/check-duplicate`  
   **Then** response is **403** with `Policies.ForbiddenByRoleMessage`

7b. **Given** I am Coordinator/Director but **deactivated**  
   **When** I `POST /api/v1/cases/check-duplicate`  
   **Then** response is **403** with `AuthService.DeactivatedMessage`

8. **Given** I am unauthenticated  
   **When** I `POST /api/v1/cases/check-duplicate`  
   **Then** response is **401** Unauthorized

9. **Given** Story 2.2 test baseline (**84** .NET: 1 unit + 83 integration; **19** web; **17** mobile)  
   **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
   **Then** all existing tests pass  
   **And** new integration tests cover: crime match, ST match, both fields same case (`Both`), crime+ST different cases, no match, case-insensitive input, wrong org no match, 400 empty/whitespace-only body, 400 over-max-length identifier, null body 400, director can check, RBAC 403 social worker **and** case worker, deactivated 403, 401, no audit rows created on check, regression that `POST` create still returns 409 on duplicate  
   **And** OpenAPI documents `POST /api/v1/cases/check-duplicate`; `packages/api-client` regenerated

10. **Given** Stories 2.4–2.9 are not yet implemented  
    **When** this story ships  
    **Then** **no web/mobile duplicate match sheet UI**, no merge endpoint — API-only duplicate probe + verified DB constraints

## Tasks / Subtasks

- [x] **Verify DB constraints (no new migration)** (AC: 6)
  - [x] Confirm `CaseConfiguration` unique indexes on `(organisation_id, crime_number)` and `(organisation_id, st_number)` — **already exist** from Story 2.1
  - [x] Do **not** add a migration unless a gap is found; document verification in completion notes

- [x] **Domain — duplicate check service** (AC: 1–5)
  - [x] Extract `NormalizeIdentifier(string? value, string fieldName)` (trim + uppercase + max 64) — **shared** by `CreateAsync` and `CheckDuplicateAsync` to prevent normalization drift
  - [x] `Infrastructure/Cases/CaseService.cs` — add `CheckDuplicateAsync(CheckCaseDuplicateRequest)`:
    - Resolve `organisation_id` from JWT (same as create/transition)
    - Require at least one non-empty identifier after trim → 400
    - Build query: `OrganisationId == org` AND (crime predicate OR st predicate) — apply predicate **only** for non-empty normalized fields
    - Distinct matches by `caseId`; compute `matchedOn` per case from which identifier(s) matched
    - Map to `CheckCaseDuplicateResultDto`; set `hasMatch = matches.Count > 0`
    - **Read-only** — no `SaveChangesAsync`, no audit

- [x] **API — DTOs, controller** (AC: 1–8, 9)
  - [x] `Models/Cases/CaseDtos.cs` — `CheckCaseDuplicateRequest`, `CaseDuplicateMatchDto`, `CheckCaseDuplicateResultDto`
  - [x] `CasesController` — `[HttpPost("check-duplicate")]` on `CasesController` (static segment; no conflict with `PATCH {id:guid}/stage`); `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] `[ProducesResponseType]` (200, 400, 401, 403)
  - [x] Null body → 400 (mirror create/stage)

- [x] **Integration tests** (AC: 1–9)
  - [x] `tests/api.integration/CaseDuplicateCheckTests.cs` — `[Collection("AuthIntegration")]`, reuse `AuthWebApplicationFactory`, `CaseTestData`, `RbacTestData`
  - [x] Helper: seed case with known crime/ST via `CaseTestData.CreateCaseAsync` or direct insert
  - [x] Tests per AC matrix above; assert **no** new audit rows on check-duplicate calls

- [x] **OpenAPI + api-client** (AC: 9)
  - [x] Export + regenerate `packages/api-client`; extend `SwaggerEndpointTests` with `check-duplicate` path

- [x] **Documentation** (AC: 9)
  - [x] README — check-duplicate endpoint, response shape, normalization, distinction from 409 on create

## Dev Notes

### Epic context

**Epic 2: Case Registry, Search & Duplicate Prevention** — Stories 2.1–2.2 delivered create + stage transition. Story 2.3 adds **pre-save duplicate probe API** and confirms DB uniqueness. Story 2.4 wires web/mobile match sheet; 2.5 merge.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `cases` unique indexes | `ix_cases_organisation_id_crime_number`, `ix_cases_organisation_id_st_number` | **Verify only** — no schema change expected |
| `POST /api/v1/cases` | Returns **409** on duplicate via Postgres `23505` | **Must not regress** |
| `CasesController` | `POST`, `PATCH {id}/stage` | Add `POST check-duplicate` |
| `CaseService` | `CreateAsync`, `TransitionStageAsync` | Add `CheckDuplicateAsync` |
| Web/mobile | Auth shells only | **No UI changes** |

**Do not break:**
- **84** .NET tests — add duplicate-check tests on top
- **19** web + **17** mobile tests (unchanged)
- Story 2.1 identifier normalization (uppercase) and 409 semantics
- Story 2.2 stage transition behaviour

### API contract

**Check duplicate** `POST /api/v1/cases/check-duplicate`  
`Authorization: Bearer` required — `CoordinatorOrAbove`

```json
// Request — at least one field required
{
  "crimeNumber": "CR-2024-001",
  "stNumber": "ST-887766"
}
```

| Field | Required | Validation |
|-------|----------|------------|
| `crimeNumber` | One of crime/ST | If present: trim → uppercase for lookup; max 64 |
| `stNumber` | One of crime/ST | If present: trim → uppercase for lookup; max 64 |

```json
// Response 200 — match found
{
  "data": {
    "hasMatch": true,
    "matches": [
      {
        "caseId": "uuid",
        "crimeNumber": "CR-2024-001",
        "stNumber": "ST-887766",
        "beneficiaryName": "Ravi Kumar",
        "currentStage": "ProcessInitiation",
        "matchedOn": "Both"
      }
    ]
  },
  "meta": { "requestId": "..." }
}
```

```json
// Response 200 — no match
{
  "data": {
    "hasMatch": false,
    "matches": []
  },
  "meta": { "requestId": "..." }
}
```

| Status | When |
|--------|------|
| 200 | Check completed (match or no match) |
| 400 | Both identifiers missing/whitespace; null body; identifier over max length |
| 401 | No/invalid JWT |
| 403 | Field worker (`ForbiddenByRoleMessage`) or deactivated (`DeactivatedMessage`) |
| 409 | **Not used** on check-duplicate — reserved for forced `POST` create |

**`matchedOn` rules:**
- Crime only matches → `CrimeNumber`
- ST only matches → `StNumber`
- Both fields match same case → `Both`
- When only one field sent in request, `matchedOn` reflects which identifier(s) actually matched

**Tenant scoping:** Filter `cases` by JWT `organisation_id` only.

**TerminationExclusion cases:** Include in duplicate results — soft-closed cases still block duplicate identifiers in v1 (no hard delete).

### Security & RBAC

- Same policy as create: `CoordinatorOrAbove` (Director included)
- Field workers cannot check or create cases in v1 pilot
- Deactivated users: `DeactivatedMessage` before role denial (Story 1.8 order)
- Match DTO includes `beneficiaryName` — acceptable for coordinator create flow; POCSO discreet rules apply in later list APIs, not this supervisor-only endpoint

### Audit

- **No audit event** on `check-duplicate` — read-only query; architecture audit rule applies to mutations only
- Create path continues to write `case.created` on successful insert only

### Suggested file structure

```
apps/api/
├── Infrastructure/Cases/CaseService.cs           # UPDATE (CheckDuplicateAsync + shared normalize helper)
├── Controllers/V1/CasesController.cs             # UPDATE (POST check-duplicate)
├── Models/Cases/CaseDtos.cs                      # UPDATE (duplicate check DTOs)

tests/api.integration/
└── CaseDuplicateCheckTests.cs                    # NEW
```

### Previous story intelligence (2.1, 2.2 + reviews)

- **Identifier normalization:** `Trim().ToUpperInvariant()` on create — duplicate check **must use identical logic** (extract private helper if needed to prevent drift)
- **409 on create:** `IsUniqueViolation` walks full `InnerException` chain for Postgres `23505`
- **Enum parsing:** Strict name match (`parsed.ToString()` equals input case-insensitive) — not relevant to duplicate check but do not weaken `TryParseEnum`
- **Null body:** Controller + service guard → 400
- **Tests:** `CaseTestData.CreateCaseAsync()`, `BuildCoordinatorSessionAsync()`, unique suffix per test
- **OpenAPI:** `EXPORT_OPENAPI_PATH` + `API_OPENAPI_FILE` on Windows
- **Route ordering:** Place `check-duplicate` as static segment before `{id:guid}` routes to avoid ASP.NET binding `check-duplicate` as a guid

### Architecture compliance

- Dedicated `POST /cases/check-duplicate` — not client-composed from search [Source: `architecture.md` §5.3, `epics.md` Story 2.3]
- Envelope `{ data, meta }` via `ApiEnvelopeFilter`
- DB uniqueness + API pre-check (FR-4, FR-5) [Source: `architecture.md` §2]
- Case create requires network for duplicate check [Source: `project-context.md`]

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 84 existing + new duplicate-check tests pass |
| Web | `npm run test:web` | 19 unchanged |
| Mobile | `npm run test:mobile` | 17 unchanged |

```csharp
[Collection("AuthIntegration")]
public class CaseDuplicateCheckTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    // Seed case, POST check-duplicate, assert matches / no audit rows
}
```

### Scope boundaries

| In scope (2.3) | Out of scope |
|----------------|--------------|
| `POST /cases/check-duplicate` API | Web/mobile duplicate match sheet (2.4) |
| Verify DB unique constraints | `POST /cases/{id}/merge` (2.5) |
| Regression: create 409 | Search/filter (2.6) |
| OpenAPI + api-client | Changing who can create cases (field worker create deferred) |
| README | New migrations (unless constraint gap found) |

### Definition of Done

- [x] `check-duplicate` returns correct match summary for crime, ST, both, and no-match paths
- [x] Tenant isolation and normalization verified by tests
- [x] Create-time 409 duplicate behaviour unchanged
- [x] 84+ .NET tests green; web/mobile unchanged
- [x] api-client regenerated; Swagger lists check-duplicate
- [x] README updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.3, FR-4, FR-5]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §2 duplicate prevention, §5.3 Cases API]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — UJ-3 duplicate block]
- [Source: `_bmad-output/specs/spec-kaval-online/SPEC.md` — CAP-3 duplicate prevention]
- [Source: `_bmad-output/project-context.md` — 409 duplicate crime/ST, dedicated endpoints]
- [Source: `_bmad-output/implementation-artifacts/2-1-case-aggregate-schema-and-create-api.md` — create contract, 409, normalization]
- [Source: `_bmad-output/implementation-artifacts/2-2-six-stage-lifecycle-transitions.md` — test baseline, patterns]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Verified existing `CaseConfiguration` unique indexes on `(organisation_id, crime_number)` and `(organisation_id, st_number)` — no migration added.
- Extracted `NormalizeRequiredIdentifier` / `TryNormalizeOptionalIdentifier` shared by create and duplicate check.
- Implemented `CheckDuplicateAsync`, `POST /api/v1/cases/check-duplicate`, and duplicate-check DTOs.
- Added 19 integration tests in `CaseDuplicateCheckTests.cs`; extended `CaseTestData.CreateCaseAsync` overload.
- Regenerated OpenAPI snapshot and `@midi-kaval/api-client`; updated README and `SwaggerEndpointTests`.
- **Test results:** 103 .NET (1 unit + 102 integration), 19 web, 17 mobile — all passing.
- Code review (2026-06-15): CaseWorker 403 message, empty-body/termination/409 rollback tests, ST max-length.

### Code Review Findings (2026-06-15)

| Severity | Finding | Resolution |
|----------|---------|------------|
| MEDIUM | CaseWorker 403 did not assert `ForbiddenByRoleMessage` | Added assertion in `CaseWorker_Check_Returns403` |
| MEDIUM | Empty `{}` request body untested | Added `EmptyRequestObject_Returns400` |
| MEDIUM | `TerminationExclusion` inclusion untested (AC/story) | Added `TerminationExclusionCase_StillReturnedAsMatch` |
| LOW | 409 regression did not prove no orphan row/audit | `CreateDuplicate_StillReturns409` asserts counts unchanged |
| LOW | Over-max-length only tested for `crimeNumber` | Added `OverMaxLengthStNumber_Returns400` |
| DEFER | No audit event on read-only check | Intentional per story/architecture |
| DISMISS | No web/mobile match sheet | Intentional per AC10 (Story 2.4) |

### File List

- apps/api/Infrastructure/Cases/CaseService.cs (modified)
- apps/api/Controllers/V1/CasesController.cs (modified)
- apps/api/Models/Cases/CaseDtos.cs (modified)
- tests/api.integration/CaseDuplicateCheckTests.cs (new)
- tests/api.integration/CaseCreateTests.cs (modified)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (modified)
- README.md (modified)

### Change Log

- 2026-06-15: Story 2.3 created — duplicate check API + verified unique Crime/ST constraints.
- 2026-06-15: Validation pass — shared normalizer, dedupe/hasMatch rules, expanded test matrix, matchedOn JSON convention.
- 2026-06-15: Implementation complete — check-duplicate API, 16 integration tests, OpenAPI/api-client regen; status review.
- 2026-06-15: Code review patches applied — 103 .NET tests (19 duplicate check), status done.
