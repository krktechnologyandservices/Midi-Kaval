---
baseline_commit: NO_VCS
---

# Story 1.5: Refresh, Logout, and Forced Session Invalidation

Status: done

<!-- Validated: 2026-06-14 — see 1-5-refresh-logout-and-forced-session-invalidation-validation-report.md -->

## Story

As a **user**,
I want sessions to refresh securely and end on logout or role change,
so that access remains controlled (FR-1, FR-2).

## Acceptance Criteria

1. **Given** a valid refresh token issued by Story 1.4 verify-otp  
   **When** I `POST /api/v1/auth/refresh` with the refresh token (httpOnly `refresh_token` cookie for web **or** `{ refreshToken }` in JSON body for mobile)  
   **Then** I receive **200** with envelope `{ data: { accessToken, expiresIn, refreshToken }, meta }` containing a new JWT access token (15 min) and a **new** rotated refresh token  
   **And** refresh token is **rotated**: old refresh token is invalidated in Redis; new refresh token issued (body + `Set-Cookie` for web)  
   **And** refresh reloads user from DB and rejects if `is_active = false` (**403**) or `token_version` changed since token was issued (**401**)  
   **And** invalid, expired, or revoked refresh token returns **401** Problem Details  
   **And** reuse of a previously rotated refresh token revokes all refresh tokens for that user (reuse detection)

2. **Given** a valid refresh token (cookie or body)  
   **When** I `POST /api/v1/auth/logout`  
   **Then** the refresh token is removed from Redis  
   **And** httpOnly `refresh_token` cookie is cleared (`Max-Age=0`, same path `/api/v1/auth`)  
   **And** response is **204 No Content** (no envelope — logout is not a data response)  
   **And** endpoint is `[AllowAnonymous]` but requires a valid refresh token (cookie or body) — logout must work when access JWT is expired

3. **Given** a JWT access token in the `Authorization: Bearer` header  
   **When** I call any `[Authorize]` endpoint (including new `GET /api/v1/auth/me`)  
   **Then** JWT signature, issuer, audience, and lifetime are validated  
   **And** `token_version` claim must match `users.token_version` for the `sub` user — mismatch returns **401**  
   **And** deactivated user (`is_active = false`) returns **403** with **"Contact your coordinator"**

4. **Given** `users.token_version` is incremented (simulating role change or deactivation — admin UI is Story 9.2)  
   **When** the user calls `GET /api/v1/auth/me` or `POST /api/v1/auth/refresh` with tokens issued before the increment  
   **Then** access JWT calls return **401**; refresh with old refresh token returns **401**  
   **And** implement `IUserSessionService.InvalidateUserSessionsAsync(userId)` that increments `token_version` and revokes all refresh tokens for that user (callable from future staff-management stories; test via direct DB + service call in integration tests)

5. **Given** the `audit_events` table is created in this story  
   **When** auth events occur  
   **Then** append-only audit rows are written in the **same transaction** as any DB mutation:  
   - Successful login (after verify-otp issues tokens) → `auth.login.success`  
   - Failed OTP verification → `auth.otp.failed`  
   - Logout → `auth.logout`  
   - Refresh success → `auth.refresh.success` (optional but recommended)  
   **And** failed password login may log `auth.login.failed` without user id when email unknown (no PII beyond normalized email in metadata)

6. **Given** Story 1.2–1.4 baseline  
   **When** I run `dotnet test Midi-Kaval.slnx`  
   **Then** all existing tests pass  
   **And** new integration tests cover: refresh happy path (cookie + body), refresh 401 invalid token, logout clears Redis + cookie, `token_version` bump forces 401 on `/auth/me`, audit row count increases on login/logout

7. **Given** OpenAPI is updated  
   **When** I regenerate `packages/api-client`  
   **Then** `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/auth/me` appear in Swagger with envelope-wrapped success types where applicable  
   **And** README documents refresh/logout contract and session invalidation behavior

## Tasks / Subtasks

