---
stepsCompleted: [1, 2, 3]
epicCount: 10
storyCount: 56
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/addendum.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/command-strip-today.html
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/mockups/crisis-queue.html
  - _bmad-output/specs/spec-kaval-online/SPEC.md
scope: v1 MVP (FR-1 through FR-25); FR-26–28 deferred v1.1/v2
---

# Midi-Kaval - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Midi-Kaval, decomposing the requirements from the PRD, UX Design, and Architecture into implementable stories.

**Stack:** Angular PWA (web supervisors) + React Native (mobile field) + ASP.NET Core 8 API + PostgreSQL.

## Requirements Inventory

### Functional Requirements

FR-1: Users can log in with email, password, and email OTP; password reset via email; admin can force reset and deactivate accounts.
FR-2: API enforces role permissions for every protected action; invalid session returns unauthorized; role change/deactivation forces logout; client UI hiding does not grant access.
FR-3: Staff can create and progress Cases through six Stages with defined sub-step data; all core fields capturable; stage transitions audited.
FR-4: System enforces unique Crime Number and ST Number per organisation; save rejected on duplicate unless merge workflow; search by Crime/ST under 2s at pilot scale.
FR-5: Possible-match warning before Case create when Crime or ST matches existing record; Coordinator can merge preserving audit history.
FR-6: Users can search/filter Cases and export results; full-text search; filters by stage, offence, classification, staff, overdue, district; saved presets; bulk Excel/PDF export.
FR-7: Receiving worker sees Handoff Whisper on Cases transferred within last seven days with prior actions, open items, next visit purpose.
FR-8: Field staff view Today, Weekly, and Overdue visits; completing visit increments count in real time; reschedule requires reason visible to supervisor; push for today/overdue visits.
FR-9: Staff navigate to visits via Google Maps; GPS coordinates, landmark, verified date/by stored; unverified GPS flagged.
FR-10: System suggests grouping same-day visits by GPS cluster with reorder option; excludes unverified GPS until landmark captured.
FR-11: Field staff start/complete visits with notes offline; sync status visible (local/pending/synced/error); cloud authoritative on reconnect; no silent data loss.
FR-12: Mobile home is Morning Command Strip — time-ordered today queue with overdue badge, court countdown, one-tap Start Visit; no dashboard required for daily field workflow.
FR-13: Staff add typed notes with optional attachments on Cases; note types Visit/Court/Intervention/General; timeline chronological; attachments in secured cloud storage.
FR-14: Case Workers track interventions needed and provided; fields type, description, priority, status, outcome, assigned staff; Legends categories; overdue alerts to assignee.
FR-15: Staff manage court sittings per Case (date/time, court, purpose, status Upcoming/Attended/Postponed); notes/outcome; next court date on completion; on worker schedule.
FR-16: System notifies field staff and Coordinators before sittings — mobile push 24h before; email to Coordinator and web user for tomorrow's sitting.
FR-17: If sitting date passes with status Upcoming, Case flagged; Coordinator Crisis Queue item; notification sent; flag clears on Attended/Postponed.
FR-18: Field staff submit travel claims with mandatory receipt images; Draft → Submitted → Approved (Director); claimant notified; monthly totals visible to supervisor.
FR-19: Multi-channel notifications per role — mobile push (visits, overdue, interventions, court, claims); web email (reports, claims, assignments, court); in-app centre with read/unread.
FR-20: Coordinators/Directors view operational dashboard with widgets (cases by stage/offence/area/staff, overdue visits, interventions, court this week, pending claims, intake trend); data within 60s of field sync.
FR-21: Coordinator primary view is Crisis Queue — overdue visits (critical), court <48h without prep (warning), recent handoffs, pending claims; one-tap to Case and worker.
FR-22: Standard operational reports (daily–yearly work, visits planned vs completed, interventions, court, offence/area counts, workload distribution, travel totals); export Excel/PDF.
FR-23: Coordinators CRUD Legends master data (offence types, classifications, intervention categories, education, occupation, outcomes, areas, designations, police stations).
FR-24: Admins manage staff directory.
FR-25: Directors retrieve audit history of data-changing events (who, what, when).

**Deferred (not v1 MVP):**
FR-26: Pre-visit experience brief (v1.1 Field Memory AI)
FR-27: Outcome-tagged learning pool (v1.1)
FR-28: Experience bridge to prior handler (v1.1)
Scheme/law AI: v2 non-goal

### NonFunctional Requirements

NFR-1: All API traffic over TLS.
NFR-2: Role-based access enforced server-side on every protected endpoint (FR-2).
NFR-3: POCSO/minor Cases support discreet capture mode on mobile — minimal on-screen beneficiary detail in public settings.
NFR-4: Beneficiary PII access logged in audit trail for sensitive Case classifications.
NFR-5: Attachments stored in secured cloud storage with role-scoped expiring URLs (15 min SAS).
NFR-6: Cloud database is source of truth; offline mobile is temporary buffer only.
NFR-7: Failed syncs surface to user; sync queue inspectable for support.
NFR-8: Dashboard and search meet FR-4 (<2s search) and FR-20 (<60s freshness) under pilot load.
NFR-9: Audit log retention 7 years (pending legal confirmation).
NFR-10: Data residency India region cloud (assumption).
NFR-11: No punitive staff performance scoring; workload reports distribution-only (SM-C1).
NFR-12: Web PWA bounded offline — read-only Crisis Queue/dashboard snapshot only; mutations require network.
NFR-13: WCAG 2.2 AA on web; platform accessibility APIs on mobile.

