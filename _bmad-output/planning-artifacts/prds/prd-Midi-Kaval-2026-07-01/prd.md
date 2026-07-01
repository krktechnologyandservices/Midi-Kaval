---
title: 'Two-Factor Authentication (2FA) — Universal Enrollment & Administration'
status: final
created: 2026-07-01
updated: 2026-07-01
---

# PRD: Two-Factor Authentication — Universal Enrollment & Administration

> **Note:** "Two-factor authentication" is hereinafter referred to as **2FA**.

## 1. Executive Summary

Kaval Online currently requires TOTP-based two-factor authentication for Directors only. All other roles — Vendors, Coordinators, SocialWorkers, CaseWorkers, Accountants — authenticate with email + password + email OTP, which is functionally single-factor (access to email equals access to the platform).

The primary trigger for this work: **Vendors must interact with raw Swagger API calls to enroll in 2FA**, creating a poor experience and compliance risk. This PRD extends 2FA to every role with a consistent enrollment UX, gives Directors the tools to manage their team's 2FA, and establishes Vendor and Coordinator as escalation/administration layers.

### Scope

| Phase | In Scope | Description | Deferred Risk |
|-------|----------|-------------|---------------|
| **1** | ✅ | Vendor Settings Page — self-service 2FA enrollment with proper UI | Vendors remain on Swagger until delivered |
| **2** | ✅ | Director 2FA Management — status view, reset, reminders, mandate toggle, bypass codes, audit log, delegation | Directors have no visibility into team 2FA compliance until delivered |
| **3** | ❌ Future | Vendor Global 2FA Administration — cross-org dashboard, bulk campaigns | No central 2FA compliance oversight across organisations |
| **4** | ❌ Future | Universal Enrollment Flow — first-login prompt for all roles; mobile enrollment; passkeys/WebAuthn | Field workers remain on email OTP; no first-login enrollment prompt for non-Director roles |

### Non-Goals (Explicitly Out of Scope)

- **React Native mobile enrollment screen** — Deferred to Phase 4. Field workers enroll from web.
- **Passkeys / WebAuthn** — Deferred to Phase 4. Directional aspiration, subject to research.
- **SMS as a 2FA delivery channel** — Not pursued due to infrastructure cost and regulatory constraints.
- **Cross-organisation bulk campaigns** — Deferred to Phase 3 (Vendor Global Admin).
- **Hardware security keys (YubiKey / FIDO2)** — Not in scope. May be evaluated in a future security uplift.
- **Automated 2FA enforcement for existing users** — No timed rollout or grace-period migration. Org-level mandate toggle (FR-2.4) is the sole enforcement mechanism.

---

## 2. Problem Statement

### 2.1 Vendor Enrollment Gap

The Vendor role manages organisations and activation tokens — a privileged backstage function. Current 2FA enrollment requires:

1. Login via Swagger UI at `/swagger`
2. Call `POST /api/v1/auth/enroll-2fa` — receives a provisioning URI in JSON
3. Manually extract the URI and scan with an authenticator app
4. Call `POST /api/v1/auth/verify-enroll-2fa` with the 6-digit code

This is not a viable user experience for production use. It forces a system administrator to navigate a developer tool, understand API semantics, and manage raw JSON responses.

### 2.2 Fragmented 2FA Enforcement

- Some roles (Director) are forced to enroll 2FA — others are not
- No visibility for Directors into who has 2FA enabled across their team
- No mechanism to mandate 2FA organisation-wide
- No recovery path when a user loses their authenticator device (beyond Director reset)
- No audit trail for 2FA events at org level

### 2.3 Security Risk

Email OTP is not true two-factor authentication — it relies on the secrecy of the email inbox, which is frequently compromised. Sensitive POCSO case data in Kaval Online requires genuine 2FA (something you have + something you know) for all roles.

### 2.4 Inclusivity Gap

Field workers (SocialWorker/CaseWorker) in remote areas may not have smartphones capable of running authenticator apps, or may have limited data connectivity. [ASSUMPTION: All field workers have access to a smartphone or a device capable of running an authenticator app. If this assumption proves false, an alternative 2FA method will need to be evaluated.] The current design relies on TOTP via authenticator apps; users without compatible devices must work with their Director to find a solution.

---

## 3. User Journeys

### UJ-1: Vendor Self-Enrollment

**Protagonist:** Meena, a system administrator managing organisations for multiple child welfare agencies.

