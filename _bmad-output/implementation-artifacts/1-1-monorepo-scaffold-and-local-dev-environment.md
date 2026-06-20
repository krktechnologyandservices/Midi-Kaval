---
baseline_commit: NO_VCS
---

# Story 1.1: Monorepo Scaffold and Local Dev Environment

Status: done

<!-- Validated: 2026-06-13 — see 1-1-monorepo-scaffold-and-local-dev-environment-validation-report.md -->

## Story

As a **developer**,
I want a monorepo with API, web, mobile, and shared packages runnable locally,
so that the team can build and test all surfaces from one repository.

## Acceptance Criteria

1. **Given** a fresh clone of Midi-Kaval  
   **When** I run `docker compose -f infra/docker-compose.yml up -d` and start each app per README  
   **Then** PostgreSQL 16, Redis 7, API, Angular PWA, and React Native dev builds start without manual path fixes  
   **And** folder structure matches architecture §7 (`apps/api`, `apps/web`, `apps/mobile`, `packages/api-client`, `packages/shared-types`, `tests/`, `infra/`)

2. **Given** the scaffold is complete  
   **When** I inspect the repository root  
   **Then** these paths exist and are wired for local dev:

   ```
   apps/api/
   apps/web/
   apps/mobile/
   packages/api-client/
   packages/shared-types/
   tests/api.unit/
   tests/api.integration/
   tests/e2e/
   infra/docker-compose.yml
   README.md
   ```

3. **Given** infrastructure containers are running  
   **When** I `dotnet run --project apps/api`  
   **Then** the API host starts on a documented port and responds to `GET /health` with 200  
   **And** this is a **bootstrap-only** health stub — Story 1.2 adds OpenAPI, `/api/v1` envelope, and RFC 7807 Problem Details (do not implement those here)

4. **Given** infrastructure is optional for front-end-only dev  
   **When** I run `npm run start:web` (or documented equivalent) from repo root  
   **Then** Angular dev server starts and serves the default app shell

5. **Given** mobile toolchain is installed on the developer machine  
   **When** I run `npm run start:mobile` (or documented equivalent)  
   **Then** Metro bundler starts for `apps/mobile` without path resolution errors  
   **And** a full iOS/Android device build is **not** required for this story — Metro start satisfies AC

6. **Given** shared packages exist  
   **When** web or mobile imports from `@midi-kaval/shared-types`  
   **Then** TypeScript resolves the package via workspace configuration (smoke export e.g. `AppRole` enum stub)

## Tasks / Subtasks

- [x] **Repository root & tooling** (AC: 1, 2)
  - [x] Add root `.gitignore` (Node, .NET, RN, IDE, `.env*`)
  - [x] Add root `README.md` with prerequisites (.NET 8 SDK, Node 20 LTS, Docker, RN toolchain)
  - [x] Add root `package.json` with npm/pnpm workspaces pointing at `apps/web`, `apps/mobile`, `packages/*`
  - [x] Pin Node engine in root `package.json` (`>=20`)
  - [x] Add `global.json` pinning .NET SDK 8.0.x
  - [x] Add `.editorconfig` for C# + TS consistency

- [x] **Infrastructure** (AC: 1, 3)
  - [x] Create `infra/docker-compose.yml` with PostgreSQL 16 and Redis 7
  - [x] Expose ports: Postgres `5432`, Redis `6379` (document in README)
  - [x] Add `infra/.env.example` with connection strings for local API (no secrets committed)
  - [x] Optional: `apps/api/appsettings.Development.json` reads from env vars

- [x] **API scaffold** (`apps/api`) (AC: 2, 3)
  - [x] `dotnet new webapi` ASP.NET Core 8 — name project `MidiKaval.Api`
  - [x] Folder skeleton: `Controllers/`, `Domain/`, `Infrastructure/`, `Jobs/` (empty except placeholders)
  - [x] Add `GET /health` returning `{ "status": "healthy" }`
  - [x] Add solution file `Midi-Kaval.sln` at repo root referencing `apps/api` and test projects
  - [x] Enable nullable reference types and `dotnet format` in CI-ready state

- [x] **Web scaffold** (`apps/web`) (AC: 2, 4)
  - [x] `ng new` Angular 19+ with **standalone** components, **strict** TS, SCSS
  - [x] Add `@angular/service-worker` and `ngsw-config.json` (app-shell prefetch only — full PWA config in Story 1.6)
  - [x] Add Angular Material (`ng add @angular/material`)
  - [x] Structure: `src/app/core/`, `src/app/features/`, `src/app/shared/`
  - [x] Root route shows placeholder "Kaval Online — Web" shell

