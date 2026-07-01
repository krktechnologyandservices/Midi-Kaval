---
stepsCompleted: [1, 2]
inputDocuments: []
session_topic: 'Two-Factor Authentication (2FA) for all user roles including Vendors'
session_goals: 'Design a production-ready, inclusive 2FA strategy that works across all roles (Director, Coordinator, SocialWorker, CaseWorker, Accountant, Vendor) in a child protection platform with web + mobile clients'
selected_approach: 'Progressive Flow — started with Journey Mapping, then expanded to role-based management'
techniques_used:
  - Journey Mapping
  - Role-Based Exploration
  - Layering (Director + Vendor powers)
ideas_generated:
  # Post-Login Flow
  - '1. Detect → Redirect → Enroll: Login response returns 2FA_SETUP_REQUIRED status with TOTP provisioning URI for unenrolled users'
  - '2. Gated session: Unenrolled user gets restricted session — only /enroll-2fa and /verify-enroll-2fa accessible'
  - '3. Login response carries setup URL: { requires2faSetup: true, setupUrl: "..." }'
  - '4. Force enrollment at first login for all roles — extend Director flow to everyone'
  # Communication-Based
  - '5. Welcome email with 2FA enrollment link on account creation'
  - '6. Enrollment link embedded in every email OTP message'
  - '7. Director-initiated "Send 2FA Setup Email" for individual users'
  # Technical Simplification
  - '8. Reframe email OTP as 2FA for Vendor role (pragmatic simplification)'
  - '9. API returns enrollment data inline in 403 response body (provisioningUri + qrCodeBase64)'
  - '10. SMS OTP as alternative 2FA method for users without smartphones'
  # Recovery & Admin Controls
  - '11. Director can force-reset vendor/user 2FA (already exists — extend)'
  - '12. Single-use backup codes on successful enrollment (5-8 codes, one-time display)'
  # Modern Auth Methods
  - '13. Passkeys / WebAuthn — biometric or device-bound auth, no authenticator app needed'
  - '14. Hardware security key (YubiKey) for sensitive roles'
  # Vendor Settings Page
  - '15. /vendor/settings page with 2FA enrollment card (reuse existing Enroll2FAComponent)'
  - '16. Vendor profile page with 2FA + password change in one place'
  - '17. Enrollment status badge: "2FA: Enabled ✓ / Not Set Up — Enable Now"'
  - '18. Soft-block: /vendor/settings is only accessible page until 2FA enrolled'
  - '19. Mobile-friendly: manual setup key + "copy key" button for authenticator apps'
  - '20. Backup codes display on enrollment completion'
  # Vendor as 2FA Support Admin (Global)
  - '21. Reset 2FA for any user across any organisation (escalation path when Director unreachable)'
  - '22. Cross-organisation 2FA enrollment dashboard (stats per org)'
  - '23. Bulk-enable 2FA — send onboarding emails to all unenrolled users in an org'
  - '24. Force 2FA toggle at organisation level (Vendor can mandate for any org)'
  - '25. Generate temporary one-time access codes (15-min validity, for Director lockout scenarios)'
  - '26. Global 2FA audit log — all enrollments, resets, lockouts, failed attempts across system'
  # Director 2FA Management Powers (Org-Level)
  - '27. Inline 2FA reset from staff management page (no navigation away)'
  - '28. Bulk 2FA status table: Staff → Role → 2FA Status → Last Login → Actions'
  - '29. "Send 2FA Reminder" button next to each unenrolled user'
  - '30. Mandate 2FA toggle for own organisation'
  - '31. Temporary bypass codes for field emergencies (30-min validity, fully audited)'
  - '32. 2FA activity log filtered to own organisation'
  - '33. Delegate 2FA reset to Coordinators (opt-in toggle for large orgs)'
  - '34. Notification on successful 2FA enrollment by any org member'
context_file: 'docs/lowlevel-technical-document.md, docs/user-manual.md'
---

# Brainstorming Session Results

**Facilitator:** Admin
**Date:** 2026-07-01

## Session Overview

**Topic:** Two-Factor Authentication (2FA) for all user roles including Vendors

**Goals:** Design a production-ready, inclusive 2FA strategy that works across all roles (Director, Coordinator, SocialWorker, CaseWorker, Accountant, Vendor) in a child protection platform with web + mobile clients

### Context Guidance

Kaval Online is a case management platform for POCSO & child protection services. Current auth:

