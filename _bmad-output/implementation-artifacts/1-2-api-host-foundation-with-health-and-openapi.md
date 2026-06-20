---
baseline_commit: NO_VCS
---

# Story 1.2: API Host Foundation with Health and OpenAPI

Status: done

<!-- Note: Validation is optional. Run bmad-create-story validate before dev-story. -->

## Story

As a **developer**,
I want an ASP.NET Core 8 API with versioning, Problem Details errors, and OpenAPI,
so that clients have a stable contract from day one.

## Acceptance Criteria

1. **Given** the API project exists (Story 1.1 scaffold)  
   **When** I request `GET /health`  
   **Then** the response is **200** with `{ "status": "healthy" }` (bootstrap health — **no** envelope; unchanged from Story 1.1)

2. **Given** the API host is running  
   **When** I request `GET /swagger/v1/swagger.json`  
   **Then** OpenAPI 3 document is returned describing at least one `/api/v1/*` route  
   **And** documented success responses use the camelCase JSON envelope `{ data, meta }` with `meta.requestId`

3. **Given** a versioned API route exists (e.g. `GET /api/v1/meta`)  
   **When** I call it successfully  
   **Then** the response body is `{ "data": { ... }, "meta": { "requestId": "<guid>" } }` with camelCase property names  
   **And** `Content-Type` is `application/json`

4. **Given** an unhandled exception or invalid route under `/api/v1`  
   **When** the API handles the error  
   **Then** the response is **RFC 7807 Problem Details** (`application/problem+json`) with appropriate status (404 for unknown route, 500 for unhandled exception in test)

5. **Given** the host runs in a **non-Development** environment  
   **When** I inspect middleware configuration  
   **Then** HTTPS redirection is enabled (NFR-1)  
   **And** Development environment allows HTTP for local dev (port 5049 per README)

6. **Given** OpenAPI is available  
   **When** I run the documented client generation script from repo root  
   **Then** TypeScript client code is emitted to `packages/api-client/`  
   **And** `packages/api-client/README.md` documents regeneration; generated files are not hand-edited

## Tasks / Subtasks

- [x] **Preserve Story 1.1 health endpoint** (AC: 1)
  - [x] Keep `GET /health` at root with `{ "status": "healthy" }` — do **not** wrap in envelope
  - [x] Ensure existing `HealthEndpointTests` still passes

- [x] **API versioning & sample v1 route** (AC: 2, 3)
  - [x] Add route prefix `/api/v1` for versioned controllers (e.g. `Controllers/V1/` folder)
  - [x] Add `GET /api/v1/meta` returning envelope with `data: { version, api: "v1" }` and `meta.requestId` (new `Guid` per request)
  - [x] Add XML doc comments on v1 controllers for OpenAPI descriptions

- [x] **Response envelope** (AC: 2, 3)
  - [x] Implement envelope for `/api/v1/*` success responses only (`Infrastructure/` filter or middleware)
  - [x] Types: `ApiResponse<T>` with `Data` + `Meta` (serialized camelCase)
  - [x] `Meta` includes `requestId` (UUID string); leave room for future `totalCount` (pagination Story 2+)
  - [x] `/health` and Swagger endpoints excluded from envelope wrapping

- [x] **RFC 7807 Problem Details** (AC: 4)
  - [x] Register `AddProblemDetails()` and global exception handler
  - [x] Map validation/business errors to architecture §6.2 status codes (scaffold handler — full domain errors in later stories)
  - [x] Integration test: unknown `/api/v1/unknown` → 404 Problem Details
  - [x] Integration test: forced exception endpoint or test hook → 500 Problem Details (dev/test only)

- [x] **OpenAPI / Swagger** (AC: 2)
  - [x] Re-add `Swashbuckle.AspNetCore` (and `Microsoft.AspNetCore.OpenApi` if needed for .NET 8)
  - [x] Configure Swagger UI at `/swagger` and JSON at `/swagger/v1/swagger.json`
  - [x] Document envelope schema in OpenAPI (schema filter or wrapper DTOs so `data`/`meta` appear in spec)
  - [x] camelCase JSON serialization via `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase`

- [x] **HTTPS in non-Development** (AC: 5)
  - [x] `UseHttpsRedirection()` + `UseHsts()` when `!IsDevelopment()`
  - [x] Document in README; do not break local `http://localhost:5049` dev workflow

- [x] **Generate `packages/api-client`** (AC: 6)
  - [x] Add `packages/api-client/package.json` scripts and `openapi-generator` or `openapi-typescript` toolchain
  - [x] Root `package.json` script: `generate:api-client` (fetch swagger from running API or build-time export)
  - [x] Output TypeScript client consumable by `apps/web` and `apps/mobile` workspaces
  - [x] Update `packages/api-client/README.md` with regen instructions
  - [x] Add smoke test or build step verifying client exports compile