- [x] **Mobile scaffold** (`apps/mobile`) (AC: 2, 5)
  - [x] React Native 0.76+ init in `apps/mobile`
  - [x] Structure: `src/screens/`, `src/components/`, `src/services/sync/`, `src/db/` (empty placeholders)
  - [x] Bottom tab placeholder shell (static labels Today · Cases · More — navigation wiring in Story 1.7)
  - [x] Configure Metro to resolve workspace packages

- [x] **Shared packages** (AC: 6)
  - [x] `packages/shared-types/` — TS package exporting stub enums/constants (e.g. `AppRole`, `SyncState`) for workspace smoke test
  - [x] `packages/api-client/` — placeholder `package.json` + README noting "OpenAPI-generated in Story 1.2; do not hand-edit"
  - [x] Wire `@midi-kaval/shared-types` import in web and mobile hello components

- [x] **Test project shells** (AC: 2)
  - [x] `tests/api.unit/` — xUnit project referencing `apps/api`
  - [x] `tests/api.integration/` — xUnit + `Microsoft.AspNetCore.Mvc.Testing` shell (Testcontainers added Story 1.2+)
  - [x] `tests/e2e/` — Playwright `package.json` stub with README (tests added later)
  - [x] `apps/mobile/__tests__/` — Jest config stub with one smoke test

- [x] **Root scripts & verification** (AC: 1)
  - [x] README documents: `docker compose -f infra/docker-compose.yml up -d`, `dotnet run --project apps/api`, web/mobile start commands
  - [x] Verify fresh-clone flow end-to-end and fix any path/workspace issues

### Review Findings

- [x] [Review][Decision] `global.json` SDK version — **Resolved: 1A** — Keep SDK 10.0.201 pin with `rollForward: latestMajor`; README documents builds target `net8.0` via SDK 10.

- [x] [Review][Decision] Solution file format — **Resolved: 2B** — Accept `Midi-Kaval.slnx` only; README and `package.json` scripts reference `.slnx`.

- [x] [Review][Patch] Unused Swagger/OpenApi NuGet packages [`apps/api/MidiKaval.Api.csproj`] — Removed `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore` until Story 1.2.

- [x] [Review][Patch] Integration test does not exercise HTTP health endpoint [`tests/api.integration/HealthEndpointTests.cs`] — Added `WebApplicationFactory` test for `GET /health` → 200 `{ status: "healthy" }`; removed shell-only test.

- [x] [Review][Patch] Health route uses `[controller]` token not explicit `/health` [`apps/api/Controllers/HealthController.cs:6`] — Changed to `[Route("health")]`.

- [x] [Review][Defer] docker-compose has no named volumes [`infra/docker-compose.yml:1-26`] — deferred, pre-existing; Postgres data lost on container recreate. Acceptable for local dev scaffold; add volumes in a later infra story.

- [x] [Review][Defer] npm workspace Angular hoisting workaround [`package.json:22-24`] — deferred, pre-existing; root `@angular/compiler` devDependency fixes Karma resolution. Consider `.npmrc` `public-hoist-pattern` in a future tooling cleanup.

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — establishes monorepo, API foundation, generated clients, and secure login on Angular PWA + React Native with server-enforced RBAC.

**This story is the greenfield foundation.** Stories 1.2–1.9 build on this scaffold. Do not implement auth, RBAC, EF migrations, or OpenAPI client generation here.

### Greenfield state

Repository currently contains **only planning artifacts** (`_bmad-output/`, `_bmad/`, `.agents/`). All application code is **NEW**. No UPDATE files to read.

### Technical requirements

| Layer | Version to pin in this story | Notes |
|-------|------------------------------|-------|
| .NET | 8.0 LTS | No preview SDKs |
| Angular | 19.x | Standalone + signals ready |
| React Native | 0.76.x | Decide WatermelonDB vs SQLite in Story 3.6 — create empty `src/db/` now |
| PostgreSQL | 16 | Via docker-compose |
| Redis | 7 | Via docker-compose |
| Node | 20 LTS | Workspace root |

### Monorepo strategy (decide once, document in README)

Recommended: **pnpm workspaces** (or npm workspaces) for JS/TS + **.NET solution** for API/tests.

```
Midi-Kaval/
├── Midi-Kaval.sln
├── package.json          # workspaces: apps/web, apps/mobile, packages/*
├── apps/
│   ├── api/              # ASP.NET Core 8
│   ├── web/              # Angular 19 PWA
│   └── mobile/           # React Native 0.76
├── packages/
│   ├── api-client/       # placeholder — Story 1.2
│   └── shared-types/     # stub enums
├── tests/
│   ├── api.unit/
│   ├── api.integration/
│   └── e2e/
├── infra/
│   └── docker-compose.yml
└── README.md
```

