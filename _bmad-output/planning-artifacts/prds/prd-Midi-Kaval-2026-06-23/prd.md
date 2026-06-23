---
title: Role Management & Registration System for Midi-Kaval
status: draft
created: 2026-06-23
updated: 2026-06-23
sources:
  - _bmad-output/brainstorming/brainstorming-session-2026-06-23-1927.md
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/validation-report.md
validation:
  grade: Fair
  findings_resolved: 17
  findings_deferred: 8 (architectural — see Open Questions tagged BLOCKS)
---

# PRD: Role Management & Registration System for Midi-Kaval

## 0. Document Purpose

This PRD defines the Role Management & Registration System for Midi-Kaval. It is written for product stakeholders, engineering, and QA teams. The document is structured with Glossary-anchored vocabulary, features grouped with nested functional requirements (FRs), and assumptions tagged inline. It builds on a brainstorming session recorded in `_bmad-output/brainstorming/brainstorming-session-2026-06-23-1927.md` and has been updated following validation review.

## 1. Vision

Midi-Kaval currently relies on hardcoded roles and accounts in configuration files — a pattern that works for initial setup but breaks down in production. The Role Management & Registration System replaces this with a secure, self-service model where:

- The application vendor bootstraps the first Director via a one-time activation link
- Directors manage the full user lifecycle within their organisation — inviting new users, assigning roles, suspending, and deleting accounts
- Every action is logged in an immutable audit trail

This turns role management from a vendor-dependent config operation into an empowered, Director-owned capability — while maintaining security through 2FA mandates for Directors, double-confirmation invitation flows, and a vendor-operated "break glass" recovery path.

The system is designed for the B2B context where organisations have a limited, known set of trusted users. No public registration, no self-nomination — every user enters through a Director's invitation.

## 2. Target User

### 2.1 Personas

**Vendor** — The application provider (Midi-Kaval operations team). Operates a secure backstage portal to bootstrap new client organisations. Not a user of the application itself.

**Director** — Senior role within a client organisation. Full authority to manage users: invite, suspend, reactivate, and permanently delete accounts across all roles including other Directors. Must enroll in 2FA before performing any management actions.

**End User** — Coordinator, Field Worker, or any role below Director. Receives invitations, registers through double-confirmation flow, and uses the application. Has no user-management privileges.

### 2.2 Jobs To Be Done

- As the Vendor, I can activate a new client organisation by sending a one-time activation link to their designated Director's email, so the organisation can begin using Midi-Kaval.
- As a Director, I can invite a new user by providing their email and selecting their role, so they can join the organisation through a secure self-registration flow.
- As a Director, I can view all users in my organisation with their roles and account status (active/suspended), so I have full visibility of who has access.
- As a Director, I can suspend a user to temporarily revoke their access, and reactivate them later. This is reversible.
- As a Director, I can permanently delete a user. This action is irreversible.
- As a Director, I can deactivate another Director, provided they are not the last active Director in the organisation.
- As a Director, I must set up two-factor authentication before I can perform any user management actions.
- As an invited user, I can complete registration through a double-confirmation flow — first link takes me to a registration form, second (email) confirms my identity and activates my account.
- As an invited user, I receive an email notification when a Director creates, suspends, or deletes my account.

### 2.3 Non-Users (v1)

- **Self-registering users** — There is no public or anonymous registration page. All onboarding is Director-initiated.
- **Multi-tenant administrators** — There is no cross-organisation super-admin role. Each organisation is managed independently by its own Directors, with Vendor-level break-glass only for bootstrap and last-Director recovery.

## 3. Glossary

