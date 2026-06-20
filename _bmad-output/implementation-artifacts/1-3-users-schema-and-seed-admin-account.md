# Story 1.3: Users Schema and Seed Admin Account

Status: done

<!-- Validated: 2026-06-14 — see 1-3-users-schema-and-seed-admin-account-validation-report.md -->

## Story

As a **Project Director**,
I want an initial admin user in the database,
so that I can log in on first deploy without manual SQL.

## Acceptance Criteria

1. **Given** PostgreSQL is running (`docker compose -f infra/docker-compose.yml up -d`)  
   **When** I apply EF Core migrations on an empty database  
   **Then** a `users` table exists with snake_case columns including at minimum: `id`, `organisation_id`, `email`, `role`, `token_version`  
   **And** no unrelated tables are created (no `cases`, `audit_events`, `legend_*`, etc.)

2. **Given** migrations have been applied  
   **When** the application starts (or documented seed command runs) on first deploy  
   **Then** exactly one **Director** seed user is created if none exists  
   **And** seed reads email and password from configuration secrets (`Seed:Admin:Email`, `Seed:Admin:Password`, `Seed:OrganisationId`) — never hard-coded or committed

3. **Given** the seed user exists  
   **When** I query the `users` table  
   **Then** the row has `role = Director`, a valid `email`, `token_version = 0`, and a non-empty `password_hash`  
   **And** `organisation_id` matches the configured pilot organisation UUID

4. **Given** seed has already run  
   **When** the application restarts or seed runs again  
   **Then** no duplicate Director user is created (idempotent seed)  
   **And** `token_version` and `password_hash` are not reset on restart

5. **Given** the schema is in place  
   **When** I run `dotnet test Midi-Kaval.slnx`  
   **Then** integration tests using **Testcontainers PostgreSQL** verify migration applies and seed creates the Director user  
   **And** existing Story 1.2 HTTP integration tests (health, meta, swagger, problem details) still pass without requiring local PostgreSQL

6. **Given** CI or fresh clone workflow  
   **When** documented migration steps run (`dotnet ef database update` or startup migrate in Development)  
   **Then** migrations are repeatable and deterministic  
   **And** README documents connection string, migration, and seed configuration

## Tasks / Subtasks

- [x] **EF Core & PostgreSQL wiring** (AC: 1, 5, 6)
  - [x] Add `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x to `apps/api`
  - [x] Add `AppDbContext` in `Infrastructure/Persistence/` with `DbSet<User>` only
  - [x] Register `DbContext` in `Program.cs` using `ConnectionStrings:DefaultConnection` (from `appsettings.Development.json` / env)
  - [x] Enable snake_case column naming via `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`) **or** explicit `HasColumnName` in `UserConfiguration`

- [x] **User entity & configuration** (AC: 1, 3)
  - [x] Create `Domain/Entities/User.cs` — `Id` (Guid), `OrganisationId`, `Email`, `Role`, `TokenVersion`, `PasswordHash`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`
  - [x] Map `Role` to string enum matching PRD roles: `Director`, `Coordinator`, `SocialWorker`, `CaseWorker`
  - [x] Unique index on `(organisation_id, email)`
  - [x] Configure table name `users` with snake_case columns per `project-context.md`

- [x] **Initial migration** (AC: 1, 6)
  - [x] Add `dotnet ef` tooling; store migrations under `apps/api/Migrations/` (canonical path `apps/api`, not stale `src/api`)
  - [x] Generate initial migration creating **only** `users` table
  - [x] Document `dotnet ef migrations add` / `dotnet ef database update` in README

- [x] **Password hashing** (AC: 3)
  - [x] Use `IPasswordHasher<User>` from `Microsoft.AspNetCore.Identity` (core package only — no full Identity UI/stack)
  - [x] Hash seed password at insert time; never store plaintext

- [x] **Idempotent seed** (AC: 2, 3, 4)
  - [x] Implement `Infrastructure/Seed/AdminUserSeeder.cs` (or `IHostedService` invoked at startup)
  - [x] Read `Seed:Admin:Email`, `Seed:Admin:Password`, `Seed:OrganisationId` from configuration / user secrets
  - [x] Add `appsettings.Development.json` placeholders + `infra/.env.example` entries (no real secrets)
  - [x] Skip insert if Director with same email already exists in organisation

- [x] **Startup / migration apply** (AC: 6)
  - [x] Apply pending migrations on startup in **Development** only (or document explicit CLI for CI/Production)
  - [x] Call seeder after migrate in Development; document Production seed approach in README
  - [x] **Do not** auto-migrate/seed when `ASPNETCORE_ENVIRONMENT=Testing` (preserves Story 1.2 HTTP tests without local Postgres)

