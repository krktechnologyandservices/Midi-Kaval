# PRD Quality Review — Role Management & Registration System for Midi-Kaval

## Overall verdict

This PRD is a well-structured, decision-rich capability spec for a B2B role management system. Its strengths are decision-readiness, scope honesty, and downstream usability — every FR has testable consequences, the Glossary is consistent, and the decision log anchors the key trade-offs. The main risks are in strategic coherence (success metrics don't fully validate the core thesis that Director-owned capability replaces vendor-dependent config) and done-ness clarity (some consequences use subjective or ambiguous language that will create rework during story creation). Overall, this PRD is solid and buildable; the gaps are refinements, not structural defects.

## Decision-readiness — strong

Decisions are stated directly, not buried. The activation-link bootstrap (FR-1), Director-propagation model (FR-5 inviting other Directors), Last-Director Protection (FR-9), and the Vendor Safety Net (FR-3) are each presented as settled choices with explicit rationales. The decision log (.decision-log.md) provides 10 recorded decisions, each with a rationale — this is unusually thorough for a draft PRD.

Trade-offs are surfaced honestly. The double-confirmation flow (FR-11) is acknowledged as adding friction; the counter-metric SM-C1 explicitly warns against minimizing invite time at security's expense. The 2FA mandate (FR-10) similarly accepts a UX cost for risk mitigation. Open Questions (section 9) are genuinely open — Q4 about 2FA device recovery and Q5 about anonymise vs. full delete are unresolved design tensions, not rhetorical.

### Findings

- **[low]** Open Question 2 ("Should the Vendor backstage support multiple administrators?") is relatively low-stakes and could be tagged `[NOTE FOR PM]` rather than left open, since it doesn't block anything in the current scope. But this is minor — keeping it open is also defensible.

## Substance over theater — strong

No persona theater. The three personas (Vendor, Director, End User) each serve distinct purposes across the FRs — Vendor owns bootstrap, Director owns lifecycle management, End User is the target of invitations. No persona appears without driving decisions.

No innovation theater. The PRD positions this as a replacement for hardcoded config files — an honest "modernization" claim, not novelty.

No NFR theater. The Security NFRs name specific mechanisms (HMAC/asymmetric signature, RFC 6238, append-only audit log). The Performance NFRs give concrete thresholds with p95 targets and user/event counts. The Availability section is thinner — it leans on the main app's 99.9% SLA via `[ASSUMPTION: 99.9%]` — but is not boilerplate.

No vision theater. The Vision statement (section 1) is specific to this PRD's problem — replacing hardcoded config with a Director-owned model — and would not survive transplant into another PRD.

### Findings

- None.

## Strategic coherence — adequate

The thesis is present in the Vision and throughout: replace vendor-dependent config operations with a self-service, Director-owned model. The features build a coherent arc from bootstrap (Vendor Backstage) through ongoing management (Dashboard, Invitation Flow, Audit Trail).

The MVP scope kind is capability/platform — appropriate for a role management system within a brownfield B2B app. The inclusion of audit (FR-13, FR-14) and notifications (FR-15, FR-16) in MVP shows a systems-thinking view of what "done" means for a user lifecycle feature.

### Findings

- **[medium]** Success Metrics do not directly validate the strategic thesis. SM-1 measures operational velocity of activation; SM-2 measures absence of support tickets. Neither measures the core bet: "Director-owned capability replaces vendor-dependent config operations." A metric such as "percentage of user management actions completed by Directors without vendor intervention" or "reduction in vendor ops tickets related to user lifecycle" would tie to the thesis more directly. SM-3 and SM-4 are secondary and operational. This does not block build-readiness, but it means the PRD cannot prove its own hypothesis post-launch without additional instrumentation.

- **[low]** No explicit thesis statement. The Vision section (§1) functions as one, but a sentence like "This PRD bets that shifting user lifecycle management to Directors will reduce vendor operational overhead while maintaining security" would give reviewers and downstream consumers a single anchor to test decisions against.

## Done-ness clarity — adequate

The structural choice of "Consequences (testable)" under each FR is excellent. Every FR has at least one testable condition, and most have 3–5. This will give story creation a strong starting point.

### Findings

- **[medium]** Several "Consequences (testable)" use subjective or ambiguous language:
  - FR-2: "Expired or consumed token returns a **clear error page**" (§ First Director registration) — "clear" is subjective. Specify content (e.g., "page displays 'This link has expired or already been used' with a prompt to contact their Director").
  - FR-4: "Columns are **sortable**" (§ View all users) — which columns? All columns? Name, email, role, status, creation date? A test cannot pass without this.
  - FR-8: "Deletion requires explicit confirmation **(e.g., type the user's email to confirm)**" — the "e.g." introduces ambiguity. Is typing the email the *requirement* or just an *example*? If this is the mechanism, remove "e.g.".
  - FR-10: "Unenrolled Director sees a **prompt to set up 2FA**" — "prompt" is underspecified. Is it a modal? A banner? Does it block the attempted action?

- **[low]** No testable consequence for the "Audit events are append-only" requirement (FR-13). The bullet "Audit events are append-only; no update or delete" is stated as a constraint but not given a testable consequence about what happens if an update/delete is attempted (e.g., "API returns 405 on update/delete requests to the audit event table").

- **[low]** "Password meets organisation-configured complexity policy" (FR-2) — if the policy is not yet defined (see Open Question 1), this cannot be tested. Tag with `[ASSUMPTION: defined in organisation settings]` or resolve the Open Question before story creation.

## Scope honesty — strong

Non-Goals section (§6) lists 7 explicit items with no ambiguity. Out-of-Scope-for-MVP (§7.2) lists 7 deferred items, each with a deferral target ("v2," "Epic 7"), and includes one `[NOTE FOR PM]` callout on 2FA backup codes — an honest tension between scope and real-world risk.

`[ASSUMPTION]` tags appear inline in 7 FRs, and the Assumptions Index (§10) lists 8 items with AS- prefixes cross-referencing back to FR numbers.

Open Questions (§9) are substantive and do not disguise answers as questions.

### Findings

- **[medium]** Tension between FR-10's assumption and Out-of-Scope decision. FR-10's Consequences state `[ASSUMPTION: SMS/email backup codes as secondary option]` (AS-4). But §7.2*Out of Scope for MVP* explicitly says: "SMS/email backup codes for 2FA — Only authenticator app (TOTP) in v1. [NOTE FOR PM: Deferred due to scope…]" The Assumption Index (AS-4) follows the FR-10 assumption, not the Out-of-Scope decision. These are in direct conflict — an engineer reading AS-4 would build backup codes; an engineer reading the Out-of-Scope section would not. This must be resolved before story creation. Either AS-4 should be removed and "TOTP only" stated in FR-10, or the Out-of-Scope decision should be walked back.

## Downstream usability — strong

Glossary is comprehensive (16 terms). All domain nouns used in FRs appear there. FR IDs are contiguous and unique (FR-1 through FR-16). SM IDs are present (SM-1 through SM-4, plus SM-C1). UJs each have a named protagonist. Cross-references use FR IDs rather than positional language.

### Findings

- None. Downstream consumers can source-extract cleanly.

## Shape fit — strong

This is a B2B capability spec for a brownfield application. The shape matches the product: UJs are present but lightweight (appropriate for a capability-focused spec); SMs are operational (appropriate for an internal-facing feature); the brownfield context is acknowledged upfront ("hardcoded roles and accounts in configuration files" in §1).

### Findings

- None.

## Mechanical notes

- **[medium]** **Assumptions Index roundtrip is broken.** Inline assumptions use `[ASSUMPTION: …]` without the AS- prefix (e.g., FR-3: `[ASSUMPTION: 1 hour]`; FR-6: `[ASSUMPTION: 5 minutes]`). The index (§10) uses AS-1 through AS-8. A downstream consumer must manually match inline text to index IDs. This creates a brittle step where a story creator might miss the roundtrip. Fix: tag each inline assumption with its AS-ID, e.g., `[ASSUMPTION: 1 hour]` → `[ASSUMPTION AS-1: 1 hour]`.

- **[low]** **Glossary entries for "Last-Director Protection" and "Vendor Safety Net" reference FR-9 and FR-3 respectively.** This is acceptable and useful for technical audiences, but Glossary terms referencing FR IDs creates a cross-document dependency — if the PRD is later revised and FR IDs shift, the Glossary becomes stale. Not actionable for a draft; flag for PM to review.

- **[low]** **No open questions tagged with FR dependencies.** Open Questions (§9) are listed as free text. Tagging each with the FR(s) it blocks (e.g., "Q1 blocks FR-2 password validation") would improve traceability for story creation.