- **Director** — The highest role within an organisation. Has full authority to invite, suspend, reactivate, and permanently delete users of any role, including other Directors.
- **End User** — Any non-Director role (Coordinator, Field Worker, etc.). Has no user-management capabilities.
- **Organisation** — A client entity using Midi-Kaval. Has its own set of Directors and End Users. Organisations are isolated from each other.
- **Vendor** — The application provider. Operates the secure backstage portal. Has no direct access to organisation data.
- **Activation Link** — A cryptographically signed, time-bound, single-use URL sent by the Vendor to the first Director's email. Consumed to bootstrap a new organisation.
- **Invitation** — A Director-initiated email to a prospective user containing a registration link.
- **Double-Confirmation Flow** — The two-step registration process: (1) invitation link opens a registration form, (2) a confirmation email is sent and must be clicked to activate the account.
- **2FA (Two-Factor Authentication)** — Mandatory security measure for Directors. TOTP-based (authenticator app). Must be set up before any user management actions are permitted.
- **Suspension** — A reversible state. A suspended user cannot log in or access the application. Can be reactivated by any Director.
- **Permanent Deletion** — An irreversible action. The user record is anonymised (personally identifying fields scrubbed) but the database row persists to maintain audit integrity. Cannot be undone.
- **Last-Director Protection** — A system guard that prevents the deactivation or permanent deletion of the last active Director in an organisation.
- **Vendor Safety Net** — The fallback process where the Vendor re-issues an activation link when an organisation has zero active Directors.
- **Audit Event** — An immutable record of every user creation, suspension, reactivation, and permanent deletion, capturing actor identity, target user identity snapshot, timestamp, and action type.
- **Vendor Backstage** — A secure portal (2FA-protected) used by the Vendor to send activation links to client organisations.
- **User Management Dashboard** — A Director-only UI displaying all users in the organisation with their roles, status, and action controls.

## 3a. Data Model

This section describes the key entities and their relationships. The data model maps onto the existing Midi-Kaval database schema, extending the existing `users` table with new columns and adding new tables as needed.

**Existing tables (extended):**
- **`users`** — Existing user table. Extended with: `organisation_id` (FK to new `organisations` table), `role` (enum/varchar: `director`, `coordinator`, `field_worker`, etc.), `is_suspended` (boolean, default false), `totp_secret` (nullable, for 2FA enrollment), `totp_enrolled_at` (nullable timestamp).

**New tables:**
- **`organisations`** — `id` (UUID, PK), `name` (varchar), `is_active` (boolean, default false), `created_at_utc` (timestamp). Created when Vendor sends first activation link.
- **`activation_tokens`** — `id` (UUID, PK), `organisation_id` (FK to organisations), `token_hash` (varchar, SHA-256 of raw token), `target_email` (varchar), `expires_at_utc` (timestamp), `consumed_at_utc` (nullable timestamp), `created_at_utc` (timestamp). One row per activation link.
- **`invitations`** — `id` (UUID, PK), `organisation_id` (FK), `invited_by_user_id` (FK to users), `target_email` (varchar), `role` (varchar), `token_hash` (varchar), `expires_at_utc` (timestamp), `status` (enum: `pending` / `confirmed` / `expired`), `created_at_utc` (timestamp), `confirmed_at_utc` (nullable timestamp). One row per invitation.
- **`audit_events`** — `id` (UUID, PK), `organisation_id` (FK), `event_type` (varchar), `actor_user_id` (FK to users, nullable — set null if actor is deleted), `target_user_id` (FK to users, nullable — set null if target is deleted), `target_user_snapshot` (JSONB — name+email at time of event, preserved even if user is later anonymised), `ip_address` (varchar), `metadata_json` (JSONB, nullable), `created_at_utc` (timestamp, immutable). Append-only — no UPDATE or DELETE permitted at database level.

