---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-07-01/prd.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/EXPERIENCE.md
  - _bmad-output/project-context.md
workflowType: 'architecture'
project_name: 'Midi-Kaval'
user_name: 'Admin'
date: '2026-07-01'
lastStep: 8
status: 'complete'
completedAt: '2026-07-01'
---

# Architecture Decision Document — 2FA Universal Enrollment & Administration

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements (7 groups):**
- FR-1: Vendor Settings Page — new Angular route, reuses existing Enroll2FAComponent
- FR-2: Director 2FA Management — extends Staff Management, Organisation Settings, new audit view
- FR-3: Proactive Onboarding Emails — extends email templates, new Hangfire background job
- FR-4: Backup Codes — new DB table, single-use hashed codes, display once on enrollment
- FR-5: Login Response Contract — extends AuthService to return setupUrl, requires2faSetup
- FR-6: API Endpoints — 8 new, 2 reused, role-enforced controller logic
- FR-7: Data Model — new backup_codes table with partial index

**Non-Functional Requirements:**
- NFR-1: Security — encrypted TOTP, SHA-256 hashed backup/bypass codes, 5-attempt lockout
- NFR-2: Audit — 11 new event types in existing audit_events table
- NFR-3: Performance — 50ms for 2FA status, 100ms for backup code verification
- NFR-4: UX — keyboard-navigable, theme-aware QR, human-readable errors

**Scale & Complexity:**
- Primary domain: Full-stack brownfield (Angular 19 + .NET 8 + PostgreSQL + Redis)
- Complexity level: Medium
- Estimated architectural components: ~15 (3 new routes, 4 extended pages, 8 API endpoints, 1 new migration, 1 background job)

### Technical Constraints & Dependencies

- Must extend existing Angular Material design system (no new UI framework)
- Must reuse existing auth pipeline (JWT + email OTP + TOTP)
- Must extend existing audit_events table (11 new event types)
- Must follow existing role guard patterns (DirectorGuard, VendorGuard, AuthGuard)
- Existing Enroll2FAComponent must be refactored from Director-only to role-agnostic
- Backup codes: new EF Core migration for backup_codes table + require_2fa column
- Bypass codes: Redis storage following OtpChallengeStore pattern

### Cross-Cutting Concerns Identified

1. **Role-based authorization** — 4 distinct permission levels (self, Director, Coordinator delegated, Vendor) across every 2FA action
2. **Audit trail consistency** — 11 new event types must match existing audit_events schema
3. **Rate limiting coherence** — new verify endpoints must align with existing auth rate limits
4. **Error message consistency** — all user-facing errors are human-readable, not JSON/stack traces
5. **Offline compatibility** — enrollment is online-only (TOTP generation requires API), but field workers can still log in with email OTP while offline
6. **Brownfield integration** — existing users without TOTP remain unenrolled; org mandate toggle is sole enforcement mechanism

## Starter Template Evaluation

### Primary Technology Domain

Full-stack brownfield extension — Angular 19 + ASP.NET Core 8 + PostgreSQL 16 + Redis 7

### Starter Options Considered

Not applicable. This is a brownfield extension to an existing, fully established project. No new project, platform, or surface is being created. The existing architecture is extended with new routes, API endpoints, and data models following established patterns.

### Architectural Patterns That Carry Forward (Inherited)