1. Meena navigates to Kaval Online web app and logs in with email + password
2. System sends email OTP — she enters it
3. The app lands on `/vendor/settings` (first-time banner: *"Two-factor authentication is required to access vendor features."*)
4. She sees a card: "Enable Two-Factor Authentication" with a QR code and manual setup key
5. Meena opens Google Authenticator on her phone, scans the QR code
6. She enters the 6-digit code shown in the app
7. **Success:** Badge updates to "2FA: Enabled ✓"
8. She is presented with 8 backup codes — downloads them, stores securely
9. She can now access `/vendor` dashboard and vendor-protected endpoints

**Failure:** If Meena closes the page without enrolling, the next navigation to any `/vendor` page redirects her back to settings until enrollment completes.

### UJ-2: Director Monitoring Team 2FA

**Protagonist:** Raj, a Director overseeing 15 field workers.

1. Raj logs in (already has 2FA enrolled)
2. Opens **Staff Management** page
3. Sees a new "2FA" column: 12 users show ✓ (green, with enrollment date tooltip on hover), 3 show ✗ (red)
4. Clicks the ✗ cell → MatMenu opens with actions: "Send 2FA Reminder" / "Reset 2FA"
5. Clicks "Send 2FA Reminder" next to a ✗ user — system sends an email: "Raj requires you to set up two-factor authentication."
6. Raj navigates to **Organisation Settings**, toggles **"Require two-factor authentication for all staff"** ON
7. Next login for any unenrolled user redirects them to enrollment

### UJ-3: Field Worker Lockout Recovery

**Protagonist:** Priya, a SocialWorker who got a new phone and lost her authenticator app.

1. Priya calls Raj: "I can't log in — my TOTP codes are on my old phone"
2. Raj opens Staff Management, finds Priya, clicks the ✓ cell → MatMenu → "Reset 2FA"
3. Confirmation dialog: *"This will clear Priya's 2FA enrollment. She'll need to re-enroll on next login. Continue?"*
4. Raj confirms — Priya's `totp_secret` is cleared, audit event logged
5. Raj generates a **temporary bypass code** (valid 30 minutes) — copies it from the dialog and shares over the phone
6. Priya logs in with email + password + bypass code, navigates to profile, re-enrolls 2FA with her new phone
7. New backup codes are generated

### UJ-4: Coordinator 2FA Reset (Delegated)

**Protagonist:** Rahul, a Coordinator whose Director has delegated 2FA reset capability.

1. Before delegation: Rahul sees the Staff Management page — "Reset 2FA" button is not visible for any user
2. Raj (Director) navigates to **Organisation Settings**, toggles **"Allow Coordinators to reset 2FA for field workers"** ON
3. Rahul now sees a "Reset 2FA" button next to SocialWorker and CaseWorker users only (not visible for other Coordinators, Accountants, or Vendors)
4. Rahul clicks "Reset 2FA" for Anita (SocialWorker) — confirmation dialog, confirms
5. Anita's `totp_secret` is cleared. Rahul does **not** have access to generate bypass codes.
6. Anita logs in, re-enrolls 2FA
7. Later, Raj toggles delegation OFF — Rahul's "Reset 2FA" button disappears. No Coordinators can reset 2FA anymore.

---

## 4. Functional Requirements

### FR-1: Vendor Settings Page

**ID: FR-1.1** — Vendor-accessible settings route `/vendor/settings` protected by `VendorGuard`

**ID: FR-1.2** — Page displays current 2FA status as a badge:
- Not enrolled: "Two-Factor Authentication: Not Set Up — Enable Now" (with CTA button)
- Enrolled: "Two-Factor Authentication: Enabled" with enrollment date and "Disable" option

**ID: FR-1.3** — Enrollment flow (identical to existing Director flow):
- Click "Enable" → API `POST /auth/enroll-2fa` → display QR code (min 200x200px, theme-aware) + manual setup key + "Copy Key" button
- Input field for 6-digit TOTP code → API `POST /auth/verify-enroll-2fa`
- On success → display 8 backup codes with download as `.txt`
- Error states: invalid code → inline error message ("The code you entered is incorrect. Try again."); expired enrollment session → "Your enrollment session has expired. Please start again."

