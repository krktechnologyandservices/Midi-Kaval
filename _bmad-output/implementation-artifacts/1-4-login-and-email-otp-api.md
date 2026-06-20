---
baseline_commit: NO_VCS
---

# Story 1.4: Login and Email OTP API

Status: done

<!-- Validated: 2026-06-14 — see 1-4-login-and-email-otp-api-validation-report.md -->

## Story

As a **user**,
I want to authenticate with email, password, and email OTP,
so that only verified staff access case data (FR-1).

## Acceptance Criteria

1. **Given** valid credentials for an active user in `users` (seeded Director or future accounts)  
   **When** I `POST /api/v1/auth/login` with correct email and password  
   **Then** the API returns **200** with envelope `{ data: { challengeId, expiresInSeconds }, meta }`  
   **And** a 6-digit OTP is emailed to the user via the configured provider  
   **And** invalid password or unknown email returns **401** with Problem Details (generic message — no account enumeration)

2. **Given** a valid login challenge from step 1  
   **When** I `POST /api/v1/auth/verify-otp` with correct `challengeId` and OTP code  
   **Then** I receive a JWT **access token** (15-minute lifetime) in the envelope `data.accessToken` + `data.expiresIn`  
   **And** a **refresh token** is issued: returned in `data.refreshToken` for mobile clients **and** documented httpOnly cookie contract for web (`Set-Cookie` with `HttpOnly`, `Secure` in non-Development, `SameSite=Strict`, path `/api/v1/auth`)  
   **And** JWT claims include at minimum: `sub` (user id), `email`, `role`, `organisation_id`, `token_version` (must match `users.token_version`)

3. **Given** OTP or credential failures  
   **When** I submit wrong OTP, expired challenge, or wrong password  
   **Then** wrong OTP returns **401**; expired/invalid challenge returns **401**  
   **And** deactivated account (`is_active = false`) returns **403** with detail message **"Contact your coordinator"** on login (before OTP send) and on verify-otp

4. **Given** auth endpoints are public  
   **When** `POST /api/v1/auth/login` or `POST /api/v1/auth/verify-otp` are called repeatedly  
   **Then** requests are **rate-limited** per IP (document limits in README; use ASP.NET Core rate limiting middleware)  
   **And** rate-limit exceeded returns **429** Problem Details

5. **Given** Story 1.2–1.3 baseline  
   **When** I run `dotnet test Midi-Kaval.slnx`  
   **Then** existing health/meta/swagger/problem-details tests still pass  
   **And** new integration tests cover happy-path login+OTP, wrong password 401, wrong OTP 401, deactivated 403, using Testcontainers PostgreSQL + Redis with fake email sender (no real SMTP in CI)

6. **Given** OpenAPI is updated  
   **When** I regenerate `packages/api-client`  
   **Then** auth DTOs and routes appear in Swagger  
   **And** README documents auth config (`Jwt:*`, `Otp:*`, email provider settings)

## Tasks / Subtasks