- [x] **Test environment strategy** (AC: 5)
  - [x] Update existing `WebApplicationFactory` fixtures to use `.WithWebHostBuilder(b => b.UseEnvironment("Testing"))` so HTTP regression tests skip DB startup
  - [x] `UsersSchemaTests` uses Testcontainers PostgreSQL with its own fixture (container connection string injected into DbContext or `WebApplicationFactory` override)
  - [x] Testcontainers provides Postgres for schema tests — **does not** require `docker compose` running during `dotnet test`
- [x] **Integration tests with Testcontainers** (AC: 5)
  - [x] Add `Testcontainers.PostgreSql` to `tests/api.integration`
  - [x] Create `UsersSchemaTests` — start Postgres container, apply migrations, run seed, assert `users` row + idempotent re-seed
  - [x] Verify all integration tests pass together in one `dotnet test` run

- [x] **Documentation & verification** (AC: 6)
  - [x] Update root `README.md` with migration + seed config steps
  - [x] Verify `dotnet test Midi-Kaval.slnx` passes end-to-end

### Review Findings

- [x] [Review][Patch] Normalize seed email to lowercase before existence check and insert [`AdminUserSeeder.cs:26`]
- [x] [Review][Patch] Fix idempotency test isolation — use per-test fresh DB or cleanup so re-seed path is actually exercised [`UsersSchemaTests.cs:90`]
- [x] [Review][Patch] Log warning in Development when seed config is missing/invalid (silent skip hides misconfiguration) [`AdminUserSeeder.cs:19`]
- [x] [Review][Patch] Assert `PasswordHasher.VerifyHashedPassword` succeeds in seed integration test [`UsersSchemaTests.cs:87`]
- [x] [Review][Patch] Add explicit `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Identity.Core` to API csproj [`MidiKaval.Api.csproj`]
- [x] [Review][Patch] Align EF Core package versions across API and test projects (resolve 8.0.11 vs 8.0.16 mismatch) [`MidiKaval.Api.csproj`]
- [x] [Review][Patch] README: document `dotnet ef migrations add` and Production/staging seed approach [`README.md:33`]

- [x] [Review][Defer] Concurrent seed race on parallel Development startups [`AdminUserSeeder.cs:28`] — deferred, dev-only edge case; upsert can wait until deploy hardening
- [x] [Review][Defer] `organisation_id` references no organisations table yet [`AdminUserSeeder.cs:41`] — deferred, organisations schema is a later epic
- [x] [Review][Defer] `TestingWebApplicationFactory` registers no DbContext substitute [`Program.cs:28`] — deferred, Story 1.4 auth tests will need factory override
- [x] [Review][Defer] No integration test for missing/invalid seed config silent skip [`AdminUserSeeder.cs:19`] — deferred, logging patch covers operational gap
- [x] [Review][Defer] Custom env names (Staging/Local) require DB connection but skip migrate/seed [`DatabaseInitializer.cs:15`] — deferred, ops documentation in README patch

### Re-review (2026-06-14, post-patch)

- [x] [Review][Patch] Trim seed password before hashing (email is trimmed; password whitespace causes login mismatch) [`AdminUserSeeder.cs:18`]
- [x] [Review][Patch] Add test asserting mixed-case config email normalizes to lowercase on insert [`UsersSchemaTests.cs`]
- [x] [Review][Patch] Use case-insensitive environment check for `Testing` (avoid `testing` vs `Testing` mismatch) [`Program.cs:28`, `DatabaseInitializer.cs:10`]

- [x] [Review][Defer] DB unique index is case-sensitive — legacy/mixed-case rows can coexist with normalized seed [`UserConfiguration.cs:32`] — deferred, citext/functional index in later story
- [x] [Review][Defer] README Production seed via temporary Development startup is operational workaround [`README.md:46`] — deferred, dedicated seed CLI in later infra story
- [x] [Review][Defer] No max-length guard on seed email before insert [`AdminUserSeeder.cs:34`] — deferred, admin email length edge case

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Story 1.3 adds the **first database schema** and bootstrap Director account. Story 1.4 implements login/OTP against this `users` table.

### Brownfield state — READ BEFORE CODING

Story 1.2 delivered API host foundation. **UPDATE** existing files; do not break envelope/Swagger/health.

| File | Current state | This story changes |
|------|---------------|-------------------|
| `apps/api/Program.cs` | Swagger, envelope, Problem Details, HTTPS | Add DbContext, migrate (dev), seed |
| `apps/api/MidiKaval.Api.csproj` | Swashbuckle, OpenApi | Add EF Core + Npgsql packages |
| `apps/api/appsettings.Development.json` | Connection string placeholders | Add `Seed:*` section placeholders |
| `apps/api/Infrastructure/` | Envelope, exception middleware | Add `Persistence/`, `Seed/` |
| `apps/api/Domain/` | Placeholder only | Add `Entities/User.cs` |
| `tests/api.integration/` | WebApplicationFactory HTTP tests | Add Testcontainers PostgreSQL tests |