**Key relationships:**
- Organisation 1→N Users (a user belongs to exactly one organisation)
- Organisation 1→N AuditEvents (audit scoped to organisation)
- Organisation 1→N Invitations
- Activation tokens are not linked to a user (the user doesn't exist yet); they link to the organisation being bootstrapped
- Audit events store a `target_user_snapshot` (JSONB) at event creation time so the target's identity is preserved even after anonymisation or deletion
- FK constraints use `ON DELETE SET NULL` for `actor_user_id` and `target_user_id` to avoid cascade-deleting audit history when a user is anonymised

## 4. Features

### 4.1 Vendor Backstage & Organisation Bootstrap

**Description:** The Vendor uses a secure backstage portal to activate a new client organisation. The Vendor provides the organisation name and the designated Director's email address. The system generates a cryptographically signed, time-bound, single-use activation link and delivers it to that email. The recipient clicks the link, sets their password, and becomes the first Director of that organisation.

**Functional Requirements:**

#### FR-1: Vendor sends activation link

The Vendor (authenticated via 2FA in the backstage portal) can submit an organisation name and a Director email address. The system generates a signed activation token, stores its hash and expiry, and emails a one-time activation link to the provided address.

**Consequences (testable):**
- Link delivery succeeds to a valid email address; delivery failure is logged and retried with exponential backoff (max 3 retries, 5-minute interval)
- Generated token is stored as a one-way SHA-256 hash in the `activation_tokens` table; the raw token is known only to the email recipient
- Token expires after a configurable duration (default 7 days, configured via environment variable `ACTIVATION_TOKEN_TTL_HOURS`)
- Token is invalidated after first use (consumed_at_utc set)
- After 3 delivery retries, the Vendor backstage shows a "delivery failed" status

#### FR-2: First Director registration

The recipient of the activation link can open it (unauthenticated), set their full name, email (pre-filled from the invitation), password, and optionally enroll 2FA immediately. On submission, the account is created with the Director role and the organisation is marked active.

**Consequences (testable):**
- Registration succeeds only with a valid, non-expired, unused token
- Password meets system-wide minimum complexity policy: minimum 8 characters, at least 1 uppercase letter, 1 lowercase letter, 1 digit (configurable via environment variable, applied uniformly across all organisations)
- Organisation is created in active state with one Director
- Expired or consumed token displays a page stating: "This link has expired or already been used. Please contact the Vendor to request a new activation link."
- Password complexity requirements are displayed on the registration form before the user submits

#### FR-3: Vendor Safety Net — zero-Director recovery

The system monitors the count of active Directors per organisation. When it reaches zero, the Vendor is notified and can re-issue an activation link through the backstage portal to a new designated Director.

**Consequences (testable):**
- Detection of zero active Directors triggers immediately (event-driven, on the delete/deactivation action itself) [ASSUMPTION AS-1: within 1 hour if event-driven detection fails]
- Vendor is notified via email alert and a dashboard indicator in the backstage portal
- Vendor can re-issue an activation link even for existing organisations
- New Director takes ownership of the organisation

### 4.2 Director User Management Dashboard

**Description:** A Director-only UI within the main application that lists all users in the organisation with their name, email, role, and account status (active/suspended). From this dashboard, Directors can invite new users, suspend/reactivate, and permanently delete accounts.

**Functional Requirements:**

#### FR-4: View all users

A Director can view a paginated, filterable list of all users in their organisation showing full name, email, role, status (active/suspended), and creation date.

**Consequences (testable):**
- List is scoped to the Director's own organisation only
- Columns can be sorted individually by clicking column headers (name, email, role, status, creation date)
- Filters include: text search on name/email, dropdown filter by role, dropdown filter by status (active/suspended). Filters can be combined simultaneously.
- End Users see their own profile only, not the user list
- Default sort is by creation date descending

#### FR-5: Director invites new user

A Director can initiate an invitation by entering the prospective user's email and selecting their role (Director or any lower role). The system sends an invitation email containing a registration link.

**Consequences (testable):**
- Invitation link expires after a configurable duration (default 7 days, configured via environment variable `INVITATION_TOKEN_TTL_HOURS`)
- Link is single-use
- Duplicate invitation to an already-registered email returns a clear error: "This email address is already registered."
- Duplicate invitation to a pending invitation (not yet confirmed) returns a clear error and suggests using the resend feature (see FR-12)
- Director receives confirmation that the invitation was sent
- Invitation creation is idempotent — rapid double-click does not create duplicate invitations (a unique constraint on `(organisation_id, target_email, status)` where status = `pending` prevents duplicate pending invitations)

#### FR-6: Director suspends a user

A Director can suspend any user (including another Director) to immediately revoke their access. Suspension is reversible.

**Consequences (testable):**
- Suspended user cannot log in or access API endpoints
- Suspended user receives an email notification
- Active sessions of the suspended user are terminated within [ASSUMPTION AS-2: 5 minutes] — requires the existing session management to support revocation (see NFRs for session management dependency)
- Last-Director Protection applies — see FR-9
- Suspension check happens on every API request (middleware checks `is_suspended` flag on the users table)

#### FR-7: Director reactivates a suspended user

A Director can reactivate a suspended user. Reactivation restores full access.

**Consequences (testable):**
- Reactivated user can log in with existing credentials
- Reactivation is logged as an audit event
- Reactivated user receives an email notification

#### FR-8: Director permanently deletes (anonymises) a user

A Director can permanently delete a user. This action is irreversible — the user record is anonymised (personally identifying fields like name and email are scrubbed) but the database row persists to maintain audit integrity. The user can never log in again.

**Consequences (testable):**
- Deletion requires explicit confirmation: the Director must type the target user's email address into a confirmation field (case-insensitive, Unicode-safe comparison)
- Deleted user cannot log in (password hash is cleared, email is overwritten with a hash of the original + random salt)
- Anonymised user fields: name set to "Deleted User", email set to `deleted-{uuid}@anonymised.local`, password hash cleared, `is_suspended` set to false (irrelevant since password is gone)
- Audit event records the deletion with actor identity and a snapshot of the target's identity at the time of anonymisation (stored in `target_user_snapshot`)
- Last-Director Protection applies — see FR-9

#### FR-9: Last-Director Protection

The system prevents suspension or permanent deletion of a Director if no other active Director remains in the organisation.

**Consequences (testable):**
- API returns a clear error message: "Cannot deactivate — no other active Director remains in the organisation."
- UI shows the action as disabled with a tooltip showing the same explanation
- The guard applies to both suspension and permanent deletion
- Self-deactivation by the last Director is also blocked

#### FR-10: Director 2FA mandate

A Director cannot perform any user management action (invite, suspend, reactivate, delete) until they have enrolled in two-factor authentication.

**Consequences (testable):**
- Unenrolled Director sees a full-page modal prompting them to set up 2FA when they attempt any management action. The modal blocks the attempted action until 2FA is enrolled or the modal is dismissed.
- 2FA is TOTP-based (authenticator app only). SMS/email backup codes are not available in v1 (see §7.2).
- Once enrolled, 2FA is required at each login for Directors
- 2FA recovery path: if a Director loses their 2FA device, another active Director can reset their 2FA enrollment from the User Management Dashboard, or the Vendor can reset it via the backstage portal as a last resort [NOTE FOR PM: this recovery path must be implemented in v1 — without it, every 2FA loss becomes a support escalation]

### 4.3 Invitation & Registration Flow

**Description:** All user onboarding flows through the double-confirmation model. A Director sends an invitation → the recipient clicks the link → fills a registration form → a confirmation email is sent → the account activates only after the second link is clicked.

**Functional Requirements:**

#### FR-11: Double-confirmation registration

The invitation link opens a registration page where the recipient sets their full name and password. On submission, a confirmation email is sent to the registered address. The account is marked active only after the confirmation link is clicked.

**Consequences (testable):**
- Registration page is accessible without authentication
- Password is set before the account is activated
- Confirmation link is time-bound (default 24 hours, configured via environment variable `CONFIRMATION_TOKEN_TTL_HOURS`)
- Account exists in "pending confirmation" state (users.is_active = false) between form submission and confirmation link click
- Pending accounts cannot log in
- After confirmation, the user can log in immediately with their chosen credentials
- Confirmation email delivery is retried with exponential backoff (max 3 retries)
- If confirmation email delivery fails after all retries, the invitation shows "delivery failed" status in the Director dashboard

#### FR-12: Invitation expiry and resend

A Director can view pending invitations (sent but not yet confirmed) and resend the invitation email. Expired invitations are automatically cleaned up.

**Consequences (testable):**
- Pending invitations are visible in the User Management Dashboard with status "Pending"
- Director can resend an invitation, which generates a new link and invalidates the previous one
- Expired invitations are marked as "Expired" in the dashboard and cannot be used
- Automatic cleanup of expired, unconsumed invitations runs daily [ASSUMPTION AS-5]
- When an invitation is resent and invalidates the previous one, the original inviting Director is notified by email [NOTE FOR PM: prevents one Director silently overriding another Director's invitation]

### 4.4 Audit Trail

**Description:** Every user lifecycle event — creation, suspension, reactivation, permanent deletion, and invitation send — is recorded in an immutable audit log. Directors can view the audit log to review all actions taken in their organisation.

**Functional Requirements:**

#### FR-13: Audit event recording

Every user management action records an audit event with: event type, actor user ID, target user ID, target user identity snapshot (name + email at time of event), timestamp, IP address, and optional metadata (reason for the action).

**Consequences (testable):**
- Audit events are append-only; any attempt to UPDATE or DELETE an audit event row is rejected at the database level (application code must use INSERT-only, and the database user should have INSERT-only privileges on the `audit_events` table)
- Each event includes a server-generated timestamp (UTC)
- Actor and target are reliably identified even if the target user is subsequently anonymised — the `target_user_snapshot` JSONB column captures name and email at the time of the event
- Audits are retained per regulatory requirements [ASSUMPTION AS-6: minimum 1 year]
- Audit events are written in the same database transaction as the action that triggered them (or in a fail-closed manner — if the audit write fails, the action is rolled back)

#### FR-14: Audit log viewer

Directors can view the audit log through a dedicated page with search and filter by event type, actor, target user, and date range.

**Consequences (testable):**
- Log is sorted by timestamp descending (most recent first)
- Filters are combined via AND logic; supports any subset of filters
- Results are paginated (default 50 per page)
- End Users cannot access the audit log
- Search by target user supports partial name/email matching against the `target_user_snapshot` JSONB field (so anonymised users are still searchable by their original identity)

### 4.5 Notifications

**Description:** Critical user management actions trigger email notifications to keep relevant parties informed.

**Functional Requirements:**

#### FR-15: Audit broadcast to all Directors

Every user creation, suspension, reactivation, and permanent deletion triggers an email notification to all active Directors in the organisation.

**Consequences (testable):**
- Email includes: action type, target user name/email, actor name/email, timestamp
- Email is sent within [ASSUMPTION AS-7: 5 minutes] of the action
- Directors cannot opt out of these notifications
- To prevent notification spam during bulk operations, notifications are batched: if multiple actions occur within 5 minutes of each other, they are combined into a single digest email per Director [NOTE FOR PM: resolves notification fatigue risk for organisations with many Directors]

#### FR-16: User notification

When a user is suspended or permanently deleted, they receive an email notification about the change to their account status.

**Consequences (testable):**
- Suspension email includes the reason (if provided by the acting Director) and instructions to contact another Director for appeal [ASSUMPTION AS-7: contact another Director]
- Permanent deletion email confirms the action and notes it is irreversible
- Notification emails to the affected user are rate-limited: no more than 3 emails of the same type per 24-hour period to prevent notification spam from repeated suspend/reactivate cycles [NOTE FOR PM: prevents targeted harassment via email spam]

## 5. Cross-Cutting NFRs

**Security**

- All invitation and activation links must be cryptographically signed using a server-side secret (HMAC-SHA256 or asymmetric signature)
- Registration and invitation endpoints must implement rate limiting:
  - Per IP: 10 requests per minute, burst of 20
  - Per email address: 5 requests per hour
  - Violation returns HTTP 429 with a `Retry-After` header
- Separate rate limit tiers for Vendor backstage endpoints (lower, since Vendor is trusted)
- 2FA tokens (TOTP) must follow RFC 6238 with a 30-second window
- Audit log must be append-only; database user for the application must have INSERT-only privileges on the `audit_events` table (no UPDATE/DELETE)
- All management action POST/PUT/DELETE endpoints in the Director dashboard must include CSRF protection (anti-forgery tokens)
- All URLs containing tokens (activation, invitation, confirmation) must enforce HTTPS; the application should reject HTTP requests with HSTS headers
- Session management strategy: if using JWT tokens, short-lived access tokens (15-minute TTL) with refresh token rotation are required to support the suspension session-termination requirement. Alternatively, server-side sessions with a revocation endpoint. [NOTE FOR PM: this is an architectural dependency for FR-6's suspension timeout. The existing Midi-Kaval auth model must be reviewed before implementation.]

**Performance**

- Invitation email delivery latency: < 5 minutes (p95)
- User Management Dashboard page load: < 2 seconds with 1,000 users
- Audit log query with combined filters: < 3 seconds for 100,000 events (requires composite indexes on event_type, created_at, organisation_id)
- Email delivery queue must process at least 100 emails per minute

**Availability**

- Registration and invitation endpoints must have the same availability SLA as the main application [ASSUMPTION AS-8: 99.9%]
- Email delivery is outsourced to a transactional email provider; system must handle provider failure gracefully (queue retry with exponential backoff: 1 min, 5 min, 15 min, then flagged for manual review)
- Email delivery failures must be visible to Directors in the User Management Dashboard (per-invitation status: "sent", "delivery failed", "pending retry")

**Existing-User Migration**

- All existing hardcoded accounts from configuration files must be migrated to the new database-driven model before cutover
- Migration strategy: a one-time script reads the existing config file and creates users in the database with appropriate roles under a default "Primary Organisation"
- The first migrated user (the original hardcoded admin) must be assigned the Director role
- During the migration window, both the old config-file auth and new DB auth must be supported to allow rollback
- Migration must be verified by integration tests before cutover [NOTE FOR PM: this is a significant operational dependency — the PRD currently does not specify a migration timeline or rollback plan]

## 6. Non-Goals (Explicit)

- **Self-registration / public signup** — There is no "Sign Up" page or self-nomination flow
- **Social login / OAuth** — All authentication is email + password (optionally with 2FA); no Google, Microsoft, or SSO in v1
- **Role hierarchy customisation** — Roles (Director, Coordinator, Field Worker, etc.) are defined by the system, not configurable by the organisation
- **Cross-organisation user management** — Directors manage only their own organisation; no super-admin or multi-org view
- **User provisioning via API** — No programmatic user management API in v1; all actions go through the UI
- **Delegated administration** — Non-Director users cannot manage any aspect of other users
- **Organisation self-service** — Organisations cannot sign up independently; they are created by the Vendor through the backstage portal
- **SMS/email backup codes for 2FA** — Only authenticator app (TOTP) in v1

## 7. MVP Scope

### 7.1 In Scope

- Vendor backstage portal with 2FA (FR-1, FR-3)
- Activation link generation and delivery (FR-1)
- First Director registration flow (FR-2)
- Vendor Safety Net recovery flow (FR-3)
- Director User Management Dashboard (FR-4)
- User invitation with role selection (FR-5, FR-12)
- User suspension and reactivation (FR-6, FR-7)
- User permanent deletion (FR-8, anonymisation model)
- Last-Director Protection (FR-9)
- Director 2FA mandate with TOTP only (FR-10)
- 2FA recovery flow (Director-to-Director reset + Vendor fallback) (FR-10)
- Double-confirmation registration flow (FR-11)
- Audit event recording with identity snapshot (FR-13)
- Audit log viewer (FR-14)
- Email notifications to all Directors on critical actions (FR-15, batched digest)
- Email notifications to affected users (FR-16, rate-limited)
- Data model implementation (organisations, activation_tokens, invitations, audit_events tables)
- Existing-user migration from config files

### 7.2 Out of Scope for MVP

- **Bulk user import** — Uploading a CSV of users to invite. Deferred to v2.
- **Role-based permission customisation** — Fixed role hierarchy; no permission toggles per role. Deferred to v2.
- **SMS/email backup codes for 2FA** — Only authenticator app (TOTP) in v1. [NOTE FOR PM: if 2FA lockouts become frequent, a bare-minimum recovery path should be added — even a "Director resets another Director's 2FA" from the dashboard, which is already in scope for FR-10.]
- **API-based user management** — All user management is UI-only. Deferred to v2.
- **Organisation branding on invitation emails** — Emails use Midi-Kaval default template. Deferred to v2.
- **Self-service password reset** — Relies on existing Midi-Kaval forgot-password flow; no new changes. [NOTE FOR PM: confirm the existing forgot-password flow works for Directors with 2FA enrolled.]
- **Delegated admin roles** — Only Directors can manage users. No sub-admin role. Deferred to v2.

## 8. Success Metrics

**Primary**

- **SM-1**: Time from Vendor activation link send to first Director registration — target < 24 hours (p95). Validates FR-1, FR-2.
- **SM-2**: Invitation-to-completion rate > 80% (percentage of invitations that result in a confirmed, active user within 7 days of being sent). Validates FR-4, FR-5, FR-6, FR-11.

**Secondary**

- **SM-3**: Director-facing user management task completion rate > 90% without needing to contact Vendor support. Validates FR-4, FR-5, FR-6, FR-7, FR-8.
- **SM-4**: All critical user management actions have a corresponding audit event (100% coverage verified by periodic automated audit). Validates FR-13.

**Counter-metrics (do not optimize)**

- **SM-C1**: Invitation-to-completion time should NOT be minimized below 2 hours. The double-confirmation flow and 2FA mandate add deliberate friction. An average completion time below 2 hours would indicate security bypass in the flow — investigate.

## 9. Open Questions

1. Should the Vendor backstage support multiple administrators (e.g., a team of Vendor ops people)? [LOW — doesn't block any FR in current scope]
2. What is the data retention requirement for audit logs? (Assumed 1 year) [BLOCKS: FR-13 retention implementation]
3. What is the expected maximum number of users per organisation for v1 performance testing? [BLOCKS: performance NFR validation]
4. What is the existing Midi-Kaval session management strategy (JWT vs server-side sessions)? [BLOCKS: FR-6 suspension session termination]
5. Does the existing forgot-password flow support Directors with 2FA enrolled? [BLOCKS: §7.2 self-service password reset deferral]

## 10. Assumptions Index

- AS-1: Vendor Safety Net detection of zero active Directors triggers within 1 hour if event-driven detection fails (FR-3)
- AS-2: Suspended user sessions are terminated within 5 minutes, assuming the session management strategy supports revocation (FR-6)
- AS-3: Permanent deletion anonymises rather than cascade-deletes user data; the database row persists for audit integrity (FR-8)
- AS-4: [REMOVED — resolved: TOTP only in v1, no SMS/email backup codes. See §7.2.]
- AS-5: Expired invitation cleanup runs daily (FR-12)
- AS-6: Audit log retention minimum of 1 year (FR-13)
- AS-7: Notification emails are delivered within 5 minutes of the triggering action (FR-15, FR-16)
- AS-8: Application availability SLA is 99.9% (NFRs)

## 11. Constraints and Guardrails

**Safety**

- Last-Director Protection prevents accidental or malicious lockout of an organisation
- Permanent deletion requires explicit confirmation (typing the target user's email)
- All destructive actions are irreversible and flagged as such in the UI

**Privacy**

- Audit events store a snapshot of target identity at event creation time, preserving information even after anonymisation
- Email notifications contain only work-relevant information (action type, user name, timestamp)
- Anonymised user records have all PII scrubbed: name → "Deleted User", email → `deleted-{uuid}@anonymised.local`