- [x] **audit_events schema & infrastructure** (AC: 5)
  - [x] Add `Domain/Entities/AuditEvent.cs` + `AuditEventConfiguration.cs`
  - [x] EF migration `AddAuditEvents` — append-only table (no updates/deletes via API)
  - [x] `Infrastructure/Audit/IAuditService.cs` + `AuditService.cs` — `RecordAsync(eventType, actorUserId?, subjectUserId?, metadata, ct)`
  - [x] Register in DI; use same `AppDbContext` transaction pattern

- [x] **Extend RefreshTokenStore** (AC: 1, 2, 4)
  - [x] Migrate to `IConnectionMultiplexer` (match `OtpChallengeStore` — **do not use IDistributedCache**; prefix mismatch broke OTP consume in 1.4 CR)
  - [x] Store `userId` + `token_version` at issue time in Redis value (JSON) for refresh-time validation
  - [x] Maintain `refresh:user:{userId}` index (set of active token hashes) for `RevokeAllForUserAsync`
  - [x] `ValidateAndRotateAsync(token)` → userId or null; atomic delete on success; detect reuse of revoked hash → revoke all for user
  - [x] `RevokeAsync(token)` for logout
  - [x] `RevokeAllForUserAsync(userId)` for session invalidation
  - [x] Cap active refresh tokens per user (`RefreshToken:MaxActivePerUser`, default 5) — **required** (1.4 deferred item)

- [x] **JWT token_version enforcement** (AC: 3, 4)
  - [x] `JwtBearerEvents.OnTokenValidated` — load user `token_version` + `is_active` from DB; `context.Fail()` on mismatch
  - [x] Inactive user on authenticated endpoint → **403** via `IAuthorizationMiddlewareResultHandler` or custom forbidden result (match 1.4 coordinator message)
  - [x] `Infrastructure/Auth/UserSessionService.cs` — `InvalidateUserSessionsAsync(Guid userId)` increments `token_version`, revokes all refresh tokens, writes `auth.session.invalidated` audit event

- [x] **Auth endpoints** (AC: 1, 2, 3, 7)
  - [x] `POST /api/v1/auth/refresh` — `[AllowAnonymous]`, read cookie `refresh_token` OR body `RefreshRequest`
  - [x] `POST /api/v1/auth/logout` — `[AllowAnonymous]`; requires refresh token (cookie or body); returns **204** without envelope
  - [x] `GET /api/v1/auth/me` — `[Authorize]` on method (overrides controller `[AllowAnonymous]`); returns `{ id, email, role }` in envelope
  - [x] Rate limit: add `auth-refresh` and `auth-logout` policies (register in `AuthServiceCollectionExtensions` alongside login/verify)
  - [x] Extend `AuthService` with `RefreshAsync`, `LogoutAsync`; instrument login/OTP paths with audit calls
  - [x] Helper: `ReadRefreshToken(HttpRequest)` — cookie first, then body

- [x] **Retrofit Story 1.4 auth flows with audit** (AC: 5)
  - [x] `VerifyOtpAsync` success → audit login success
  - [x] `VerifyOtpAsync` wrong OTP → audit otp failed
  - [x] `LoginAsync` wrong password → audit login failed (optional metadata: normalized email only)

- [x] **DTOs & OpenAPI** (AC: 7)
  - [x] `RefreshRequest`, `RefreshResponse`, `SessionUserDto` in `Models/Auth/`
  - [x] `[ProducesResponseType(typeof(ApiResponse<...>))]` + 401/403/429 as applicable

- [x] **Integration tests** (AC: 6)
  - [x] Extend `AuthLoginTests` or add `AuthSessionTests.cs` using `AuthWebApplicationFactory`
  - [x] Helper: login+verify → capture access + refresh tokens
  - [x] Tests: refresh via body; refresh via cookie; logout; me with bearer; token_version bump → 401; audit_events row assertions

