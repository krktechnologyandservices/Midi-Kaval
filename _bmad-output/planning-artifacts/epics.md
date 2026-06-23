---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md
  - _bmad-output/planning-artifacts/architecture.md
extracted_fr_count: 16
extracted_nfr_count: 16
extracted_ar_count: 10
epic_count: 6
story_count: 14
---

# Midi-Kaval - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for the Role Management & Registration System, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: The Vendor (authenticated via 2FA) can submit an organisation name and Director email, generating a signed, time-bound, single-use activation link.
FR-2: The activation link recipient can register as the first Director by setting their name and password; the organisation is marked active on completion.
FR-3: When an organisation reaches zero active Directors, the Vendor is notified and can re-issue an activation link (Vendor Safety Net).
FR-4: A Director can view a paginated, filterable, sortable list of all users in their organisation with name, email, role, and status.
FR-5: A Director can invite a new user by entering their email and selecting their role; the system sends a single-use invitation link.
FR-6: A Director can suspend any user (including another Director) to immediately revoke their access; suspension is reversible.
FR-7: A Director can reactivate a suspended user, restoring full access.
FR-8: A Director can permanently delete (anonymise) a user — PII is scrubbed, but the row persists for audit integrity. This is irreversible.
FR-9: The system prevents suspension or permanent deletion of a Director if no other active Director remains (Last-Director Protection).
FR-10: A Director cannot perform any user management action until they have enrolled in TOTP-based 2FA.
FR-11: Invited users register through a double-confirmation flow: invitation link → registration form → confirmation email → account activated.
FR-12: Directors can view pending invitations, resend them (invalidating previous links), and expired invitations are auto-cleaned.
FR-13: Every user management action records an immutable audit event with actor, target, identity snapshot, timestamp, and IP.
FR-14: Directors can view the audit log with search and filter by event type, actor, target user, and date range.
FR-15: Every user creation, suspension, reactivation, and permanent deletion triggers a batched email notification to all active Directors.
FR-16: Suspended or permanently deleted users receive email notification about their account status change (rate-limited to 3/24h per type).

### NonFunctional Requirements

NFR-1 (Security): All invitation and activation links must be cryptographically signed (HMAC-SHA256 or asymmetric signature).
NFR-2 (Security): Registration/invitation endpoints must implement rate limiting: 10 req/min per IP (burst 20), 5 req/h per email. Returns HTTP 429.
NFR-3 (Security): 2FA tokens must follow RFC 6238 with a 30-second window. TOTP only (no SMS/email backup codes in v1).
NFR-4 (Security): Audit log must be append-only; application DB user has INSERT-only privileges on audit_events table.
NFR-5 (Security): All management action POST/PUT/DELETE endpoints must include CSRF protection.
NFR-6 (Security): All URLs containing tokens must enforce HTTPS; HSTS headers required.
NFR-7 (Security): Session management strategy must support token revocation for suspension — short-lived JWT (15-min) with refresh token rotation, or server-side sessions.
NFR-8 (Performance): Invitation email delivery latency < 5 minutes (p95).
NFR-9 (Performance): User Management Dashboard page load < 2 seconds with 1,000 users.
NFR-10 (Performance): Audit log query with combined filters < 3 seconds for 100,000 events.
NFR-11 (Performance): Email delivery queue must process at least 100 emails per minute.
NFR-12 (Availability): Registration/invitation endpoints must have 99.9% availability SLA.
NFR-13 (Availability): Email provider failures must be handled with exponential backoff (1 min, 5 min, 15 min, then flagged for manual review).
NFR-14 (Availability): Email delivery failures visible to Directors in the dashboard (per-invitation status).
NFR-15 (Migration): All existing hardcoded accounts must be migrated to the new DB model before cutover.
NFR-16 (Migration): Dual auth (config-file + DB) supported during migration window to allow rollback.

### Additional Requirements (Architecture)

- AR-1: JWT access token (15 min) + refresh token (httpOnly cookie web / secure storage mobile) pattern.
- AR-2: Token version on `users` table for force logout on role change/deactivation — supports FR-6 suspension.
- AR-3: API uses RFC 7807 Problem Details for error responses — important for FR-9 error message format.
- AR-4: UUID v4 for all entity IDs.
- AR-5: ISO 8601 UTC timestamps.
- AR-6: Pagination pattern: `?page=1&pageSize=25` with `meta.totalCount`.
- AR-7: API endpoints use `/api/v1/` prefix.
- AR-8: Policy-based authorization (`[Authorize(Policy = Policies.DirectorOnly)]`) for all management endpoints.
- AR-9: Integration tests using WebApplicationFactory + Testcontainers PostgreSQL.
- AR-10: All business rules live in API, not duplicated in clients.