### Additional Requirements

- **Monorepo scaffold (Epic 1 Story 1):** `apps/api`, `apps/web` (Angular PWA), `apps/mobile` (RN), `packages/api-client`, `packages/shared-types`, `tests/`, `infra/docker-compose.yml`.
- **API:** ASP.NET Core 8 REST `/api/v1`, RFC 7807 errors, JWT 15min + refresh (httpOnly cookie web / secure storage mobile), policy-based RBAC, rate limiting on auth, CORS allowlist.
- **Database:** PostgreSQL 16+, EF Core migrations, UNIQUE(organisation_id, crime_number/st_number), snake_case tables, soft-delete via Termination/Exclusion only.
- **Cache:** Redis for sessions, Crisis Queue snapshot TTL 30s, dashboard widget counts.
- **Object storage:** Azure Blob (or S3-compatible); upload via presign → PUT → confirm pattern.
- **OpenAPI-generated TypeScript client** — sole HTTP layer in web and mobile.
- **Background jobs (Hangfire/Quartz):** court reminder 24h, court miss escalation hourly, overdue visits daily 06:00, intervention overdue daily, async report export.
- **Push:** FCM + APNs; device tokens in `user_devices`.
- **Email:** SMTP/SendGrid for OTP, court reminders, claim status.
- **Maps:** Google Maps SDK (mobile), Maps JS (web where needed).
- **Mobile offline:** WatermelonDB or SQLite; `POST /sync/push` idempotent batch; server wins conflicts except note merge by timestamp; offline scope = visits, visit notes, draft travel claims only.
- **Web PWA:** `@angular/service-worker`, ngsw-config prefetch app shell, freshness cache for queue/dashboard snapshot, network-only mutations.
- **Reporting server-side:** ClosedXML (Excel), QuestPDF (PDF); async jobs for large exports (>30s).
- **POCSO API:** `sensitivity_level = POCSO` → initials-only in mobile list DTOs; column-level encryption assumption for beneficiary contact.
- **Dedicated endpoints:** `GET /supervisor/crisis-queue`, `GET /visits/today`, `POST /cases/check-duplicate` — not client-composed from generic lists.
- **Every mutation writes audit_events.**
- **Testing:** xUnit API unit/integration (Testcontainers), Jasmine+ATL web, Jest+RN Testing Library mobile, Playwright E2E web critical paths.
- **One-time Excel migration tool** maps legacy columns to Case fields; not ongoing sync.
- **Multi-tenancy:** schema tenant-ready; single-org pilot v1 assumption.

### UX Design Requirements