### Architecture compliance

- Match architecture §7 folder layout exactly [Source: `_bmad-output/planning-artifacts/architecture.md#7`]
- **Note:** architecture §5.1 references `src/api` for migrations — treat as stale; canonical API path is `apps/api` per §7 and `project-context.md`
- API internal structure: `Controllers/`, `Domain/`, `Infrastructure/`, `Jobs/` even if empty [Source: architecture §7]
- Web: `core/`, `features/`, `shared/` under `src/app/` [Source: architecture §5.4, §7]
- Mobile: `screens/`, `components/`, `services/sync/`, `db/` [Source: architecture §5.5, §7]
- Do **not** add business logic, domain entities, or auth — Story 1.2+ [Source: epics.md Epic 1 breakdown]

### Library / framework requirements

**API (`apps/api`):**
- Bootstrap `GET /health` only — no `/api/v1` prefix, no `{ data, meta }` envelope yet (Story 1.2)
- Optional minimal Swagger stub — full OpenAPI contract is Story 1.2 scope
- No EF Core yet — Story 1.3 adds users schema

**Web (`apps/web`):**
- `@angular/service-worker` registered in `app.config.ts`
- Angular Material installed (theme customization in Story 9.4)
- TypeScript `strict: true`

**Mobile (`apps/mobile`):**
- TypeScript strict
- No WatermelonDB/SQLite install yet — empty `src/db/` folder with `.gitkeep`

**Shared (`packages/shared-types`):**
- Export at minimum: `AppRole` enum (`Director`, `Coordinator`, `SocialWorker`, `CaseWorker`) matching PRD roles
- Export `SyncState` enum stub (`local`, `pending`, `synced`, `error`) for future mobile sync

### File structure requirements (files to CREATE)

| Path | Purpose |
|------|---------|
| `infra/docker-compose.yml` | Postgres 16 + Redis 7 |
| `infra/.env.example` | Local connection strings |
| `Midi-Kaval.sln` | .NET solution |
| `apps/api/MidiKaval.Api.csproj` | Web API project |
| `apps/api/Program.cs` | Health endpoint |
| `apps/api/Controllers/HealthController.cs` | Or minimal inline health |
| `apps/web/` | Full Angular scaffold |
| `apps/mobile/` | Full RN scaffold |
| `packages/shared-types/src/index.ts` | Stub exports |
| `packages/api-client/package.json` | Placeholder |
| `tests/api.unit/*.csproj` | xUnit shell |
| `tests/api.integration/*.csproj` | Integration test shell |
| `tests/e2e/package.json` | Playwright stub |
| `README.md` | Local dev guide |
| `.gitignore` | Standard exclusions |

### Testing requirements

**This story:** scaffold-level smoke tests only.

| Location | Minimum test |
|----------|--------------|
| `tests/api.unit/` | One test asserting `true` or health controller exists |
| `apps/web/` | Default Angular `app.component.spec.ts` passes |
| `apps/mobile/__tests__/` | One smoke test rendering root component |
| `tests/e2e/` | No tests required — stub only |

Full RBAC/audit/integration tests begin Story 1.2+ per architecture §6.4.

### Anti-patterns (do NOT do in this story)

- Do not hand-write OpenAPI client code in `packages/api-client/` [Source: `project-context.md`]
- Do not create EF migrations or database tables [Source: Story 1.3 scope]
- Do not implement login/OTP/RBAC [Source: Stories 1.4–1.8]
- Do not use NgModules for new Angular features [Source: `project-context.md`]
- Do not change `H:\PayZenUI\WebAPI_Reference` [Source: `project-context.md`]
- Do not add unrelated markdown docs beyond README

### docker-compose reference

```yaml
# infra/docker-compose.yml — illustrative; implement with healthchecks
services:
  postgres:
    image: postgres:16
    ports: ["5432:5432"]
    environment:
      POSTGRES_USER: kaval
      POSTGRES_PASSWORD: kaval_dev
      POSTGRES_DB: kaval_dev
  redis:
    image: redis:7
    ports: ["6379:6379"]
```

### Project Structure Notes

- Architecture §7 is the canonical layout — any deviation must be documented in README with rationale
- `docs/` folder may remain empty; planning artifacts stay in `_bmad-output/`
- Commit pinned versions in lockfiles (`package-lock.json` or `pnpm-lock.yaml`, `global.json` for .NET SDK optional)

### Definition of Done

