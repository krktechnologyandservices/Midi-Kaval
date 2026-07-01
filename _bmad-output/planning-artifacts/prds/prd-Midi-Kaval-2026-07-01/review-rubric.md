# PRD Quality Review — Two-Factor Authentication — Universal Enrollment & Administration

## Overall verdict

**Conditional pass.** This is a well-structured, substantive internal-tool PRD with clear scope demarcation, specific NFRs, and strong brownfield awareness. The problem statement is grounded in real operational pain (Vendors using Swagger for 2FA enrollment), and the phased approach makes the delivery path obvious. However, it falls short on implementation readiness in three areas: open questions lack assigned owners (all "TBD"), several FRs are underspecified (delegation UX, edge cases around lockout/recovery), and assumptions are entirely implicit with no Assumptions Index. These gaps are fixable and do not undermine the PRD's strategic thesis, but they must be closed before story creation begins.

---

## Decision-readiness — adequate

The PRD communicates its decisions clearly — the scope table with ✅/❌ markings, the explicit deferral of Phases 3 and 4 to future scope, and the named API endpoints all leave little ambiguity about *what* will be built. The Open Questions section contains genuinely unresolved tensions (e.g., OQ-4 on TOTP lockout policy, OQ-5 on whether email OTP should count as 2FA). However, the PRD does not surface trade-offs as named decisions with their rejected alternatives — the reasoning for some prioritization choices is left implicit.

### Findings

- **high** [Open Questions lack owners] (§ 6 Open Questions) — All five open questions list "TBD" as owner. Without an owner and a resolution target, these questions risk persisting into implementation and causing churn. *Fix:* Assign an owner and a resolution deadline for each OQ, or explicitly state which OQs are acceptable to carry into build as spikes.

- **medium** [Trade-off reasoning is implicit] (§ 2 Problem Statement / § 4 Phases) — The PRD prioritizes Vendor enrollment (Phase 1) before Director management tools (Phase 2), and defers Universal Enrollment (Phase 4). The *why* behind these prioritizations is never stated as a deliberate trade-off. *Fix:* Add a brief trade-off note for each major priority decision — e.g., "Vendor enrollment first because current Swagger-only flow is a compliance risk."

- **low** [No explicit deferral cost] (§ 4 Scope table) — What is the cost of deferring Universal Enrollment (Phase 4)? Field workers remain on email OTP only, which § 2.3 calls out as a security risk. The PRD should name the residual risk of deferral. *Fix:* Add a "Deferred Risk" column or note to the scope table.

---

## Substance over theater — strong

This is the PRD's strongest dimension. Personas in the User Journeys (Meena, Raj, Priya, Rahul) drive real design decisions — the Meena journey directly motivates the Vendor settings page, and the Priya journey defines the reset/bypass flow. NFRs are refreshingly specific: rate limits (5 attempts/min per user, 2 bypass codes/hour per Director), performance thresholds (<50ms for 2FA status, <100ms for backup code verification), and UX requirements (200×200px QR minimum, keyboard-navigable). There is no boilerplate "system must be scalable/secure/reliable" language. The security requirements are concrete (encrypted-at-rest, SHA-256 hashing, rate limiting). The success metrics are operational and tied to specific 2FA behaviors rather than vanity metrics.

### Findings

- No critical or high findings. This dimension is solid.

- **low** [Future Scope references novel capabilities without grounding] (§ 7 Phase 4 — "Passkeys / WebAuthn support") — Mentioning WebAuthn as future scope is fine, but it's not earned by any discovery or user research cited. *Fix:* Minor — either remove as ungrounded speculation or add a note that it's a directional aspiration subject to research.

---

## Strategic coherence — adequate

The PRD has a clear, defensible thesis: *"2FA is currently Director-only; Vendors are forced to use raw API calls; this extends 2FA consistently to all roles with management tooling."* The feature set follows from this thesis — Phase 1 closes the Vendor enrollment gap, Phase 2 gives Directors the controls they need. The success metrics validate the thesis (Vendor enrollment rate, bypass code usage, failed TOTP attempts). However, the PRD does not name any counter-metrics, and the relationship between the three scope phases and the strategic thesis could be tighter.

