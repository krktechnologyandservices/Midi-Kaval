---
baseline_commit: NO_VCS
---

# Story 1.8: RBAC Policies on Protected Endpoints

Status: done

<!-- Validated: 2026-06-14 — see 1-8-rbac-policies-on-protected-endpoints-validation-report.md -->

## Story

As a **system owner**,
I want every protected API mutation authorized by role policy,
so that client UI cannot bypass permissions (FR-2, NFR-2).

## Acceptance Criteria

1. **Given** ASP.NET Core authorization policies map to the four PRD roles (`Director`, `Coordinator`, `SocialWorker`, `CaseWorker`) plus composite policies used by architecture (`CoordinatorOrAbove`, `FieldWorker`, `DirectorOnly`)  
   **When** policies are registered in `AddMidiKavalAuth`  
   **Then** each policy uses `ClaimTypes.Role` from the JWT (issued by `JwtTokenService`) matched against `UserRoles` constants  
   **And** the default policy continues to require authenticated + `ActiveUserRequirement` (Stories 1.4–1.5)

2. **Given** a Social Worker access token  
   **When** they call a **Coordinator-only** protected endpoint (e.g. `POST /api/v1/rbac-probe/coordinator-mutation`)  
   **Then** API returns **403** `application/problem+json` with `title: "Forbidden"` and a generic `detail` (no role enumeration)  
   **And** the same applies in reverse: Director denied on `FieldWorker`-only probe; Coordinator denied on `DirectorOnly` probe

3. **Given** integration tests with users seeded for all four roles  
   **When** `dotnet test Midi-Kaval.slnx` runs  
   **Then** tests assert **at least one denied call per role** (4 roles × 1 forbidden scenario minimum)  
   **And** tests assert **at least one allowed call per role** on the matching probe endpoint (see matrix below)  
   **And** unauthenticated calls to probe endpoints return **401** (not 403)  
   **And** all existing 30 integration/unit tests still pass

   **Denied matrix (minimum):**

   | Role | Endpoint | Expected |
   |------|----------|----------|
   | Director | `GET /api/v1/rbac-probe/field-action` | 403 |
   | Coordinator | `GET /api/v1/rbac-probe/director-only` | 403 |
   | SocialWorker | `POST /api/v1/rbac-probe/coordinator-mutation` | 403 |
   | CaseWorker | `POST /api/v1/rbac-probe/coordinator-mutation` | 403 |

   **Allowed matrix (minimum):**

   | Role | Endpoint | Expected |
   |------|----------|----------|
   | Director | `POST /api/v1/rbac-probe/coordinator-mutation` + `GET director-only` | 204 / 200 |
   | Coordinator | `POST /api/v1/rbac-probe/coordinator-mutation` | 204 |
   | SocialWorker | `GET /api/v1/rbac-probe/field-action` | 200 |
   | CaseWorker | `GET /api/v1/rbac-probe/field-action` | 200 |

4. **Given** RBAC infrastructure is in place  
   **When** future Epic 2+ controllers add mutation endpoints  
   **Then** they apply `[Authorize(Policy = Policies.*)]` — never `[AllowAnonymous]` on data mutations  
   **And** `Policies.cs` + registration pattern is documented for copy-paste in README

5. **Given** auth endpoints from Stories 1.4–1.5  
   **When** this story ships  
   **Then** `AuthController` remains `[AllowAnonymous]` on login/verify/refresh/logout only  
   **And** `GET /api/v1/auth/me` stays `[Authorize]` (any authenticated active role) — unchanged behavior  
   **And** deactivated-user **403** ("Contact your coordinator") remains distinct from RBAC **403** (generic permission message)

6. **Given** Stories 1.6–1.7 client shells  
   **When** this story completes  
   **Then** **no web or mobile changes** are required (server enforcement only)  
   **And** `npm run test:web` and `npm run test:mobile` still pass unchanged

## Tasks / Subtasks