- **Platforms:** Angular 19 Web App + React Native Mobile App (Android)
- **Role hierarchy:** Director → Coordinator → SocialWorker/CaseWorker → Accountant → Vendor
- **Current auth:** Email + Password → Email OTP → JWT Access/Refresh tokens
- **Current 2FA:** TOTP (authenticator app) available for Directors only
- **Database:** PostgreSQL with `totp_secret` column on users table
- **Cache:** Redis (used for OTP challenges, refresh tokens)
- **Infrastructure:** .NET 8 API, Hangfire background jobs, MailKit for email
- **Key constraints:** Field workers (SocialWorker/CaseWorker) work offline in remote areas, Vendors are external third parties

---

## Consolidated 2FA Strategy

### Phase 1: Vendor Self-Enrollment + Settings Page

| # | Idea | Detail |
|---|------|--------|
| 15 | **Vendor Settings Page** | Add `/vendor/settings` route — a simple page with 2FA enrollment card. Reuse the existing `Enroll2FAComponent` that Directors already use. The VendorGuard already protects `/vendor`. |
| 16 | **Profile page bundle** | Same page includes password change and account info. Two settings in one place. |
| 17 | **Status badge** | "2FA: Enabled ✓" or "2FA: Not Set Up — Enable Now" as a prominent card at the top. |
| 18 | **Soft-block on first login** | If vendor has no `totp_secret`, `/vendor` redirects to `/vendor/settings` with a banner: *"Two-factor authentication is required. Please set it up to continue."* |
| 19 | **Manual key + copy button** | Below the QR code show the plain-text secret key with a "Copy" button — vendors can paste into Google Authenticator / Authy if screen-scanning isn't convenient. |
| 20 | **Backup codes on completion** | After QR verification, show 5 one-time backup codes. One-time display with download option. |

**What changes:**
- Angular: Add `/vendor/settings` route under `VendorGuard`, reuse `Enroll2FAComponent`
- Angular: Add redirect logic in `VendorComponent` — if `totp_secret IS NULL`, push to `/vendor/settings`
- API: Already works — `POST /auth/enroll-2fa` and `POST /auth/verify-enroll-2fa` exist and are role-agnostic
- API (minor): Add a `GET /auth/2fa-status` endpoint so the Angular app can check enrollment state

---

### Phase 2: Director 2FA Management (Org-Level)

| # | Idea | Detail |
|---|------|--------|
| 27 | **Inline reset** | Staff Management page — click user → "Reset 2FA" button. Confirmation dialog. No page navigation. |
| 28 | **2FA status table column** | Add a "2FA" column to the staff list showing ✓ / ✗ with a tooltip showing enrollment date if enabled. |
| 29 | **Remind button** | Next to each ✗ user, a "Remind" button sends an email: *"Your Director requires 2FA — click here to set it up"* with direct link to the enrollment page. |
| 30 | **Mandate 2FA toggle** | Organisation Settings → "Require two-factor authentication for all staff." Toggle ON → all unenrolled users get redirected to enrollment on next login. |
| 31 | **Temporary bypass codes** | When a field worker loses their phone, Director generates a one-time code (valid 30 min). Logged in audit_events. Worker can log in and re-enroll 2FA. |
| 32 | **2FA audit log** | Filtered view of audit_events showing: enrollment, reset, bypass code usage, failed TOTP — scoped to their organisation. |
| 33 | **Delegate to Coordinators** | Settings toggle: "Allow Coordinators to reset 2FA for field workers." Helps large orgs avoid Director bottleneck. |
| 34 | **Enrollment notification** | Director receives in-app notification when any user in their org enrolls 2FA. |

**What changes:**
- Angular: Extend staff management table with 2FA column + actions
- Angular: Add "2FA Settings" section to Organisation Settings page
- API: New `Admin.UsersController` endpoint: `POST /admin/users/{id}/reset-2fa`
- API: New endpoint: `POST /admin/users/{id}/generate-bypass-code`
- API: New query: `GET /admin/audit/2fa?orgId=...` — filtered audit trail
- API: New org-level setting `require_2fa` column on `organisations` table
- API: Login flow checks org-level `require_2fa` flag and redirects if unenrolled

---

### Phase 3: Vendor Global 2FA Administration