### Findings

- **medium** [No counter-metrics] (§ 8 Success Metrics) — All five metrics are one-directional (lower is better). There are no counter-metrics to detect negative side effects — e.g., increased Director support burden from resets, user friction from the mandate toggle, or helpdesk tickets from lockouts. *Fix:* Add 1–2 counter-metrics such as "Director-initiated resets per month" or "time-to-complete for 2FA enrollment flow (p95)."

- **low** [Phased scope vs. strategic narrative] (§ 4 vs § 7) — The PRD does not explicitly connect Phases 3 and 4 back to the thesis statement. Phase 3 (Vendor Global Admin) and Phase 4 (Universal Enrollment) are described as future scope but their strategic motivation is not stated. *Fix:* Add a sentence per future phase explaining why it's deferred and what signal would trigger re-prioritization.

---

## Done-ness clarity — adequate

For an internal tool PRD at this stage, the FRs are reasonably testable. FR-1 has an explicit acceptance criterion ("A Vendor with 0 prior knowledge can complete 2FA enrollment entirely through the web UI without touching Swagger"). API endpoints are named with HTTP methods and paths. The backup codes data model is specified in SQL. However, several FRs underspecify behavior, and edge cases are inconsistently addressed.

### Findings

- **high** [FR-2.7 Delegation is underspecified] (§ 4 FR-2.7) — "Allow Coordinators to reset 2FA for field workers" is described in three bullet points. Missing: How does the Director toggle this? Is it a checkbox? A role-assignment UI? What confirmation/audit flow exists? How is delegation revoked? What does the Coordinator UI look like before vs. after delegation? *Fix:* Expand FR-2.7 with an acceptance criterion, a brief UI sketch, and the revocation mechanism.

- **medium** [FR-2.1 action menu is ambiguous] (§ 4 FR-2.1) — "Clicking the cell shows action menu: 'Reset 2FA' / 'Send Reminder'" is underspecified. Is this a dropdown on hover? A context menu on click? A dialog on cell click? Does it apply to both ✓ and ✗ states? *Fix:* Specify the interaction pattern (e.g., "Click opens a MatMenu with contextual actions based on enrollment state").

- **medium** [FR-2.8 in-app notification is vague] (§ 4 FR-2.8) — "Director receives in-app notification" does not specify where (bell icon? toast? email supplement?), how it's delivered, or whether it's dismissible. *Fix:* Link to the existing in-app notification system and specify delivery channel.

- **low** [OQ-4 TOTP lockout policy is an implementation blocker] (§ 6 OQ-4) — The question about lockout after consecutive failures is presented as open, but rate limiting (NFR-1.4, NFR-1.5) is already specified. These should be reconciled. *Fix:* Either resolve OQ-4 inline (the PRD already names the proposed resolution) or reference the NFR rate limits directly.

---

## Scope honesty — adequate

The scope table is a strong artifact — it explicitly marks what is in Phase 1, Phase 2, and deferred to future. The Future Scope section (§ 7) names what's coming but won't be built now. The Open Questions section is honest and does not pretend to have answers it doesn't. However, the PRD is missing several scope-honesty mechanisms that an internal/production PRD at this stakes level should have.

### Findings

- **medium** [No Assumptions Index] (§ 1–9) — The PRD contains zero `[ASSUMPTION]` tags. Several implicit assumptions exist: that the existing `totp_secret` column encryption is adequate; that `OtpChallengeStore` is a reliable template for Redis-backed bypass codes; that Vendors have smartphones capable of scanning QR codes; that email delivery infrastructure is sufficiently reliable for reminders. *Fix:* Surface these as inline `[ASSUMPTION: ...]` tags and collect them in an Assumptions Index at the end of the document.