- [x] **Tests & verification** (AC: 1–6)
  - [x] Extend `tests/api.integration/`: swagger JSON reachable, `/api/v1/meta` envelope shape, Problem Details 404
  - [x] `dotnet test Midi-Kaval.slnx` passes (existing + new tests)
  - [x] Run `npm run generate:api-client` after API start (document in Completion Notes)
  - [x] README: add Swagger URL and client generation commands

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Story 1.2 establishes the **API contract foundation** used by all later stories. Stories 1.3+ add database, auth, and RBAC on top of this host.

### Brownfield state — READ BEFORE CODING

Story 1.1 delivered a working scaffold. **UPDATE** these files; do not recreate the monorepo.

| File | Current state | This story changes |
|------|---------------|-------------------|
| `apps/api/Program.cs` | Minimal: `AddControllers`, `MapControllers`, `public partial class Program` | Add Swagger, Problem Details, HTTPS middleware, JSON options, exception handling |
| `apps/api/Controllers/HealthController.cs` | `GET /health` → `{ status: "healthy" }` | **Preserve as-is** (no envelope) |
| `apps/api/MidiKaval.Api.csproj` | net8.0, no NuGet packages (Swagger removed in 1.1 review) | Re-add Swashbuckle/OpenApi packages |
| `tests/api.integration/HealthEndpointTests.cs` | `WebApplicationFactory` tests `/health` 200 | Must still pass |
| `packages/api-client/` | Placeholder `package.json` + README | Generate real client |

### Scope boundaries (critical)

| In scope (1.2) | Out of scope — later stories |
|----------------|------------------------------|
| `/api/v1` prefix, envelope, Problem Details, Swagger, client gen | EF Core, migrations, `users` table (1.3) |
| Sample `GET /api/v1/meta` only | Auth endpoints `/api/v1/auth/*` (1.4) |
| HTTPS redirect non-dev | RBAC policies (1.8) |
| OpenAPI + TS client scaffold | Testcontainers PostgreSQL (1.3+ when DB required) |
| camelCase JSON serialization | Rate limiting, CORS (auth story) |

### Technical requirements

| Item | Requirement |
|------|-------------|
| .NET | 8.0 (`net8.0`) — build with SDK 10 + `global.json` roll-forward per Story 1.1 |
| OpenAPI | Swashbuckle.AspNetCore 6.x compatible with ASP.NET Core 8 |
| Envelope | `{ "data": {}, "meta": { "requestId": "..." } }` per architecture §5.3 |
| Errors | RFC 7807 `application/problem+json` per `project-context.md` |
| Health | Root `/health` **excluded** from envelope (infrastructure probe) |
| Client gen | Output to `packages/api-client/` — **never hand-edit** generated files |

### Architecture compliance

- REST prefix `/api/v1`, plural kebab-case resources for future endpoints [Source: architecture §5.3]
- Response envelope on versioned routes [Source: architecture §5.3, project-context §Framework Rules]
- OpenAPI drives `packages/api-client` [Source: architecture §5.3, project-context Technology Stack]
- Thin controllers; envelope/error infrastructure in `Infrastructure/` [Source: architecture §7, project-context]
- XML doc comments on controllers for OpenAPI [Source: project-context Code Quality]
- Integration tests in `tests/api.integration/` with `WebApplicationFactory` [Source: architecture §6.4]

### Suggested file structure (CREATE / UPDATE)

| Path | Action |
|------|--------|
| `apps/api/Infrastructure/ApiEnvelopeFilter.cs` (or middleware) | NEW — wrap `/api/v1` success responses |
| `apps/api/Infrastructure/GlobalExceptionHandler.cs` | NEW — Problem Details |
| `apps/api/Models/ApiResponse.cs`, `ApiMeta.cs` | NEW — envelope DTOs |
| `apps/api/Controllers/V1/MetaController.cs` | NEW — sample v1 route |
| `apps/api/Program.cs` | UPDATE |
| `apps/api/MidiKaval.Api.csproj` | UPDATE — packages |
| `tests/api.integration/MetaEndpointTests.cs` | NEW |
| `tests/api.integration/SwaggerEndpointTests.cs` | NEW |
| `tests/api.integration/ProblemDetailsTests.cs` | NEW |
| `packages/api-client/` | UPDATE — generated TS + package.json scripts |
| `package.json` (root) | UPDATE — `generate:api-client` script |
| `README.md` | UPDATE — Swagger + codegen docs |

### Library / framework requirements

**NuGet (apps/api):**
- `Swashbuckle.AspNetCore` 6.6.x (or latest 6.x for net8.0)
- `Microsoft.AspNetCore.OpenApi` 8.0.x (optional companion)

**npm (packages/api-client):**
- `@openapitools/openapi-generator-cli` **or** `openapi-typescript` + `openapi-fetch` — pick one, document in README
- Pin versions in `package-lock.json`

**Do not add:** EF Core, Identity, JWT, Hangfire, Testcontainers (not needed until 1.3).

### Testing requirements