- [x] **Policy constants + registration** (AC: 1, 4)
  - [x] `Infrastructure/Auth/Policies.cs` — static policy name constants
  - [x] Extend `AuthServiceCollectionExtensions.AddAuthorization` with role policies:
    - `DirectorOnly` → `UserRoles.Director`
    - `CoordinatorOrAbove` → `Director`, `Coordinator`
    - `FieldWorker` → `SocialWorker`, `CaseWorker`
    - Per-role policies: `Director`, `Coordinator`, `SocialWorker`, `CaseWorker` (for fine-grained Epic 2+ use)
  - [x] All policies include `ActiveUserRequirement` (compose with `RequireAuthenticatedUser()`)

- [x] **RBAC forbidden Problem Details** (AC: 2, 5)
  - [x] Extend `InactiveUserAuthorizationMiddlewareResultHandler` (or rename to `MidiKavalAuthorizationMiddlewareResultHandler`) to return RFC 7807 for **role-based** `Forbid` results  
  - [x] Handler order: (1) inactive user → deactivated 403; (2) `authorizeResult.Forbidden` → RBAC 403; (3) else delegate to default handler (401 for `Challenged`)  
  - [x] RBAC `detail`: **"You do not have permission to perform this action."** (distinct from deactivated message)  
  - [x] Preserve existing inactive-user branch (`AuthService.DeactivatedMessage`); update DI registration if class renamed

- [x] **RBAC probe controller (scaffold until Epic 2)** (AC: 2, 4)
  - [x] `Controllers/V1/RbacProbeController.cs` — `[ApiExplorerSettings(IgnoreApi = true)]` (keep OpenAPI/api-client clean)
  - [x] `POST /api/v1/rbac-probe/coordinator-mutation` — `[Authorize(Policy = Policies.CoordinatorOrAbove)]` — returns **204** on success
  - [x] `GET /api/v1/rbac-probe/director-only` — `[Authorize(Policy = Policies.DirectorOnly)]`
  - [x] `GET /api/v1/rbac-probe/field-action` — `[Authorize(Policy = Policies.FieldWorker)]`
  - [x] **No `[AllowAnonymous]`** on probe mutations; probe routes are the pattern Epic 2 controllers will copy

- [x] **Integration tests** (AC: 3)
  - [x] `tests/api.integration/RbacAuthorizationTests.cs` — **`[Collection("AuthIntegration")]`** (same as `AuthLoginTests` / `AuthSessionTests`)
  - [x] Extend `AuthTestHelpers.LoginAndVerifyAsync(client, emailSender, email, password)` — do not duplicate OTP flow
  - [x] Seed three extra users in test `IAsyncLifetime` (Coordinator, SocialWorker, CaseWorker) with `AuthTestData.OrganisationId`; **idempotent** upsert on `(organisation_id, email)`; shared password `AuthTestData.Password`
  - [x] Denied + allowed matrices per AC3
  - [x] `Unauthenticated_Probe_Returns401` test
  - [x] Assert Problem Details `detail` + `application/problem+json` on forbidden responses

- [x] **Controller audit** (AC: 4)
  - [x] Confirm no `[AllowAnonymous]` on non-auth mutation routes today (only `AuthController` login/verify/refresh/logout — auth surface, not data mutations)
  - [x] `HealthController`, `MetaController`, `DiagnosticsController` remain public read/diagnostic — **not** RBAC scope