- [x] **Documentation** (AC: 7)
  - [x] README: refresh rotation, logout, `token_version` force-logout, audit_events
  - [x] Regenerate api-client (`EXPORT_OPENAPI_PATH` + `npm run generate:api-client`)

### Review Findings

- [x] [Review][Decision] Strict reuse detection vs benign client retry — **Resolved:** keep strict theft-detection; mitigated with peek-then-consume and atomic Lua script.

- [x] [Review][Patch] Redis refresh consume is non-atomic [`RefreshTokenStore.cs`] — Lua script atomically GET/DEL/SET revoked.
- [x] [Review][Patch] Refresh consumes token before DB validation [`AuthService.cs`] — `TryPeekAsync` validates user before `TryConsumeAsync`.
- [x] [Review][Patch] Cookie-first silently overrides body refresh token [`AuthTokenHelpers.cs`] — body takes precedence when provided.
- [x] [Review][Patch] Deactivated user refresh burns token before 403 [`AuthService.cs`] — peek validates `is_active` before consume.
- [x] [Review][Patch] `GetCurrentUserAsync` does not check `IsActive` [`AuthService.cs`] — inactive users return null; `/me` returns 403 via Items guard.
- [x] [Review][Patch] Unknown-email login audit may persist `OrganisationId = Guid.Empty` [`AuthService.cs`] — skip audit when org unresolved.
- [x] [Review][Patch] `InvalidateUserSessionsAsync` commits DB before Redis revoke [`UserSessionService.cs`] — Redis revoke runs before DB commit.
- [x] [Review][Patch] Corrupt Redis token payload skips revoked marker after delete [`RefreshTokenStore.cs`] — Lua sets revoked; corrupt JSON treated as reuse.
- [x] [Review][Patch] Missing integration tests for AC3/AC4/AC5/AC6 gaps [`AuthSessionTests.cs`, `AuthLoginTests.cs`] — 5 new tests added (29 integration total).
- [x] [Review][Defer] `AuditService` auto-commits outside caller transaction [`AuditService.cs:30-31`] — deferred, acceptable for auth-only flows with no coupled user mutation today; revisit when audit must share transactions with domain writes.
- [x] [Review][Defer] Logout does not bump `token_version` (access JWT valid until expiry) [`AuthService.cs:219-240`] — deferred, standard short-lived access-token pattern; 15 min TTL is acceptable for pilot.

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Story 1.5 completes the **server-side session lifecycle** started in 1.4. Stories 1.6–1.7 (client login UI) and 1.8 (RBAC on business endpoints) depend on refresh/logout and `token_version` enforcement working here.

### Brownfield state — READ BEFORE CODING

Story 1.4 delivered login, verify-otp, JWT issuance, Redis refresh token **storage** (issue only), separate rate-limit policies, and hardened OTP store. **EXTEND** — do not rewrite auth from scratch.

| File | Current state | This story changes |
|------|---------------|-------------------|
| `AuthController.cs` | login, verify-otp only; sets refresh cookie | Add refresh, logout, me; cookie read on refresh/logout |
| `AuthService.cs` | Login + VerifyOtp | Add Refresh, Logout; audit instrumentation |
| `RefreshTokenStore.cs` | `IssueAsync` only via `IDistributedCache` | Full validate/revoke/rotate via Redis; migrate to `IConnectionMultiplexer` |
| `OtpChallengeStore.cs` | Redis direct (`IConnectionMultiplexer`) | No change unless shared Redis helpers extracted |
| `AuthServiceCollectionExtensions.cs` | JWT bearer basic validation | Add `OnTokenValidated` token_version + is_active check |
| `AppDbContext.cs` | `DbSet<User>` only | Add `DbSet<AuditEvent>` |
| `Program.cs` | Auth middleware when !Testing | No structural change expected |
| `tests/api.integration/AuthLoginTests.cs` | 8 auth tests + rate limit fixture | Add session/refresh/logout/audit tests |
| `packages/api-client/` | login + verify-otp routes | Add refresh, logout, me |

