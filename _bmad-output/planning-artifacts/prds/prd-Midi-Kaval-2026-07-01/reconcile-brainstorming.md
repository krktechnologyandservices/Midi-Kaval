---
title: 'Brainstorming → PRD Reconciliation Report'
source: 'brainstorming-session-2026-07-01-0637.md'
target: 'prd.md'
date: 2026-07-01
status: complete
---

# Brainstorming → PRD Reconciliation Report

## Scope

All 34 ideas from the 2FA brainstorming session mapped against the PRD's functional requirements, future scope, and open questions.

---

## Coverage Summary

| Status | Count | Ideas |
|--------|-------|-------|
| **Captured in-scope** | ~15 | 15, 16, 17, 18, 19, 20, 27, 28, 29, 30, 31, 32, 33, 34, 12 |
| **Deferred to Future Scope** | ~6 | 1, 2, 4, 13 (Phase 4), 21, 22, 23, 24, 25, 26 (Phase 3) |
| **Missing / Silently Dropped** | ~7 | 3, 5, 6, 7, 8, 9, 10, 14 |
| **Drifted Intent** | 5+ | Multiple items where scope or feel shifted |

> Numbers overlap because bundled ideas span phases.

---

## Gaps — Ideas Missing from the PRD

### GAP-1: Proactive Onboarding Emails (Ideas 5, 6, 7)
- **Brainstorming:** Welcome email with 2FA enrollment link on account creation (idea 5); enrollment link embedded in every email OTP message (idea 6); Director can send "2FA Setup Email" as a **first-time invitation** (idea 7).
- **PRD:** Only a reactive "Send Reminder" (FR-2.3) — assumes the user already knows about 2FA and is just being nudged.
- **Impact:** New users (especially Vendors) don't receive a setup link until someone manually reminds them. No automated welcome flow.