- [x] **Documentation** (AC: 4, 6)
  - [x] README API section: policy list, `[Authorize(Policy = ...)]` example, probe endpoints marked dev/test scaffold
  - [x] Note: Epic 2+ replaces probe with real business endpoints using same policies

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Stories 1.4–1.5 delivered auth + session lifecycle; 1.6–1.7 delivered **client-side role routing** (supervisor vs field). **Story 1.8 is the server-side FR-2 gate** — API must reject unauthorized roles regardless of client UI. Story 1.9 (password reset) follows; Epic 2 case APIs will apply these policies to real mutations.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `AuthServiceCollectionExtensions.cs` | Default policy = authenticated + `ActiveUserRequirement`; **no role policies** | Register `Policies.*` role policies |
| `JwtTokenService.cs` | Emits `ClaimTypes.Role` from `user.Role` | **No change** — policies consume existing claim |
| `UserRoles.cs` | Four string constants matching `AppRole` enum | **No change** — single source in API domain |
| `InactiveUserAuthorizationMiddlewareResultHandler.cs` | 403 Problem Details for deactivated users only | Add RBAC forbidden branch |
| `AuthController.cs` | `[AllowAnonymous]` class; `[Authorize]` on `me` only | **No change** to auth routes |
| Controllers | Health, Meta, Diagnostics, Auth only — **no business mutations** | Add `RbacProbeController` scaffold |
| `tests/api.integration` | 29 integration + 1 unit = **30** .NET tests | Add `RbacAuthorizationTests` |
| `apps/web`, `apps/mobile` | Client role guards (1.6/1.7) | **No changes** |

**Do not break:**
- `TestingWebApplicationFactory` — Testing env skips auth middleware (unchanged)
- Auth integration tests (`AuthLoginTests`, `AuthSessionTests`) — Director seed user `director@pilot.example`
- Web cookie refresh vs mobile body refresh contracts (Stories 1.5–1.7)
- Deactivated 403 message: **"Contact your coordinator"** on auth paths

### Policy matrix (architecture §6.3)

| Policy constant | Roles allowed | Typical use (Epic 2+) |
|-----------------|---------------|------------------------|
| `Policies.DirectorOnly` | Director | Audit log, org settings, claim approval |
| `Policies.CoordinatorOrAbove` | Director, Coordinator | Case create, reports, staff management |
| `Policies.FieldWorker` | SocialWorker, CaseWorker | Visits, field notes, travel claims |
| `Policies.Director` | Director only | Alias for fine-grained checks |
| `Policies.Coordinator` | Coordinator only | Rare — prefer composite |
| `Policies.SocialWorker` | SocialWorker only | Role-specific mutations |
| `Policies.CaseWorker` | CaseWorker only | Role-specific mutations |

JWT role claim values **must** match `UserRoles` / `AppRole` strings exactly (`Director`, not `director`).

### Authorization registration pattern

```csharp
// Infrastructure/Auth/Policies.cs
public static class Policies
{
    public const string DirectorOnly = nameof(DirectorOnly);
    public const string CoordinatorOrAbove = nameof(CoordinatorOrAbove);
    public const string FieldWorker = nameof(FieldWorker);
    // ...
}

// AuthServiceCollectionExtensions.cs — inside AddAuthorization
options.AddPolicy(Policies.CoordinatorOrAbove, policy =>
    policy.RequireAuthenticatedUser()
        .RequireRole(UserRoles.Director, UserRoles.Coordinator)
        .AddRequirements(new ActiveUserRequirement()));
```

Controller usage (Epic 2 pattern):

```csharp
[Authorize(Policy = Policies.CoordinatorOrAbove)]
[HttpPost]
public IActionResult CreateCase(...) { ... }
```

**Never** `[AllowAnonymous]` on data mutations. Auth endpoints are the only anonymous surface.

### RBAC 403 vs deactivated 403

| Scenario | HTTP | `detail` |
|----------|------|----------|
| Deactivated user (auth or `/me`) | 403 | "Contact your coordinator" |
| Valid user, wrong role | 403 | "You do not have permission to perform this action." |
| Missing/expired JWT | 401 | "Invalid access token." / unauthorized |

Extend the existing `IAuthorizationMiddlewareResultHandler` — do **not** add a second handler.

```csharp
// Pseudocode — inactive branch MUST stay first
if (context.Items.ContainsKey(InactiveUserAuthConstants.InactiveUserItemKey) && !authorizeResult.Succeeded)
    → 403 Problem Details (DeactivatedMessage)

else if (!authorizeResult.Succeeded && authorizeResult.Forbidden)
    → 403 Problem Details ("You do not have permission...")

else
    → _defaultHandler (401 for unauthenticated / Challenged)
```

