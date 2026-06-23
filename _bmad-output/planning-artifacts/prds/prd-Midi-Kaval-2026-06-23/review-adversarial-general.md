# Adversarial Review: Role Management & Registration System PRD

**Review date:** 2026-06-23
**Document:** `prd.md` (Role Management & Registration System for Midi-Kaval)
**Decision log reviewed:** `.decision-log.md` (10 decisions)
**Reviewer stance:** Skeptical engineering lead who needs to estimate and build this

---

## Executive Summary

This PRD describes a reasonable role management and registration flow for a B2B application, but it contains **several critical design contradictions, hand-waved security mechanisms, and unresolved dependencies that will generate significant rework during implementation**. The document is strongest on personae and high-level flows, weakest on data model, failure modes, and integration with the existing Midi-Kaval platform. An engineering lead should **not** sign off on this for estimation until at least the 5 critical items below are addressed — several of them represent mutually exclusive design choices that could double implementation effort if chosen wrong.

**Total findings: 27** (Critical: 5, High: 8, Medium: 9, Low: 5)

---

## Critical Findings

### C-1: No Director 2FA recovery flow creates a guaranteed support escalations path

**Section:** 4.2 FR-10, Open Question #4, Section 7.2

**Quoted phrases:**
- *"A Director cannot perform any user management action [...] until they have enrolled in two-factor authentication"* (FR-10)
- *"SMS/email backup codes for 2FA — Only authenticator app (TOTP) in v1. [NOTE FOR PM: Deferred due to scope; revisit if client organisations report frequent 2FA lockouts.]"* (Section 7.2)
- *"What is the process for a Director who loses their 2FA device?"* (Open Question #4)

**Analysis:** The PRD mandates 2FA for all Director management actions but explicitly defers SMS/email backup codes to v2, and lists the 2FA loss recovery process as an open question. In the real world, Directors **will** lose their phones, wipe authenticator apps, or get new devices. When they do, the PRD provides exactly **zero** paths to recover:

- They cannot perform any management action (they're locked out of their Director role for management purposes).
- The only "break glass" is the Vendor Safety Net (FR-3), but that only triggers when **all** Directors are gone — it doesn't help a single Director who lost their phone while other Directors exist.
- There is no "re-enroll 2FA" flow described, and no fallback.

This means every 2FA loss becomes a support ticket requiring manual intervention. For a system designed to *reduce* vendor dependency ("empowered, Director-owned capability"), this creates a new dependency that the PRD doesn't acknowledge. The "revisit if client organisations report frequent lockouts" note is naive — the first lockout will be a support escalation.

**Risk:** Guaranteed support escalations for a common real-world scenario. Directly contradicts the PRD's stated goal of reducing vendor dependency.

---

### C-2: "Permanent deletion" semantics are contradictory and block implementation

**Sections:** FR-8, FR-13, AS-3

**Quoted phrases:**
- *"A Director can permanently delete a user. This action is irreversible — the user record is fully removed or anonymised."* (FR-8)
- *"Actor and target are reliably identified even if the target user is subsequently deleted"* (FR-13)
- *"AS-3: Permanent deletion anonymises rather than cascade-deletes user data (FR-8)"* (Assumptions Index)

**Analysis:** These three statements are mutually inconsistent:

1. If the user record is **fully removed**, FR-13's requirement to "reliably identify" the target user in past audit events is broken. Audit events would have foreign key references to a deleted row.
2. If the user record is **anonymised** (AS-3), the "Permanent Deletion" label is misleading — the record persists; it's just scrubbed. This has different UX, GDPR, and data-model implications than true deletion (e.g., do anonymised records count toward storage limits? Do they appear in the user list as "Deleted User"?).
3. FR-13 requires identifying the target user after deletion — which implies **keeping** some identity in the audit record (name? email?). This starts to look like anonymisation, not deletion.

The PRD chooses **none** of these paths definitively. FR-8 says "or" (it's ambiguous). AS-3 says "anonymises" (implying a choice). FR-13 implies deletes must be preserved in audit context (favoring anonymisation or soft-delete with masking). An implementation cannot proceed until this is resolved because it determines:
- Database schema design (cascade? set null? soft delete? dedicated audit identity snapshot?)
- GDPR compliance posture
- UI wording ("delete" vs "anonymise" vs "remove")
- Query complexity for audit log viewer

**Risk:** An implementation that goes down the "full removal" path will have to re-architect when audit integrity requirements surface. This is a foundational data model choice that the PRD leaves dangling.

---

### C-3: Session termination for suspended users is specified as a 5-minute SLA with no feasible mechanism

**Section:** FR-6, AS-2

**Quoted phrases:**
- *"Active sessions of the suspended user are terminated within [ASSUMPTION: 5 minutes]"* (FR-6)
- *"AS-2: Suspended user sessions are terminated within 5 minutes (FR-6)"* (Assumptions Index)

**Analysis:** This is an implementation landmine. The PRD doesn't say how the application manages sessions. If using JWT tokens (a common modern pattern for APIs), **there is no server-side session to terminate** — the token is self-contained and valid until expiry. The 5-minute SLA cannot be met without one of:
- A token blacklist checked on every request (stateful JWT, negating the stateless benefit)
- Very short token lifetimes (~1 minute) plus refresh token rotation (significant UX and reliability cost)
- Server-side sessions with active invalidation (WebSocket push or polling)
- Database-backed session store with a "suspended" flag checked on each request

None of these are mentioned. The PRD doesn't even specify the authentication mechanism (JWT, server-side sessions, cookie-based). The 5-minute SLA is presented as a casual assumption but implies a significant architectural commitment. If the existing system uses long-lived JWTs, this requirement alone could force a re-architecture of the auth layer.

**Risk:** The suspension feature cannot meet its specified SLA without choosing (and possibly building) a session management strategy. This is not acknowledged as a design dependency.

---

### C-4: Vendor Backstage is a second undefined application with its own security requirements

**Sections:** 4.1 (FR-1, FR-3), 2.1, Open Question #2

**Quoted phrases:**
- *"The Vendor uses a secure backstage portal to activate a new client organisation"* (Section 4.1)
- *"The Vendor (authenticated via 2FA in the backstage portal)"* (FR-1)
- *"Vendor Backstage — A secure portal (2FA-protected) used by the Vendor to send activation links to client organisations"* (Glossary)
- *"Should the Vendor backstage support multiple administrators?"* (Open Question #2)

**Analysis:** The Vendor Backstage is treated as a given but is itself a new system that needs:
- Its own authentication system (Vendor accounts with 2FA — is this the same 2FA system or different?)
- Its own user management (who creates Vendor accounts? How? Open Question #2 says this isn't decided.)
- Its own UI (not the same as the Director dashboard — different personas, different features)
- Its own authorization model (can one Vendor see all organisations? Is there vendor-to-organisation mapping?)
- Its own session management, rate limiting, audit trail
- Its own deployment (same app with role-based hiding? Separate app? Shared auth domain?)

The PRD treats the backstage portal as a minor requirement ("FR-1: Vendor sends activation link") but it's essentially a **second admin application** with its own full stack. This is roughly half the scope of the Director dashboard, but its requirements occupy about 10% of the PRD's feature section. The scope is significantly underestimated.

**Risk:** The Vendor Backstage will be discovered as a major scope item during implementation, causing planning and timeline surprises. The open question about multiple admins means even the basic auth model isn't decided.

---

### C-5: No data model or domain model defined anywhere

**Sections:** Entire document

**Quoted phrases:**
- *"Organisation — A client entity using Midi-Kaval. Has its own set of Directors and End Users."* (Glossary — only domain definition)
- *"List is scoped to the Director's own organisation only"* (FR-4)

**Analysis:** The PRD operates entirely at the feature/UI level with no data model. There is no entity-relationship definition, no schema, no description of how these concepts map to the existing Midi-Kaval data model. This means the following fundamental design questions are completely unaddressed:

- Is an "Organisation" a tenant in a multi-tenant architecture, or just a grouping column on the user table?
- Is there an `Organisations` table? A `Roles` table? A `UserRoles` join table?
- How are activation tokens stored? In a `Tokens` table with a hash? Are they embedded in a JWT?
- What's the relationship between an invitation and a user? Are invitations a separate entity?
- How does the existing user table get a `organisation_id` and `role` column?
- Is "Director" a role value or a role ID referencing a roles table?
- How do audit events reference users — foreign key to users table (what about deleted users?), or snapshots of identity at time of event?

Without this foundation, every team member will make different implicit assumptions, and integration points with the existing system are invisible. An engineering lead cannot produce a meaningful estimate from this document.

**Risk:** Schema changes discovered during implementation will cascade through all features. The absence of a data model makes the PRD impossible to estimate reliably.

---

## High Findings

### H-1: Rate limiting is mentioned in NFRs but has no spec, no thresholds, and no testable consequences

**Section:** 5 (Cross-Cutting NFRs — Security)

**Quoted phrases:**
- *"Registration and invitation endpoints must implement rate limiting to prevent enumeration attacks"* (Section 5)

**Analysis:** This is a single sentence in the NFRs with no specifics:
- What is the rate limit? Per-IP? Per-account? Per-email domain? Per-time-window?
- Burst handling? (e.g., "X requests per minute with Y burst")
- What happens when the limit is hit? HTTP 429? Account lockout? Throttled delay?
- Are different endpoints rate-limited differently? (activation link vs invitation vs registration submission)
- Are there separate limits for Vendor backstage endpoints?
- Is rate limiting configurable?

For a security feature that's specifically called out to prevent enumeration attacks, this is dangerously underspecified. Implementation without these details will produce a rate limiter that either blocks legitimate users or doesn't stop attackers — likely both, just on different endpoints.

**Risk:** A security requirement that can't be validated or tested as specified. Either over-engineered (cost) or ineffective (security gap).

---

### H-2: Email delivery failure handling is described once in NFRs but never reflected in any FR

**Sections:** 5 (Cross-Cutting NFRs — Availability), FR-1, FR-3, FR-5, FR-11, FR-12, FR-15, FR-16

**Quoted phrases:**
- *"Email delivery is outsourced to a transactional email provider; system must handle provider failure gracefully (queue retry with exponential backoff)"* (Section 5)
- *"Link delivery succeeds to a valid email address"* (FR-1 — the only testable consequence for delivery)

**Analysis:** The entire system depends on email delivery:
- Organisation bootstrap starts with an activation link email (FR-1)
- Zero-Director recovery requires the Vendor to send another activation link (FR-3)
- Every user invitation is an email (FR-5)
- Confirmation of registration is an email (FR-11)
- Every suspension/reaction/deletion notification goes through email (FR-15, FR-16)

But the testable consequences for every FR assume **email delivery always works** ("Link delivery succeeds to a valid email address"). There is no:
- Retry strategy described per email type (activation emails might need faster/higher-priority retries than notifications)
- Visibility into delivery status (does the Director know if an invitation email bounced?)
- Bounce/feedback loop handling (what happens when an email address is invalid?)
- Graceful degradation if email is temporarily unavailable (can a Director still submit an invitation that will be queued?)
- Monitoring/alerts for email delivery failures
- "Resend" for activation links specifically (FR-1 has no resend; FR-12 has resend for invitations but not for activation links)

The NFR mentions "queue retry with exponential backoff" but this is disconnected from any FR. If a Director sends an invitation and the email provider is down, does the Director see an error? A success message with delayed delivery? Does the invitation show as "pending send" in the dashboard?

**Risk:** The system appears to work but silent email failures mean users never receive critical lifecycle emails. This is a reliability gap in the core flow.

---

### H-3: Password complexity is a dangling dependency with no resolution path

**Sections:** FR-2, Open Question #1, Section 6

**Quoted phrases:**
- *"Password meets organisation-configured complexity policy"* (FR-2)
- *"What is the minimum password complexity policy for organisations?"* (Open Question #1)
- *"Role hierarchy customisation — Roles [...] are defined by the system, not configurable by the organisation"* (Section 6)

**Analysis:** FR-2 requires the password to meet an "organisation-configured complexity policy," but:
1. There is no mechanism described in the PRD for an organisation to **configure** a password policy (Section 6 says role customisation is out of scope; there's no mention of org-level configuration at all)
2. Open Question #1 asks what the minimum policy is — implying it isn't decided yet
3. If the policy is system-wide (not per-organisation), FR-2's language is misleading

This creates a dependency chain: either (a) the PRD needs to specify a system-wide default policy with a concrete definition (e.g., "minimum 8 characters, at least 1 uppercase, 1 digit"), or (b) it needs to add a Director-facing password policy configuration UI that doesn't exist in the spec. Currently it's in limbo.

**Risk:** The registration flow cannot be built without this being resolved, and it's at the end of the PRD as an "open question."

---

### H-4: No existing-user migration or cutover strategy mentioned

**Section:** Entire document

**Quoted phrases:**
- *"Midi-Kaval currently relies on hardcoded roles and accounts in configuration files"* (Section 1, Vision)

**Analysis:** The PRD acknowledges that the existing system has hardcoded accounts, but never describes what happens to them. Are existing users migrated? Are they left in place? Is there a cutover from config-file-based auth to database-driven auth? This is a fundamental integration concern:

- What happens to existing user passwords?
- What organisation do existing users belong to?
- Who is the first Director — is it automatically assigned or manually bootstrapped?
- What happens during the migration window — can users log in via both systems?
- Is there a rollback plan if the new system has issues?

The PRD starts with "here's what's broken" and then describes a greenfield replacement with no migration path. This is a classic integration blind spot.

**Risk:** The existing system and new system cannot coexist. Cutover and migration are non-trivial and unplanned.

---

### H-5: FR-15 broadcasts all management actions to all Directors with no rate limiting or digest — creates a notification spam problem

**Section:** FR-15

**Quoted phrases:**
- *"Every user creation, suspension, reactivation, and permanent deletion triggers an email notification to all active Directors in the organisation"* (FR-15)
- *"Directors cannot opt out of these notifications"* (FR-15)

**Analysis:** In an organisation with, say, 20 Directors and active personnel management:
- Suspension of User A → 20 emails to all Directors
- Reactivation of User A → 20 emails to all Directors
- Suspension of User B → 20 emails to all Directors
- Repeat for each management action

If a single Director is going through a list of users, each action generates O(n * d) emails. For 50 users and 20 Directors, that's potentially 1,000+ emails in a session. Directors "cannot opt out" means they have no escape from this flood.

The "audit broadcast" intent (shared awareness) is valid, but the implementation as described will generate notification fatigue, cause Directors to ignore/mute these emails, and potentially trigger spam complaints from the email provider. No rate limiting, no digest option, no per-Director preference.

**Risk:** A well-intentioned feature that becomes counterproductive in practice due to volume. Email deliverability could be impacted by complaint rates.

---

### H-6: "Immutable audit log" is stated but not defined — critical for security and compliance

**Sections:** FR-13, Glossary

**Quoted phrases:**
- *"Audit events are append-only; no update or delete"* (FR-13)
- *"Every action is logged in an immutable audit trail"* (Section 1, Vision)
- *"Immutable audit log with distinct suspend vs delete semantics"* (Decision Log #9)

**Analysis:** The PRD uses "immutable" and "append-only" as if they have a single obvious implementation, but they don't. Consider the implementation spectrum:
1. A database table with `INSERT`-only privileges (DBA can still modify)
2. An append-only file or log stream
3. A cryptographically chained log (hash-linked entries)
4. An external audit service (e.g., AWS CloudTrail)

Each has radically different properties for:
- **Tamper evidence** — Can a compromised database server modify past entries?
- **Operational complexity** — How does log rotation work? How do you query an append-only file?
- **Cost** — External services have per-event costs
- **Performance** — Append-only DB tables have different query patterns than standard tables

The PRD also requires "search and filter by event type, actor, target user, and date range" (FR-14). This is inherently at odds with immutable append-only storage — querying an append-only log efficiently requires indexes, which are themselves mutable. The tension between immutability and queryability is not acknowledged.

**Risk:** The engineering team may choose a weak "immutable" implementation (plain DB table) that doesn't meet audit/compliance expectations, or over-engineer a blockchain-style solution that's expensive and hard to query.

---

### H-7: "Vendor Safety Net" monitoring is described as a monitoring alert with 1-hour detection latency — too slow for a critical fallback

**Section:** FR-3, AS-1

**Quoted phrases:**
- *"Detection of zero active Directors triggers a monitoring alert within [ASSUMPTION: 1 hour]"* (FR-3)
- *"AS-1: Vendor Safety Net detection of zero active Directors triggers within 1 hour (FR-3)"* (Assumptions Index)

**Analysis:** An organisation losing its last Director means **no one can manage users, invitations, or security** in that organisation. A 1-hour detection window means:
- If the last Director is deleted at 9:00 AM, the organisation is unmanaged until at least 10:00 AM
- The monitoring mechanism isn't specified (polling? event-driven? cron job?)
- The 1-hour SLA seems arbitrary — it should be driven by business impact analysis
- There's no escalation path specified (email alert to Vendor? PagerDuty? automated ticket?)

For a "Safety Net" that's meant to prevent organisations from being stranded, a 1-hour detection latency is surprisingly slow. Event-driven detection (triggered on the deletion/deactivation action itself) would be near-instantaneous and is not mentioned.

**Risk:** Orgs are left unmanaged for up to an hour. The monitoring assumption needs justification or tightening.

---

### H-8: Success Metric SM-2 is poorly defined and incentivizes the wrong behavior

**Section:** 8 (Success Metrics), SM-2

**Quoted phrases:**
- *"SM-2: Zero support tickets related to 'can't invite a new user' or 'user locked out due to role issues.' Validates FR-4, FR-5, FR-6."* (Primary metrics)

**Analysis:** "Zero support tickets" is not a success metric — it's a wish. It conflates:
- **Product quality** — Is the UI clear enough that users don't get stuck?
- **Documentation quality** — Do help docs resolve issues before tickets are filed?
- **User patience/tolerance** — Do users just give up instead of filing tickets?
- **Ticket classification** — Are tickets about invitations categorized correctly?

A metric of "zero" is unrealistic and creates perverse incentives: engineering could reduce ticket count by making it harder to file tickets, or by closing tickets without resolving root causes. A better metric would be "invitation-to-completion rate > X%" (which the PRD has as SM-3, but as a secondary metric) or "time-to-invite < Y minutes for Directors."

**Risk:** A metric that can't meaningfully validate the feature and incentivizes the wrong behaviors.

---

## Medium Findings

### M-1: "Configurable duration" for token expiry without specifying the configuration mechanism

**Sections:** FR-1, FR-5, FR-11, FR-12

**Quoted phrases:**
- *"Token expires after a configurable duration (default 7 days)"* (FR-1)
- *"Invitation link expires after a configurable duration (default 7 days)"* (FR-5)
- *"Confirmation link is time-bound (default 24 hours)"* (FR-11)

**Analysis:** The PRD says these durations are "configurable" but never says how. Environment variable? App settings DB table? Director dashboard setting? The configuration surface matters because:
- If env vars: requires deployment/restart to change → operations overhead
- If DB: needs UI and RBAC → feature scope creep
- If app settings: may differ per environment → inconsistency risk

A single "configurable" tag across three FRs masks very different implementation costs.

---

### M-2: No mention of CSRF protection for management action endpoints

**Section:** 5 (Cross-Cutting NFRs)

**Analysis:** The PRD has a Security section in NFRs but doesn't mention CSRF protection. If the Director dashboard is a browser-based UI making POST requests for destructive actions (suspend, delete, invite), it needs CSRF tokens. Omission is notable given the security-conscious tone of the rest of the document.

---

### M-3: No idempotency for invitation creation — double-click risk

**Section:** FR-5

**Quoted phrases:**
- *"Duplicate invitation to an already-registered email returns a clear error"* (FR-5)

**Analysis:** The only duplicate check described is for already-registered emails. What about double-clicks? If a Director clicks "Invite" twice in rapid succession, does the system create two invitations? Two confirmation links? The testable consequences don't address idempotency. A simple client-side button disable isn't sufficient for network retries.

---

### M-4: Deactivation of a non-last Director by another Director is unrestricted — allows single-person purge

**Section:** FR-9

**Quoted phrases:**
- *"The system prevents suspension or permanent deletion of a Director if no other active Director remains in the organisation"* (FR-9)
- *"Directors can deactivate other Directors (peer accountability)"* (Decision Log #10)

**Analysis:** The Last-Director Protection only guards the final Director. A rogue Director with access could:
1. Deactivate Director B
2. Deactivate Director C
3. Deactivate themselves

Steps 1-2 are allowed (there are still other Directors). Step 3 is blocked (last Director). But steps 1-2 alone could cripple an organisation's management capability if the rogue Director then goes on leave or loses access. There's no discussion of this risk profile.

---

### M-5: Self-service password reset relies on an existing flow that may not exist for Directors

**Section:** 7.2 (Out of Scope for MVP)

**Quoted phrases:**
- *"Self-service password reset — Relies on existing Midi-Kaval forgot-password flow; no new changes. Deferred to Epic 7."*

**Analysis:** This assumes the existing forgot-password flow handles all user types equally. If the existing flow was designed for End Users only, it may not work for Directors (who have different auth requirements including 2FA). The dependency on "Epic 7" (not defined in this PRD) means the Director experience has an undefined password recovery path.

---

### M-6: Counter-metric SM-C1 is tautological

**Section:** 8 (Success Metrics), SM-C1

**Quoted phrases:**
- *"Acceptable invite completion time is < 7 days; optimizing below that risks security bypass."*

**Analysis:** The invitation link expires in 7 days (FR-5, default). The counter-metric says acceptable invite completion time is < 7 days. This is circular — the metric says "don't optimize for completion within the link lifetime," which is equivalent to "don't change anything." It doesn't define what an *unacceptable* completion time would be (e.g., > 7 days = expired = failure by definition). A meaningful counter-metric would set a floor (e.g., "do not optimize below 2 days") to prevent removing friction that has security value.

---

### M-7: Performance bounds for audit log query (3s for 100k events) may not hold with combined filters on indexed columns

**Section:** 5 (Cross-Cutting NFRs — Performance)

**Quoted phrases:**
- *"Audit log query with combined filters: < 3 seconds for 100,000 events"*

**Analysis:** 100k events is a small dataset — this target should be easy with proper indexing. But the real concern is how quickly the audit log grows and what happens at 1M, 10M+ events. The PRD doesn't specify whether the 100k target is a steady-state performance guarantee or a one-time performance test at 100k rows. Combined filters across event_type + actor + target_user + date_range need composite indexes; filter cardinality matters. Also note contradiction with "append-only" requirement — append-only tables often have different (worse) query performance on typical RDBMS because they lack indexes or use write-optimized storage.

---

### M-8: "Reset invitation" invalidates previous link — could be used to deny a valid invitation

**Section:** FR-12

**Quoted phrases:**
- *"Director can resend an invitation, which generates a new link and invalidates the previous one"* (FR-12)

**Analysis:** A Director can invalidate another Director's invitation by using the "resend" feature. If Director A sends an invitation to User X, and Director B "resends" that same invitation (generating a new link and invalidating Director A's), Director A's invitation silently stops working. This is a potential Denial-of-Service scenario within a Director team that the PRD doesn't acknowledge. A notification to the original inviting Director when their invitation is superseded would be prudent.

---

### M-9: No mention of rate limits on email notifications to individual users

**Section:** FR-16

**Analysis:** If a Director repeatedly suspends and reactivates a user (accidentally or maliciously), that user receives an email each time. There's no rate limit or cooldown on notification emails to individual users. Combined with the inability to opt out, a targeted user could be spammed with suspension/reactivation notifications.

---

## Low Findings

### L-1: Inconsistent spelling — "organisation" vs "organization"

**Sections:** 2.3 (uses "organisation"), Section 3 (Glossary — uses both)

**Quoted phrases:**
- *"Each organisation is managed independently"* (Section 2.3) vs consistent "organisation" elsewhere

---

### L-2: Explicit confirmation for permanent deletion is in "Constraints and Guardrails" but not an FR

**Section:** 11 (Constraints and Guardrails)

**Quoted phrases:**
- *"Permanent deletion requires explicit confirmation (typing the target user's email)"*

**Analysis:** For an irreversible, destructive action, this UX safeguard should be a formal FR with testable consequences (including edge cases like: what if the user's email is very long? Is it case-sensitive? Unicode-safe?). Being in an unstructured section means it may be overlooked during QA test case generation.

---

### L-3: No mention of TLS/transport security for link URLs

**Sections:** 4.1, 5

**Analysis:** Activation and invitation links contain sensitive tokens. If the application doesn't enforce HTTPS, links can be intercepted in transit. This is basic OWASP but not mentioned.

---

### L-4: No specification of what "filterable" and "sortable" mean for the user list

**Section:** FR-4

**Quoted phrases:**
- *"Columns are sortable"* (FR-4)
- *"a paginated, filterable list of all users"* (FR-4)

**Analysis:** Which columns? Any-click sorting or pre-defined sort? Single-column or multi-column? What filters — text search, dropdown, date range? Can filter by "suspended" status and "role" simultaneously? These are standard UI questions with non-trivial backend implications (query optimization, coverage indexes).

---

### L-5: No mention of automated testing strategy for security-critical flows

**Section:** Entire document

**Analysis:** For a system where security is paramount (cryptographic tokens, rate limiting, 2FA, immutable audit logs), there's no mention of security testing approach — penetration testing, auth fuzzing, token replay testing, race condition testing for Last-Director Protection. The PRD includes testable consequences but no guidance on how to test the security properties specifically.

---

## Overall Assessment

**This PRD is a good conversation starter but not a buildable specification.**

It correctly identifies a real pain point (hardcoded config-file roles) and proposes a sensible high-level architecture (Director-invited registration with 2FA and audit). The glossary is solid. The open questions acknowledge some gaps.

However, **the document has a systematic pattern of specifying UI/frontend behavior while hand-waving the backend mechanisms that make it work.** The five critical findings share a common root cause: the PRD describes *what should happen* without specifying *how it must work* at the data and infrastructure level. An engineering team starting implementation from this document will discover foundational ambiguities at every layer.

**Top 3 recommendations before estimation:**

1. **Resolve C-2 (deletion semantics) and add a data model** — This is the single highest-impact change. A one-page entity-relationship diagram for User, Organisation, Role, Invitation, Token, and AuditEvent will force resolution of the deletion/audit contradiction and provide a shared foundation for estimation.

2. **Define the Director 2FA recovery path** (C-1) — Even if the solution is "Director contacts Vendor support, Vendor verifies identity and resets 2FA enrollment," it needs to be specified. The current "open question" + "deferred to v2" = guaranteed support escalations.

3. **Specify the session management strategy** (C-3) — The suspension feature cannot realistically meet its SLA without this. Choose: short-lived JWT with blacklist, server-side sessions, or adjust the SLA. Document the choice and its architectural implications.

---

## Finding Summary

| Severity | Count | Key Themes |
|----------|-------|------------|
| Critical | 5 | 2FA recovery, deletion semantics, session termination, Vendor Backstage scope, no data model |
| High | 8 | Rate limiting, email delivery, password policy, migration, notification spam, audit immutability, Safety Net latency, poor metric |
| Medium | 9 | Config mechanism, CSRF, idempotency, purge risk, password reset, tautological metric, audit perf, invitation DoS, notification rate limits |
| Low | 5 | Spelling, missing FR for confirmation, TLS, unspecified filtering, test strategy |
| **Total** | **27** | |
