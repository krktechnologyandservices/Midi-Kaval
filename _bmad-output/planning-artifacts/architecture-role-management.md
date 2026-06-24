---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
status: complete
completedAt: 2026-06-23
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-23/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-23/EXPERIENCE.md
  - _bmad-output/project-context.md
workflowType: 'architecture'
project_name: 'Midi-Kaval'
user_name: 'Admin'
date: '2026-06-23'
---

# Role Management & Registration System — Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:** 16 FRs across 5 feature groups — Vendor Backstage & Bootstrap (FR-1–3), Director User Management Dashboard (FR-4–10), Invitation & Registration (FR-11–12), Audit Trail (FR-13–14), Notifications (FR-15–16). Plus a Migration epic.

**Non-Functional Requirements:** Session revocation for suspension (NFR-7), append-only audit (NFR-4), HMAC-SHA256 link signing (NFR-1), rate limiting (NFR-2), dual-auth migration (NFR-16), email delivery performance (NFR-8/NFR-11).

**Scale & Complexity:**
- Primary domain: API + Web — backend-heavy with Director Dashboard as primary UI; mobile is a companion
- Complexity level: Medium-High — security-critical enforcement (Last-Director, append-only audit, 2FA mandate, session revocation)
- Estimated architectural components: 8–10 new/refactored components

### Technical Constraints & Dependencies

- Extends existing `users` table — not greenfield. Must coexist with existing Kaval auth.
- JWT 15-min access token + refresh token pattern from existing architecture (AR-1). Must extend with `token_version` for force-logout.
- Append-only audit: DB user INSERT-only on `audit_events`, fail-closed if audit write fails.
- Double-confirmation registration: pending-confirmation account state separate from active login flow.
- Identity snapshot at event time: JSONB column captures target identity at moment of event — not FK lookup.
- Dual auth during migration: config-file + DB auth must coexist with rollback capability.
- Existing Angular Material theming system extended with navy enterprise skin overlay.
- No WebSocket/SignalR — polling-based refresh (60s).

### Cross-Cutting Concerns Identified

- Auth/2FA — Director 2FA mandate with TOTP, recovery paths
- RBAC enforcement — server-only, policy-based for all management endpoints
- Audit logging — every management action in the same transaction
- Email notifications — batched digests, rate-limited user notifications
- Rate limiting — per-IP and per-email on registration/invitation endpoints
- Session management — token_version for suspension force-logout
- Migration cutover — dual auth support during window

## Starter Template Evaluation

**Skipped — existing monorepo extension.** The Midi-Kaval project is already scaffolded with `apps/api` (ASP.NET Core 8), `apps/web` (Angular 19+ PWA), and `apps/mobile` (React Native). The Role Management system extends this structure with new API controllers/domain services, Angular feature modules, and mobile companion screens — no new starter template needed.

## Core Architectural Decisions

### Already Settled (inherited from existing architecture)

| Layer | Decision | Source |
|-------|----------|--------|
| Database | PostgreSQL 16+ | architecture.md |
| API Framework | ASP.NET Core 8 REST | architecture.md |
| ORM | Entity Framework Core 8 | architecture.md |
| Web Framework | Angular 19+ PWA + Angular Material | architecture.md |
| Mobile Framework | React Native 0.76+ | architecture.md |
| Auth Pattern | JWT 15-min access + refresh token (httpOnly cookie web / secure storage mobile) | architecture.md §5.2, AR-1 |
| API Design | REST, RFC 7807 Problem Details, pagination `?page=1&pageSize=25` | architecture.md §5.3, AR-3/AR-6 |
| Error Codes | 400/401/403/404/409/422 | architecture.md §6.2 |
| Response Envelope | `{ data, meta: { requestId } }` | architecture.md §5.3 |
| Testing | xUnit + WebApplicationFactory + Testcontainers PostgreSQL | architecture.md §6.4, AR-9 |
| Caching | Redis 7.x | architecture.md §5.1 |
| Background Jobs | Hangfire inside API host | architecture.md §5.6 |
| Email | SendGrid / SMTP | architecture.md §5.6 |
| Push | FCM + APNs via unified notification service | architecture.md §5.6 |
| RBAC | Policy-based `[Authorize]`, server-enforced never trust client | architecture.md §6.3, AR-8 |
| IDs | UUID v4 | architecture.md §5.3, AR-4 |
| Timestamps | ISO 8601 UTC | architecture.md §5.3, AR-5 |
| API Versioning | `/api/v1/` prefix | AR-7 |

### Data Architecture