### GAP-2: Email OTP as Pragmatic 2FA / SMS Fallback (Ideas 8, 10)
- **Brainstorming:** Reframe email OTP as "2FA" for vendors (pragmatic simplification, idea 8); offer SMS OTP as an alternative for users without smartphones (idea 10).
- **PRD:** Explicitly rejects email OTP as true 2FA (§2.3). No SMS fallback anywhere. Open question OQ-5 punts the discussion without a requirement.
- **Impact:** Users without smartphones (or who can't install authenticator apps) have no alternative path. The inclusivity goal from the brainstorming is dropped.

### GAP-3: Login Response `setupUrl` Contract (Idea 3)
- **Brainstorming:** Login response carries `{ requires2faSetup: true, setupUrl: "..." }` — a specific JSON contract to tell the client exactly where to redirect.
- **PRD:** Mentions `2FA_SETUP_REQUIRED` flag but omits the `setupUrl` field.
- **Impact:** Client-side routing becomes more fragile — the UI must hardcode the redirect URL instead of receiving it from the API.

### GAP-4: 403 Response-Body Enrollment (Idea 9)
- **Brainstorming:** API returns provisioning data (provisioningUri + qrCodeBase64) inline in the 403 response body — an alternative enrollment trigger.
- **PRD:** No equivalent. Enrollment is always a separate GET-then-POST flow.
- **Impact:** Lost opportunity for a streamlined inline enrollment pattern that would work without a dedicated settings page.

### GAP-5: Hardware Security Keys (Idea 14)
- **Brainstorming:** YubiKey / hardware security key for sensitive roles.
- **PRD:** Not mentioned at all — not in-scope, not in future scope, not even as an open question.
- **Impact:** High-security roles (Vendors, Directors) have no hardware-bound option.

### GAP-6: WebAuthn / Passkeys Detail (Idea 13)
- **Brainstorming:** Passkeys as a serious alternative with biometric or device-bound auth, no authenticator app needed.
- **PRD:** A single bullet under Phase 4 future scope — "Passkeys / WebAuthn support as alternative to TOTP" — with zero detail, UX, or requirements.
- **Impact:** The idea is acknowledged but not scoped. No path to implementation.

### GAP-7: Legacy User Migration Strategy
- **Brainstorming Open Question 5:** Users who already exist without TOTP — soft reminder or forced enrollment with a grace period?
- **PRD:** Not addressed. No migration plan for existing unenrolled users (outside the org-level mandate toggle).
- **Impact:** Existing users remain unenrolled indefinitely unless their Director flips the toggle. No timed rollout.

---

## Drift — Ideas with Changed Intent or Scope

### DRIFT-1: Backup Code Count (Idea 12 → FR-3)
- **Brainstorming:** "5-8 codes, one-time display" — flexible count, implying design-space exploration.
- **PRD:** Fixed at exactly 5 codes (FR-3.1). The range is silently narrowed without acknowledging the decision rationale.
- **Consequence:** Minor, but the flexibility that allowed for future adjustment is gone.

### DRIFT-2: Relative Priority Reversal (Phase Sequencing)
- **Brainstorming:** Phase 4 (Universal Enrollment, all roles) = **MEDIUM**, Phase 3 (Vendor Global Admin) = **LOWER**. Universal enrollment is the higher priority.
- **PRD:** Both Phase 3 and Phase 4 are "Future" with no relative priority. In practice Phase 3 (Vendor Admin) gets more detailed bullets in Future Scope than Phase 4.
- **Consequence:** Priority signal inverted. Universal enrollment — the most impactful inclusion feature — is deprioritised relative to the brainstorming's intent.

### DRIFT-3: Coordinator Delegation Scope Narrowing (Idea 33 → FR-2.7)
- **Brainstorming:** "Delegate 2FA reset to Coordinators (opt-in toggle for large orgs)." Open-ended — reset for anyone.
- **PRD:** "When ON, Coordinators see the 'Reset 2FA' action for SocialWorker/CaseWorker roles **only**." Does not grant bypass code generation.
- **Consequence:** Improved security but silently narrows the idea. A Director expecting Coordinators to handle all resets (including other roles) will be surprised.

### DRIFT-4: Vendor Temporary Access Code Detail Loss (Idea 25 → Future Scope)
- **Brainstorming:** "15-min validity, for Director lockout scenarios" — specific parameters.
- **PRD Future Scope:** "Escalation reset when Director is locked out" — drops the 15-min validity and the temporary-access-code framing.
- **Consequence:** Design intent preserved at a headline level, but the implementation guardrails (time-bound, one-time) are lost from the spec.

### DRIFT-5: Soft-Block Wording Muted (Idea 18 → FR-1.4)
- **Brainstorming:** "/vendor/settings is **only accessible page** until 2FA enrolled" — emphatic, hard gate.
- **PRD:** "Any navigation to `/vendor/*` redirects to `/vendor/settings`" — functionally the same, but the hard-gate framing ("only accessible page") is replaced by a softer redirect.
- **Consequence:** Equivalent in code, but the PRD's language doesn't convey the same severity. A reader might underestimate the enforcement.

---

## Nuances Silently Dropped

These are qualitative or sentiment-level elements from the brainstorming that the PRD's FR structure cannot express:

1. **Field worker inclusivity as primary motivator** — The brainstorming repeatedly foregrounds field workers without smartphones in remote areas. The PRD opens with "Vendors must interact with raw Swagger API calls" as the primary trigger. The PRD's **problem frame** shifted from *inclusion* to *developer UX*.

2. **Proactive tone → Reactive tone** — The brainstorming has 5 proactive-outreach ideas (welcome email, OTP-embedded link, setup invitation, bulk campaigns, reminders). The PRD implements only the reactive "Director Remind" button. The design posture shifted.

3. **Backup vs bypass code design question unresolved** — Brainstorming Open Question 2 explicitly asks: should bypass codes be unified with backup codes? The PRD keeps them as separate mechanisms (FR-2.5 vs FR-3) without addressing the design question.

4. **Flexibility language removed** — The brainstorming uses phrases like "5-8 codes", "opt-in toggle", "consider using email OTP as 2FA". The PRD fixes all open parameters (5 codes exactly, role-scoped delegation, TOTP-only) without rationale.

5. **SMS as inclusivity mechanism** — The brainstorming's strongest inclusivity mechanism (SMS OTP for non-smartphone users) is entirely absent. The PRD's OQ-5 hand-waves: "discuss if email should count."

---

## Appendix: Full 34-Idea Mapping

| # | Brainstorming Idea | PRD Status | Notes |
|---|-------------------|------------|-------|
| 1 | Detect → Redirect → Enroll | Future (Phase 4) | Deferred |
| 2 | Gated restricted session | Future (Phase 4) | Deferred |
| 3 | setupUrl in login response | **Missing** | JSON contract dropped |
| 4 | Force enrollment all roles | Future (Phase 4) | Deferred |
| 5 | Welcome email on account creation | **Missing** | No equivalent |
| 6 | Link in every OTP email | **Missing** | No equivalent |
| 7 | Director send setup invitation | **Missing** | FR-2.3 is "Remind," not "Invite" |
| 8 | Email OTP as 2FA for Vendors | **Missing** | Rejected (§2.3) |
| 9 | 403 response carries provisioning | **Missing** | No equivalent |
| 10 | SMS OTP alternative | **Missing** | OQ-5 only, no requirement |
| 11 | Director force-reset 2FA (extend) | FR-2.2 | Captured ✅ |
| 12 | Backup codes (5-8) | FR-3 | **Drifted**: fixed at 5 |
| 13 | Passkeys / WebAuthn | Future (Phase 4) | Bare minimum mention |
| 14 | YubiKey / hardware key | **Missing** | Not mentioned |
| 15 | /vendor/settings page | FR-1.1–1.3 | Captured ✅ |
| 16 | Profile page bundle (password) | FR-1.5 | Captured ✅ |
| 17 | Status badge | FR-1.2 | Captured ✅ |
| 18 | Soft-block on first login | FR-1.4 | **Drifted**: softer language |
| 19 | Manual key + copy | FR-1.3 | Captured ✅ |
| 20 | Backup codes display | FR-3.4 | Captured ✅ |
| 21 | Cross-org 2FA reset | Future (Phase 3) | Deferred |
| 22 | Cross-org dashboard | Future (Phase 3) | Deferred |
| 23 | Bulk reminder campaigns | Future (Phase 3) | Deferred |
| 24 | Force 2FA any org | Future (Phase 3) | Deferred |
| 25 | Temp access codes (15-min) | Future (Phase 3) | **Drifted**: detail dropped |
| 26 | Global 2FA audit log | Future (Phase 3) | Deferred |
| 27 | Inline reset (staff mgmt) | FR-2.2 | Captured ✅ |
| 28 | 2FA status table column | FR-2.1 | **Drifted**: "last login" column dropped |
| 29 | Send 2FA Reminder button | FR-2.3 | Captured ✅ |
| 30 | Mandate 2FA toggle (org) | FR-2.4 | Captured ✅ |
| 31 | Temp bypass codes (30-min) | FR-2.5 | Captured ✅ |
| 32 | 2FA audit log (org-scoped) | FR-2.6 | Captured ✅ |
| 33 | Delegate reset to Coordinators | FR-2.7 | **Drifted**: role-scoped narrower |
| 34 | Enrollment notification | FR-2.8 | Captured ✅ |