### Scope boundaries (critical)

| In scope (1.3) | Out of scope — later stories |
|----------------|------------------------------|
| `users` table + EF migration + seed Director | `POST /api/v1/auth/login` (1.4) |
| Password hash storage for seed user | Login API, OTP, JWT, refresh tokens (1.4–1.5) |
| Testcontainers integration test | RBAC policies (1.8) |
| `token_version` column (default 0) | `audit_events` table (later epics) |
| Single organisation_id for pilot | Cases, legends, visits schema (Epic 2+) |

**Story 1.3 / 1.4 boundary:** This story creates a **login-ready** Director row (`password_hash` stored). Story 1.4 adds `/api/v1/auth/*` endpoints that read this table — do not expose user data via API in 1.3.

**Epic AC clarification:** “columns only” means **no unrelated tables** in this migration — not that `password_hash`, `id`, or timestamps are forbidden. `password_hash` is required so Story 1.4 login can authenticate the seed Director without a follow-up migration.

### Technical requirements

| Item | Requirement |
|------|-------------|
| EF Core | 8.x with Npgsql 8.x |
| PostgreSQL | 16 via existing `infra/docker-compose.yml` |
| IDs | UUID v4 (`Guid`) per architecture |
| Timestamps | `DateTime` UTC (`CreatedAtUtc`, `UpdatedAtUtc`) |
| Column naming | snake_case in DB, PascalCase in C# |
| Roles | `Director`, `Coordinator`, `SocialWorker`, `CaseWorker` |
| Secrets | `Seed:Admin:Email`, `Seed:Admin:Password`, `Seed:OrganisationId` via env/user secrets |

### Suggested schema (`users`)

| Column | Type | Notes |
|--------|------|-------|
| `id` | `uuid` PK | gen_random or app-assigned Guid |
| `organisation_id` | `uuid` NOT NULL | tenant-ready pilot org |
| `email` | `varchar(320)` NOT NULL | unique with organisation_id |
| `role` | `varchar(32)` NOT NULL | Director, etc. |
| `token_version` | `int` NOT NULL DEFAULT 0 | force-logout (Story 1.5) |
| `password_hash` | `text` NOT NULL | hashed, never plaintext |
| `is_active` | `boolean` NOT NULL DEFAULT true | deactivation (Story 1.4) |
| `created_at_utc` | `timestamptz` NOT NULL | |
| `updated_at_utc` | `timestamptz` NOT NULL | |

### Architecture compliance

- Migrations live in `apps/api` (canonical per §7; ignore stale §5.1 `src/api` reference) [Source: architecture §7, Story 1.1 dev notes]
- `Infrastructure/` for EF DbContext and seeding [Source: project-context Framework Rules]
- Integration tests use Testcontainers PostgreSQL [Source: architecture §6.4]
- `organisation_id` on tenant-scoped tables [Source: project-context]
- No `[AllowAnonymous]` mutations — no user-facing API in this story [Source: project-context]

### Library / framework requirements