**ID: FR-1.4** — Redirection guard: if Vendor is authenticated but `totp_secret IS NULL`, any navigation to `/vendor/*` redirects to `/vendor/settings` with banner: *"Two-factor authentication is required. Please set it up to continue."* (This is a hard gate — no other vendor page is accessible until enrollment completes.)

**ID: FR-1.5** — Page also includes password change form (reuses existing `ChangePasswordComponent`)

**Acceptance:** A Vendor with 0 prior knowledge can complete 2FA enrollment entirely through the web UI without touching Swagger.

---

### FR-2: Director 2FA Management

**ID: FR-2.1** — Staff Management table gains a "2FA" column showing:
- ✓ (green) if `totp_secret` is set — hover tooltip shows enrollment date
- ✗ (red) if unenrolled
- Interaction: clicking the cell opens an Angular Material `MatMenu` with contextual actions:
  - ✓ state menu: "Reset 2FA"
  - ✗ state menu: "Reset 2FA", "Send Reminder"
  - The menu hides entirely if the current user has no permissions (e.g., Coordinator before delegation)

**ID: FR-2.2** — "Reset 2FA" action:
- Confirmation dialog: *"This will clear [user]'s 2FA enrollment. They will need to re-enroll on next login. Continue?"* with Cancel / Confirm buttons
- Calls `POST /admin/users/{id}/reset-2fa`
- Clears `totp_secret` and logs in `audit_events` with `event_type: 2fa_reset` (actor, target user, organisation, timestamp, IP)

**ID: FR-2.3** — "Send Reminder" action:
- Sends email to user: *"Your Director [name] requires you to set up two-factor authentication. Please log in and follow the enrollment prompt."*
- Includes direct link to the web app
- A confirmation toast appears: "Reminder sent to [user email]."

**ID: FR-2.4** — Organisation-level "Require 2FA" toggle:
- Located in Organisation Settings as a mat-slide-toggle
- Tooltip: "When enabled, users without 2FA will be prompted to set it up on their next login."
- When enabled, login response for any unenrolled user in that org includes `2FA_SETUP_REQUIRED: true` and `setupUrl: "/settings/2fa"` — client auto-redirects to enrollment
- Stored as `require_2fa` column on `organisations` table (default: `FALSE`)

**ID: FR-2.5** — Temporary bypass code generation:
- Accessible via Staff Management → user with 2FA enrolled → MatMenu → "Generate Bypass Code" (only for Director role; not for Coordinators even if delegated)
- Dialog shows the generated code with a "Copy" button and warning: *"This code expires in 30 minutes and can only be used once. Share it securely."*
- API: `POST /admin/users/{id}/generate-bypass-code` — returns single-use code valid 30 minutes
- Rate limited: 2 per hour per Director (enforced server-side)
- Fully logged in `audit_events` — actor, target user, timestamp, IP, event_type `2fa_bypass_generated`
- Code stored as SHA-256 hash in Redis with 30-min TTL (pattern follows existing `OtpChallengeStore`)
- `[ASSUMPTION: OTPChallengeStore is a reliable template for Redis-backed temporary code storage. If clock drift or Redis unavailability proves problematic, a database-backed fallback may be needed.]`

**ID: FR-2.6** — 2FA audit log filter:
- New view in admin area accessible via sidebar: "2FA Audit Log"
- Columns: Timestamp, User, Event Type (enrollment, reset, bypass_generated, bypass_used, failed_totp), IP Address
- Filterable by: user, event type, date range
- Scoped to Director's own organisation
- Exports as CSV

**ID: FR-2.7** — Delegation of 2FA reset to Coordinators:
- Organisation Settings → new toggle: "Allow Coordinators to reset 2FA for field workers" (mat-slide-toggle)
- When set to ON:
  - Coordinators see the "Reset 2FA" action in their Staff Management view, but **only** for users with role `SocialWorker` or `CaseWorker`
  - The "Generate Bypass Code" action is **never** visible to Coordinators
  - Delegation is logged as `event_type: 2fa_delegation_enabled` in audit_events
- When set to OFF:
  - All Coordinator reset buttons disappear immediately
  - Logged as `event_type: 2fa_delegation_disabled` in audit_events
- UI: The toggle has clear on/off state. A tooltip explains: "Coordinators can reset 2FA for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."
- Revocation always succeeds — even if a Coordinator has an in-progress reset, the toggle flips off and any subsequent API call from that Coordinator returns 403