**Deactivated user on RBAC probe:** JWT validates but `ActiveUserRequirement` fails → inactive branch fires (same as `/auth/me`), not RBAC message.

### RBAC probe controller (temporary scaffold)

Purpose: prove policies work **before** Epic 2 case endpoints exist. Mark `[ApiExplorerSettings(IgnoreApi = true)]` so OpenAPI/api-client are unchanged.

| Route | Method | Policy | Success |
|-------|--------|--------|---------|
| `/api/v1/rbac-probe/coordinator-mutation` | POST | `CoordinatorOrAbove` | 204 |
| `/api/v1/rbac-probe/director-only` | GET | `DirectorOnly` | 200 `{ data: { ok: true } }` |
| `/api/v1/rbac-probe/field-action` | GET | `FieldWorker` | 200 `{ data: { ok: true } }` |

Remove or repoint probe routes when Epic 2 controllers ship — policies remain.

### Integration test strategy

Reuse `AuthWebApplicationFactory` (PostgreSQL + Redis Testcontainers, `FakeEmailSender`).

**Seed three extra test users** in test setup (not production seed); Director already exists via `AuthTestData`:

| Email | Role |
|-------|------|
| `coordinator@rbac.test` | Coordinator |
| `social@rbac.test` | SocialWorker |
| `case@rbac.test` | CaseWorker |

Use `IPasswordHasher<User>` + `AuthTestData.OrganisationId` + password `AuthTestData.Password`. Upsert pattern: skip insert if `(organisation_id, email)` exists.

Helper flow: extend `AuthTestHelpers.LoginAndVerifyAsync` to accept `email` parameter → `POST login` → OTP from `FakeEmailSender` → `POST verify-otp` → bearer token.

Assert forbidden responses parse as Problem Details (`application/problem+json`).

### Previous story intelligence (1.5–1.7)

- **JWT claims:** `sub`, `email`, `ClaimTypes.Role`, `organisation_id`, `token_version` — policies use `Role` only; `ActiveUserAuthorizationHandler` handles inactive separately.
- **Story 1.6 explicitly deferred RBAC** to this story — do not add policies in web guards beyond existing client UX routing.
- **Story 1.7 mobile** uses `shouldAttachBearer()` — unaffected; server returns 403 on forbidden probes if mobile ever calls them.
- **Review pattern:** run adversarial code review after implementation; extend authorization handler carefully to avoid breaking deactivated 403 tests in `AuthSessionTests`.

### Scope boundaries

| In scope (1.8) | Out of scope |
|----------------|--------------|
| Policy constants + DI registration | Epic 2 case CRUD endpoints |
| RBAC probe scaffold controller | Web/mobile UI changes |
| RFC 7807 forbidden for role denial | Password reset (Story 1.9) |
| Integration tests per role | Per-resource authorization (case assignment) — Epic 2+ |
| README policy documentation | OpenAPI/api-client regeneration (probe hidden from Swagger) |

### Project structure (files to add/update)

```
apps/api/
├── Infrastructure/Auth/
│   ├── Policies.cs                          # NEW
│   ├── AuthServiceCollectionExtensions.cs   # UPDATE — register policies
│   └── InactiveUserAuthorizationMiddlewareResultHandler.cs  # UPDATE — RBAC 403
├── Controllers/V1/
│   └── RbacProbeController.cs               # NEW — scaffold
tests/api.integration/
├── RbacAuthorizationTests.cs                # NEW
├── AuthSessionTests.cs                        # UPDATE — extend AuthTestHelpers.LoginAndVerifyAsync(email)
README.md                                    # UPDATE — RBAC section
```

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | All existing **30** + new RBAC tests pass |
| Web | `npm run test:web` | **15** pass — no changes |
| Mobile | `npm run test:mobile` | **15** pass — no changes |

No api-client regeneration unless probe endpoints are added to Swagger (they should **not** be).

### Definition of Done