- [x] **JWT & auth infrastructure** (AC: 2, 6)
  - [x] Add `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt` 8.x
  - [x] Register JWT + email services **only when `!environment.IsTesting()`** (with Redis/DbContext pattern)
  - [x] Configure `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (user secrets / env — never commit real key)
  - [x] Register `AddAuthentication().AddJwtBearer()` in `Program.cs` (validate tokens; full RBAC policies are Story 1.8)
  - [x] Add `UseAuthentication()` and `UseAuthorization()` middleware after exception handler, before `MapControllers`
  - [x] Create `Infrastructure/Auth/JwtTokenService.cs` — issue access token with 15-min expiry; claims: `sub`, `email`, `role`, `organisation_id`, `token_version` (custom claim names matching DB intent)

- [x] **Redis OTP challenge store** (AC: 1, 2, 3)
  - [x] Add `Microsoft.Extensions.Caching.StackExchangeRedis` 8.0.x
  - [x] Register Redis **only when `!environment.IsTesting()`** (mirror DbContext — preserves Story 1.2 HTTP smoke tests)
  - [x] Wire `ConnectionStrings:Redis` from `appsettings.Development.json` (already present)
  - [x] Implement `Infrastructure/Auth/OtpChallengeStore.cs` — store challenge `{ userId, organisationId, email, otpHash, expiresAt }` keyed by `challengeId` with TTL (default 5 min, configurable `Otp:ExpiryMinutes`)
  - [x] OTP: 6-digit cryptographically random; store **hashed** OTP in Redis (not plaintext)

- [x] **Email OTP delivery** (AC: 1)
  - [x] Implement `Infrastructure/Email/IEmailSender` + `SmtpEmailSender` (MailKit) for Development SMTP
  - [x] Add `FakeEmailSender` for test environments — captures **plaintext OTP in message body** before hashing (tests read OTP from sender, not Redis)
  - [x] Configure `Email:Smtp:*` placeholders in `appsettings.Development.json` + `infra/.env.example`
  - [x] OTP email body per UX tone: "Enter the 6-digit code from your email." [Source: EXPERIENCE.md]

- [x] **Login endpoint** (AC: 1, 3)
  - [x] `Controllers/V1/AuthController.cs` — `POST /api/v1/auth/login`
  - [x] `[AllowAnonymous]` on auth controller only
  - [x] Normalize email: `Trim().ToLowerInvariant()` (match Story 1.3 seeder)
  - [x] Verify password via `IPasswordHasher<User>.VerifyHashedPassword`
  - [x] Check `is_active`; if false → 403 "Contact your coordinator"
  - [x] On success: create challenge, send OTP email, return `{ challengeId, expiresInSeconds }`
  - [x] Wrong credentials → 401 generic Problem Details

- [x] **Verify OTP endpoint** (AC: 2, 3)
  - [x] `POST /api/v1/auth/verify-otp` with `{ challengeId, code }`
  - [x] Validate OTP against Redis challenge; expired/missing → 401
  - [x] Re-check user still active and `token_version` unchanged
  - [x] Issue access JWT + refresh token (opaque random, store hash in Redis with user binding and TTL e.g. 7 days)
  - [x] Return envelope with `accessToken`, `expiresIn`, `refreshToken` (mobile)
  - [x] Set httpOnly refresh cookie for web (document `X-Client-Platform: web` header **or** always set cookie + return body — document chosen approach in README)

- [x] **Rate limiting** (AC: 4)
  - [x] Add `Microsoft.AspNetCore.RateLimiting` fixed window on `/api/v1/auth/*` (e.g. 10 req/min/IP — configurable `Auth:RateLimitPermitLimit`)
  - [x] Return 429 via Problem Details — configure rate limiter `OnRejected` to emit `application/problem+json`

- [x] **DTOs & OpenAPI** (AC: 6)
  - [x] Request/response models in `Models/Auth/` with XML doc comments
  - [x] Ensure Swagger documents auth routes and envelope-wrapped success responses

- [x] **Integration tests** (AC: 5)
  - [x] New `AuthWebApplicationFactory` + `AuthLoginTests`: Testcontainers PostgreSQL + Redis (**not** `Testing` env — override `ConnectionStrings:DefaultConnection` and `ConnectionStrings:Redis` in `ConfigureWebHost`)
  - [x] Register `FakeEmailSender` in auth test factory; extract OTP from last captured email body for verify-otp step
  - [x] Seed test user via `AdminUserSeeder` pattern or direct insert with known password
  - [x] Tests: login→verify happy path JWT claims; wrong password 401; wrong OTP 401; deactivated user 403
  - [x] Preserve existing `TestingWebApplicationFactory` tests without Redis/Postgres dependency

- [x] **Documentation** (AC: 6)
  - [x] Update README: JWT secret, OTP expiry, SMTP config, rate limits, refresh cookie contract
  - [x] Regenerate api-client after implementation (`npm run generate:api-client`)

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Story 1.4 delivers the **first auth API** against the `users` table from Story 1.3. Stories 1.5 (refresh/logout/token_version invalidation), 1.6–1.7 (client login UI), and 1.8 (RBAC policies) build on this.

### Brownfield state — READ BEFORE CODING

Story 1.3 delivered `users` table, EF Core, seed Director, Testcontainers patterns. **EXTEND** existing API host — do not break envelope/Swagger/health.

| File | Current state | This story changes |
|------|---------------|-------------------|
| `apps/api/Program.cs` | DbContext, seed, Swagger, envelope, Problem Details | Add Redis (non-Testing), JWT auth + middleware, rate limiting, email sender DI |
| `apps/api/MidiKaval.Api.csproj` | EF Core 8.0.11, Identity.Core, Swashbuckle | Add JWT, Redis, MailKit, RateLimiting |
| `apps/api/Infrastructure/Persistence/AppDbContext.cs` | `DbSet<User>` only | **No new tables** — OTP/refresh state in Redis for 1.4 |
| `apps/api/appsettings.Development.json` | Postgres + Redis + Seed | Add `Jwt:*`, `Otp:*`, `Email:Smtp:*` |
| `apps/api/Controllers/V1/` | Meta, Diagnostics | Add `AuthController.cs` |
| `tests/api.integration/` | HTTP smoke + UsersSchemaTests | Add `AuthLoginTests` with PG+Redis fixture |

### Scope boundaries (critical)

| In scope (1.4) | Out of scope — later stories |
|----------------|------------------------------|
| `POST /api/v1/auth/login` | `POST /api/v1/auth/refresh` (1.5) |
| `POST /api/v1/auth/verify-otp` | `POST /api/v1/auth/logout` (1.5) |
| Issue access JWT + refresh token on verify-otp | Refresh token rotation endpoint (1.5) |
| Redis OTP challenges + refresh token storage | `audit_events` table writes (1.5) |
| Rate limiting on auth routes | RBAC `[Authorize(Policy=...)]` on case endpoints (1.8) |
| Fake email in tests | Angular/RN login UI (1.6–1.7) |
| `[AllowAnonymous]` on auth endpoints only | Password reset/forgot-password (1.9) |
| Deactivated user 403 message | Admin force-reset UI (9.2) |

**Story 1.4 / 1.5 boundary:** 1.4 **issues** refresh tokens (cookie + body). 1.5 implements **refresh** and **logout** endpoints plus `token_version` enforcement on subsequent API calls and audit logging.

**Story 1.4 / 1.8 boundary:** Register JWT bearer authentication so tokens are validated, but do **not** add role policies to business endpoints yet — only auth routes in this story.

### Test environment strategy (critical — Story 1.2 regression)

| Fixture | Environment | DB / Redis | Purpose |
|---------|-------------|------------|---------|
| `TestingWebApplicationFactory` | `Testing` | **Skipped** (no DbContext, no Redis, no JWT email) | HTTP smoke: health, meta, swagger, problem details |
| `AuthWebApplicationFactory` | `Development` or `AuthIntegration` | Testcontainers Postgres + Redis injected via `ConfigureWebHost` | Login + OTP integration tests |
| `UsersSchemaTests` | N/A (direct DbContext) | Testcontainers Postgres only | Schema/migration regression |

**Do not** register Redis, JWT, or SMTP in `Testing` environment — same pattern as Story 1.3 DbContext skip.

### Technical requirements

| Item | Requirement |
|------|-------------|
| Access token | JWT, 15-minute expiry, HS256 or RS256 (HS256 acceptable for pilot with secret in config) |
| Refresh token | Opaque string; store SHA-256 hash in Redis; 7-day TTL default |
| OTP | 6 digits; 5-minute TTL default; single-use (delete challenge on success) |
| Email lookup | Case-insensitive (`ToLowerInvariant`) matching Story 1.3 seed |
| Password verify | `IPasswordHasher<User>` — reuse existing registration from 1.3 |
| Redis | Existing `infra/docker-compose.yml` Redis 7 on `localhost:6379` |
| Errors | RFC 7807 Problem Details for 401/403/429 (existing middleware) |
| Success | Envelope `{ data, meta }` for `/api/v1/auth/*` success responses |
| Health/Swagger | Unchanged — no envelope on `/health` |

### Suggested API contracts

**POST /api/v1/auth/login**

```json
{ "email": "director@pilot.example", "password": "..." }
```

Success `200`:

```json
{
  "data": { "challengeId": "<uuid>", "expiresInSeconds": 300 },
  "meta": { "requestId": "..." }
}
```

**POST /api/v1/auth/verify-otp**

```json
{ "challengeId": "<uuid>", "code": "123456" }
```

Success `200`:

```json
{
  "data": {
    "accessToken": "<jwt>",
    "expiresIn": 900,
    "refreshToken": "<opaque>",
    "user": { "id": "...", "email": "...", "role": "Director" }
  },
  "meta": { "requestId": "..." }
}
```

Plus `Set-Cookie: refresh_token=...; HttpOnly; Path=/api/v1/auth; SameSite=Strict` (add `Secure` when not Development).

### Architecture compliance

- Auth endpoints under `/api/v1/auth/*` [Source: architecture §5.3]
- JWT 15 min + refresh [Source: architecture §5.2, project-context]
- Redis for session/OTP state [Source: architecture §5.1 caching]
- Rate limiting on auth [Source: architecture §5.2, epics FR-1]
- Business rules in `Domain/` or `Infrastructure/Auth/` — thin controller [Source: project-context]
- Canonical API path `apps/api` (not stale `src/api`) [Source: architecture §7]
- `[AllowAnonymous]` only on auth controller — never on data mutations elsewhere [Source: project-context]

### Library / framework requirements

**NuGet (`apps/api`):**
- `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x
- `System.IdentityModel.Tokens.Jwt` 8.x
- `Microsoft.Extensions.Caching.StackExchangeRedis` 8.0.x (or `StackExchange.Redis`)
- `MailKit` 4.x (SMTP email)
- Built-in `Microsoft.AspNetCore.RateLimiting` (part of shared framework)

**NuGet (`tests/api.integration`):**
- `Testcontainers.Redis` 4.x (pair with existing PostgreSql Testcontainers)
- Reuse existing EF + Testcontainers.PostgreSql packages

**Configuration keys (secrets via user secrets / env):**
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (min 32 bytes)
- `Otp:ExpiryMinutes` (default 5)
- `Auth:RateLimitPermitLimit`, `Auth:RateLimitWindowSeconds`
- `Email:Smtp:Host`, `Email:Smtp:Port`, `Email:Smtp:User`, `Email:Smtp:Password`, `Email:From`

### Testing requirements

| Test | Location | Minimum coverage |
|------|----------|------------------|
| Login happy path | `AuthLoginTests` | 200 + challengeId; OTP read from `FakeEmailSender` last message |
| Verify OTP happy path | `AuthLoginTests` | JWT parses; claims match user; refresh token returned |
| Wrong password | `AuthLoginTests` | 401 Problem Details |
| Wrong OTP | `AuthLoginTests` | 401 |
| Deactivated user | `AuthLoginTests` | 403 detail "Contact your coordinator" |
| HTTP regression | existing tests | Story 1.2 tests unchanged with `TestingWebApplicationFactory` |
| Schema regression | `UsersSchemaTests` | Still passes |

**Test fixture pattern:** `AuthWebApplicationFactory` overrides connection strings to Testcontainers endpoints and replaces `IEmailSender` with `FakeEmailSender`. Optionally disable or raise rate limits in test config.

### Previous story intelligence (1.3)

- `TestingWebApplicationFactory` + `IsTesting()` skips DbContext — **auth integration tests must use a separate fixture** with real PG+Redis containers
- Email normalized `Trim().ToLowerInvariant()` in seeder — **match in login lookup**
- `IPasswordHasher<User>` already registered outside Testing
- `dotnet test` requires Docker for Testcontainers
- Stop running `MidiKaval.Api` process if file lock during test
- `baseline_commit: NO_VCS` — git not on PATH
- EF packages pinned @ 8.0.11 — align new packages to 8.0.x
- Problem Details: 404 test needed `DiagnosticsController` Testing env allowance — auth errors use same middleware

### Anti-patterns (do NOT do in this story)

- Do not create `audit_events`, `cases`, or other DB tables (Redis only for OTP/refresh state)
- Do not register Redis/JWT/email in `Testing` environment (breaks Story 1.2 HTTP tests)
- Do not implement `/auth/refresh` or `/auth/logout` (Story 1.5)
- Do not add RBAC policies to non-auth controllers (Story 1.8)
- Do not implement forgot-password/reset (Story 1.9)
- Do not build Angular/RN login screens (Stories 1.6–1.7)
- Do not store OTP plaintext in Redis or logs
- Do not reveal whether email exists on failed login (same 401 message)
- Do not break Story 1.2 envelope on `/api/v1/*` or health/swagger behavior
- Do not hand-edit `packages/api-client/src/generated/*`

### Definition of Done

- [x] Login and verify-otp endpoints work against seeded Director user
- [x] JWT access token issued with correct claims and 15-min expiry
- [x] Refresh token issued (body + cookie contract documented)
- [x] OTP email sent via provider (fake in tests)
- [x] Rate limiting active on auth routes
- [x] `dotnet test Midi-Kaval.slnx` — all tests pass
- [x] README documents auth configuration
- [x] Story file `File List` updated by dev agent on completion

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.4]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.2 Auth, §5.3 API, §6.4 Testing]
- [Source: `_bmad-output/project-context.md` — Auth, errors, testing rules]
- [Source: `_bmad-output/specs/spec-kaval-online/roles-and-access.md` — Auth flows]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — OTP copy, Flow 7]
- [Source: `_bmad-output/implementation-artifacts/1-3-users-schema-and-seed-admin-account.md` — users schema baseline]

## Dev Agent Record

### Agent Model Used

Claude (Cursor Agent)

### Debug Log References

- JWT claim assertions in `AuthLoginTests` failed when using `ValidateToken` principal lookup — inbound claim mapping renamed `sub`/`email`. Fixed by validating signature then reading claims via `ReadJwtToken`.
- `AuthWebApplicationFactory` config applied via environment variables in `CreateHost` (overrides `appsettings.Development.json`).
- Api-client regeneration without running API: export OpenAPI via `EXPORT_OPENAPI_PATH` + `TestingWebApplicationFactory`, then `API_OPENAPI_FILE` for `generate.mjs`.

### Completion Notes List

- Implemented `POST /api/v1/auth/login` and `POST /api/v1/auth/verify-otp` with envelope responses, Problem Details errors, and rate limiting on `/api/v1/auth/*`.
- Redis stores hashed OTP challenges and refresh token hashes; `FakeEmailSender` captures plaintext OTP for integration tests.
- JWT access tokens (15 min) include `sub`, `email`, `role`, `organisation_id`, `token_version`; refresh token returned in body and httpOnly cookie.
- Four `AuthLoginTests` pass against Testcontainers PostgreSQL + Redis; Story 1.2 smoke tests unchanged (14 integration + 1 unit = 15 total).
- Regenerated `@midi-kaval/api-client` with `/api/v1/auth/login` and `/api/v1/auth/verify-otp` routes.
- MailKit 4.11.0 reports NU1902 moderate vulnerability — consider upgrading in a follow-up.

### File List

- apps/api/Controllers/V1/AuthController.cs
- apps/api/Infrastructure/Auth/AuthClaimTypes.cs
- apps/api/Infrastructure/Auth/AuthOptions.cs
- apps/api/Infrastructure/Auth/AuthService.cs
- apps/api/Infrastructure/Auth/JwtTokenService.cs
- apps/api/Infrastructure/Auth/OtpChallenge.cs
- apps/api/Infrastructure/Auth/OtpChallengeStore.cs
- apps/api/Infrastructure/Auth/OtpHasher.cs
- apps/api/Infrastructure/Auth/RefreshTokenStore.cs
- apps/api/Infrastructure/AuthServiceCollectionExtensions.cs
- apps/api/Infrastructure/Email/FakeEmailSender.cs
- apps/api/Infrastructure/Email/IEmailSender.cs
- apps/api/Infrastructure/Email/SmtpEmailSender.cs
- apps/api/Models/Auth/AuthDtos.cs
- apps/api/MidiKaval.Api.csproj
- apps/api/Program.cs
- apps/api/appsettings.Development.json
- infra/.env.example
- README.md
- packages/api-client/openapi-snapshot.json
- packages/api-client/scripts/generate.mjs
- packages/api-client/src/generated/api.ts
- tests/api.integration/AuthLoginTests.cs
- tests/api.integration/AuthWebApplicationFactory.cs
- tests/api.integration/MidiKaval.Api.IntegrationTests.csproj
- tests/api.integration/SwaggerEndpointTests.cs
- _bmad-output/implementation-artifacts/sprint-status.yaml

### Review Findings

- [x] [Review][Decision→Patch] Empty SMTP host allows login without sending OTP — **Resolved: 1A** fail login when SMTP unconfigured outside Testing [apps/api/Infrastructure/Email/SmtpEmailSender.cs]
- [x] [Review][Decision→Patch] verify-otp returns 403 when user row missing — **Resolved: 2A** return 401 for deleted user [apps/api/Infrastructure/Auth/AuthService.cs:89]
- [x] [Review][Decision→Patch] Login and verify-otp share one rate-limit bucket — **Resolved: 3A** separate `auth-login` and `auth-verify` policies [apps/api/Controllers/V1/AuthController.cs]
- [x] [Review][Patch] No per-challenge OTP attempt limit — brute-forceable within TTL [apps/api/Infrastructure/Auth/AuthService.cs:84]
- [x] [Review][Patch] Multiple active OTP challenges per user — new login does not revoke prior challenges [apps/api/Infrastructure/Auth/AuthService.cs:47]
- [x] [Review][Patch] Concurrent verify-otp race can issue multiple token pairs — challenge removed non-atomically [apps/api/Infrastructure/Auth/AuthService.cs:95]
- [x] [Review][Patch] Orphaned OTP challenge if email send fails after Redis write [apps/api/Infrastructure/Auth/AuthService.cs:57]
- [x] [Review][Patch] Orphaned OTP challenge on request cancellation after CreateAsync [apps/api/Infrastructure/Auth/AuthService.cs:57]
- [x] [Review][Patch] Challenge consumed before token issuance — JWT/refresh failure leaves user without tokens [apps/api/Infrastructure/Auth/AuthService.cs:95]
- [x] [Review][Patch] Null JSON email causes NullReferenceException → 500 [apps/api/Infrastructure/Auth/AuthService.cs:25]
- [x] [Review][Patch] Corrupt Redis challenge JSON can throw JsonException → 500 [apps/api/Infrastructure/Auth/OtpChallengeStore.cs:34]
- [x] [Review][Patch] Missing startup validation for Jwt/Otp config (zero/negative expiry, short signing key) [apps/api/Infrastructure/Auth/JwtTokenService.cs:16]
- [x] [Review][Patch] OpenAPI omits envelope wrappers on auth success responses [apps/api/Controllers/V1/AuthController.cs:19]
- [x] [Review][Patch] OpenAPI missing 429 response on auth routes [apps/api/Controllers/V1/AuthController.cs:19]
- [x] [Review][Patch] Missing test: deactivated user on verify-otp returns 403 [tests/api.integration/AuthLoginTests.cs]
- [x] [Review][Patch] Missing test: expired/invalid challengeId returns 401 [tests/api.integration/AuthLoginTests.cs]
- [x] [Review][Patch] Missing test: rate limit exceeded returns 429 Problem Details [tests/api.integration/AuthLoginTests.cs]
- [x] [Review][Patch] Missing test: verify-otp sets httpOnly refresh_token Set-Cookie [tests/api.integration/AuthLoginTests.cs]
- [x] [Review][Patch] Missing test: unknown email returns 401 generic message [tests/api.integration/AuthLoginTests.cs]
- [x] [Review][Patch] README cookie contract incomplete — missing SameSite=Strict and Secure behavior [README.md:77]
- [x] [Review][Patch] README and infra/.env.example omit Email:Smtp:User and Password keys [README.md:69]
- [x] [Review][Patch] Swagger regression test does not assert auth routes present [tests/api.integration/SwaggerEndpointTests.cs:43]
- [x] [Review][Defer] OTP stored as SHA256 without per-challenge salt — pilot TTL mitigates; harden in security pass [apps/api/Infrastructure/Auth/OtpHasher.cs] — deferred, pre-existing
- [x] [Review][Defer] Refresh token stored as SHA256 digest not slow hash — Story 1.5 refresh scope [apps/api/Infrastructure/Auth/RefreshTokenStore.cs] — deferred, pre-existing
- [x] [Review][Defer] Timing-based email enumeration via login response latency — broader hardening pass [apps/api/Infrastructure/Auth/AuthService.cs:26] — deferred, pre-existing
- [x] [Review][Defer] No per-email OTP send throttle beyond IP rate limit — enhancement for abuse prevention [apps/api/Infrastructure/Auth/AuthService.cs:59] — deferred, pre-existing
- [x] [Review][Defer] HS256 signing key rotation not implemented — ops concern for production deploy [apps/api/Infrastructure/Auth/JwtTokenService.cs:26] — deferred, pre-existing
- [x] [Review][Defer] Refresh token no rotation-on-use or reuse detection — Story 1.5 scope [apps/api/Infrastructure/Auth/RefreshTokenStore.cs:11] — deferred, pre-existing
- [x] [Review][Defer] Rate limit bypass via proxy rotation — infrastructure/WAF concern [apps/api/Infrastructure/AuthServiceCollectionExtensions.cs:80] — deferred, pre-existing
- [x] [Review][Defer] Unbounded refresh tokens per user in Redis — cap in Story 1.5 [apps/api/Infrastructure/Auth/RefreshTokenStore.cs:11] — deferred, pre-existing
- [x] [Review][Defer] Test config applied in CreateHost not ConfigureWebHost — works; style deviation only [tests/api.integration/AuthWebApplicationFactory.cs:26] — deferred, pre-existing

### Change Log

- 2026-06-13: Story 1.4 — login + email OTP API, JWT/Redis auth infrastructure, integration tests, api-client regeneration.
- 2026-06-14: Code review — 3 decision-needed, 19 patch, 9 defer, 7 dismissed.
- 2026-06-14: Code review patches applied — 22 fixes, 18 integration tests passing, api-client regenerated.