**ID: FR-2.8** — Enrollment notification:
- When any user in the org successfully enrolls 2FA, Director receives an in-app notification via the existing notification system (bell icon in top nav)
- Notification body: *"[User full name] has enabled two-factor authentication."*
- Notification is dismissible — clicking it marks as read
- Delivery: in-app only (no email supplement at this time; [ASSUMPTION: in-app notification delivery is sufficient for Directors who are active users. If a Director is consistently missing notifications, email supplement can be added.])

---

### FR-3: Proactive Onboarding Emails

**ID: FR-3.1** — Welcome email with 2FA setup link:
- When a new user account is created (via invitation or seed), an automatic email is sent: *"Welcome to Kaval Online. To get started, set up your account and enable two-factor authentication: [Setup Link]."*
- The link directs to the web app enrollment page
- Only sent once per user

**ID: FR-3.2** — 2FA enrollment link in email OTP messages:
- Every email OTP message footer includes: *"First time? Enable two-factor authentication [here] for stronger account security."*
- The link directs to `/settings/2fa` (or `/vendor/settings` for Vendor role)

**ID: FR-3.3** — Director-initiated 2FA invitation:
- In Staff Management → "Invite New Staff" flow, the Director sees an additional checkbox: *"Include 2FA setup instructions in the invitation email"* (default: checked)
- When checked, the invitation email includes a paragraph: *"After setting up your account, please enable two-factor authentication for enhanced security: [Setup Link]."*

**ID: FR-3.4** — Legacy user migration campaign:
- A one-time background job scans all users with `totp_secret IS NULL` and sends a one-off email: *"Kaval Online now requires two-factor authentication for all users. Please set up 2FA on your next login: [Setup Link]."*
- Throttled to 100 emails per hour to avoid overwhelming SMTP
- Can be triggered manually by Vendor on demand (deferred to Phase 3 for full UI; initial trigger via API or Hangfire dashboard)

**Acceptance:** Every user receives at least one proactive communication about 2FA within their first week — either at account creation, via OTP footer, or via migration campaign.

---

### FR-4: Backup Codes

**ID: FR-4.1** — On successful 2FA enrollment, system generates 8 single-use backup codes

**ID: FR-4.2** — Each code is a cryptographically random 10-character alphanumeric string (e.g., `A3K9X7M2P1`)

**ID: FR-4.3** — Codes are hashed (SHA-256) and stored alongside a `used` flag per code in a `backup_codes` table

**ID: FR-4.4** — Backup codes are displayed exactly once — on the enrollment success screen — with a "Download as .txt" button. The downloaded file includes a header: *"Kaval Online Backup Codes — [User Email] — Store these securely. Each code can only be used once."*

**ID: FR-4.5** — On login, after 1 failed TOTP attempt, the user sees "Use a backup code instead" option:
- Enter backup code → verify against stored hashes → mark as used → grant access
- After using a backup code, user is prompted to re-enroll 2FA (which generates a new set of 8 codes)

**ID: FR-4.6** — When fewer than 3 backup codes remain unused, a warning banner appears at the top of the user's profile: *"You have [N] backup code(s) remaining. Consider re-enrolling 2FA to generate new codes."*

---

### FR-5: Login Response Contract Updates

**ID: FR-5.1** — Login response for unenrolled users includes:

```json
{
  "requires2faSetup": true,
  "setupUrl": "/vendor/settings",
  "message": "Two-factor authentication is required. Please set it up to continue."
}
```

The `setupUrl` is role-aware — `/vendor/settings` for Vendors, `/settings/2fa` for other roles.

**ID: FR-5.2** — When org-level `require_2fa` is enabled (FR-2.4), the login response for any unenrolled user in that org also includes `"orgRequires2fa": true`.

**ID: FR-5.3** — The Angular client uses `setupUrl` to redirect post-login, rather than hardcoding the path. [ASSUMPTION: The Angular router can handle dynamic redirect URLs from login response. If the routing architecture requires static paths, a mapping layer in the auth guard will be needed.]

---

### FR-6: API Endpoints

**New endpoints:**