| Area | Existing Pattern | 2FA Impact |
|------|-----------------|------------|
| Component model | Angular standalone components (no NgModules) | All new components follow this pattern |
| Routing | Path + Component + Guard | New routes: /vendor/settings, /settings/2fa, /admin/audit/2fa |
| API layering | Controller → Service → EF Core DbContext | New: TwoFactorController, AdminTwoFactorController, BackupCodeService |
| Auth guards | AuthGuard, GuestGuard, DirectorGuard, VendorGuard | New redirect guard for 2FA enrollment |
| State management | AuthSessionService with Angular signals | Add requires2faSetup signal |
| API response format | ApiEnvelopeFilter (data + meta.requestId) | All new endpoints follow this pattern |
| Error handling | ProblemDetails RFC 9457 | Human-readable 2FA error messages |
| CSS/theme | Angular Material theme + SCSS | QR code must be theme-aware |

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- Backup code storage: New `backup_codes` table with SHA-256 hashed codes
- Bypass code storage: Redis with TTL (OtpChallengeStore pattern)
- QR code rendering: Client-side via `qrcode` npm package in Angular
- Login response contract: Add fields to existing envelope (backward compatible)
- Audit event naming: `snake_case` with `2fa_` prefix

**Important Decisions (Shape Architecture):**
- Rate limiting: 5/min for verify endpoints, 2/hr for bypass code generation
- TOTP lockout: 5 consecutive failures → 15-min lockout
- Backup code count: 8 per enrollment

**Deferred Decisions (Post-MVP):**
- Hardware security keys / WebAuthn — Phase 4
- SMS fallback — decision not to pursue in scope
- Mobile enrollment screen — Phase 4

### Data Architecture

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Backup codes table | `backup_codes` (user_id FK, code_hash SHA-256, used flag, created/used timestamps) | Clean schema, indexable, separate lifecycle from users; FR-7 |
| Partial index | `ix_backup_codes_user_id_unused` WHERE used = FALSE | Optimizes "remaining codes" queries for FR-4.6 warning banner |
| require_2fa flag | New column on `organisations` table (boolean, default FALSE) | Minimal schema change, org-scoped policy; FR-2.4 |

### Authentication & Security

| Decision | Choice | Rationale |
|----------|--------|-----------|
| TOTP secret storage | Existing `bytea` column with EF Core value converter | Already in production; encryption at rest; NFR-1.1 |
| Bypass code storage | Redis with 30-min TTL, SHA-256 hashed | Auto-expiry, matches OtpChallengeStore pattern; FR-2.5 |
| Backup code hashing | SHA-256, stored per-code in backup_codes table | Irreversible, single-use verification; NFR-1.3 |
| TOTP lockout | 5 consecutive failures → 15-min lockout | NFR-1.7; email OTP still works during lockout |
| Rate limiting — verify endpoints | 5 attempts/min per user | Matches existing auth rate limits (10/60s); NFR-1.4, 1.5 |
| Rate limiting — bypass generation | 2 per hour per Director | Prevents abuse of emergency access; NFR-1.6 |

### API & Communication Patterns

| Decision | Choice | Rationale |
|----------|--------|-----------|
| API response format | Existing ApiEnvelopeFilter (data + meta.requestId) | All new endpoints consistent with existing; no exceptions |
| Error handling | ProblemDetails RFC 9457 | Existing pattern; 2FA errors override detail field with human-readable messages |
| Login response contract | Append-only pattern — add `requires2faSetup`, `setupUrl`, `orgRequires2fa` | Backward compatible; existing clients ignore unknown fields; FR-5 |
| New controllers | `TwoFactorController` (self-service), `AdminTwoFactorController` (Director/Coordinator) | Separation of concerns; role enforcement at controller level |
| Endpoint naming | `/auth/*` for self-service, `/admin/*` for management | Follows existing route convention (/admin prefix for admin actions) |

### Frontend Architecture

| Decision | Choice | Rationale |
|----------|--------|-----------|
| QR code library | `qrcode` npm package (client-side rendering) | Provisioning URI generated server-side; client renders QR only. No additional server load. Theme-aware. |
| Component reuse | Refactor existing `Enroll2FAComponent` from Director-only to role-agnostic | Avoids duplicating enrollment logic; PIN-driven role configuration |
| State management | AuthSessionService signals — add `requires2faSetup` signal | Follows existing pattern; FR-5 contract drives auth guard redirect |
| New route guard | `TwoFactorSetupGuard` — reads login response, redirects to setup URL if unenrolled | Clean separation from existing AuthGuard; FR-1.4, FR-2.4 |
| MatMenu integration | Extend existing `StaffListComponent` with 2FA column + contextual actions | No new table component; extend in place; FR-2.1 |