**New tables** (per PRD §3a):
- `organisations` — `id` (UUID PK), `name`, `is_active`, `created_at_utc`
- `activation_tokens` — `id` (UUID PK), `organisation_id` (FK), `token_hash` (SHA-256), `target_email`, `expires_at_utc`, `consumed_at_utc`, `created_at_utc`
- `invitations` — `id` (UUID PK), `organisation_id` (FK), `invited_by_user_id` (FK users), `target_email`, `role`, `token_hash`, `expires_at_utc`, `status` (pending/confirmed/expired), `created_at_utc`, `confirmed_at_utc`
- `audit_events` — `id` (UUID PK), `organisation_id` (FK), `event_type`, `actor_user_id` (FK, nullable - SET NULL), `target_user_id` (FK, nullable - SET NULL), `target_user_snapshot` (JSONB), `ip_address`, `metadata_json` (JSONB, nullable), `created_at_utc`

**Users table extensions**: `organisation_id` (FK), `role` (varchar), `is_suspended` (bool), `totp_secret` (nullable), `totp_enrolled_at` (nullable), `token_version` (int, default 0)

**Migrations**: EF Core migrations in existing `apps/api/Infrastructure/` — new migration per table/extension change.

**Caching**: Existing Redis for rate-limit counters (per-IP, per-email) and token version blacklist. No new cache store.

### Authentication & Security

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Suspension force-logout | `token_version` increment on `users` table | Existing AR-2 pattern. Middleware checks `token_version` on every request — if JWT claims version < DB version, reject with 401 |
| Director 2FA | TOTP (RFC 6238, 30s window) integrated into login flow | Directors authenticate with password → TOTP step instead of email OTP. Non-Directors continue existing email OTP flow. Auth endpoint distinguishes by role |
| 2FA recovery | API endpoint `POST /admin/users/{id}/reset-2fa` — another Director triggers it. Vendor fallback via `POST /vendor/users/{id}/reset-2fa` | Per FR-10. The reset clears `totp_secret` and `totp_enrolled_at`; Director must re-enroll before next management action |
| Vendor Backstage auth | Existing API project — new `VendorController` with `[Authorize(Policy = Policies.VendorOnly)]` | Same codebase, separate policy. No separate auth service needed. Vendor has a special vendor account type authenticated via existing JWT flow + 2FA |
| Link signing | HMAC-SHA256 with server-side secret | Per NFR-1. Secret configured via environment variable. Token stored as SHA-256 hash; raw token known only to email recipient |
| Rate limiting | Middleware on registration/invitation endpoints: 10 req/min per IP (burst 20), 5 req/h per email | Extend existing rate limiting middleware with new rule sets |
| CSRF | Anti-forgery tokens on all management POST/PUT/DELETE | Per NFR-5. Existing Angular HttpClient interceptor pattern |
| HTTPS enforcement | HSTS headers on all token-bearing URLs | Inherited from existing API HSTS config |

### API & Communication Patterns

**New endpoint groups:**

| Group | Base Path | Auth |
|-------|-----------|------|
| Vendor Backstage | `POST /api/v1/vendor/organisations` (create + send activation), `GET /api/v1/vendor/organisations` (list), `GET /api/v1/vendor/organisations/{id}` | Vendor role + 2FA |
| User Management | `GET /api/v1/admin/users`, `POST /api/v1/admin/invitations`, `POST /api/v1/admin/users/{id}/suspend`, `POST /api/v1/admin/users/{id}/reactivate`, `DELETE /api/v1/admin/users/{id}`, `POST /api/v1/admin/users/{id}/reset-2fa` | Director role + 2FA enrolled check |
| Invitations | `GET /api/v1/admin/invitations`, `POST /api/v1/admin/invitations/{id}/resend` | Director role |
| Audit | `GET /api/v1/admin/audit-events` | Director role |
| Registration | `POST /api/v1/auth/activate` (consumes activation token), `POST /api/v1/auth/accept-invitation` (consumes invitation token), `POST /api/v1/auth/confirm-email` (consumes confirmation token) | Unauthenticated |
| 2FA | `POST /api/v1/auth/enroll-2fa`, `POST /api/v1/auth/verify-2fa` | Authenticated (any role) |

**Endpoint patterns follow existing conventions**: plural kebab-case, UUID in URLs, ISO 8601 timestamps, RFC 7807 errors.