| Method | Path | Role | Purpose | Request Body | Response |
|--------|------|------|---------|-------------|----------|
| `GET` | `/auth/2fa-status` | All | Check 2FA enrollment state | — | `{ enrolled: bool, enrolledAt: dateISO }` |
| `POST` | `/admin/users/{id}/reset-2fa` | Director/Coordinator* | Clear user's TOTP secret | — | `{ success: true }` |
| `POST` | `/admin/users/{id}/send-2fa-reminder` | Director | Send enrollment reminder email | — | `{ success: true }` |
| `POST` | `/admin/users/{id}/generate-bypass-code` | Director | Generate temporary access code | — | `{ code: "ABC123XYZ", expiresInSeconds: 1800 }` |
| `GET` | `/admin/audit/2fa` | Director | Organisation-scoped 2FA audit log | Query: `?eventType=&userId=&from=&to=` | `{ events: [...] }` |
| `PUT` | `/admin/settings/require-2fa` | Director | Toggle org-level 2FA requirement | `{ enabled: bool }` | `{ require2fa: bool }` |
| `PUT` | `/admin/settings/delegate-2fa-reset` | Director | Delegate reset to Coordinators | `{ enabled: bool }` | `{ delegationEnabled: bool }` |
| `POST` | `/auth/verify-backup-code` | All | Verify a backup code during login | `{ code: string }` | `{ valid: bool, token?: string }` |

_* Coordinator scope limited to SocialWorker/CaseWorker roles — enforced in controller logic_

**Existing endpoints reused** (available to Vendors via settings page, not just Swagger):
- `POST /auth/enroll-2fa`
- `POST /auth/verify-enroll-2fa`

---

### FR-7: Backup Codes Data Model

```sql
CREATE TABLE backup_codes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    code_hash TEXT NOT NULL,            -- SHA-256 of the plaintext code
    used BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    used_at_utc TIMESTAMPTZ
);

CREATE INDEX ix_backup_codes_user_id ON backup_codes(user_id);
CREATE INDEX ix_backup_codes_user_id_unused ON backup_codes(user_id) WHERE used = FALSE;
```

---

## 5. Non-Functional Requirements

### NFR-1: Security

**ID: NFR-1.1** — All TOTP secrets must be stored encrypted-at-rest in PostgreSQL (`totp_secret` column already exists as `bytea` with EF Core value converter)

**ID: NFR-1.2** — Bypass codes must be single-use with 30-minute TTL, stored as SHA-256 hash only in Redis

**ID: NFR-1.3** — Backup codes must be single-use, stored as SHA-256 hash only, regenerated on every 2FA enrollment

**ID: NFR-1.4** — Rate limiting on `/auth/verify-enroll-2fa`: 5 attempts per minute per user

**ID: NFR-1.5** — Rate limiting on `/auth/verify-backup-code`: 5 attempts per minute per user

**ID: NFR-1.6** — Rate limiting on `/admin/users/{id}/generate-bypass-code`: 2 per hour per Director

**ID: NFR-1.7** — After 5 consecutive failed TOTP verification attempts, lock TOTP verification for that user for 15 minutes. Log as `2fa_failed_totp` in audit_events after the 5th failure. The user can still use email OTP + password to log in during the lockout period.

### NFR-2: Audit

**ID: NFR-2.1** — All 2FA lifecycle events must be recorded in `audit_events` table:

| Event Type | Trigger |
|-----------|---------|
| `2fa_enrolled` | Successful TOTP verification |
| `2fa_disabled` | User disables 2FA |
| `2fa_reset` | Director/Coordinator resets user's 2FA |
| `2fa_bypass_generated` | Bypass code created |
| `2fa_bypass_used` | Bypass code consumed |
| `2fa_backup_used` | Backup code consumed |
| `2fa_failed_totp` | 5th consecutive failed TOTP attempt triggers lockout |
| `2fa_mandate_enabled` | Org-level requirement turned ON |
| `2fa_mandate_disabled` | Org-level requirement turned OFF |
| `2fa_delegation_enabled` | Coordinator delegation turned ON |
| `2fa_delegation_disabled` | Coordinator delegation turned OFF |

### NFR-3: Performance

**ID: NFR-3.1** — 2FA status check (`GET /auth/2fa-status`) must complete in < 50ms (reads from DB, no computation)

**ID: NFR-3.2** — Backup code verification must complete in < 100ms (hash lookup)

**ID: NFR-3.3** — All 2FA API endpoints must respect the existing rate limiting policy

### NFR-4: UX

**ID: NFR-4.1** — Vendor settings page must be fully keyboard-navigable

**ID: NFR-4.2** — QR code must render at minimum 200x200px with light/dark theme support

**ID: NFR-4.3** — All error states must show human-readable messages (not JSON or stack traces)

