# Validation Report — Role Management & Registration System for Midi-Kaval

- **PRD:** `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md`
- **Rubric:** `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-06-23T20:00
- **Grade:** Fair

## Overall verdict

The PRD is a well-structured, decision-rich capability spec. Its strengths are decision-readiness, scope honesty, and downstream usability — every FR has testable consequences, the Glossary is consistent, and the decision log anchors key trade-offs. However, the adversarial review surfaced critical gaps in data model, session management, and 2FA recovery that make this a good conversation starter rather than a buildable specification. The PRD systematically specifies UI/frontend behavior while hand-waving the backend mechanisms that make it work.

## Dimension verdicts

| Dimension | Verdict |
|---|---|
| Decision-readiness | strong |
| Substance over theater | strong |
| Strategic coherence | adequate |
| Done-ness clarity | adequate |
| Scope honesty | strong |
| Downstream usability | strong |
| Shape fit | strong |

## Findings by severity

### Critical (5)

**[Adversarial] — No Director 2FA recovery path (§ FR-10, OQ #4, §7.2)**
2FA is mandated but SMS/email backup codes are deferred to v2, and the recovery process is an open question. Every 2FA loss becomes a support escalation, directly contradicting the PRD's goal of reducing vendor dependency.

**[Adversarial] — Permanent deletion semantics are contradictory (§ FR-8, FR-13, AS-3)**
FR-8 says "removed or anonymised", FR-13 requires identifying deleted users in audit events, and AS-3 says anonymises. These are mutually exclusive and determine the entire data model (schema, GDPR, UI wording, query complexity).

**[Adversarial] — Session termination for suspension has no feasible mechanism (§ FR-6, AS-2)**
5-minute SLA for terminating sessions is specified but no session management strategy is defined. With JWT tokens, there is no server-side session to revoke. This could force a re-architecture of the auth layer.

**[Adversarial] — Vendor Backstage is a second undefined application (§ FR-1, FR-3)**
Treated as a minor requirement but requires its own auth, user management, UI, authorization, deployment model — roughly half the Director dashboard scope, occupying ~10% of the PRD.

**[Adversarial] — No data model defined (entire document)**
No entity-relationship definitions, schema, or mapping to the existing Midi-Kaval data model. Every team member will make different assumptions. An engineering lead cannot produce a meaningful estimate.

### High (8)

**[Adversarial] — Rate limiting underspecified (§5 Security NFR)**
Single sentence with no thresholds, burst handling, endpoint differentiation, or HTTP response code.

**[Adversarial] — Email delivery failure handling disconnected from FRs (§5, FR-1, FR-3, FR-5, etc.)**
Every FR assumes email delivery always works. No retry strategy per email type, no delivery status visibility, no bounce handling, no resend for activation links.

**[Adversarial] — Password complexity is a dangling dependency (§ FR-2, OQ #1)**
FR-2 requires "organisation-configured" policy but no configuration mechanism exists or is specified. Limbo between system-wide default and per-org config.

**[Adversarial] — No existing-user migration or cutover strategy (entire document)**
Hardcoded accounts exist but the PRD describes a greenfield replacement with no migration path, coexistence plan, or rollback.

**[Adversarial] — Audit broadcast creates notification spam (§ FR-15)**
N Directors × M actions = N*M emails with no digest, no rate limiting, no opt-out. Notification fatigue guaranteed.

**[Adversarial] — "Immutable audit log" not defined (§ FR-13, Glossary)**
Implementation spectrum from plain DB table to cryptographic chain has radically different security, performance, and query properties. Tension between immutability and queryability (FR-14) not acknowledged.

**[Adversarial] — 1-hour Safety Net detection too slow (§ FR-3, AS-1)**
Last-Director loss means unmanaged org for up to 1 hour. Event-driven detection not considered.

**[Adversarial] — SM-2 is poorly defined (§8, SM-2)**
"Zero support tickets" is not a meaningful metric — conflates product quality, documentation, user patience, and ticket classification. Incentivizes wrong behavior.

### Medium (9)

**[Rubric] — Success Metrics don't validate strategic thesis (§8)**
No metric tracks "Director-owned vs vendor-dependent" reduction. Core bet untestable.

**[Rubric] — Several Consequences use subjective language (FR-2, FR-4, FR-8, FR-10)**
"Clear error page", "columns are sortable", "e.g." examples — ambiguous for test creation.

**[Rubric] — Contradiction between FR-10 assumption and Out-of-Scope decision (AS-4 vs §7.2)**
FR-10 assumes SMS backup codes as secondary option; §7.2 defers them to v2. Direct conflict.

**[Rubric] — Assumptions Index roundtrip broken**
Inline `[ASSUMPTION: ...]` tags lack AS- prefixes. Manual matching required.

**[Adversarial] — "Configurable duration" without configuration mechanism (FR-1, FR-5, FR-11)**
Env var? DB? UI? Different implementation costs.

**[Adversarial] — No CSRF protection mentioned (§5)**
Destructive browser actions need CSRF tokens.

**[Adversarial] — No idempotency for invitation creation (FR-5)**
Double-click can create duplicate invitations.

**[Adversarial] — No protection against single-Director purge of other Directors (FR-9)**
Only the last Director is protected; a rogue Director can deactivate all others then themselves.

**[Adversarial] — Counter-metric SM-C1 is tautological (§8, SM-C1)**
"Do not optimize below 7 days" when link already expires in 7 days. Circular.

### Low (8)

- **Inconsistent spelling** — "organisation" vs "organization" (Glossary)
- **Permanent delete confirmation should be an FR** — in unstructured §11 instead of testable FR
- **No TLS/transport security mentioned** for link URLs
- **"Filterable" and "sortable" unspecified** (FR-4) — which columns, which filter types
- **No automated testing strategy** for security-critical flows
- Open Question 2 low-stakes (multiple Vendor admins)
- No explicit thesis statement in §1
- No testable consequence for "append-only" audit constraint

## Mechanical notes

- Glossary drift: "organisation" vs "organization" used inconsistently in §2.3 vs Glossary
- FR/ID continuity: contiguous FR-1 through FR-16, no gaps
- Assumptions Index: inline tags lack AS- prefix (e.g., `[ASSUMPTION: 1 hour]` instead of `[ASSUMPTION AS-1: 1 hour]`)
- No open questions tagged with FR dependencies

## Reviewer files

- `review-rubric.md`
- `review-adversarial-general.md`