### Infrastructure & Deployment

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Migration campaign | Hangfire background job, throttled 100 emails/hr | Uses existing Hangfire infrastructure; FR-3.4 |
| No new infrastructure | All changes within existing API + Angular + PostgreSQL + Redis | Zero new services, containers, or cloud resources |
| Configuration | Existing `appsettings.json` pattern — add `TwoFactorSettings` section | Follows existing config structure |

### Decision Impact Analysis

**Implementation Sequence:**
1. DB migration: backup_codes table + require_2fa column (foundation)
2. API: BackupCodeService + TwoFactorController + AdminTwoFactorController
3. API: Extend AuthService login response contract (FR-5)
4. Angular: Refactor Enroll2FAComponent to be role-agnostic
5. Angular: Build Vendor Settings page (FR-1)
6. Angular: Extend Staff Management with 2FA column + MatMenu (FR-2.1-2.3, 2.5)
7. Angular: Add Org Settings toggles + 2FA Audit Log (FR-2.4, 2.6, 2.7)
8. API: Bypass code Redis storage (FR-2.5)
9. API + Angular: Proactive onboarding emails (FR-3)
10. Jobs: Legacy migration campaign (FR-3.4)

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Critical Conflict Points Identified:** 8 areas where AI agents working on different 2FA components could make different choices

### Naming Patterns

**Database Naming Conventions:**
- Table/column naming: `snake_case` (existing FK pattern: `user_id`, `organisation_id`)
- New tables: `backup_codes` (plural, snake_case — matches existing `audit_events`, `confirmation_tokens`)
- New column on organisations: `require_2fa` (boolean, default `FALSE`)
- Index naming: `ix_backup_codes_user_id` (matches existing `ix_audit_events_organisation_id_created_at_utc`)
- Partial index: `ix_backup_codes_user_id_unused` WHERE `used = FALSE`