**ID: NFR-4.4** — Backup code download must produce a plain `.txt` file with clear labeling

**ID: NFR-4.5** — MatMenu interaction for 2FA column must be consistent with existing Angular Material patterns used in the app

---

## 6. Open Questions

| # | Question | Proposed Resolution | Owner | Target |
|---|----------|--------------------|-------|--------|
| OQ-1 | Should SocialWorker/CaseWorker get a first-login enrollment prompt (like Directors) now, or only when Director enables org-level mandate? | Phase 4 — defer until org mandate is enabled. In the meantime, proactive onboarding emails (FR-3) serve as the primary channel. | Admin | Sprint planning |
| OQ-2 | How do we handle the case where a user exhausts all 8 backup codes and loses their phone again before re-enrolling? | They must contact Director for a 2FA reset + new bypass code. The Director should then verify the user's identity over a known channel before resetting. | Admin | Sprint planning |
| OQ-3 | Should the React Native mobile app get an enrollment screen for field workers who primarily use mobile? | Deferred — web-only enrollment for now (see Non-Goals). | Admin | Phase 4 planning |
| OQ-4 | What is the cooldown/lockout policy after N consecutive failed TOTP attempts? | Resolved in NFR-1.7: 5 failed attempts → 15-min lockout, log as `2fa_failed_totp`, email OTP still works. | Admin | Resolved |
| OQ-5 | Should email OTP count as a 2FA method for users who genuinely cannot use TOTP? | No — email OTP is not true 2FA (see §2.3). If a user has no compatible device, the Director should assist with a hardware key or alternative arrangement. This is an edge case to be handled manually by the Director until a programmatic solution is scoped. | Admin | Phase 4 planning |
| OQ-6 | Should backup codes and bypass codes be unified? | No — they serve different purposes: backup codes are user-generated at enrollment (persistent, many), bypass codes are Director-generated on-demand (time-limited, emergency only). Keep separate. | Admin | Resolved |
| OQ-7 | Legacy migration campaign: automated or manual trigger? | Automated one-time job (FR-3.4) triggered via Hangfire. Manual trigger for Vendor will be added in Phase 3. | Admin | Sprint planning |

---

## 7. Assumptions Index

| # | Assumption | Location | Impact if Wrong |
|---|-----------|----------|-----------------|
| A-1 | All field workers have access to a smartphone or device capable of running an authenticator app | §2.4 | Non-smartphone users cannot enroll 2FA; alternative method needed |
| A-2 | `OtpChallengeStore` is a reliable template for Redis-backed bypass code storage | FR-2.5 | Redis unavailability would block bypass code generation; DB fallback needed |
| A-3 | Angular router can handle dynamic redirect URLs from login response | FR-5.3 | Mapping layer in auth guard needed — adds 1-2 days to frontend work |
| A-4 | In-app notification delivery is sufficient for Directors to learn about enrollments | FR-2.8 | Email supplement may be needed if Directors miss in-app notifications |
| A-5 | Email delivery infrastructure is sufficiently reliable for 2FA reminder emails | FR-2.3, FR-3.x | Failed email delivery would mean users never get reminders; retry logic needed |
| A-6 | Existing `totp_secret` column encryption (bytea + EF Core value converter) is adequate | Implicit | If encryption strength is insufficient, migration to a stronger scheme needed |
| A-7 | All users have a verified email address on file | Implicit | Users without email cannot receive OTP or 2FA communications |

---

## 8. Future Scope

### Phase 3 — Vendor Global 2FA Administration

- Cross-organisation enrollment dashboard (table: Organisation → Total → Enrolled → %)
- Bulk 2FA reminder campaigns (select org → "Send to all unenrolled")
- Global 2FA audit log (unfiltered, exportable)
- Force-mandate 2FA for any organisation (bypasses Director toggle)
- Escalation reset when Director is locked out (15-min temporary access codes)
- Manual trigger for legacy migration campaign

**Strategic motivation:** Vendors currently have no system-wide view of 2FA compliance. This phase builds oversight capability without requiring access to every organisation. Deferred because the immediate need is to enable enrollment, not to report on it.

### Phase 4 — Universal Enrollment Flow