### UX Design Requirements

None — this is a backend/enterprise feature with no UX design document.

### FR Coverage Map

| Feature Group | FRs | NFRs | ARs |
|---|---|---|---|
| Vendor Backstage & Organisation Bootstrap | FR-1, FR-2, FR-3 | NFR-1, NFR-2, NFR-12, NFR-13, NFR-14 | AR-3, AR-4, AR-5, AR-7, AR-8 |
| Director User Management Dashboard | FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10 | NFR-5, NFR-6, NFR-7, NFR-9 | AR-1, AR-2, AR-3, AR-4, AR-5, AR-6, AR-8 |
| Invitation & Registration Flow | FR-11, FR-12 | NFR-1, NFR-2, NFR-8, NFR-11, NFR-14 | AR-3, AR-4, AR-5, AR-7 |
| Audit Trail | FR-13, FR-14 | NFR-4, NFR-10 | AR-3, AR-4, AR-5, AR-6, AR-7, AR-8 |
| Notifications | FR-15, FR-16 | NFR-8, NFR-11, NFR-12, NFR-13, NFR-14 | AR-7 |
| Data Model & Migration | — | NFR-15, NFR-16 | AR-4, AR-5, AR-9, AR-10 |

## Epic List

### Epic 1: Vendor Backstage & Organisation Bootstrap

**Goal:** Enable the Vendor to bootstrap new client organisations and Directors to self-register for the first time. Covers the activation link flow, first Director registration, and the Vendor Safety Net for recovering organisations that lose all Directors.

**Covers:** FR-1, FR-2, FR-3, NFR-1, NFR-2, NFR-12, NFR-13, NFR-14, AR-3, AR-4, AR-5, AR-7, AR-8

#### Story 1.1: Data Model — Add organisations and activation_tokens tables

As a developer,
I want the database schema to support organisations and activation tokens with their relationships,
So that the Vendor bootstrap flow has a persisted foundation.

**Acceptance Criteria:**

- An `organisations` table exists with: `id` (UUID PK), `name` (varchar), `is_active` (boolean, default false), `created_at_utc` (timestamp)
- An `activation_tokens` table exists with: `id` (UUID PK), `organisation_id` (FK → organisations), `token_hash` (varchar, SHA-256), `target_email` (varchar), `expires_at_utc` (timestamp), `consumed_at_utc` (nullable timestamp), `created_at_utc` (timestamp)
- The `users` table is extended with: `organisation_id` (FK → organisations, nullable), `role` (varchar), `is_suspended` (boolean, default false), `totp_secret` (nullable varchar), `totp_enrolled_at` (nullable timestamp)
- EF Core migration is generated and can be applied
- Existing seed data accommodates the new columns (default values for existing rows)

#### Story 1.2: Vendor Backstage Portal — Authentication & Activation Link Generation

As a Vendor,
I want a secure 2FA-protected backstage portal where I can submit an organisation name and Director email to generate and send an activation link,
So that I can bootstrap a new client organisation without hardcoded config files.

**Acceptance Criteria:**

- Vendor backstage portal requires 2FA authentication (TOTP)
- Vendor submits: organisation name + target Director email
- System generates a cryptographically signed (HMAC-SHA256) activation token
- Token is stored as SHA-256 hash in `activation_tokens` table with 7-day expiry (configurable via `ACTIVATION_TOKEN_TTL_HOURS`)
- A one-time activation link is emailed to the target address
- If email delivery fails, system retries with exponential backoff (max 3 retries, 5-minute interval)
- After 3 failed delivery retries, Vendor sees "delivery failed" status in the backstage portal
- Rate limiting: 10 req/min per IP (burst 20), 5 req/h per email — returns HTTP 429 with Retry-After header

#### Story 1.3: First Director Registration Flow

As a recipient of an activation link,
I want to open the link, set my name and password, and activate as the first Director of my organisation,
So that I can begin using Midi-Kaval as the administrator.

**Acceptance Criteria:**

- Activation link opens an unauthenticated registration page
- Email is pre-filled from the activation token; recipient sets full name and password
- Password must meet minimum policy: 8+ chars, 1 uppercase, 1 lowercase, 1 digit
- Password complexity requirements are displayed before submission
- Registration succeeds only with a valid, non-expired, unused token
- Organisation is set to `is_active = true` after registration
- Expired or consumed token displays: "This link has expired or already been used. Please contact the Vendor to request a new activation link."
- After registration, the user account exists with role = Director and can log in immediately
- The raw activation token is consumed (consumed_at_utc set) and cannot be reused