| # | Idea | Detail |
|---|------|--------|
| 21 | **Cross-org 2FA reset** | Vendor can reset 2FA for *any* user in *any* organisation. Escalation path when a Director is locked out or unreachable. |
| 22 | **Enrollment dashboard** | Vendor sees a table: Organisation → Total Users → Enrolled → Not Enrolled → % Compliance. Sortable, filterable. |
| 23 | **Bulk reminder campaign** | Vendor selects an organisation → "Send 2FA reminders to all unenrolled users." One email per user with enrollment link. |
| 24 | **Force 2FA at org level** | Vendor can toggle `require_2fa` for any organisation — same effect as Director toggle but available when Director hasn't done it. |
| 25 | **Temporary access codes** | Vendor generates one-time codes (15-min validity) specifically for: Director lockout, emergency access, system testing. |
| 26 | **Global 2FA audit log** | Full unfiltered audit — all 2FA events across all organisations. Exportable for compliance reporting. |

**What changes:**
- Angular: Add a "2FA Oversight" section to the Vendor portal (`/vendor/2fa`)
- API: New `Vendor.TwoFactorController` — endpoints mirror Admin ones but operate globally
- API: New endpoint: `GET /vendor/2fa/stats` — cross-org enrollment statistics
- API: New endpoint: `POST /vendor/2fa/campaigns` — bulk send 2FA reminder emails

---

### Phase 4: Universal 2FA Enrollment (All Roles)

| # | Idea | Detail |
|---|------|--------|
| 1 | **Detect → Redirect on login** | Login response for any unenrolled user includes `2FA_SETUP_REQUIRED`. Client auto-redirects to enrollment. |
| 4 | **Force enrollment at first login** | Just like Directors already get — every role gets prompted to scan QR and verify on first login. |
| 12 | **Backup codes for everyone** | On enrollment, every user gets 5 one-time backup codes. Critical for field workers who may lose phone/service. |
| 2 | **Gated session** | If user postpones, they get a restricted session until enrollment completes. Only `/enroll-2fa` is accessible. |

**What changes:**
- API: Login flow checks `totp_secret` universally (not just for Directors) — return `requiresTotpSetup: true` if null
- Angular: Enrollment prompt component is already built — just remove the Director-only guard
- Mobile (React Native): Add enrollment screen for field workers doing first login from phone
- API: Generate and return backup codes on enrollment verification

---

### Role Responsibility Matrix

| Capability | User Self-Service | Coordinator (if delegated) | Director (Org) | Vendor (Global) |
|---|---|---|---|---|
| Enroll own 2FA | ✅ First login prompt | ✅ Same | ✅ Same | ✅ Settings page |
| Reset own 2FA | ❌ (would need current TOTP) | ❌ | ❌ | ❌ |
| Reset 2FA for others | — | ✅ (if delegated) | ✅ Own org | ✅ Any org |
| View 2FA status | ✅ Own profile | ✅ Their team | ✅ Org-wide | ✅ Cross-org |
| Send 2FA reminders | — | — | ✅ To their team | ✅ Bulk campaigns |
| Mandate 2FA | — | — | ✅ Toggle for org | ✅ Toggle for any org |
| Generate bypass codes | — | — | ✅ Field emergencies | ✅ Director lockout |
| View 2FA audit log | — | — | ✅ Org scope | ✅ Full system |
| Backup codes | ✅ Gets on enrollment | ✅ Gets on enrollment | ✅ Gets on enrollment | ✅ Gets on enrollment |

---

## Implementation Priority

```
Phase 1 ─── Vendor Self-Enrollment ─────────────────▸ HIGH (unblocks Vendor 2FA mandate)
Phase 2 ─── Director 2FA Management ────────────────▸ HIGH (daily use, field worker support)
Phase 4 ─── Universal Enrollment Flow ──────────────▸ MEDIUM (extend existing Director flow)
Phase 3 ─── Vendor Global 2FA Admin ────────────────▸ LOWER (escalation/admin layer)
```

---

## Open Questions to Resolve

1. **Backup code storage** — How do we store backup codes? BCrypt hash each one individually? Encrypted blob? How to show them exactly once without risking loss?
2. **Bypass code vs backup code** — Should bypass codes be a separate mechanism (Director generates on-demand) or should the user's own backup codes cover this scenario?
3. **Mobile enrollment** — The React Native app needs an enrollment screen. Should field workers enroll from the web (their Director can help) or from their phone?
4. **Cooldown after failed TOTP** — After N failed TOTP attempts, should we lock 2FA and force email OTP re-verification? (Anti-brute-force)
5. **Legacy migration** — Users who already exist without TOTP: soft-reminder on login, or forced enrollment with a grace period?