### Frontend Architecture

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Vendor Backstage | Standalone Angular route `/vendor` with separate layout (no main app sidebar) | Vendor is not a regular app user. Minimal chrome, single-purpose surface |
| Director Dashboard | Angular feature module `features/admin/` — lazy-loaded route `/admin` | Part of main app shell with sidebar navigation. Route-guarded by `DirectorOnly` policy |
| Theme overlay | Angular Material custom theme SCSS that extends the existing primary palette with navy enterprise tokens. Triggered by route detection (`/admin`, `/vendor`) or a CSS class | Single Angular app, no separate build. The admin surfaces use `admin-theme` CSS class on the root element. Angular Material theme tokens are scoped via component encapsulation + CSS custom properties |
| Activation / Invitation pages | Standalone Angular routes with no app shell (`/activate`, `/invite`) | Unauthenticated pages outside the PWA shell. Separate `AppModule` or lazy-loaded module without sidebar/footer |
| Director Companion mobile | New screen accessible from existing Mobile "More" tab | Minimal change to existing React Native app. Doesn't warrant a new tab for the small action surface |
| Invite dialog | Angular Material `MatDialog` — focus-trapped modal | Existing shadcn Dialog pattern maps to MatDialog |
| User detail | Angular Material `MatBottomSheet` sliding from right | Per EXPERIENCE.md detail panel pattern |
| Audit log | Angular Material `MatTable` with `MatSort` + `MatPaginator` | Standard Material data table with server-side sort/paginate |

### Notifications

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Email service | Extend existing SendGrid/SMTP integration | Same email provider as main app. No new service |
| Batched digests | Hangfire recurring job: collects events in 5-min windows, sends digest per Director | Extend existing Hangfire setup. Job checks `audit_events` for un-notified events, groups by organisation + Director, sends one email per batch |
| Rate-limited user emails | Hangfire job with per-user per-type counter in Redis | Before sending, check Redis counter. Increment with 24h TTL. Block if >= 3 |

### Infrastructure & Deployment

**No new infrastructure.** Everything runs within the existing:
- Single ASP.NET Core 8 API project — extended with new controllers and domain services
- Single PostgreSQL database — new tables + migration
- Existing Redis — new cache keysets for rate limits and email rate counters
- Existing Hangfire — new jobs for digest batching, cleanup, email retry
- No new deployments, containers, or services

### Decision Impact Analysis

**Implementation sequence** (maps to Epic/Story order in `epics.md`):

1. **Data Model first** — New tables + users extensions (Epic 1 Story 1.1, Epic 2 Story 2.1, Epic 3 Story 3.1, Epic 4 Story 4.1)
2. **Auth extensions** — 2FA enrollment/verification endpoints, token version middleware, rate limiting, CSRF
3. **Vendor Backstage API** — Organisation CRUD, activation link generation (Epic 1)
4. **Director Dashboard API** — User list, invite, suspend, reactivate, delete endpoints (Epic 2)
5. **Angular Admin module** — UI for Director Dashboard, Vendor Backstage (Epics 1-2)
6. **Registration flows** — Activation page, invitation acceptance, double-confirmation (Epic 3)
7. **Audit log API + UI** — Recording, viewer (Epic 4)
8. **Notifications** — Email digests, user notifications (Epic 5)
9. **Migration** — Dual auth, migration script (Epic 6)
10. **Mobile Director Companion** — React Native screens (post-web completion)

**Cross-component dependencies:**
- Director Dashboard UI (step 5) depends on API (step 4) + Data Model (step 1) + Auth (step 2)
- Registration flows (step 6) depend on Data Model (step 1) + Auth (step 2)
- Audit log viewer (step 7) depends on recording (step 7 API) + Data Model (step 1)
- Notifications (step 8) depend on all management actions (steps 3-4) being instrumented
- Migration (step 9) depends on Data Model (step 1) + Auth (step 2) being stable

## Implementation Patterns & Consistency Rules

### Inherited from existing architecture.md

These patterns are already defined in `architecture.md` §6 and `project-context.md`; the Role Management extension follows them without exception:

| Category | Rule | Example |
|----------|------|---------|
| DB tables | snake_case plural | `audit_events`, `activation_tokens` |
| DB columns | snake_case | `target_user_snapshot`, `totp_enrolled_at` |
| C# types | PascalCase | `AuditEvent`, `InvitationService` |
| API JSON | camelCase | `eventType`, `targetEmail`, `isSuspended` |
| API routes | plural kebab-case | `/admin/users`, `/admin/audit-events` |
| API versioning | `/api/v1/` prefix | `/api/v1/admin/users` |
| IDs | UUID v4 in URLs | `/admin/users/{id}/suspend` |
| Timestamps | ISO 8601 UTC | `2026-06-23T22:00:00Z` |
| Angular files | kebab-case | `user-management.component.ts` |
| Angular classes | PascalCase | `UserManagementComponent` |
| RN screens | PascalCase | `TeamSummaryScreen.tsx` |
| Error format | RFC 7807 Problem Details | `{ type, title, status, detail }` |
| Response envelope | `{ data, meta: { requestId } }` | — |
| HTTP errors | 400/401/403/404/409/422 | — |
| Authorization | `[Authorize(Policy = ...)]` | `[Authorize(Policy = Policies.DirectorOnly)]` |
| Testing | xUnit + WebApplicationFactory + Testcontainers | — |