**API Naming Conventions:**
- Endpoint path: `kebab-case` (`/admin/users/{id}/reset-2fa`, `/auth/verify-backup-code`)
- Route parameters: `{id}` (C# convention, existing pattern)
- Query parameters: `camelCase` (`?eventType=&userId=&from=&to=`)
- JSON field names in response: `camelCase` (existing ApiEnvelopeFilter pattern)

**Code Naming Conventions (C#):**
- Controllers: `TwoFactorController`, `AdminTwoFactorController` (PascalCase, Controller suffix)
- Services: `BackupCodeService`, `TwoFactorService` (PascalCase, Service suffix)
- Entity classes: `BackupCode` (singular, PascalCase — maps to `backup_codes` table)
- Endpoint method names: `Enroll2FA`, `VerifyEnroll2FA`, `VerifyBackupCode` (PascalCase)

**Code Naming Conventions (Angular):**
- Components: `VendorSettingsComponent`, `TwoFactorEnrollmentComponent` (PascalCase, Component suffix)
- Routes: `/vendor/settings`, `/settings/2fa`, `/admin/audit/2fa` (kebab-case)
- Services: `TwoFactorService`, `BackupCodeService` (PascalCase, Service suffix)
- Signals in AuthSessionService: `requires2faSetup` (camelCase signal)

### Format Patterns

**API Response Formats:**
- All responses wrapped in existing `{ data: ..., meta: { requestId } }` envelope (ApiEnvelopeFilter)
- Error responses: ProblemDetails RFC 9457 (`{ type, title, status, detail }`)
- 2FA status response: `{ enrolled: bool, enrolledAt: dateISO }`
- Login response additions: `requires2faSetup: bool`, `setupUrl: string`, `orgRequires2fa: bool`

**Data Exchange Formats:**
- Date format in JSON: ISO 8601 strings (existing pattern: `created_at_utc` → `createdAtUtc`)
- Boolean representations: `true`/`false` (JSON native)
- Backup code format: 10-character alphanumeric, dashed groups (e.g., `A3K9-X7M2-P1`)
- Bypass code format: 12-character alphanumeric, dashed groups (e.g., `A3K9-X7M2-P1Q8`)

### Process Patterns

**Error Handling Patterns:**
- All user-facing 2FA errors must be human-readable strings (not JSON, not stack traces)
- Error messages follow voice rules from EXPERIENCE.md: clear, direct, no false urgency
- API errors use ProblemDetails with `detail` field for human-readable message
- Rate limit errors return 429 with existing rate limiting middleware

**Loading State Patterns:**
- QR generation: `MatProgressSpinner` in placeholder area, label "Generating your code..."
- TOTP verification: inline spinner on verify button, label changes to "Verifying..."
- Toggle save: indeterminate toggle state with brief spinner
- All other CRUD operations: follow existing Angular patterns (loading$ observables)

**Audit Event Naming:**
- All 2FA events prefixed with `2fa_` in `snake_case`
- Event types: `2fa_enrolled`, `2fa_reset`, `2fa_bypass_generated`, `2fa_bypass_used`, `2fa_backup_used`, `2fa_failed_totp`, `2fa_mandate_enabled`, `2fa_mandate_disabled`, `2fa_delegation_enabled`, `2fa_delegation_disabled`, `2fa_disabled`

### Enforcement Guidelines

**All AI Agents MUST:**
- Use existing Angular Material components (MatTable, MatMenu, MatSlideToggle, MatDialog) — do not create custom alternatives
- Follow existing `Controller → Service → DbContext` layering — do not inline DB logic in controllers
- Add new audit event types to the existing `audit_events` table — do not create a separate audit table
- Use the existing `ApiEnvelopeFilter` response format — do not create custom response wrappers
- Follow existing role guard patterns — do not inline role checks in component code

**Pattern Examples:**

*Good — API endpoint follows existing layering:*
```
AdminTwoFactorController → calls → TwoFactorService → calls → BackupCodeService + AppDbContext
```

*Anti-pattern — Inline DB logic in controller:*
```
AdminTwoFactorController → direct DbContext query (do not do this)
```

*Good — New component reuses Material:*
```
StaffListComponent → extends MatTable with new 2FA column → MatMenu for actions
```

*Anti-pattern — Custom table component:*
```
StaffListComponent → custom table implementation (do not do this — use MatTable)
```

## Project Structure & Boundaries

### Requirements to Component Mapping

| FR Category | Location (New/Existing) | Key Files |
|------------|------------------------|-----------|
| FR-1: Vendor Settings | `apps/web/` (New + Extend) | New route + component, extend VendorGuard |
| FR-2: Director 2FA Mgmt | `apps/web/` + `apps/api/` | Extend Staff/Org Settings/Staff controllers |
| FR-3: Onboarding Emails | `apps/api/` (Extend) | Extend Email templates, new Hangfire job |
| FR-4: Backup Codes | `apps/api/` (New) | New entity, service, DB migration |
| FR-5: Login Contract | `apps/api/` (Extend) | Extend AuthService login response |
| FR-6: API Endpoints | `apps/api/` (New) | New controllers + services |
| FR-7: DB Schema | `apps/api/` (New) | New migration |

### File Changes — API (Backend)

**New Files:**

```
apps/api/MidiKaval.Api/
├── Domain/Entities/BackupCode.cs              # NEW: EF Core entity
├── Infrastructure/Auth/BackupCodeService.cs    # NEW: Backup code CRUD + verification
├── Controllers/V1/
│   ├── Auth/TwoFactorController.cs            # EXTEND: Add 2FA-status, backup-code-verify endpoints
│   └── Admin/AdminTwoFactorController.cs      # NEW: Director 2FA management endpoints
├── Models/
│   ├── TwoFactorStatusResponse.cs             # NEW: DTO for 2FA status
│   ├── BypassCodeResponse.cs                  # NEW: DTO for bypass code generation
│   └── AdminTwoFactorRequest.cs               # NEW: DTOs for admin 2FA actions
└── Migrations/                                # NEW: 2 migrations
    ├── 20260701000000_AddBackupCodes.cs
    └── 20260701000001_AddOrganisationRequire2fa.cs
```

**Modified Files:**

```
apps/api/MidiKaval.Api/
├── Domain/Entities/User.cs                    # MODIFY: + BackupCodes navigation property
├── Domain/Entities/Organisation.cs            # MODIFY: + Require2fa property
├── Infrastructure/Auth/AuthService.cs         # MODIFY: Login response returns 2FA setup fields
├── Controllers/V1/Auth/TwoFactorController.cs # MODIFY: Keep existing enrollment, add new endpoints
├── Infrastructure/Persistence/AppDbContext.cs  # MODIFY: + DbSet<BackupCode>, + Organisation config
├── Jobs/                                      # NEW: Hangfire job
│   └── Legacy2faMigrationJob.cs
└── Program.cs                                 # MODIFY: Register new services
```

### File Changes — Web (Angular Frontend)

**New Files:**

```
apps/web/src/app/
├── vendor/
│   └── settings/                              # NEW: Vendor Settings module
│       ├── vendor-settings.component.ts
│       ├── vendor-settings.component.html
│       ├── vendor-settings.component.scss
│       └── vendor-settings.routes.ts
├── admin/
│   └── audit-2fa/                            # NEW: 2FA Audit Log
│       ├── audit-2fa.component.ts
│       ├── audit-2fa.component.html
│       └── audit-2fa.routes.ts
├── auth/
│   └── two-factor-setup.guard.ts             # NEW: Route guard for 2FA enrollment redirect
├── shared/
│   └── components/2fa/                        # NEW: Shared 2FA components
│       ├── two-factor-enrollment.component.ts  # Refactored from Director-only
│       ├── two-factor-enrollment.component.html
│       ├── backup-codes-display.component.ts
│       ├── backup-codes-display.component.html
│       └── 2fa-status-badge.component.ts
└── services/
    ├── two-factor.service.ts                  # NEW: API service for 2FA endpoints
    └── backup-code.service.ts                 # NEW: API service for backup codes
```

**Modified Files:**

```
apps/web/src/app/
├── vendor/vendor.routes.ts                   # MODIFY: Add /settings route
├── admin/
│   ├── staff/                                # MODIFY: + 2FA column in MatTable
│   │   └── staff-list.component.ts
│   ├── admin.routes.ts                       # MODIFY: Add /audit/2fa route
│   └── settings/                             # MODIFY: + 2FA toggles
│       └── organisation-settings.component.ts
├── auth/
│   ├── auth-session.service.ts               # MODIFY: + requires2faSetup signal
│   └── auth.routes.ts                        # MODIFY: + /settings/2fa route
├── settings/
│   └── profile.component.ts                  # MODIFY: + backup codes warning banner
└── app.routes.ts                             # MODIFY: Register new routes
```

### Integration Boundaries

**API → Frontend Communication:**
- Login response carries `{ requires2faSetup, setupUrl }` → Angular auth guard reads and redirects
- All 2FA data flows through REST API (no WebSockets, no real-time)
- Enrollment notifications to Director flow through existing in-app notification system (polling-based)

**Service Layering:**
```
TwoFactorController ───────→ TwoFactorService ───────→ OtpChallengeStore (Redis)
                                                      → BackupCodeService → AppDbContext (backup_codes)
                                                      → AuthService (auth context)
AdminTwoFactorController ──→ AdminTwoFactorService ──→ TwoFactorService
                                                      → UserManagementService
                                                      → AuditEventService
BackupCodeService ─────────→ AppDbContext (backup_codes)
Legacy2faMigrationJob ─────→ TwoFactorService → EmailSender
```

**Data Boundaries:**
- `backup_codes` table: lives in existing PostgreSQL, FK to `users(id)`, no cross-database concerns
- Bypass codes: Redis-only, no DB persistence. TTL handles cleanup.
- `require_2fa` column: additive change to `organisations` table, default FALSE
- All 2FA audit events: appended to existing `audit_events` table with `2fa_` prefix
- No new caches, queues, or external services needed

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** All technology choices are already proven in production — no new runtimes, databases, or infrastructure. ASP.NET Core 8, Angular 19 + Material, PostgreSQL 16, Redis 7, and Hangfire are all in current use.

**Pattern Consistency:** Naming conventions (snake_case DB, camelCase JSON, PascalCase C#, kebab-case Angular routes) follow existing project patterns exactly. No new conventions introduced.

**Structure Alignment:** Project structure extends existing directories (Controllers/V1/Auth/, Controllers/V1/Admin/, Domain/Entities/, Infrastructure/Auth/). No structural conflicts with existing files.

### Requirements Coverage Validation ✅

**Functional Requirements Coverage:**
- FR-1 (Vendor Settings): New route + refactored component + TwoFactorController
- FR-2 (Director Management): New AdminTwoFactorController + StaffListComponent extension + Org settings toggles
- FR-3 (Onboarding Emails): Email template extension + Legacy2faMigrationJob
- FR-4 (Backup Codes): BackupCode entity + BackupCodeService + DB migration
- FR-5 (Login Contract): AuthService extension + TwoFactorSetupGuard
- FR-6 (API): 8 new endpoints across 2 controllers + 3 DTOs
- FR-7 (Data Model): 2 new migrations

**Non-Functional Requirements Coverage:**
- NFR-1 (Security): SHA-256 hashing, Redis TTL, rate limiting, lockout all specified
- NFR-2 (Audit): 11 event types defined, appends to existing audit_events table
- NFR-3 (Performance): Sub-50ms target for status endpoint, sub-100ms for code verification
- NFR-4 (UX): Theme-aware QR, keyboard navigation, human-readable errors in EXPERIENCE.md

### Gap Analysis Results

| Priority | Gap | Resolution |
|----------|-----|-----------|
| Low | No explicit logging strategy documented for 2FA services | Follow existing `ILogger<T>` pattern — standard across all existing services |
| Low | Enroll2FAComponent refactoring scope not fully verified | Verify existing component has no Director-specific business logic; extract to TwoFactorService if found |

### Architecture Completeness Checklist

**Requirements Analysis:**
- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed (medium)
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped (6 items)

**Architectural Decisions:**
- [x] Critical decisions documented with rationale
- [x] Technology stack fully specified (inherited)
- [x] Integration patterns defined (REST, layering, guard-based)
- [x] Performance considerations addressed (NFR-3 timing targets)

**Implementation Patterns:**
- [x] Naming conventions established (DB, API, C#, Angular)
- [x] Structure patterns defined (layered controllers, shared components)
- [x] Communication patterns specified (login contract, audit events)
- [x] Process patterns documented (error handling, loading states)

**Project Structure:**
- [x] Complete directory structure defined (16 new files, 12 modified)
- [x] Component boundaries established (service layering diagram)
- [x] Integration points mapped (API → Frontend contract, service dependencies)
- [x] Requirements to structure mapping complete (table of 7 FR groups)

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION
**Confidence Level:** High

**Key Strengths:**
- Brownfield extension of proven architecture — no new infrastructure risk
- Every FR has a mapped file path and component
- Patterns and anti-patterns explicitly documented to prevent agent divergence
- All security decisions (hashing, encryption, rate limiting) are precise and testable

**Areas for Future Enhancement:**
- Enroll2FAComponent code audit needed before refactoring
- Logging audit to verify all new services use ILogger&lt;T&gt;