**NuGet (`apps/api`):**
- `Microsoft.EntityFrameworkCore` 8.0.x
- `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.x
- `Microsoft.EntityFrameworkCore.Design` 8.0.x (PrivateAssets)
- `EFCore.NamingConventions` 8.x (if using `UseSnakeCaseNamingConvention`)
- `Microsoft.Extensions.Identity.Core` 8.0.x for `IPasswordHasher<User>` — **not** full `Microsoft.AspNetCore.Identity.EntityFrameworkCore`

**NuGet (`tests/api.integration`):**
- `Testcontainers.PostgreSql` 3.x (or current stable)
- `Microsoft.EntityFrameworkCore` (transitive via API project)

**CLI:** `dotnet tool install dotnet-ef` (document if not bundled)

### Testing requirements

| Test | Location | Minimum coverage |
|------|----------|------------------|
| Migration applies | `UsersSchemaTests` | `users` table exists via Testcontainers |
| Seed creates Director | `UsersSchemaTests` | One row, correct role/email, password hash not empty |
| Idempotent seed | `UsersSchemaTests` | Second seed run does not duplicate |
| API regression | existing HTTP integration tests | Story 1.2 tests pass with `Testing` environment (no DB) |
| Health/meta/swagger | unchanged | Story 1.2 ACs preserved |

### Previous story intelligence (1.2)

- `Program.cs` registers Swagger, exception handler, envelope filter — extend without reordering breaking middleware
- `public partial class Program { }` required for `WebApplicationFactory`
- `dotnet test Midi-Kaval.slnx` — stop running `MidiKaval.Api` process if file lock errors occur
- `baseline_commit: NO_VCS` — git not on PATH
- Integration tests use `IClassFixture<WebApplicationFactory<Program>>` — add `UseEnvironment("Testing")` to existing fixtures after DbContext registration to avoid localhost Postgres dependency
- Testcontainers tests use a **separate** fixture/collection from HTTP smoke tests
- Set `ASPNETCORE_ENVIRONMENT=Development` for local `dotnet run` to avoid Production HTTPS redirect warnings

### Anti-patterns (do NOT do in this story)

- Do not create `cases`, `audit_events`, `legend_*`, or other tables
- Do not implement login, OTP, JWT, or RBAC endpoints
- Do not commit seed passwords or real emails to git
- Do not use auto-increment integer public user IDs
- Do not hand-edit generated migration designer files after creation (regenerate if wrong)
- Do not break Story 1.2 health/meta/swagger/problem-details behavior

### Definition of Done

- [x] `users` table exists after migration on empty PostgreSQL
- [x] Director seed user created from configuration secrets
- [x] Seed is idempotent across restarts
- [x] Testcontainers integration test passes
- [x] `dotnet test Midi-Kaval.slnx` — all tests pass (Story 1.2 HTTP regression + new schema tests)
- [x] README documents migration, seed config, and docker-compose prerequisite
- [x] Story file `File List` updated by dev agent on completion

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.3]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.1 Data, §5.2 Auth, §6.4 Testing, §7 Structure]
- [Source: `_bmad-output/project-context.md` — EF naming, organisation_id, roles]
- [Source: `_bmad-output/implementation-artifacts/1-2-api-host-foundation-with-health-and-openapi.md` — API baseline]
- [Source: `packages/shared-types/src/index.ts` — `AppRole` enum for role alignment]

## Dev Agent Record

### Agent Model Used

Claude (Cursor Agent)

### Debug Log References

- DiagnosticsController updated to allow `Testing` environment for `/api/v1/diagnostics/throw` (HTTP regression tests use Testing env, not Development).
- Testcontainers schema tests require Docker daemon running (`dotnet test` pulls `postgres:16-alpine`).

### Completion Notes List

- Added EF Core 8 + Npgsql with snake_case naming; `users` table only in `InitialUsers` migration.
- `AdminUserSeeder` idempotently seeds Director from `Seed:*` configuration using `IPasswordHasher<User>`.
- Development startup auto-migrates and seeds; `Testing` environment skips DB registration/migrate/seed.
- `TestingWebApplicationFactory` preserves Story 1.2 HTTP tests without local Postgres.
- `UsersSchemaTests` (Testcontainers) verifies migration, seed, and idempotency.
- `dotnet test Midi-Kaval.slnx`: 9 passed (1 unit + 8 integration).
- Code review re-review patches: password trim, mixed-case email test, case-insensitive Testing env helper.

### File List

- `apps/api/Domain/Entities/User.cs` (added)
- `apps/api/Domain/Entities/UserRoles.cs` (added)
- `apps/api/Infrastructure/Persistence/AppDbContext.cs` (added)
- `apps/api/Infrastructure/Persistence/UserConfiguration.cs` (added)
- `apps/api/Infrastructure/Seed/AdminUserSeeder.cs` (added)
- `apps/api/Infrastructure/Seed/DatabaseInitializer.cs` (added)
- `apps/api/Migrations/20260614034204_InitialUsers.cs` (added)
- `apps/api/Migrations/20260614034204_InitialUsers.Designer.cs` (added)
- `apps/api/Migrations/AppDbContextModelSnapshot.cs` (added)
- `apps/api/MidiKaval.Api.csproj` (modified)
- `apps/api/Program.cs` (modified)
- `apps/api/appsettings.Development.json` (modified)
- `apps/api/Controllers/V1/DiagnosticsController.cs` (modified)
- `tests/api.integration/TestingWebApplicationFactory.cs` (added)
- `tests/api.integration/UsersSchemaTests.cs` (added)
- `tests/api.integration/HealthEndpointTests.cs` (modified)
- `tests/api.integration/MetaEndpointTests.cs` (modified)
- `tests/api.integration/SwaggerEndpointTests.cs` (modified)
- `tests/api.integration/ProblemDetailsTests.cs` (modified)
- `tests/api.integration/MidiKaval.Api.IntegrationTests.csproj` (modified)
- `infra/.env.example` (modified)
- `README.md` (modified)

### Change Log

- 2026-06-14: Story 1.3 — users schema, EF migration, idempotent Director seed, Testcontainers integration tests.
- 2026-06-14: Code review — 7 patches applied (email normalization, test isolation, seed logging, EF packages, README).
- 2026-06-14: Re-review — 3 patches applied (password trim, email case test, IsTesting helper).