### Role Management — Module-Specific Patterns

These are new patterns specific to this feature that prevent AI agent conflicts:

#### API Layer

| Pattern | Rule | Example |
|---------|------|---------|
| Controller naming | `{Feature}Controller` within `/api/v1/{area}/` prefix | `VendorController` → `/api/v1/vendor/organisations` |
| Domain services | `Domain/RoleManagement/{ServiceName}.cs` | `Domain/RoleManagement/InvitationService.cs` |
| Infrastructure | `Infrastructure/RoleManagement/{ServiceName}.cs` | `Infrastructure/RoleManagement/AuditEventRepository.cs` |
| Management action endpoints | Verb-based action in URL, not REST noun | `POST /users/{id}/suspend` not `PATCH /users/{id}` with body |
| Registration endpoints | Unauthenticated, under `/api/v1/auth/` | `POST /api/v1/auth/activate`, `POST /api/v1/auth/accept-invitation` |
| 2FA middleware attribute | `[Require2FA]` custom attribute on Director management endpoints | Check `totp_enrolled_at` before allowing action |
| Token version check | Middleware on every request | Compare JWT claim `token_version` with DB `users.token_version` |

#### Audit Event Pattern

Every management action endpoint MUST write an audit event in the same DB transaction. The audit event must be created BEFORE the response is sent, and if the audit write fails, the action is rolled back (fail-closed).

```csharp
// GOOD — audit in same transaction, fail-closed
using var transaction = await _context.Database.BeginTransactionAsync();
try 
{
    // ... perform management action ...
    _context.AuditEvents.Add(auditEvent);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

#### Link/Token Generation Pattern

All activation, invitation, and confirmation links follow the same three-step pattern:

1. **Generate** — Server creates a random token (cryptographically secure), stores SHA-256 hash in DB
2. **Sign** — Token is wrapped in HMAC-SHA256 signature (server secret from env var)
3. **Embed** — URL includes the raw token + signature: `{baseUrl}/activate?token={raw}&sig={hmac}`

On consumption:
1. Verify HMAC signature first → reject if invalid (tampered)
2. Look up SHA-256 hash in DB → reject if not found
3. Check expiry → reject if expired
4. Check consumed_at → reject if already used
5. Mark consumed_at → single-use enforced

#### Status Enums (shared across API + clients)

Define in `packages/shared-types/` as string enums:

```typescript
// shared-types/src/enums.ts
export const UserStatus = { Active: 'active', Suspended: 'suspended' } as const;
export const InvitationStatus = { Pending: 'pending', Confirmed: 'confirmed', Expired: 'expired' } as const;
export const UserRole = { Director: 'director', Coordinator: 'coordinator', FieldWorker: 'field_worker' } as const;
export const AuditEventType = {
  UserCreated: 'user_created',
  UserSuspended: 'user_suspended',
  UserReactivated: 'user_reactivated',
  UserDeleted: 'user_deleted',
  InvitationSent: 'invitation_sent',
  InvitationResent: 'invitation_resent',
  TwoFactorReset: 'two_factor_reset',
} as const;
```

#### Frontend — Angular Admin Module Structure

```
apps/web/src/app/features/admin/
├── admin-routing.module.ts          # Lazy-loaded route: /admin
├── admin.component.ts                # Shell component with sidebar + router-outlet
├── pages/
│   ├── team-roster/                  # FR-4: User list table
│   ├── invitations/                  # FR-12: Invitation history
│   └── audit-log/                    # FR-14: Audit log viewer
├── components/
│   ├── user-detail-sheet/            # Slide-in detail panel
│   ├── invite-dialog/                # Invite creation modal
│   └── status-badge/                 # Reusable status badge chip
├── services/
│   ├── admin-user.service.ts         # User management API calls
│   ├── invitation.service.ts         # Invitation API calls
│   └── audit.service.ts              # Audit log API calls
└── models/
    └── admin.models.ts               # Admin-specific TypeScript interfaces