UX-DR1: Implement semantic color tokens from DESIGN.md in Angular Material theme (status-critical/warning/info/neutral, sync-local/pending/synced/error, primary teal #0D6E6E, surfaces, ink).
UX-DR2: Command Strip card component — visit order, crime/ST, sync chip, Navigate + Start Visit (mobile Today tab).
UX-DR3: Crisis Queue row variants — crisis-row-critical/warning/info/neutral with left border, badge text + icon (Overdue, Court 48h, Handoff, Claim); sorted critical → warning → info → neutral.
UX-DR4: Handoff Whisper — max 3 lines + "View full timeline" on Case detail ≤7 days post-transfer.
UX-DR5: Duplicate match sheet — blocking modal; Open existing, Merge (Coordinator only), Cancel; no duplicate create; focus trap, aria-modal.
UX-DR6: Sync chip states — local/pending/synced/error with text labels per WCAG; error tap navigates to Sync queue.
UX-DR7: Discreet header for POCSO/sensitive cases — initials + crime number default; expand after OTP re-entry (same session ≤5 min acceptable).
UX-DR8: OTP input — accessible 6-digit entry, aria-live errors, "Code expired — request new code" timeout announcement.
UX-DR9: Court sitting row — Upcoming/Attended/Postponed chips; past-due Upcoming uses critical accent.
UX-DR10: Mobile bottom tabs — Today · Cases · More; pull-to-refresh on Today and Cases.
UX-DR11: Web sidebar IA — Crisis Queue (Coordinator home), Dashboard, Case registry, Reports, Legends, Admin (Director).
UX-DR12: Voice and tone strings per EXPERIENCE.md ("Saved on this device", not "Offline mode engaged").
UX-DR13: Empty states — Crisis Queue, court schedule, travel drafts, notifications, Legends category per State Patterns table.
UX-DR14: Session expired forced logout screen both surfaces; mobile-only roles see "Use the mobile app for your role" on web login.
UX-DR15: Web responsive — sidebar + main ≥1024px; collapsed 768–1023px; 200% zoom no horizontal scroll on queue rows; touch targets ≥44pt mobile / 40px web.
UX-DR16: Anti-patterns enforced — no gamification, leaderboards, hover-only on touch widths, modal stack >1, infinite scroll on visits.
UX-DR17: Light mode default; dark mode supported via DESIGN.md dark tokens.
UX-DR18: Experience brief card (v1.1 placeholder only — do not build in v1 MVP stories).

### FR Coverage Map

FR-1: Epic 1 — Email/password + OTP authentication
FR-2: Epic 1 — Server-side RBAC on all protected endpoints
FR-3: Epic 2 — Case create and six-stage lifecycle
FR-4: Epic 2 — Unique Crime/ST enforcement
FR-5: Epic 2 — Duplicate prevention and merge
FR-6: Epic 2 — Search, filter, export
FR-7: Epic 2 — Transfer handoff whisper
FR-8: Epic 3 — Visit scheduler (today/weekly/overdue)
FR-9: Epic 3 — GPS and Maps navigation
FR-10: Epic 3 — Proximity grouping
FR-11: Epic 3 — Offline visit capture and sync
FR-12: Epic 3 — Morning Command Strip
FR-13: Epic 4 — Case notes timeline and attachments
FR-14: Epic 4 — Interventions tracking
FR-15: Epic 5 — Court sitting records
FR-16: Epic 5 — Court reminders
FR-17: Epic 5 — Court miss escalation
FR-18: Epic 6 — Travel claims workflow
FR-19: Epic 7 — Multi-channel notifications
FR-20: Epic 8 — Operational dashboard
FR-21: Epic 8 — Crisis Queue
FR-22: Epic 8 — Standard reports and export
FR-23: Epic 9 — Legends administration
FR-24: Epic 9 — Staff directory
FR-25: Epic 9 — Audit log
Excel migration: Epic 10 — One-time legacy import

## Epic List

### Epic 1: Platform Bootstrap & Secure Access
Team can log in on web and mobile with OTP, reset forgotten passwords via email, and have roles enforced server-side; monorepo and API foundation ready.
**FRs covered:** FR-1, FR-2

### Epic 2: Case Registry, Search & Duplicate Prevention
Coordinators register and find Cases; duplicates blocked at entry; receiving workers see Handoff Whisper.
**FRs covered:** FR-3, FR-4, FR-5, FR-6, FR-7

### Epic 3: Field Visit Day (Command Strip & Offline)
Field staff run today's visits from Command Strip with GPS, proximity grouping, offline capture, and visible sync.
**FRs covered:** FR-8, FR-9, FR-10, FR-11, FR-12

### Epic 4: Case Notes & Interventions
Staff record notes with attachments; Case Workers track interventions with overdue alerts.
**FRs covered:** FR-13, FR-14

### Epic 5: Court Sittings, Reminders & Miss Prevention
Court sittings scheduled and tracked; 24h reminders sent; missed sittings escalate to Crisis Queue.
**FRs covered:** FR-15, FR-16, FR-17

### Epic 6: Travel Claims & Director Approval
Field staff submit claims with receipts; Director approves; claimant notified.
**FRs covered:** FR-18

### Epic 7: Notifications & In-App Centre
Users receive push, email, and in-app notifications for operational events.
**FRs covered:** FR-19

### Epic 8: Supervisor Crisis Queue, Dashboard & Reports
Coordinators triage from Crisis Queue; dashboard reflects field activity; standard reports exportable.
**FRs covered:** FR-20, FR-21, FR-22

### Epic 9: Admin — Legends, Staff Directory & Audit
Coordinators manage master data; Directors manage users and view audit trail.
**FRs covered:** FR-23, FR-24, FR-25

### Epic 10: Legacy Excel Migration
Organisation migrates historical Cases from Excel once with no ongoing sync.
**FRs covered:** MVP §6.1 one-time import

---

## Epic 1: Platform Bootstrap & Secure Access

Establish the monorepo, API foundation, generated clients, and secure login on Angular PWA and React Native with server-enforced RBAC.

### Story 1.1: Monorepo Scaffold and Local Dev Environment

As a **developer**,
I want a monorepo with API, web, mobile, and shared packages runnable locally,
So that the team can build and test all surfaces from one repository.

**Acceptance Criteria:**

**Given** a fresh clone of Midi-Kaval
**When** I run `docker-compose up` and start each app per README
**Then** PostgreSQL, Redis, API, Angular PWA, and React Native dev builds start without manual path fixes
**And** folder structure matches architecture §7 (`apps/api`, `apps/web`, `apps/mobile`, `packages/api-client`, `packages/shared-types`, `tests/`, `infra/`)

### Story 1.2: API Host Foundation with Health and OpenAPI

As a **developer**,
I want an ASP.NET Core 8 API with versioning, Problem Details errors, and OpenAPI,
So that clients have a stable contract from day one.

**Acceptance Criteria:**

**Given** the API project exists
**When** I request `GET /health` and `GET /swagger/v1/swagger.json`
**Then** health returns 200 and OpenAPI documents `/api/v1` routes with camelCase JSON envelope `{ data, meta }`
**And** unhandled errors return RFC 7807 Problem Details; HTTPS enforced in non-dev environments (NFR-1)

### Story 1.3: Users Schema and Seed Admin Account

As a **Project Director**,
I want an initial admin user in the database,
So that I can log in on first deploy without manual SQL.

**Acceptance Criteria:**

**Given** EF Core migrations run on empty PostgreSQL
**When** seed runs on first deploy
**Then** `users` table exists with role, email, token_version, organisation_id columns only (no unrelated tables)
**And** one Director seed account is created via configuration secret; migration is repeatable in CI

### Story 1.4: Login and Email OTP API

As a **user**,
I want to authenticate with email, password, and email OTP,
So that only verified staff access case data (FR-1).

**Acceptance Criteria:**

**Given** valid credentials for an active user
**When** I `POST /api/v1/auth/login` then `POST /api/v1/auth/verify-otp` with correct OTP
**Then** I receive JWT access token (15 min) and refresh mechanism (httpOnly cookie contract documented for web; token payload for mobile)
**And** invalid password returns 401; invalid OTP returns 401; deactivated account returns 403 with message "Contact your coordinator"; OTP email sent via configured provider; auth endpoints rate-limited

### Story 1.5: Refresh, Logout, and Forced Session Invalidation

As a **user**,
I want sessions to refresh securely and end on logout or role change,
So that access remains controlled (FR-1, FR-2).

**Acceptance Criteria:**

**Given** a valid refresh token
**When** I call `POST /api/v1/auth/refresh` or `POST /api/v1/auth/logout`
**Then** refresh issues new access token; logout invalidates refresh token
**And** incrementing `users.token_version` on role change or deactivation causes next API call to return 401; audit_events records login, logout, failed OTP

### Story 1.6: Angular PWA Shell with Login and OTP Flow

As a **Project Coordinator or Director**,
I want to log in on the web app with OTP,
So that I can access supervisor tools (FR-1, UX-DR8, UX-DR14).

**Acceptance Criteria:**

**Given** the Angular PWA loads
**When** I enter email/password and OTP on labelled inputs with `aria-live` error region
**Then** successful login routes to role-appropriate home placeholder
**And** Social Worker/Case Worker roles see "Use the mobile app for your role" without supervisor routes; session expiry shows forced logout screen; `@angular/service-worker` registered with app-shell prefetch only

### Story 1.7: React Native Shell with Login and OTP Flow

As a **Social Worker or Case Worker**,
I want to log in on mobile with OTP,
So that I can access field tools (FR-1, UX-DR8).

**Acceptance Criteria:**

**Given** the RN app launches
**When** I complete login + OTP using accessible OTP input
**Then** I land on Today tab placeholder with bottom tabs Today · Cases · More (UX-DR10)
**And** tokens stored in secure storage; expired session returns to login; voice/tone strings match EXPERIENCE.md

### Story 1.8: RBAC Policies on Protected Endpoints

As a **system owner**,
I want every protected API mutation authorized by role policy,
So that client UI cannot bypass permissions (FR-2, NFR-2).

**Acceptance Criteria:**

**Given** policies map to Director, Coordinator, SocialWorker, CaseWorker
**When** a Social Worker calls a Coordinator-only endpoint
**Then** API returns 403 Problem Details
**And** `[AllowAnonymous]` is absent on all data mutation endpoints; integration tests cover at least one denied call per role

### Story 1.9: Self-Service Password Reset

As a **user who forgot my password**,
I want to reset it via a secure email link,
So that I can regain access without waiting for an administrator (FR-1).

**Acceptance Criteria:**

**Given** an active user account with a registered email
**When** I `POST /api/v1/auth/forgot-password` with my email
**Then** a time-limited single-use reset token is created and a reset link is sent via the configured email provider
**And** the API returns a generic success response whether or not the email exists (no account enumeration); endpoint is rate-limited; deactivated accounts do not receive a usable reset link

**Given** a valid, unexpired reset token from the email link
**When** I `POST /api/v1/auth/reset-password` with token and new password meeting policy
**Then** my password updates, existing refresh tokens are invalidated (`token_version` incremented), and I must log in again with OTP
**And** expired or already-used tokens return 400; invalid token returns 401; audit_events records password reset request and successful reset

**Given** the Angular PWA or React Native login screen
**When** I tap "Forgot password?" and submit my email, then open the link and set a new password
**Then** accessible labelled inputs and `aria-live` errors guide the flow (UX-DR8)
**And** success returns me to login; voice/tone strings match EXPERIENCE.md; admin force-reset in Story 9.2 remains a separate Director action

---

## Epic 2: Case Registry, Search & Duplicate Prevention

### Story 2.1: Case Aggregate Schema and Create API

As a **Project Coordinator**,
I want to create a Case with core beneficiary and offence fields,
So that field work is tracked in one record (FR-3).

**Acceptance Criteria:**

**Given** I am authenticated as Coordinator
**When** I `POST /api/v1/cases` with required fields from PRD §5.2
**Then** Case is persisted with initial Stage and audit_events entry
**And** only `cases` and directly required child tables are created in this story's migration; invalid field validation returns 400

### Story 2.2: Six-Stage Lifecycle Transitions

As a **Coordinator**,
I want to advance Cases through six lifecycle Stages,
So that progress is structured and auditable (FR-3).

**Acceptance Criteria:**

**Given** a Case in Stage N
**When** I `PATCH /api/v1/cases/{id}/stage` with required sub-step data
**Then** Stage updates and audit_events records who/when/from/to
**And** Termination/Exclusion is the only soft-close path; hard delete is unavailable in v1

### Story 2.3: Unique Crime and ST Constraints with Duplicate Check

As a **Coordinator**,
I want duplicate Crime/ST blocked at save,
So that one beneficiary has one active record (FR-4, FR-5).

**Acceptance Criteria:**

**Given** an existing Case with Crime X
**When** I `POST /api/v1/cases/check-duplicate` or save with same Crime/ST
**Then** API returns match summary without creating duplicate
**And** DB enforces `UNIQUE(organisation_id, crime_number)` and `UNIQUE(organisation_id, st_number)`; conflict returns 409 on forced duplicate save

### Story 2.4: Duplicate Match Sheet on Web and Mobile Create

As a **staff member creating a Case**,
I want a blocking warning when Crime/ST matches an existing Case,
So that I review before saving (FR-5, UX-DR5).

**Acceptance Criteria:**

**Given** I enter a matching Crime or ST on create form
**When** duplicate check returns a match
**Then** duplicate match sheet blocks save with Open existing, Merge (Coordinator only), Cancel
**And** modal uses focus trap and `aria-modal`; no "create duplicate" action; save disabled until resolved

### Story 2.5: Case Merge Workflow

As a **Project Coordinator**,
I want to merge duplicate intake into an existing Case,
So that history is preserved (FR-5, UJ-3).

**Acceptance Criteria:**

**Given** duplicate sheet shows match
**When** Coordinator confirms `POST /api/v1/cases/{id}/merge`
**Then** duplicate draft is abandoned and audit trail links merge event
**And** single active Crime/ST remains; merge unavailable to Social Worker role

### Story 2.6: Case Search, Filters, and Saved Presets

As a **supervisor**,
I want to search and filter Cases quickly,
So that I find records without Excel (FR-6).

**Acceptance Criteria:**

**Given** Cases exist in pilot volume
**When** I search by crime, ST, name, contact, or area with filters (stage, offence, staff, overdue, district)
**Then** results return in under 2 seconds at 10k-case assumption
**And** users can save and reload filter presets per user; web registry supports `/` focus search shortcut

### Story 2.7: Case Export to Excel and PDF

As a **Coordinator**,
I want to export filtered Cases,
So that I share read-only reports without operational Excel sync (FR-6).

**Acceptance Criteria:**

**Given** a filtered case list
**When** I request export Excel or PDF
**Then** file downloads matching current filter columns
**And** export is read-only snapshot; does not write back to Excel as sync channel

### Story 2.8: Case Assignment Transfer and Handoff Whisper

As a **Social Worker receiving a transferred Case**,
I want a compressed handoff summary,
So that I start visits without reading full history (FR-7, UX-DR4).

**Acceptance Criteria:**

**Given** a Case assigned to me within the last 7 days
**When** I open Case detail on mobile or web
**Then** Handoff Whisper shows max 3 lines (prior actions, open items, next visit purpose) plus "View full timeline"
**And** whisper hides after day 8; full notes timeline remains accessible

### Story 2.9: Web Case Registry and Detail UI

As a **Coordinator**,
I want web screens to browse and edit Cases,
So that desk work is efficient (FR-3, FR-6, UX-DR11).

**Acceptance Criteria:**

**Given** I am logged in on web
**When** I use sidebar Case registry and open detail
**Then** create, search, stage edit, and handoff display work via generated api-client only
**And** sidebar IA includes Crisis Queue placeholder route, Registry, Reports, Legends, Admin per UX-DR11

---

## Epic 3: Field Visit Day (Command Strip & Offline)

### Story 3.1: Visit Scheduler API

As a **field worker**,
I want today, weekly, and overdue visit lists,
So that I know what to execute (FR-8).

**Acceptance Criteria:**

**Given** scheduled visits assigned to me
**When** I call `GET /api/v1/visits/today`, weekly, and overdue endpoints
**Then** visits return time-ordered with Case summary DTO
**And** completing visit increments Case visit count; reschedule requires reason persisted and visible to supervisor API

### Story 3.2: Morning Command Strip Mobile Home

As a **Social Worker**,
I want Today tab as my home queue,
So that I start visits without a dashboard (FR-12, UX-DR2, UX-DR10).

**Acceptance Criteria:**

**Given** I open the mobile app after login
**When** Today tab loads
**Then** Command Strip shows time-ordered visits, overdue badge, court countdown banner, Navigate + Start Visit per row
**And** matches `mockups/command-strip-today.html` semantics; pull-to-refresh works; cold open shows cached strip with skeleton if empty

### Story 3.3: Active Visit Flow with Start and Complete

As a **Social Worker**,
I want to start and complete visits with notes,
So that supervisors see progress (FR-8, FR-11).

**Acceptance Criteria:**

**Given** a visit on my strip
**When** I tap Start Visit then Complete with note
**Then** `POST /visits/{id}/start` and `/complete` persist state and notes
**And** completion visible to supervisor APIs within normal sync path; reschedule from visit requires reason field

### Story 3.4: GPS Capture, Landmark, and Google Maps Navigation

As a **Social Worker**,
I want GPS verified and Maps navigation,
So that visit location is trustworthy (FR-9).

**Acceptance Criteria:**

**Given** a visit with Case GPS data
**When** GPS is unverified
**Then** UI prompts "Capture landmark before navigate" before opening Google Maps
**And** verified GPS stores coordinates, landmark, verified date/by on Case; unverified flagged in API

### Story 3.5: Proximity Visit Grouping Suggestion

As a **Social Worker**,
I want same-day visits grouped by proximity,
So that I travel efficiently (FR-10).

**Acceptance Criteria:**

**Given** multiple visits today with verified GPS
**When** I request grouping suggestion on Today tab
**Then** API returns clusters with suggested order and manual reorder option
**And** cases with unverified GPS excluded until landmark captured

### Story 3.6: Offline Visit Storage and Sync Push API

As a **Social Worker in low signal**,
I want visits saved locally and synced later,
So that no data is lost (FR-11, NFR-6, NFR-7).

**Acceptance Criteria:**

**Given** device is offline
**When** I start/complete visit with notes
**Then** mutations queue locally with `clientMutationId`
**And** `POST /api/v1/sync/push` replays idempotently; server wins conflicts except note merge by timestamp; failed items remain in queue for support inspection

### Story 3.7: Sync Chip and Sync Queue Mobile UI

As a **Social Worker**,
I want visible sync status,
So that I trust what is saved (FR-11, UX-DR6, UX-DR12).

**Acceptance Criteria:**

**Given** local, pending, synced, or error sync states
**When** I view Command Strip or active visit
**Then** sync chip shows text labels ("Saved on this device", "Uploading", "Synced", "Sync failed")
**And** error chip tap opens More → Sync queue with retry; no silent failures

### Story 3.8: Discreet POCSO Capture Mode

As a **Social Worker on a POCSO Case in public**,
I want minimal on-screen beneficiary detail,
So that privacy is protected (NFR-3, UX-DR7).

**Acceptance Criteria:**

**Given** Case `sensitivity_level = POCSO`
**When** I open active visit
**Then** discreet header shows initials + crime number only
**And** expand full detail requires OTP re-entry (same session OTP within 5 min acceptable); list APIs return initials-only DTOs; PII access logged in audit (NFR-4)

---

## Epic 4: Case Notes & Interventions

### Story 4.1: Case Notes API and Timeline

As a **staff member**,
I want typed notes on Cases,
So that activity is documented (FR-13).

**Acceptance Criteria:**

**Given** a Case I can access
**When** I `POST /api/v1/cases/{id}/notes` with type Visit/Court/Intervention/General and optional action due date
**Then** note appears in chronological timeline with author and timestamp
**And** every note mutation writes audit_events

### Story 4.2: Attachment Presign Upload for Notes

As a **staff member**,
I want to attach files to notes securely,
So that evidence is stored safely (FR-13, NFR-5).

**Acceptance Criteria:**

**Given** a note being created
**When** I follow presign → PUT blob → confirm flow
**Then** attachment links to note with role-scoped SAS URL expiring 15 minutes
**And** unauthorized role cannot obtain URL; upload failures surface to user

### Story 4.3: Notes Timeline UI (Web and Mobile)

As a **staff member**,
I want to read and add notes on Case detail,
So that context is shared across roles (FR-13).

**Acceptance Criteria:**

**Given** Case detail on web or mobile
**When** I view timeline and add a note with attachment
**Then** entries render chronologically with type badges
**And** uses generated api-client only; attachment preview respects role access

### Story 4.4: Interventions CRUD API

As a **Case Worker**,
I want to track interventions needed and provided,
So that support actions are accountable (FR-14).

**Acceptance Criteria:**

**Given** a Case
**When** I create/update intervention with type, priority, status, outcome, assigned staff
**Then** records persist with Legends category reference
**And** RBAC limits edit to assigned roles; mutations audited

### Story 4.5: Interventions UI and Overdue Job

As a **Case Worker**,
I want overdue intervention alerts,
So that nothing stalls (FR-14, Flow 5).

**Acceptance Criteria:**

**Given** intervention past due date with open status
**When** daily overdue job runs
**Then** assignee receives push notification and intervention appears in notification centre
**And** updating status + outcome clears overdue alert; UI on mobile Case detail matches court/intervention row patterns (UX-DR9)

---

## Epic 5: Court Sittings, Reminders & Miss Prevention

### Story 5.1: Court Sitting CRUD API

As a **Case Worker**,
I want to record court sittings on Cases,
So that appearances are tracked (FR-15).

**Acceptance Criteria:**

**Given** a Case
**When** I CRUD court sitting with date/time, court name, purpose, status Upcoming/Attended/Postponed
**Then** sitting persists with notes/outcome and optional next court date on completion
**And** sittings appear on assigned worker schedule endpoints

### Story 5.2: Court Schedule UI (Web Case Detail and Mobile)

As a **Case Worker**,
I want to view and update sittings,
So that I prepare for court (FR-15, UX-DR9).

**Acceptance Criteria:**

**Given** upcoming sittings exist
**When** I view court schedule on mobile Today header/filter or web Case detail
**Then** rows show date, court, status chip; past-due Upcoming uses critical accent
**And** empty state shows "No sittings this week" (UX-DR13)

### Story 5.3: Court Reminder Background Job

As a **field worker and Coordinator**,
I want reminders 24 hours before court,
So that sittings are not missed (FR-16, SM-3).

**Acceptance Criteria:**

**Given** sitting status Upcoming in 24 hours
**When** reminder job runs
**Then** assigned field worker receives mobile push; Coordinator receives email; web user receives email for tomorrow's sitting
**And** delivery logged; no duplicate sends within same window

### Story 5.4: Court Miss Escalation and Crisis Queue Feed

As a **Project Coordinator**,
I want missed sittings escalated automatically,
So that I intervene before harm (FR-17, UJ-2).

**Acceptance Criteria:**

**Given** sitting date passed with status still Upcoming
**When** hourly escalation job runs
**Then** Case flagged, Coordinator notified, Crisis Queue API includes critical court-miss row
**And** flag clears when status updated to Attended or Postponed; row uses crisis-row-critical styling (UX-DR3)

---

## Epic 6: Travel Claims & Director Approval

### Story 6.1: Travel Claim API with Receipt Validation

As a **Social Worker**,
I want to submit travel claims with evidence,
So that allowances are reimbursed fairly (FR-18).

**Acceptance Criteria:**

**Given** claim in Draft
**When** I submit with mandatory receipt images for bus/auto/petrol modes
**Then** status moves to Submitted; missing receipt returns 422
**And** monthly totals per staff available via supervisor API; workflow Draft → Submitted → Approved (Director)

### Story 6.2: Mobile Travel Claim Capture

As a **Social Worker**,
I want to create claims from More tab with photos,
So that I submit from the field (FR-18, UX-DR13).

**Acceptance Criteria:**

**Given** More → Travel
**When** I create draft with receipt photo capture
**Then** claim saves locally if offline (sync scope) and submits when online
**And** empty state "No claims yet" + create CTA; voice/tone per EXPERIENCE.md

### Story 6.3: Director Claim Approval on Web

As a **Project Director**,
I want to approve or return claims with comments,
So that spend is controlled (FR-18, Flow 6).

**Acceptance Criteria:**

**Given** submitted claim with receipt images
**When** Director approves or returns from Admin/Crisis Queue neutral claim row
**Then** status updates and comment stored
**And** claimant notified via notification pipeline; pending claims appear in Crisis Queue (UX-DR3 neutral)

### Story 6.4: Claim Status Notifications

As a **claimant**,
I want approval status notifications,
So that I know outcome without WhatsApp chasing (FR-18, FR-19 partial).

**Acceptance Criteria:**

**Given** claim decision recorded
**When** notification service processes event
**Then** claimant receives mobile push and in-app notification
**And** monthly total widget updates for supervisor dashboard (Epic 8 dependency: event emitted even if dashboard UI follows in Epic 8)

---

## Epic 7: Notifications & In-App Centre

### Story 7.1: Device Token Registration and Notification Store

As a **user**,
I want notifications stored and devices registered,
So that alerts reach me (FR-19).

**Acceptance Criteria:**

**Given** logged-in mobile or web user
**When** app registers FCM/APNs token to `user_devices`
**Then** `GET/PATCH /api/v1/notifications` supports list and read/unread
**And** tokens removed on logout; preferences stub per role for v1

### Story 7.2: Push Delivery Service

As a **field worker**,
I want push for visits, court, interventions, and claims,
So that I act on time (FR-19).

**Acceptance Criteria:**

**Given** notification events from visits, court, interventions, claims epics
**When** push sender runs
**Then** FCM/APNs delivers to registered devices with actionable payload
**And** failures logged; no SMS/WhatsApp in v1 (deferred v1.1)

### Story 7.3: Email Notification Templates

As a **Coordinator**,
I want email for court tomorrow, claims, assignments, and reports,
So that desk staff stay informed (FR-19).

**Acceptance Criteria:**

**Given** configured SMTP/SendGrid
**When** email-triggering events fire
**Then** templates send to correct roles (court tomorrow, claim status, new assignment, report ready)
**And** emails contain no full beneficiary PII for POCSO cases beyond policy

### Story 7.4: In-App Notification Bell (Web and Mobile)

As a **user**,
I want a notification centre with read state,
So that I review alerts in one place (FR-19, UX-DR13).

**Acceptance Criteria:**

**Given** unread notifications exist
**When** I open bell on web or mobile
**Then** list shows read/unread with tap-through to Case or claim
**And** empty state "You're up to date"; deep links respect RBAC

---

## Epic 8: Supervisor Crisis Queue, Dashboard & Reports

### Story 8.1: Crisis Queue Prioritized API

As a **Project Coordinator**,
I want a prioritized crisis feed,
So that I triage risks first (FR-21).

**Acceptance Criteria:**

**Given** overdue visits, court <48h without prep, recent handoffs, pending claims exist
**When** I `GET /api/v1/supervisor/crisis-queue`
**Then** DTO returns rows sorted critical → warning → info → neutral with Case and worker ids
**And** Redis cache TTL 30s; not composed client-side from generic lists

### Story 8.2: Crisis Queue Web UI

As a **Project Coordinator**,
I want Crisis Queue as my home,
So that I prevent misses (FR-21, UX-DR3, UJ-2).

**Acceptance Criteria:**

**Given** I log in as Coordinator
**When** home loads
**Then** Crisis Queue renders severity rows with badge text Overdue, Court 48h, Handoff, Claim
**And** matches `mockups/crisis-queue.html`; empty state "No urgent items" with links to Dashboard and Cases; row click opens Case; 200% zoom wraps without horizontal scroll (UX-DR15)

### Story 8.3: Dashboard API and Redis-Cached Widgets

As a **Coordinator or Director**,
I want dashboard metrics,
So that I see organisation status (FR-20, NFR-8).

**Acceptance Criteria:**

**Given** field data synced
**When** I `GET /api/v1/supervisor/dashboard`
**Then** widgets return cases by stage/offence/area/staff, overdue visits, interventions gauge, court this week, pending claims, 12-month intake trend
**And** data reflects field updates within 60 seconds of sync; widget counts cached in Redis

### Story 8.4: Dashboard Web UI

As a **supervisor**,
I want chart dashboard as secondary view,
So that I analyze trends after triage (FR-20, UX-DR11).

**Acceptance Criteria:**

**Given** Dashboard sidebar route
**When** page loads
**Then** skeleton widgets render then populate from API
**And** no gamification or worker rankings (NFR-11, UX-DR16); light mode default (UX-DR17)

### Story 8.5: Standard Reports API and Async Export Jobs

As a **Coordinator**,
I want operational reports exported,
So that funders and courts receive evidence (FR-22, SM-C1).

**Acceptance Criteria:**

**Given** report type selected
**When** I `POST /api/v1/reports/{type}/export` with Excel or PDF format
**Then** async job generates file via ClosedXML/QuestPDF to blob and returns download URL when ready
**And** requests >30s never block HTTP; staff workload report is distribution-only not performance scoring

### Story 8.6: Reports Web UI with Export Progress

As a **Coordinator**,
I want to generate reports from web,
So that I avoid manual Excel assembly (FR-22, UX-DR13).

**Acceptance Criteria:**

**Given** Reports sidebar page
**When** I start export
**Then** progress indicator shows and duplicate export disabled while running
**And** completed download uses signed URL; error surfaces clearly

### Story 8.7: Angular PWA Offline Snapshot Cache

As a **Coordinator on patchy office Wi‑Fi**,
I want read-only queue/dashboard when offline,
So that I can still orient (NFR-12).

**Acceptance Criteria:**

**Given** PWA installed with service worker
**When** network drops
**Then** last Crisis Queue/dashboard snapshot displays with stale banner
**And** mutations (create case, approve claim) require network; app shell prefetched per ngsw-config

---

## Epic 9: Admin — Legends, Staff Directory & Audit

### Story 9.1: Legends CRUD API and Web UI

As a **Project Coordinator**,
I want to manage master data without Excel,
So that dropdowns stay current (FR-23).

**Acceptance Criteria:**

**Given** Legends types listed in PRD
**When** I CRUD offence types, classifications, intervention categories, education, occupation, outcomes, areas, designations, police stations
**Then** changes persist and appear on next client fetch
**And** empty category shows empty table + add row CTA (UX-DR13); mutations audited

### Story 9.2: Staff Directory Management

As a **Project Director**,
I want to manage staff accounts and roles,
So that access matches organisation structure (FR-24, FR-1 admin actions).

**Acceptance Criteria:**

**Given** Admin staff screen
**When** Director creates, edits role, deactivates, or forces password reset
**Then** user record updates and token_version increments on role change/deactivation
**And** deactivated users cannot complete OTP login

### Story 9.3: Audit Log API and Director UI

As a **Project Director**,
I want audit history of mutations,
So that compliance is demonstrable (FR-25, NFR-9).

**Acceptance Criteria:**

**Given** mutations occurred across epics
**When** Director queries `GET /api/v1/audit` with filters
**Then** who/what/when returned for data-changing events
**And** POCSO PII access events included (NFR-4); retention policy documented as 7-year assumption

### Story 9.4: Angular Material Theme from DESIGN Tokens

As a **user on web**,
I want consistent operational styling,
So that status and trust cues are clear (UX-DR1, UX-DR15, UX-DR17).

**Acceptance Criteria:**

**Given** Angular Material theme configured
**When** I view Crisis Queue, forms, and buttons
**Then** colors match DESIGN.md semantic tokens (teal primary, status critical/warning/info/neutral, surfaces)
**And** WCAG AA contrast pairs verified for crisis rows and primary buttons; dark mode tokens supported

---

## Epic 10: Legacy Excel Migration

### Story 10.1: Excel Column Mapping Specification and Validation

As a **Project Director**,
I want validated mapping from legacy Excel to Case fields,
So that migration is accurate (addendum, open question #5).

**Acceptance Criteria:**

**Given** sample legacy Excel from pilot NGO
**When** mapping document defines column → field pairs with validation rules
**Then** unmapped required PRD fields flagged before import runs
**And** mapping reviewed and stored in repo docs; no ongoing sync semantics

### Story 10.2: One-Time Import Tool API and Admin UI

As a **Project Director**,
I want to import legacy Cases once,
So that cloud becomes source of truth (MVP §6.1, addendum).

**Acceptance Criteria:**

**Given** validated Excel file uploaded
**When** Director runs import from Admin
**Then** Cases created with duplicate check per Crime/ST; import summary shows success/skipped/errors
**And** import is idempotent-safe via dry-run mode; post-migration Excel exports are read-only from Kaval only; WhatsApp remains non-integration per addendum

