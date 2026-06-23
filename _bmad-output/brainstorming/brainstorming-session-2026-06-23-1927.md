---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Role management and registration system for Midi-Kaval'
session_goals: 'Replace hardcoded accounts with self-service Director registration and Director-managed role lifecycle (create, invite, activate/deactivate)'
selected_approach: 'ai-recommended'
techniques_used: ['First Principles Thinking', 'What If Scenarios']
ideas_generated: 12
session_active: false
workflow_completed: true
context_file: ''
---

# Brainstorming Session Results

**Facilitator:** Admin
**Date:** 2026-06-23

## Session Overview

**Topic:** Role management and registration system for Midi-Kaval
**Goals:** Replace hardcoded accounts with self-service Director registration and Director-managed role lifecycle (create, invite, activate/deactivate)

### Session Setup

The current system uses hardcoded roles and accounts in configuration files. The goal is to design a production-ready system where:
- The first Director can register and bootstrap the system
- Directors can create other roles (Coordinator, Field Worker, etc.)
- Invitation links are sent for users to self-register
- Directors can activate, deactivate, and manage users
- All role changes are audited

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** Role management and registration system with focus on replacing hardcoded accounts with self-service Director registration and Director-managed role lifecycle

**Recommended Techniques:**

- **First Principles Thinking:** Strip away all assumptions about hardcoded accounts and rebuild from fundamental truths about who needs access and how trust is established
- **What If Scenarios:** Stress-test the design against edge cases — stolen invites, orphaned organisations, Director lockout

## Idea Organization and Prioritization

### Complete Idea Inventory

**Theme 1: Vendor Bootstrap & Safety Net**
*How the system gets its first Director and recovers from total Director loss*

- **[#1] Vendor-Bootstrapped Activation** — Vendor has a secure 2FA backstage; sends a one-time activation link to the first Director's designated email
- **[#2] Targeted Invitation** — Link goes to the specific email the client provides; no open/public registration
- **[#4] Vendor Safety Net** — If all Directors are gone (deactivated/lost), vendor re-issues activation link. Vendor power is scoped to this "break glass" scenario.

**Theme 2: Director Role Self-Management**
*How Directors govern their own ranks*

- **[#3] Peer Director Invitation** — Any Director can invite a new Director; the role is self-propagating within the organisation
- **[#5] Director-to-Director Deactivation** — A Director can suspend (reversible) or permanently delete (irreversible) another Director
- **[#7] Last-Director Protection** — System prevents deactivation of the last active Director; warns: "Cannot deactivate — no other active Director remains."

**Theme 3: Role Hierarchy & Access Control**
*How lower roles are managed*

- **[#8] Director-Only Role Management** — Only Directors can create/invite/suspend/delete users of any role (Coordinator, Field Worker, etc.)
- **[#12] B2B Invitation-Only Model** — No self-nomination or public registration pages. All onboarding is Director-initiated invitation-only.

**Theme 4: Invitation Security**
*Protecting the invitation-to-activation pipeline*

- **[#9] Double-Confirmation Flow** — Invitation link → registration form (set name/password) → confirmation email → account activates only after second link is clicked
- **[#10a] 2FA Mandate for Directors** — Directors must set up two-factor authentication before they can manage any users
- **[#10b] Audit Broadcast** — All critical user management actions trigger email notifications to every active Director

**Theme 5: Audit & Irreversibility**
*Logging and lifecycle guarantees*

- **[#6] Immutable Audit Log** — Every user creation, suspension, reactivation, and permanent deletion is logged with actor identity and timestamp
- **Permanent delete** — cannot be revoked
- **Suspension** — can be revoked by any Director

### Prioritization Results

**Tier 1 — Foundation (Must Build First):**
- Vendor Bootstrap & Safety Net (Theme 1)
- Role Hierarchy & Access Control (Theme 3)

**Tier 2 — Core Invitation & Role Lifecycle:**
- Director Role Self-Management (Theme 2)
- Invitation Security (Theme 4)

**Tier 3 — Governance & Observability:**
- Audit & Irreversibility (Theme 5)

### Action Plans

**Tier 1 — Vendor Bootstrap & Safety Net:**
1. Build secure vendor backstage page (2FA-protected, stored vendor contact info)
2. Generate cryptographically signed, time-bound, single-use activation tokens
3. Email delivery integration for activation links
4. Break-glass recovery: detect zero active Directors → allow vendor to re-issue

**Tier 1 — Role Hierarchy & Access Control:**
1. Define role hierarchy (Director > Coordinator, Field Worker, etc.)
2. Build Director user management dashboard (list users, roles, status)
3. Implement "Invite User" flow (email + role selection → invitation link)
4. API authorization: all management endpoints check currentUser role is Director

**Tier 2 — Director Role Self-Management:**
1. Extend invite flow to allow Director role selection
2. Implement suspend (reversible) with last-Director guard
3. Implement permanent delete (irreversible) with last-Director guard
4. Last-Director query: block deactivation if only one Director remains

**Tier 2 — Invitation Security:**
1. Double-confirmation: first link → registration → second link → activation
2. Force 2FA setup (TOTP) before Director can manage users
3. Email broadcast to all active Directors on every critical action

**Tier 3 — Audit & Irreversibility:**
1. Schema: EventType, ActorUserId, TargetUserId, Action, Timestamp, Metadata
2. Instrument all user management endpoints to write audit events
3. Build Director-only audit log viewer with search/filter
4. Physical delete vs soft suspend enforcement

## Session Summary and Insights

**Key Achievements:**
- 12 structured ideas generated across 2 creative techniques
- Complete vendor-bootstrapped Director registration flow designed
- Director self-management model with peer oversight and last-Director protection
- B2B-appropriate invitation-only architecture (no public pages needed)
- Double-confirmation security model for invitations
- 2FA mandate + audit broadcast for Director accountability
- Immutable audit trail with clear suspend vs delete semantics

**Techniques Used:**
- **First Principles Thinking** — Stripped away the hardcoded config assumption and rebuilt from "how does the first person get in?"
- **What If Scenarios** — Stress-tested against intercepted links, compromised Director accounts, and orphaned organisations

**Session Reflections:**
The session revealed a clean, hierarchical trust model: Vendor → Director → All Users. The key insight was that Directors are not a singleton — they're a self-managing peer group with mutual accountability (deactivation) and system-enforced protection (last-Director guard). The B2B constraint kept the design appropriately simple: no public registration, no self-nomination flow.