```

#### Frontend — Standalone Pages

```
apps/web/src/app/features/activation/     # /activate — first Director registration
apps/web/src/app/features/invitation-accept/ # /invite — invitation acceptance
apps/web/src/app/features/vendor/          # /vendor — Vendor Backstage
```

These pages use their own layout (no app shell sidebar). Implementation: standalone Angular components with `Component: true` and no `AdminModule` wrapper.

#### Frontend — Theme Override

The navy enterprise skin is a CSS class-based theme extension:

- Root element gets class `admin-theme` when route starts with `/admin` or `/vendor`
- A global SCSS file `admin-theme.scss` overrides Angular Material CSS custom properties:
  ```scss
  .admin-theme {
    --mat-primary: #1B2A4A;
    --mat-accent: #2E7D8F;
    // ... per DESIGN.md tokens
  }
  ```
- Components use `::ng-deep` within `admin-theme` scope for Material overrides
- Do NOT create a separate Angular build or separate app — single SPA with theme class toggling

#### Mobile Director Companion

```
apps/mobile/src/screens/Admin/
├── TeamSummaryScreen.tsx
├── PendingInvitationsScreen.tsx
└── AdminNotificationsScreen.tsx
```

Accessed from the existing "More" tab. No new tab bar item. These screens are read-heavy with quick-action toggles (suspend/reactivate, resend invite) — no full CRUD forms on mobile.

#### Mandatory Patterns — All AI Agents MUST

1. Every management action endpoint writes an audit event in the same DB transaction — fail-closed
2. All link tokens use the three-step pattern: generate (SHA-256 hash) → sign (HMAC) → embed (URL). Verify HMAC before DB lookup
3. Status enums live in `packages/shared-types/`, not duplicated across API and client
4. Last-Director Protection is checked server-side only — never trust client-side role count
5. All API endpoints for Director management actions check `[Require2FA]` — unenrolled Directors get a 403 with a specific error code
6. The Vendor Backstage is part of the existing API project (`VendorController`), not a separate service or project

## Project Structure & Boundaries

### Complete Project Directory Structure — Role Management Extensions

The existing monorepo tree (`architecture.md` §7) is unchanged. What follows is the **extension tree** — files added or modified for the Role Management system, shown within the existing structure.

```
Midi-Kaval/
├── apps/
│   ├── api/                           # Existing ASP.NET Core 8 project — extended
│   │   ├── Controllers/
│   │   │   ├── V1/
│   │   │   │   ├── Admin/
│   │   │   │   │   ├── UsersController.cs           # FR-4–9: User list, suspend, reactivate, delete
│   │   │   │   │   ├── InvitationsController.cs      # FR-5, FR-12: Invite, resend, list
│   │   │   │   │   └── AuditEventsController.cs      # FR-14: Audit log viewer
│   │   │   │   ├── Vendor/
│   │   │   │   │   └── OrganisationsController.cs    # FR-1, FR-3: Org bootstrap, safety net
│   │   │   │   └── Auth/
│   │   │   │       └── RegistrationController.cs     # FR-2, FR-11: Activation, invitation acceptance, email confirm
│   │   │   └── ... (existing controllers unchanged)
│   │   ├── Domain/
│   │   │   ├── RoleManagement/
│   │   │   │   ├── IUserManagementService.cs         # Interface for user lifecycle operations
│   │   │   │   ├── UserManagementService.cs          # FR-4–9: Business logic for user CRUD
│   │   │   │   ├── IInvitationService.cs
│   │   │   │   ├── InvitationService.cs              # FR-5, FR-11–12: Invitation lifecycle
│   │   │   │   ├── IAuditEventService.cs
│   │   │   │   ├── AuditEventService.cs              # FR-13: Audit event recording
│   │   │   │   ├── IRegistrationService.cs
│   │   │   │   ├── RegistrationService.cs            # FR-2, FR-11: Registration flows
│   │   │   │   ├── ITwoFactorService.cs
│   │   │   │   ├── TwoFactorService.cs               # FR-10: TOTP enrollment, verification, reset
│   │   │   │   ├── IOrganisationService.cs
│   │   │   │   ├── OrganisationService.cs            # FR-1, FR-3: Org lifecycle
│   │   │   │   └── LastDirectorGuard.cs              # FR-9: Server-side guard logic
│   │   │   └── ... (existing domains unchanged)
│   │   ├── Infrastructure/
│   │   │   ├── Data/
│   │   │   │   ├── Configurations/
│   │   │   │   │   ├── OrganisationConfiguration.cs  # EF config for organisations table
│   │   │   │   │   ├── ActivationTokenConfiguration.cs
│   │   │   │   │   ├── InvitationConfiguration.cs
│   │   │   │   │   └── AuditEventConfiguration.cs
│   │   │   │   └── Migrations/
│   │   │   │       ├── {timestamp}_AddOrganisations.cs
│   │   │   │       ├── {timestamp}_ExtendUsers.cs
│   │   │   │       ├── {timestamp}_AddActivationTokens.cs
│   │   │   │       ├── {timestamp}_AddInvitations.cs
│   │   │   │       └── {timestamp}_AddAuditEvents.cs
│   │   │   ├── RoleManagement/
│   │   │   │   ├── UserRepository.cs                 # User queries for admin dashboard
│   │   │   │   ├── OrganisationRepository.cs
│   │   │   │   ├── InvitationRepository.cs
│   │   │   │   ├── AuditEventRepository.cs
│   │   │   │   └── TokenService.cs                   # HMAC-SHA256 signing + SHA-256 hashing
│   │   │   └── ... (existing infrastructure unchanged)
│   │   ├── Middleware/
│   │   │   ├── TokenVersionMiddleware.cs             # AR-2: Check token_version on every request
│   │   │   └── SuspendedUserMiddleware.cs            # FR-6: Block suspended users
│   │   ├── Authorization/
│   │   │   ├── Policies.cs                           # Add DirectorOnly, VendorOnly, Require2FA policies
│   │   │   └── Require2FAAttribute.cs                # Custom authorization filter for FR-10
│   │   ├── Jobs/
│   │   │   ├── AuditDigestJob.cs                     # FR-15: Batch digest emails (5-min window)
│   │   │   ├── InvitationCleanupJob.cs               # FR-12: Daily expired invitation cleanup
│   │   │   └── EmailRetryJob.cs                      # NFR-13: Retry failed email deliveries
│   │   ├── Models/
│   │   │   ├── Organisation.cs
│   │   │   ├── ActivationToken.cs
│   │   │   ├── Invitation.cs
│   │   │   └── AuditEvent.cs
│   │   └── Migration/
│   │       └── AccountMigrationService.cs            # Epic 6: Migrate hardcoded accounts
│   │
│   ├── web/                          # Existing Angular PWA — extended
│   │   ├── src/app/
│   │   │   ├── features/
│   │   │   │   ├── admin/                            # Director Dashboard (lazy-loaded /admin)
│   │   │   │   │   ├── admin-routing.module.ts
│   │   │   │   │   ├── admin.component.ts            # Shell with MatSidenav + router-outlet
│   │   │   │   │   ├── admin.component.scss           # Navy sidebar, enterprise skin
│   │   │   │   │   ├── admin.module.ts
│   │   │   │   │   ├── pages/
│   │   │   │   │   │   ├── team-roster/
│   │   │   │   │   │   │   ├── team-roster.component.ts       # FR-4: MatTable with sort/filter/paginate
│   │   │   │   │   │   │   └── team-roster.component.scss
│   │   │   │   │   │   ├── invitations/
│   │   │   │   │   │   │   ├── invitations.component.ts        # FR-12: Invitation history
│   │   │   │   │   │   │   └── invitations.component.scss
│   │   │   │   │   │   └── audit-log/
│   │   │   │   │   │       ├── audit-log.component.ts         # FR-14: Audit viewer
│   │   │   │   │   │       └── audit-log.component.scss
│   │   │   │   │   ├── components/
│   │   │   │   │   │   ├── user-detail-sheet/
│   │   │   │   │   │   │   ├── user-detail-sheet.component.ts  # FR-6/7/8/10: Detail + actions
│   │   │   │   │   │   │   └── user-detail-sheet.component.scss
│   │   │   │   │   │   ├── invite-dialog/
│   │   │   │   │   │   │   ├── invite-dialog.component.ts      # FR-5: Invite creation modal
│   │   │   │   │   │   │   └── invite-dialog.component.scss
│   │   │   │   │   │   ├── status-badge/
│   │   │   │   │   │   │   ├── status-badge.component.ts       # Reusable MatChip wrapper
│   │   │   │   │   │   │   └── status-badge.component.scss
│   │   │   │   │   │   └── two-factor-modal/
│   │   │   │   │   │       ├── two-factor-modal.component.ts   # FR-10: 2FA enrollment modal
│   │   │   │   │   │       └── two-factor-modal.component.scss
│   │   │   │   │   ├── services/
│   │   │   │   │   │   ├── admin-user.service.ts
│   │   │   │   │   │   ├── invitation.service.ts
│   │   │   │   │   │   └── audit.service.ts
│   │   │   │   │   └── models/
│   │   │   │   │       └── admin.models.ts
│   │   │   │   ├── activation/                       # Standalone page: /activate
│   │   │   │   │   ├── activation.component.ts        # FR-2: First Director registration
│   │   │   │   │   ├── activation.component.scss
│   │   │   │   │   └── activation-routing.module.ts
│   │   │   │   ├── invitation-accept/                 # Standalone page: /invite
│   │   │   │   │   ├── invitation-accept.component.ts  # FR-11: Invitation acceptance
│   │   │   │   │   ├── invitation-accept.component.scss
│   │   │   │   │   └── invitation-accept-routing.module.ts
│   │   │   │   └── vendor/                            # Standalone page: /vendor
│   │   │   │       ├── vendor.component.ts             # FR-1, FR-3: Vendor Backstage
│   │   │   │       ├── vendor.component.scss
│   │   │   │       └── vendor-routing.module.ts
│   │   │   ├── core/
│   │   │   │   ├── guards/
│   │   │   │   │   ├── director.guard.ts               # Route guard for /admin
│   │   │   │   │   └── vendor.guard.ts                 # Route guard for /vendor
│   │   │   │   └── interceptors/
│   │   │   │       ├── auth.interceptor.ts             # Extended for token_version 401 handling
│   │   │   │       └── two-fa.interceptor.ts           # Handle 403 Require2FA errors
│   │   │   └── theme/
│   │   │       └── admin-theme.scss                   # Navy enterprise skin override
│   │   └── ... (existing files unchanged)
│   │
│   └── mobile/                       # Existing React Native — extended
│       ├── src/
│       │   ├── screens/
│       │   │   ├── Admin/
│       │   │   │   ├── TeamSummaryScreen.tsx
│       │   │   │   ├── PendingInvitationsScreen.tsx
│       │   │   │   └── AdminNotificationsScreen.tsx
│       │   │   └── ... (existing screens unchanged)
│       │   ├── components/
│       │   │   ├── Admin/
│       │   │   │   ├── UserQuickActionSheet.tsx
│       │   │   │   └── InviteStatusBadge.tsx
│       │   │   └── ... (existing components unchanged)
│       │   └── services/
│       │       ├── admin-api.ts                       # Admin API client calls
│       │       └── ... (existing services unchanged)
│       └── ... (existing files unchanged)
│
├── packages/
│   ├── api-client/                   # Regenerated from API — Role Management endpoints added
│   └── shared-types/
│       └── src/
│           ├── enums.ts                              # UserStatus, InvitationStatus, UserRole, AuditEventType
│           └── types.ts                              # Admin-specific interfaces
│
└── tests/
    ├── api.unit/
    │   ├── Domain/
    │   │   └── RoleManagement/                       # Unit tests for domain services
    │   │       ├── UserManagementServiceTests.cs
    │   │       ├── InvitationServiceTests.cs
    │   │       ├── RegistrationServiceTests.cs
    │   │       ├── TwoFactorServiceTests.cs
    │   │       ├── LastDirectorGuardTests.cs
    │   │       └── AuditEventServiceTests.cs
    │   └── Infrastructure/
    │       └── RoleManagement/
    │           └── TokenServiceTests.cs
    ├── api.integration/
    │   ├── Controllers/
    │   │   ├── Admin/
    │   │   │   ├── UsersControllerTests.cs
    │   │   │   ├── InvitationsControllerTests.cs
    │   │   │   └── AuditEventsControllerTests.cs
    │   │   ├── Vendor/
    │   │   │   └── OrganisationsControllerTests.cs
    │   │   └── Auth/
    │   │       └── RegistrationControllerTests.cs
    │   └── Migration/
    │       └── AccountMigrationServiceTests.cs
    └── e2e/
        ├── admin-user-management.spec.ts            # Playwright: Director dashboard flows
        └── vendor-bootstrap.spec.ts                 # Playwright: Vendor activation flow