- [x] `Policies.cs` registered; composite + per-role policies include `ActiveUserRequirement`
- [x] RBAC 403 returns RFC 7807; deactivated 403 unchanged in auth tests
- [x] Probe controller enforces policies; hidden from Swagger
- [x] Integration tests: 4 denied + 4 allowed + 1 unauthenticated 401
- [x] `dotnet test Midi-Kaval.slnx` — all tests pass (39 total: 1 unit + 38 integration)
- [x] `npm run test:web` + `npm run test:mobile` — unchanged pass
- [x] README RBAC section updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.8, FR-2]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.2 Auth, §6.2 HTTP codes, §6.3 Authorization pattern]
- [Source: `_bmad-output/specs/spec-kaval-online/roles-and-access.md` — role matrix]
- [Source: `_bmad-output/project-context.md` — Policy-based auth; never AllowAnonymous on mutations]
- [Source: `_bmad-output/implementation-artifacts/1-5-refresh-logout-and-forced-session-invalidation.md` — JWT claims, ActiveUser handler, 403 inactive]
- [Source: `_bmad-output/implementation-artifacts/1-6-angular-pwa-shell-with-login-and-otp-flow.md` — client role routing; deferred RBAC to 1.8]
- [Source: `_bmad-output/implementation-artifacts/1-7-react-native-shell-with-login-and-otp-flow.md` — inverse client routing; no API changes]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- `Policies.cs` — seven policies (3 composite + 4 per-role) with `ActiveUserRequirement`.
- `AuthServiceCollectionExtensions` — `AddActiveUserRolePolicy` helper registers all policies.
- `InactiveUserAuthorizationMiddlewareResultHandler` — RBAC forbidden branch before default handler.
- `RbacProbeController` — three probe routes, `[ApiExplorerSettings(IgnoreApi = true)]`.
- `RbacAuthorizationTests` — 9 tests: 4 denied, 4 allowed, 1 unauthenticated 401.
- `AuthTestHelpers.LoginAndVerifyAsync` extended with email/password overload.
- README RBAC section with policy table and controller example.
- Tests: 40 .NET (1+39), 15 web, 15 mobile — all pass; no client changes.
- Code review (2026-06-15): RoleClaimType, deactivated probe test, swagger exclusion, test user upsert, RFC 7807 Type.

### Code Review Findings (2026-06-15)

| Severity | Finding | Resolution |
|----------|---------|------------|
| HIGH | JWT `RoleClaimType` not explicit — policies could break on claim mapping changes | `RoleClaimType = ClaimTypes.Role` on JwtBearer |
| HIGH | Deactivated user on RBAC probe untested — handler order regression risk | `DeactivatedUser_RbacProbe_Returns403_DeactivatedMessage` |
| MEDIUM | RBAC test users not reset on re-run (stale role/inactive) | Upsert updates role, `IsActive`, password, `TokenVersion` |
| MEDIUM | Unauthenticated POST probe not covered | Extended `Unauthenticated_Probe_Returns401` for POST |
| LOW | Forbidden Problem Details missing `type` URI | `AuthorizationProblemTypes.Forbidden` on handler |
| LOW | Swagger could leak probe routes | `Assert.DoesNotContain` in `SwaggerEndpointTests` |

### File List

- apps/api/Infrastructure/Auth/Policies.cs
- apps/api/Infrastructure/Auth/InactiveUserAuthorizationMiddlewareResultHandler.cs
- apps/api/Infrastructure/AuthServiceCollectionExtensions.cs
- apps/api/Controllers/V1/RbacProbeController.cs
- tests/api.integration/RbacAuthorizationTests.cs
- tests/api.integration/AuthSessionTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- README.md

### Change Log

- 2026-06-14: Story 1.8 created — RBAC policy infrastructure + probe scaffold + per-role integration tests.
- 2026-06-14: Validation pass — handler order, test matrices, AuthIntegration collection, helper reuse.
- 2026-06-14: Story 1.8 implemented — policies, probe controller, RBAC tests, README.
- 2026-06-15: Code review patches applied — 40 .NET tests, status done.