- **medium** [No explicit Non-Goals section] (§ 1–9) — While the scope table and Future Scope section provide partial coverage, there is no dedicated Non-Goals section. Examples of useful non-goals: React Native enrollment (Phase 4), WebAuthn/Passkeys, organization-wide bulk campaigns, global (cross-org) 2FA audit. *Fix:* Add a Non-Goals section that lists what is explicitly out of scope for this delivery.

- **low** [TBD owners on all OQs] (§ 6) — Same finding as Decision-readiness, but treated here as a scope-honesty signal: unresolved ownership on questions that affect implementation scope is a risk that should be flagged. *Fix:* See Decision-readiness finding.

---

## Downstream usability — adequate

This is a chain-top PRD (it feeds UX design, architecture, and story creation), so downstream usability matters. The PRD performs well on structure: FR/UJ IDs are contiguous, API endpoints are tabulated with full paths and methods, the backup_codes data model is specified as executable SQL, and the Dependencies section accurately distinguishes existing components from new migration work. Cross-references within the document resolve cleanly. UJ protagonists are named and carry context inline.

### Findings

- **medium** [No glossary] (§ 1–9) — Domain terms are used consistently (TOTP, backup code, bypass code, email OTP, mandate, delegation), but never defined. For a team that may include newer developers or cross-functional members, the distinction between "bypass code" (Director-generated, 30-min TTL) and "backup code" (user-generated at enrollment, persistent until used) is critical and should be canonicalized. *Fix:* Add a Glossary section defining: TOTP, backup code, bypass code, email OTP, mandate, delegation, and enrollment.

- **low** [FR-4 endpoint table could include request/response shapes] (§ 4 FR-4) — The API endpoint table is useful but omits request bodies and response schemas (except for the `GET /auth/2fa-status` response). *Fix:* Add a "Request Body" and "Response" column or link to existing OpenAPI specs for the reused endpoints.

---

## Shape fit — strong

The PRD's shape matches its content and stakes well. This is an internal/production tool with multiple operator roles (Vendor, Director, Coordinator, field worker). The capability-spec-with-UJs hybrid is appropriate — the UJs earn their place by distinguishing role-specific flows that could otherwise be conflated. The PRD handles brownfield gracefully: it identifies existing components (`Enroll2FAComponent`, `Staff Management page`, `Organisation Settings page`), existing endpoints (`POST /auth/enroll-2fa`, `POST /auth/verify-enroll-2fa`), and distinguishes genuinely new work (backup_codes table, Redis bypass code storage). It is not over-formalized — no unnecessary sections that exist "because the template said so."

### Findings

- **low** [FR-2.7 delegation warrants its own UJ] (§ 3 vs § 4) — The delegation feature (FR-2.7) is described in functional requirements but has no corresponding User Journey. Given that it introduces a new permission boundary and UI change for a different role (Coordinator), a dedicated journey would clarify the end-to-end flow. *Fix:* Add a UJ-5 for Director-to-Coordinator delegation, or expand UJ-4 to cover the delegation toggle itself.

---

## Mechanical notes

- **ID continuity:** All FR IDs (FR-1.x through FR-5.x) and NFR IDs (NFR-1.x through NFR-4.x) are contiguous with no gaps or duplicates. UJ-1 through UJ-4 are similarly clean. No broken internal cross-references detected.
- **Protagonist naming:** All four UJs have named protagonists (Meena, Raj, Priya, Rahul) who carry context. Good.
- **Glossary drift:** Minor inconsistency — the PRD uses "two-factor authentication" (title/§ 1), "2FA" (body), and "2 Factor Auth" (implied in column headers) interchangeably. All are clearly the same concept, but a note at first use ("hereinafter '2FA'") would clarify.
- **Assumptions roundtrip:** No inline `[ASSUMPTION]` tags exist. An Assumptions Index cannot be validated. This should be addressed (see Scope Honesty findings).
- **Required sections present:** All major sections expected for an internal/production PRD at this stage are present — Executive Summary, Problem Statement, User Journeys, Functional Requirements (with IDs), Non-Functional Requirements, Open Questions, Future Scope, Success Metrics, and Dependencies.