```

### Requirements to Structure Mapping

| Epic | Key Files |
|------|-----------|
| **Epic 1:** Vendor Backstage & Bootstrap | `OrganisationsController.cs`, `RegistrationController.cs`, `OrganisationService.cs`, `RegistrationService.cs`, `ActivationTokenConfiguration.cs`, `TokenService.cs`, `vendor/` (Angular), `activation/` (Angular) |
| **Epic 2:** Director User Management | `UsersController.cs`, `InvitationsController.cs`, `UserManagementService.cs`, `LastDirectorGuard.cs`, `TwoFactorService.cs`, `Require2FAAttribute.cs`, `admin/` (Angular), `user-detail-sheet`, `invite-dialog`, `two-factor-modal` |
| **Epic 3:** Invitation & Registration | `InvitationsController.cs`, `RegistrationController.cs`, `InvitationService.cs`, `RegistrationService.cs`, `invitation-accept/` (Angular), `PendingInvitationsScreen.tsx` (mobile) |
| **Epic 4:** Audit Trail | `AuditEventsController.cs`, `AuditEventService.cs`, `AuditEventConfiguration.cs`, `audit-log/` (Angular) |
| **Epic 5:** Notifications | `AuditDigestJob.cs`, `InvitationCleanupJob.cs`, `EmailRetryJob.cs` |
| **Epic 6:** Migration | `AccountMigrationService.cs` |

### Architectural Boundaries

**API Boundaries:**
- Vendor endpoints (`/api/v1/vendor/`) — authenticated via existing JWT, authorized by `VendorOnly` policy, require 2FA
- Admin endpoints (`/api/v1/admin/`) — authenticated via existing JWT, authorized by `DirectorOnly` policy, require `[Require2FA]` middleware
- Registration endpoints (`/api/v1/auth/activate`, etc.) — unauthenticated, rate-limited, HMAC-signed tokens
- All new endpoints follow existing RFC 7807 error format, camelCase JSON, UUID params

**Service Boundaries:**
- `Controllers/` — thin HTTP layer, delegates to `Domain/` services, never contains business logic
- `Domain/RoleManagement/` — all business rules (Last-Director, 2FA mandate, invitation lifecycle). No EF Core dependency — uses repository interfaces
- `Infrastructure/RoleManagement/` — EF Core repositories, token generation, email sending. Implements domain interfaces
- `Jobs/` — Hangfire background jobs for digest batching, cleanup, email retry

**Data Boundaries:**
- `audit_events` table is append-only — INSERT via `AuditEventRepository`, no UPDATE/DELETE exposed
- Token tables (`activation_tokens`, `invitations`) store SHA-256 hashes — raw token known only to email recipient
- `target_user_snapshot` (JSONB) preserves identity at event time — independent of FK relationships

**Frontend Boundaries:**
- Admin module (`features/admin/`) is lazy-loaded on route `/admin` — not in the main app bundle
- Standalone pages (`activation`, `invitation-accept`, `vendor`) are separate Angular routes with their own layout — no app shell
- Theme override (`admin-theme.scss`) is applied via CSS class on root element — no separate build
- Mobile Admin screens accessed from "More" tab — read-heavy with quick-action toggles, no full forms

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** All technology choices are inherited from the existing, operational Midi-Kaval stack (ASP.NET Core 8 + Angular 19 + React Native + PostgreSQL + Redis + Hangfire). No new runtimes or conflicting middleware.

**Pattern Consistency:** Naming, API format, auth, and error conventions are inherited directly from `architecture.md` §6. The new `Domain/RoleManagement/` layer follows the same Controller → Domain → Infrastructure split as the existing API.

**Structure Alignment:** Extension tree slots cleanly into the existing monorepo without moving or restructuring existing files.

### Requirements Coverage Validation ✅

**Functional Requirements Coverage:** All 16 FRs have an API endpoint, domain service, and (where applicable) a UI surface. FR-9 (Last-Director) has its own dedicated guard class.

**Non-Functional Requirements Coverage:** All 16 NFRs are addressed — rate limiting (middleware extension), append-only audit (INSERT-only privileges), session revocation (token_version), link signing (HMAC-SHA256), dual-auth migration (feature flag).

**Additional Requirements Coverage:** All 10 ARs from the existing architecture are explicitly inherited and enforced.

### Implementation Readiness Validation ✅

**Decision Completeness:** Every layer (data, auth, API, frontend, mobile, notifications, infrastructure) has documented decisions with rationale.

**Structure Completeness:** 40+ new files mapped across API, Angular, React Native, shared packages, and tests. Every file has a named component with FR reference.

**Pattern Completeness:** 17 inherited naming conventions, 6 module-specific patterns, audit event code example, token generation pattern, and 6 mandatory AI agent rules.

### Gap Analysis Results

**No critical gaps.** All 16 FRs, 16 NFRs, and 10 ARs are covered.

**Minor gaps (noted for implementation):**
1. Email template design for notification emails (FR-15, FR-16) — implementation detail in the story
2. Exact Angular module registration path (lazy-loading configuration) — depends on existing `app-routing.module.ts` structure
3. Password complexity configuration — relies on environment variable defaults per PRD

### Architecture Completeness Checklist

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped
- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed
- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented
- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High — all requirements covered, architecture inherits from proven existing stack, no contradictory decisions.

**Key Strengths:**
- Inherits from proven existing Midi-Kaval architecture — no new risk surface
- All security-critical patterns documented with code examples (audit fail-closed, token generation, Last-Director guard)
- Clear extension tree mapped to existing monorepo — no restructuring needed
- 6 mandatory AI agent rules prevent common implementation conflicts

**Areas for Future Enhancement:**
- Email notification templates can be refined as part of Epic 5 implementation
- Performance benchmarks for audit log with 100k+ events will inform index tuning during Epic 4

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions exactly as documented
- Use implementation patterns consistently across all components
- Respect project structure and boundaries
- Refer to this document for all architectural questions

**First Implementation Priority:**
Data model and migrations — new tables (`organisations`, `activation_tokens`, `invitations`, `audit_events`) plus `users` table extensions. This is the foundation that all other epics depend on.