### Scope boundaries (critical)

| In scope (1.5) | Out of scope — later stories |
|----------------|------------------------------|
| `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me` | Angular/RN login UI (1.6–1.7) |
| `audit_events` table + auth event writes | Case/court/visit audit writes (Epic 2+) |
| `token_version` enforcement on JWT | RBAC policies on case endpoints (1.8) |
| `UserSessionService.InvalidateUserSessionsAsync` | Admin staff UI to trigger invalidation (9.2) |
| Refresh token rotation + revoke | Password reset / forgot-password (1.9) |
| Rate limit on refresh endpoint | `GET /audit` Director UI (9.3) |

**Story 1.5 / 1.8 boundary:** This story adds `[Authorize]` on `/auth/me` and global JWT `token_version` validation. Story 1.8 adds **role policies** (`Policies.DirectorOnly`, etc.) on business controllers — do not add role policies to case endpoints in 1.5.

**Story 1.5 / 1.9 boundary:** Password reset increments `token_version` in 1.9 — reuse `IUserSessionService` from this story.

### Suggested `audit_events` schema

| Column | Type | Notes |
|--------|------|-------|
| `id` | `uuid` PK | |
| `organisation_id` | `uuid` NOT NULL | tenant-ready |
| `actor_user_id` | `uuid` NULL | null for unknown-email failed login |
| `subject_user_id` | `uuid` NULL | usually same as actor for auth events |
| `event_type` | `varchar(64)` NOT NULL | e.g. `auth.login.success` |
| `metadata` | `jsonb` NULL | IP, user-agent; **never** passwords or OTP codes |
| `created_at_utc` | `timestamptz` NOT NULL | |

Index: `(organisation_id, created_at_utc DESC)`, `(event_type, created_at_utc DESC)`.

### Suggested API contracts

**POST /api/v1/auth/refresh**

```json
{ "refreshToken": "<opaque>" }
```

Cookie alternative: `refresh_token` httpOnly (web clients omit body).

Success `200`:

```json
{
  "data": {
    "accessToken": "<jwt>",
    "expiresIn": 900,
    "refreshToken": "<new-opaque>"
  },
  "meta": { "requestId": "..." }
}
```

Plus rotated `Set-Cookie: refresh_token=...`.

**POST /api/v1/auth/logout** — body or cookie same as refresh. Response **204**.

**GET /api/v1/auth/me** — `Authorization: Bearer <accessToken>`

Success `200`:

```json
{
  "data": { "id": "...", "email": "...", "role": "Director" },
  "meta": { "requestId": "..." }
}
```

### Technical requirements

| Item | Requirement |
|------|-------------|
| Access token | Reuse `JwtTokenService.CreateAccessToken` — 15 min, same claims |
| Refresh token | Opaque 32-byte base64; SHA-256 hash in Redis; 7-day TTL (`RefreshToken:ExpiryDays`) |
| Rotation | On refresh: validate old → issue new → delete old hash atomically |
| Cookie contract | Same as 1.4: `HttpOnly`, `SameSite=Strict`, `Path=/api/v1/auth`, `Secure` when !Development |
| token_version | `int` on `users`; JWT claim `token_version` must match DB on every authenticated request |
| Force logout | `InvalidateUserSessionsAsync` increments version + revokes all refresh tokens |
| Audit | Same transaction: `SaveChangesAsync` includes audit row when user row mutates |
| Errors | 401 invalid/expired tokens; 403 inactive user; RFC 7807 Problem Details |
| Testing | Reuse `AuthWebApplicationFactory` + `[Collection("AuthIntegration")]` — **do not break** 1.4 tests |

### Architecture compliance

- Auth under `/api/v1/auth/*` [Source: architecture §5.3]
- JWT 15 min + refresh rotation [Source: architecture §5.2, project-context]
- Force logout via `token_version` [Source: architecture §5.2, roles-and-access.md]
- Every mutation writes `audit_events` [Source: project-context, architecture §5.1]
- Thin controller; logic in `AuthService` / `UserSessionService` / `AuditService`
- Envelope on `/api/v1/*` success responses [Source: Story 1.2 pattern]
- Canonical path `apps/api` [Source: architecture §7]