#### Story 1.4: Vendor Safety Net — Zero-Director Recovery

As a Vendor,
I want to be automatically notified when an organisation has zero active Directors and be able to re-issue an activation link,
So that organisations aren't permanently stranded.

**Acceptance Criteria:**

- Event-driven detection: when the last active Director is deleted or deactivated, the system immediately triggers recovery
- Vendor receives an email alert + dashboard indicator in the backstage portal
- Vendor can re-issue an activation link for an existing organisation (same flow as Story 1.2)
- The new Director takes ownership of the organisation
- Fallback: if event-driven detection fails, a monitoring check runs within [ASSUMPTION AS-1: 1 hour]

---

### Epic 2: Director User Management Dashboard

**Goal:** Enable Directors to manage all users in their organisation — view the user list, invite new users, suspend/reactivate, permanently delete, and manage Director 2FA. Enforces Last-Director Protection.

**Covers:** FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, NFR-5, NFR-6, NFR-7, NFR-9, AR-1, AR-2, AR-3, AR-4, AR-5, AR-6, AR-8

#### Story 2.1: Extend Users Schema with Role and Organisation Columns

As a developer,
I want the users table to support multiple organisations with role-based access,
So that the Director management dashboard has a data foundation.

**Acceptance Criteria:**

- Users table has `organisation_id` FK (nullable — null for pre-migration users), `role` column (varchar), `is_suspended` (boolean, default false), `totp_secret` (nullable), `totp_enrolled_at` (nullable), `token_version` (int, default 0)
- Token version supports force-logout pattern (increment on role change / deactivation)
- EF Core migration is generated and can be applied
- Existing users get a default organisation assignment via the migration script

#### Story 2.2: User Management Dashboard — List, Filter, Sort

As a Director,
I want to view a paginated table of all users in my organisation with filter and sort capability,
So that I have full visibility of who has access.

**Acceptance Criteria:**

- Director-only page accessible via navigation (authorized via `[Authorize(Policy = Policies.DirectorOnly)]`)
- Table shows columns: full name, email, role, status (active/suspended), creation date
- Default sort is by creation date descending
- Columns sortable individually by clicking column headers
- Filters: text search (name/email), dropdown filter by role, dropdown filter by status (active/suspended)
- Filters can be combined simultaneously
- Paginated with `?page=1&pageSize=25` and `meta.totalCount`
- End Users see their own profile only, not the full user list
- Page loads within 2 seconds with 1,000 users

#### Story 2.3: Invite New User Flow

As a Director,
I want to invite a new user by entering their email and selecting their role,
So that new team members can join the organisation through a secure registration flow.

**Acceptance Criteria:**

- Director can enter a target email and select a role (Director or any lower role) from a dropdown
- Invitation link is generated (cryptographically signed, 7-day expiry via `INVITATION_TOKEN_TTL_HOURS`) and emailed to the target
- Duplicate invitation to an already-registered email returns: "This email address is already registered."
- Duplicate invitation to a pending (unconfirmed) invitation returns an error and suggests using the resend feature
- Rapid double-click does not create duplicate invitations — unique constraint on `(organisation_id, target_email, status)` where status = `pending` prevents it
- Director receives a confirmation that the invitation was sent
- All POST requests include CSRF protection

#### Story 2.4: User Suspension and Reactivation

As a Director,
I want to suspend a user to temporarily revoke their access and later reactivate them,
So that I can manage access without permanently removing anyone.

**Acceptance Criteria:**

- Director can suspend any user (including another Director) from the dashboard
- Suspended user cannot log in or access any API endpoints (middleware checks `is_suspended` flag on every request)
- Suspended user receives an email notification with the reason (if provided)
- Active sessions must be terminated — requires short-lived JWT (15-min TTL) with refresh token rotation, OR server-side sessions with revocation endpoint
- Suspension is reversible: Director can reactivate from the dashboard
- Reactivated user can log in with existing credentials
- Reactivation is logged as an audit event (FR-13)
- Reactivated user receives an email notification
- Last-Director Protection applies: cannot suspend the last active Director

#### Story 2.5: Permanent Deletion (User Anonymisation)

As a Director,
I want to permanently delete a user, removing their access and anonymising their data,
So that departing team members no longer have access.

**Acceptance Criteria:**