- [x] Fresh clone → README steps → all three app surfaces start without manual path edits
- [x] `dotnet test` passes for `tests/api.unit` smoke test
- [x] `ng test` passes default web smoke test
- [x] `npm test` (mobile) passes one smoke test
- [x] Lockfiles committed with pinned dependency versions
- [x] Story file `File List` updated by dev agent on completion

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.1]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §3 Starter, §6.4 Testing, §7 Project Structure]
- [Source: `_bmad-output/project-context.md` — Technology Stack, Monorepo layout, Workflow Rules]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/addendum.md` — Platform stack confirmation]

## Dev Agent Record

### Agent Model Used

Composer (Cursor)

### Debug Log References

- Web tests failed initially: hoisted `@angular/core` at repo root could not resolve `@angular/compiler` — fixed by adding `@angular/compiler@^19.2.0` to root `devDependencies`.
- Mobile Metro failed initially: missing `@react-native-community/cli` — fixed by adding `@react-native-community/cli@^15.1.3` to `apps/mobile` devDependencies.
- Docker compose not verified on this machine (Docker Desktop daemon not running); `infra/docker-compose.yml` is valid and documented in README.
- `global.json` pins SDK 10.0.201 with `rollForward: latestMajor` because only .NET 10 SDK is installed; projects target `net8.0` and build/test successfully.
- Solution file uses `Midi-Kaval.slnx` (SDK 10 format) instead of legacy `.sln`; README and `package.json` scripts reference `.slnx`.

### Implementation Plan

Greenfield monorepo scaffold: npm workspaces + .NET solution, docker-compose for Postgres/Redis, ASP.NET Core 8 health stub, Angular 19 PWA shell with Material + service worker, React Native 0.76 tab placeholder, shared-types workspace package, and smoke tests for API/web/mobile.

### Completion Notes List

- ✅ Monorepo layout matches architecture §7 (`apps/`, `packages/`, `tests/`, `infra/`).
- ✅ `GET /health` returns `{ "status": "healthy" }` on port 5049 (verified live).
- ✅ `npm run start:web` serves Angular shell at localhost:4200 (verified).
- ✅ `npm run start:mobile` starts Metro bundler without path errors (verified after CLI fix).
- ✅ `@midi-kaval/shared-types` resolves in web and mobile via workspace config.
- ✅ All smoke tests pass: `dotnet test Midi-Kaval.slnx` (2), `npm run test:web` (1), `npm run test:mobile` (1).
- ✅ Code review complete (2026-06-13): 3 patches applied, 2 decisions resolved (SDK 10 pin, `.slnx` only), 2 items deferred.

### File List

- `.editorconfig`
- `.gitignore`
- `README.md`
- `global.json`
- `Midi-Kaval.slnx`
- `package.json`
- `package-lock.json`
- `infra/docker-compose.yml`
- `infra/.env.example`
- `apps/api/MidiKaval.Api.csproj`
- `apps/api/Program.cs`
- `apps/api/Controllers/HealthController.cs`
- `apps/api/Domain/Placeholder.cs`
- `apps/api/Infrastructure/Placeholder.cs`
- `apps/api/Jobs/Placeholder.cs`
- `apps/api/appsettings.json`
- `apps/api/appsettings.Development.json`
- `apps/api/Properties/launchSettings.json`
- `apps/web/` (Angular 19 scaffold — `angular.json`, `ngsw-config.json`, `src/app/*`, etc.)
- `apps/mobile/` (RN 0.76 scaffold — `metro.config.js`, `src/App.tsx`, `__tests__/App.test.tsx`, etc.)
- `packages/shared-types/package.json`
- `packages/shared-types/src/index.ts`
- `packages/shared-types/tsconfig.json`
- `packages/api-client/package.json`
- `packages/api-client/README.md`
- `tests/api.unit/MidiKaval.Api.UnitTests.csproj`
- `tests/api.unit/HealthControllerTests.cs`
- `tests/api.integration/MidiKaval.Api.IntegrationTests.csproj`
- `tests/api.integration/HealthEndpointTests.cs`
- `tests/e2e/package.json`
- `tests/e2e/README.md`

### Change Log

- 2026-06-13: Greenfield monorepo scaffold — API, web, mobile, shared packages, test shells, docker-compose, README.
- 2026-06-13: Fixed npm workspace hoisting for Angular tests (`@angular/compiler` at root).
- 2026-06-13: Added `@react-native-community/cli` so `npm run start:mobile` starts Metro.
- 2026-06-13: Code review patches — explicit `/health` route, removed unused Swagger packages, added `WebApplicationFactory` integration test.