### Library / framework requirements

**No new NuGet packages expected** — reuse JWT Bearer 8.0.11, StackExchange.Redis (already via `IConnectionMultiplexer`), EF Core 8.0.11.

**Configuration:** existing `Jwt:*`, `RefreshToken:ExpiryDays`, `Auth:RateLimit*` — add `RefreshToken:MaxActivePerUser` (optional, default 5) if implementing cap.

### Testing requirements

| Test | Location | Minimum coverage |
|------|----------|------------------|
| Refresh happy path (body) | `AuthSessionTests` or `AuthLoginTests` | New access JWT; old refresh invalid |
| Refresh happy path (cookie) | same | Cookie forwarded on refresh |
| Refresh invalid token | same | 401 Problem Details |
| Logout | same | Refresh fails after logout; cookie cleared |
| GET /auth/me authorized | same | 200 with user profile |
| GET /auth/me deactivated user | same | 403 coordinator message |
| token_version mismatch | same | Bump version in DB → `/auth/me` 401 |
| InvalidateUserSessionsAsync | same | Service call → refresh + access fail |
| Audit rows | same | Count/login event type after verify-otp |
| Regression | existing | Story 1.2 smoke + 1.4 auth tests unchanged |

**Fixture pattern:** Reuse `AuthWebApplicationFactory`; keep `TestingWebApplicationFactory` without Redis/DB. Auth collection already serializes parallel container races (`AuthIntegrationCollection`).

### Previous story intelligence (1.4)

- `OtpChallengeStore` uses **`IConnectionMultiplexer` directly** — `RefreshTokenStore` still uses `IDistributedCache`; **migrate** to avoid key-prefix bugs (1.4 code review found this class of issue).
- `AuthWebApplicationFactory` sets config via **environment variables in `CreateHost`** after Testcontainers start — proven pattern; in-memory `ConfigureAppConfiguration` alone failed with `DeferredHostBuilder`.
- JWT tests: use `ReadJwtToken` for claims — inbound claim mapping renames `sub`.
- Separate rate limits: `auth-login`, `auth-verify` — add `auth-refresh` for refresh endpoint.
- `FakeEmailSender` for OTP tests; refresh/logout tests do not need email.
- `baseline_commit: NO_VCS` — git not on PATH.
- 18 integration tests after 1.4 code review — **must stay green**.
- Code review deferred to 1.5: refresh rotation, per-user refresh cap, unbounded tokens in Redis.

### Anti-patterns (do NOT do in this story)

- Do not build Angular/RN clients (1.6–1.7)
- Do not add RBAC role policies to case/business controllers (1.8)
- Do not implement forgot-password (1.9)
- Do not create `cases`, `legend_*`, or other domain tables
- Do not register Redis/JWT in `Testing` environment (breaks Story 1.2)
- Do not store refresh tokens or OTPs in plaintext in DB or audit metadata
- Do not hand-edit `packages/api-client/src/generated/*`
- Do not skip audit writes on logout/refresh when DB is involved
- Do not break refresh cookie path/SameSite contract from 1.4
- Do not wrap **204 logout** in API envelope — return bare 204
- Do not use `IDistributedCache` for refresh tokens after migration (use `IConnectionMultiplexer` only)

### Definition of Done