| Test | Location | Minimum coverage |
|------|----------|------------------|
| Health regression | `HealthEndpointTests` | `/health` 200 unchanged |
| Meta envelope | `MetaEndpointTests` | `data` + `meta.requestId` present |
| Swagger | `SwaggerEndpointTests` | `/swagger/v1/swagger.json` 200, contains `/api/v1/meta` |
| Problem Details | `ProblemDetailsTests` | 404 on unknown v1 route returns `application/problem+json` |
| Client compile | `packages/api-client` | `npm run build` or `tsc --noEmit` after generation |

Full RBAC/audit/Testcontainers tests begin Story 1.3+ per architecture §6.4.

### Previous story intelligence (1.1)

- `GET /health` uses explicit `[Route("health")]` — keep it [Source: 1.1 review patch]
- `public partial class Program { }` required for `WebApplicationFactory` [Source: 1.1 implementation]
- Swagger packages were **removed** in 1.1 code review because unused — **re-add in 1.2** with full wiring
- Solution file is `Midi-Kaval.slnx` (not `.sln`); tests run via `dotnet test Midi-Kaval.slnx`
- `baseline_commit: NO_VCS` — git not on PATH; dev agent should set `NO_VCS` again if unchanged
- Integration test pattern established: `IClassFixture<WebApplicationFactory<Program>>` + `HttpClient`

### Anti-patterns (do NOT do in this story)

- Do not wrap `/health` in `{ data, meta }` envelope
- Do not hand-write HTTP client code in `packages/api-client/` — generate from OpenAPI
- Do not implement auth, RBAC, EF migrations, or business endpoints
- Do not change `H:\PayZenUI\WebAPI_Reference` [Source: project-context]
- Do not break existing web/mobile tests (no web/mobile code changes required unless client package wiring needed)

### Definition of Done

- [x] `GET /health` and `GET /swagger/v1/swagger.json` both return 200
- [x] `GET /api/v1/meta` returns envelope with `requestId`
- [x] Problem Details returned for at least one error scenario (404)
- [x] `dotnet test Midi-Kaval.slnx` passes (all tests)
- [x] `npm run generate:api-client` produces compilable TypeScript in `packages/api-client/`
- [x] README documents Swagger URL and client regeneration
- [x] Story file `File List` updated by dev agent on completion

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.2]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 API design, §6.2 Error codes, §6.4 Testing, §7 Structure]
- [Source: `_bmad-output/project-context.md` — C# rules, envelope, Problem Details, api-client]
- [Source: `_bmad-output/implementation-artifacts/1-1-monorepo-scaffold-and-local-dev-environment.md` — scaffold baseline]

## Dev Agent Record

### Agent Model Used

Composer (Cursor)

### Debug Log References

- 404 Problem Details initially returned `application/json` — fixed by serializing with explicit `application/problem+json` content type in middleware.
- `npm run generate:api-client` interrupted on first attempt; retried successfully with API running on :5049.
- Background API process can lock `MidiKaval.Api.exe` during `dotnet test` — stop process before test runs if needed.

### Completion Notes List

- ✅ `GET /health` unchanged; `HealthEndpointTests` passes.
- ✅ `/api/v1/meta` returns `{ data: { version, api }, meta: { requestId } }` via `ApiEnvelopeFilter`.
- ✅ Swagger at `/swagger/v1/swagger.json` documents `/api/v1/meta` with envelope schema.
- ✅ RFC 7807 for 404 (`/api/v1/*` unknown) and 500 (`/api/v1/diagnostics/throw` in Development).
- ✅ HTTPS redirection + HSTS when `!IsDevelopment()`.
- ✅ `openapi-typescript` generates `packages/api-client/src/generated/api.ts`; `npm run build -w @midi-kaval/api-client` passes.
- ✅ Code review complete (2026-06-13): clean review — all ACs verified, 6/6 tests pass.

### File List

- `apps/api/Program.cs`
- `apps/api/MidiKaval.Api.csproj`
- `apps/api/Models/ApiMeta.cs`
- `apps/api/Models/ApiResponse.cs`
- `apps/api/Models/MetaDto.cs`
- `apps/api/Infrastructure/RequestIdMiddleware.cs`
- `apps/api/Infrastructure/ApiEnvelopeFilter.cs`
- `apps/api/Infrastructure/ApiProblemDetailsMiddleware.cs`
- `apps/api/Infrastructure/GlobalExceptionHandler.cs`
- `apps/api/Controllers/V1/MetaController.cs`
- `apps/api/Controllers/V1/DiagnosticsController.cs`
- `tests/api.integration/MetaEndpointTests.cs`
- `tests/api.integration/SwaggerEndpointTests.cs`
- `tests/api.integration/ProblemDetailsTests.cs`
- `packages/api-client/package.json`
- `packages/api-client/tsconfig.json`
- `packages/api-client/scripts/generate.mjs`
- `packages/api-client/src/index.ts`
- `packages/api-client/src/generated/api.ts`
- `packages/api-client/README.md`
- `package.json`
- `README.md`

### Change Log

- 2026-06-13: API host foundation — versioning, envelope, Problem Details, Swagger, api-client generation.
- 2026-06-13: Code review approved — story marked done.