- Director can permanently delete any non-last-Director user (FR-9 guard)
- Deletion requires explicit confirmation: Director must type the target user's email address (case-insensitive, Unicode-safe)
- On anonymisation: name → "Deleted User", email → `deleted-{uuid}@anonymised.local`, password hash cleared, `is_suspended` set to false
- User cannot log in after anonymisation
- Audit event records the deletion with actor identity and a snapshot of the target's identity at time of anonymisation (stored in `target_user_snapshot`)
- The action is logged to the audit log (FR-13)
- Deleted user receives an email notification confirming the action and noting it is irreversible
- Notification email to the affected user is rate-limited: max 3 emails of same type per 24 hours

#### Story 2.6: Last-Director Protection

As a Director,
I want the system to prevent me from being the last active Director and being suspended or deleted,
So that the organisation never becomes unmanaged.

**Acceptance Criteria:**

- API returns 400 with message: "Cannot deactivate — no other active Director remains in the organisation." when attempting to suspend/delete the last Director
- UI shows the action as disabled with a tooltip showing the same explanation
- Self-deactivation by the last Director is also blocked
- The guard applies to both suspension AND permanent deletion
- The guard checks are performed server-side (never trust client-side role checks)

#### Story 2.7: Director 2FA Mandate & Recovery

As a Director,
I must enroll in two-factor authentication before managing any users,
So that user management actions are protected by an additional security layer.

**Acceptance Criteria:**

- Unenrolled Director sees a full-page modal prompting 2FA setup when they attempt any management action
- Modal blocks the attempted action until 2FA is enrolled or dismissed
- 2FA is TOTP-based (authenticator app only, RFC 6238, 30-second window)
- Once enrolled, 2FA is required at each login for Directors
- Recovery: another active Director can reset a Director's 2FA enrollment from the User Management Dashboard
- Recovery fallback: Vendor can reset 2FA via the backstage portal
- After 2FA reset, the affected Director must re-enroll before performing management actions

---

### Epic 3: Invitation & Registration Flow

**Goal:** Implement the double-confirmation registration flow for invited users, including pending invitation management, expiry, and resend capabilities.

**Covers:** FR-11, FR-12, NFR-1, NFR-2, NFR-8, NFR-11, NFR-14, AR-3, AR-4, AR-5, AR-7

#### Story 3.1: Invitations Data Model

As a developer,
I want an `invitations` table to track pending, confirmed, and expired invitations,
So that the invitation lifecycle is persisted.

**Acceptance Criteria:**

- `invitations` table with: `id` (UUID PK), `organisation_id` (FK), `invited_by_user_id` (FK → users), `target_email` (varchar), `role` (varchar), `token_hash` (varchar), `expires_at_utc` (timestamp), `status` (enum: pending/confirmed/expired), `created_at_utc` (timestamp), `confirmed_at_utc` (nullable timestamp)
- Unique constraint on `(organisation_id, target_email, status)` where status = `pending`
- EF Core migration generated

#### Story 3.2: Double-Confirmation Registration Flow

As an invited user,
I want to click my invitation link, set up my account, and confirm my email to activate,
So that my identity is verified before I can access the application.

**Acceptance Criteria:**

- Invitation link opens an unauthenticated registration page
- User sets full name and password (meeting minimum complexity policy)
- On submission, a confirmation email is sent to the registered address
- Account exists in "pending confirmation" state — cannot log in yet
- Confirmation link is time-bound (24 hours, configurable via `CONFIRMATION_TOKEN_TTL_HOURS`)
- After clicking confirmation link, account is marked active and user can log in immediately
- Confirmation email delivery is retried with exponential backoff (max 3 retries)
- If confirmation email fails after all retries, invitation shows "delivery failed" status in the Director dashboard

#### Story 3.3: Pending Invitations Management & Resend

As a Director,
I want to view pending invitations and resend them when needed,
So that I can manage the invitation lifecycle.

**Acceptance Criteria:**

- Pending invitations visible in the User Management Dashboard with status "Pending"
- Director can resend an invitation, generating a new link and invalidating the previous one
- Expired invitations show as "Expired" in the dashboard and cannot be used
- Daily cleanup job removes expired, unconsumed invitations
- When an invitation is resent (invalidating the previous one), the original inviting Director receives an email notification

---

### Epic 4: Audit Trail

**Goal:** Implement immutable audit event recording and a Director-only audit log viewer with search and filter capabilities.

**Covers:** FR-13, FR-14, NFR-4, NFR-10, AR-3, AR-4, AR-5, AR-6, AR-7, AR-8