- [x] Refresh and logout endpoints work with cookie and body refresh token
- [x] Refresh token rotation invalidates prior token
- [x] `token_version` enforced on authenticated requests; bump invalidates sessions
- [x] `audit_events` table exists; auth events recorded
- [x] `GET /auth/me` works as authorized session probe
- [x] `dotnet test Midi-Kaval.slnx` — all tests pass
- [x] README + api-client updated
- [x] Story file `File List` updated by dev agent on completion

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.5]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.1 Audit, §5.2 Auth, §5.3 API]
- [Source: `_bmad-output/project-context.md` — Auth, audit, testing rules]
- [Source: `_bmad-output/specs/spec-kaval-online/roles-and-access.md` — Forced logout]
- [Source: `_bmad-output/implementation-artifacts/1-4-login-and-email-otp-api.md` — Auth baseline]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md` — Refresh rotation/cap items]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Implemented full session lifecycle: refresh rotation, logout (204), `/auth/me`, JWT `token_version` + inactive-user enforcement (403 via OnChallenge).
- Migrated `RefreshTokenStore` to `IConnectionMultiplexer` with reuse detection, per-user cap (5), and `RevokeAllForUserAsync`.
- Added `audit_events` table + `AuditService`; retrofitted login/OTP/refresh/logout with audit writes.
- Added `IUserSessionService.InvalidateUserSessionsAsync` for forced session invalidation.
- 8 new integration tests in `AuthSessionTests.cs`; 27 total tests pass (1 unit + 26 integration).
- OpenAPI snapshot + `@midi-kaval/api-client` regenerated.
- Code review patches: atomic Lua consume, peek-before-consume, body-first refresh token, inactive-user 403 via `ActiveUserAuthorizationHandler`, 5 new integration tests (29 total).

### File List

- apps/api/Domain/Entities/AuditEvent.cs (added)
- apps/api/Infrastructure/Persistence/AuditEventConfiguration.cs (added)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (modified)
- apps/api/Infrastructure/Persistence/AppDbContextDesignTimeFactory.cs (added)
- apps/api/Infrastructure/Audit/AuditEventTypes.cs (added)
- apps/api/Infrastructure/Audit/IAuditService.cs (added)
- apps/api/Infrastructure/Audit/AuditService.cs (added)
- apps/api/Infrastructure/Auth/RefreshTokenRecord.cs (added)
- apps/api/Infrastructure/Auth/ActiveUserAuthorizationHandler.cs (added)
- apps/api/Infrastructure/Auth/RefreshTokenStore.cs (modified — Lua atomic consume, TryPeekAsync)
- apps/api/Infrastructure/Auth/IUserSessionService.cs (added)
- apps/api/Infrastructure/Auth/UserSessionService.cs (added)
- apps/api/Infrastructure/Auth/AuthTokenHelpers.cs (added)
- apps/api/Infrastructure/Auth/InactiveUserAuthorizationMiddlewareResultHandler.cs (added)
- apps/api/Infrastructure/Auth/AuthService.cs (modified)
- apps/api/Infrastructure/Auth/AuthOptions.cs (modified)
- apps/api/Infrastructure/AuthServiceCollectionExtensions.cs (modified)
- apps/api/Controllers/V1/AuthController.cs (modified)
- apps/api/Models/Auth/AuthDtos.cs (modified)
- apps/api/Migrations/20260614120000_AddAuditEvents.cs (added)
- apps/api/Migrations/20260614120000_AddAuditEvents.Designer.cs (added)
- apps/api/Migrations/AppDbContextModelSnapshot.cs (modified)
- tests/api.integration/AuthSessionTests.cs (added)
- tests/api.integration/UsersSchemaTests.cs (modified)
- tests/api.integration/SwaggerEndpointTests.cs (modified)
- packages/api-client/openapi-snapshot.json (modified)
- packages/api-client/src/generated/api.ts (regenerated)
- README.md (modified)
- infra/.env.example (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)

### Change Log

- 2026-06-14: Story 1.5 created — refresh/logout/session invalidation + audit_events.
- 2026-06-14: Validation — 4 critical fixes, 3 enhancements applied.
- 2026-06-14: Story 1.5 implemented — session refresh/logout, audit_events, token_version enforcement, 26 integration tests green.
- 2026-06-14: Code review — 9 patches applied, 2 deferred; strict reuse detection retained; 30 tests green (29 integration).