- First-login 2FA enrollment prompt for all roles (extends existing Director flow to every new user)
- Gated session until enrollment complete (enforced client-side + server-side)
- React Native mobile enrollment screen (for field workers who primarily use mobile)
- Passkeys / WebAuthn support as alternative to TOTP:
  - User registers a passkey (biometric or PIN) on their device
  - Login flow offers "Use Passkey" alongside TOTP
  - No authenticator app required — uses platform authenticator (Windows Hello, Touch ID, Android fingerprint)
  - Future: cross-device passkey sync via iCloud/Google Password Manager

**Strategic motivation:** Universal enrollment moves 2FA from "opt-in for most roles" to "required for everyone." This is the most impactful inclusion feature — it ensures no user falls through the cracks. Deferred because Phase 1+2 solve the urgent Vendor UX problem and give Directors tooling to manage their teams manually.

### Trigger for Re-Prioritization

- Phase 3 reprioritization: A compliance audit finds that >20% of users across any organisation are unenrolled 90 days after Phase 2 deployment.
- Phase 4 reprioritization: Field workers report that web-only enrollment is a barrier (e.g., no office computer access), or a security review flags email OTP as an unacceptable risk for non-Director roles.

---

## 9. Success Metrics

| Metric | Target | Measurement | Counter-Metric |
|--------|--------|-------------|----------------|
| Vendor 2FA enrollment rate | 100% within 7 days of deploy | Query `totp_secret` on vendor users | Director-initiated resets per month (if high, enrollment flow may be confused) |
| Director-initiated 2FA reminders | < 5 reminders per month for orgs < 20 users | Audit log — `2fa_reminder_sent` count | Support tickets related to 2FA (if high, UX may be unclear) |
| Support tickets related to 2FA | < 2 per month | Zendesk/email tracking | Time-to-complete for 2FA enrollment flow (p95) — if > 5 min, step is confusing |
| Bypass code usage | < 1 per org per quarter | Audit log — `2fa_bypass_*` events | (tracked as security signal, not performance) |
| Failed TOTP attempts per user | < 3/month average | Audit log — `2fa_failed_totp` events | (tracked as security signal, not performance) |

**Counter-metrics rationale:**
- **Director-initiated resets** could spike if the enrollment flow is confusing or if hardware changes (new phones) are common. High reset count suggests a UX or education gap.
- **Support tickets** help distinguish between "system works but user needs help" (acceptable) and "system is unclear" (fix).
- **Time-to-complete** for enrollment (p95) identifies friction in the QR scan → verify step. If users consistently take >5 minutes, the flow needs simplification.

---

## 10. Dependencies

- Angular `Enroll2FAComponent` — exists, used by Directors. Minor refactor to make role-agnostic
- `totp_secret` column — exists on `users` table as `bytea` with EF Core encryption
- `audit_events` table — exists, extend to accept new event types (list in NFR-2.1)
- `POST /auth/enroll-2fa` and `/auth/verify-enroll-2fa` — exist and are role-agnostic
- Staff Management page — exists, extend with 2FA column and MatMenu actions
- Organisation Settings page — exists, add "Require 2FA" and "Delegate" toggles
- Email sending infrastructure — exists (MailKit via `SmtpEmailSender`)
- In-app notification system — exists (bell icon in top nav)
- Hangfire background jobs — exists (for legacy migration campaign, FR-3.4)
- New: `backup_codes` table — needs EF Core migration
- New: `require_2fa` column on `organisations` — needs EF Core migration
- New: Redis-backed bypass code storage — follows `OtpChallengeStore` pattern

---

## 11. Glossary

| Term | Definition |
|------|-----------|
| **2FA** | Two-Factor Authentication — an authentication method requiring two distinct factors (something you know + something you have) |
| **TOTP** | Time-based One-Time Password — a 6-digit code generated by an authenticator app, valid for 30 seconds |
| **Email OTP** | A one-time password sent via email. Used as the second factor in the current login flow. Not considered true 2FA for security compliance purposes. |
| **Backup code** | A single-use alphanumeric code generated at 2FA enrollment. Used when the user cannot access their authenticator app. 8 per enrollment. Persistent until used. |
| **Bypass code** | A single-use, time-limited (30 min) alphanumeric code generated by a Director on-demand. Used for emergency access when a user is locked out. Stored in Redis, not in the database. |
| **Enrollment** | The process of setting up 2FA for the first time: scan QR, enter TOTP code, receive backup codes |
| **Mandate** | An organisation-level setting that requires 2FA for all users in that organisation |
| **Delegation** | A Director-level setting that allows Coordinators to reset 2FA for field workers |