#### Story 4.1: Audit Events Data Model & Recording

As a developer,
I want an append-only `audit_events` table that captures every user management action with identity snapshots,
So that the audit trail is comprehensive and immutable.

**Acceptance Criteria:**

- `audit_events` table: `id` (UUID PK), `organisation_id` (FK), `event_type` (varchar), `actor_user_id` (FK → users, nullable — SET NULL on delete), `target_user_id` (FK → users, nullable — SET NULL on delete), `target_user_snapshot` (JSONB — name+email at time of event), `ip_address` (varchar), `metadata_json` (JSONB, nullable), `created_at_utc` (timestamp)
- Application DB user has INSERT-only privileges on `audit_events` — UPDATE and DELETE rejected at DB level
- Every user management action (invite, suspend, reactivate, delete, 2FA reset) writes an audit event in the same DB transaction
- If the audit write fails, the triggering action is rolled back (fail-closed)
- FK constraints use `ON DELETE SET NULL` to preserve audit history when users are anonymised
- EF Core migration generated

#### Story 4.2: Audit Log Viewer

As a Director,
I want to view and search the audit log by event type, actor, target, and date range,
So that I can review all actions taken in my organisation.

**Acceptance Criteria:**

- Director-only page (authorized via `[Authorize(Policy = Policies.DirectorOnly)]`)
- Log is sorted by timestamp descending (most recent first)
- Filters: event type (dropdown), actor (user search), target user (user search), date range (date picker)
- Filters combine via AND logic; any subset of filters can be active
- Search by target user supports partial name/email matching against `target_user_snapshot` JSONB (anonymised users still searchable)
- Results paginated (default 50 per page)
- Query completes within 3 seconds for 100,000 events (composite indexes on event_type, created_at, organisation_id)
- End Users cannot access the audit log

---

### Epic 5: Notifications

**Goal:** Implement email notifications for user lifecycle events — batched digests to all Directors and individual notifications to affected users with rate limiting.

**Covers:** FR-15, FR-16, NFR-8, NFR-11, NFR-12, NFR-13, NFR-14, AR-7

#### Story 5.1: Audit Broadcast — Batched Email Digests to Directors

As a Director,
I want to receive email notifications when user management actions happen in my organisation,
So that I'm aware of all changes without having to constantly check the audit log.

**Acceptance Criteria:**

- Every user creation, suspension, reactivation, and permanent deletion triggers an email notification to all active Directors
- If multiple actions occur within 5 minutes, they are combined into a single digest email per Director
- Each email includes: action type, target user name/email, actor name/email, timestamp
- Directors cannot opt out of these notifications
- Email delivery uses exponential backoff for provider failures (1 min, 5 min, 15 min, then flagged)
- Delivery queue processes at least 100 emails per minute

#### Story 5.2: User Notification — Suspension and Deletion Emails

As a user,
I want to receive an email when my account is suspended or permanently deleted,
So that I'm informed of changes to my access.

**Acceptance Criteria:**

- Suspension notification includes the reason (if provided) and instructions to contact another Director for appeal
- Permanent deletion notification confirms the action and notes it is irreversible
- Notification emails to the same user are rate-limited: max 3 emails of the same type per 24-hour period
- Email delivery status is tracked; failures are retried

---

### Epic 6: Existing-User Migration

**Goal:** Migrate existing hardcoded accounts from configuration files to the new database-driven model with a safe cutover strategy.

**Covers:** NFR-15, NFR-16, AR-9, AR-10

#### Story 6.1: Migration Script for Existing Hardcoded Accounts

As a developer,
I want to create a one-time script that reads existing config-file accounts and creates users in the database,
So that the existing admin users are not lost during the transition.

**Acceptance Criteria:**

- Script reads the existing configuration file with hardcoded accounts
- Creates users in the `users` table with appropriate roles and a default "Primary Organisation"
- The first migrated user (original hardcoded admin) is assigned the Director role
- Passwords are preserved or reset with a temporary password requiring change on first login
- Script is idempotent (safe to re-run)

#### Story 6.2: Dual Auth Support During Migration Window

As a developer,
I want both the old config-file authentication and the new database-driven authentication to work during migration,
So that rollback is possible if issues arise.

**Acceptance Criteria:**

- During migration window, the auth layer checks both config file and database for credentials
- After successful migration verification, the config-file path can be disabled via a feature flag
- Integration tests verify that users can authenticate through both paths during the window
- The migration cutover is verified by integration tests before the config-file path is disabled
- Regular authentication falls back gracefully if one path is unavailable
