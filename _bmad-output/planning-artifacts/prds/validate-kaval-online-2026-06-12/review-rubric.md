# PRD Quality Review — Kaval Online (Kaaval_Online_PRD.docx.pdf)

**Artifact validated:** `docs/Kaaval_Online_PRD.docx.pdf` (source PRD; no formal `prd.md` exists yet)

## Overall verdict

The PDF PRD delivers a credible domain picture—roles, case lifecycle, and core operational modules are recognizable and aligned with a real NGO field-work problem. It is **not yet decision-ready for build**: scope mixes v1 essentials with aspirational AI, integrations, and portals without MVP cuts; internal contradictions (Excel sync vs cloud-only) block architecture; and problem-statement pains (duplicate registration, handover loss) are not carried into testable functional requirements. Downstream workflows (UX, architecture, epics) cannot source-extract cleanly without FR IDs, glossary, journeys, or acceptance consequences.

## Decision-readiness — thin

§4 Scope and §5 Functional Requirements list capabilities but rarely state decisions. Aspirational items—“Public-facing portal for beneficiaries - If possible,” “Third-party integrations,” dual AI bullets—read as a wishlist, not phased commitments. No Open Questions section surfaces the Excel-import vs no-sync tension or AI phasing. A stakeholder cannot sign off on v1 boundaries from this document alone.

### Findings
- **[critical]** Scope lacks MVP decision boundary (§4.1) — §4.1 bundles cloud core, AI, beneficiary portal, budget, police integrations, and explicit outs (biometric) in one undifferentiated list. *Fix:* Split §6 MVP In/Out Scope with explicit v1 vs deferred and one-line rationale per deferral.
- **[high]** Excel synchronisation contradiction (§1 Executive Summary vs §4.1) — Executive Summary states “no manual Excel synchronisation” and real-time cloud; §4.1 also lists “Excel file import for sync.” *Fix:* Decide migration-only import vs ongoing sync; state decision in PRD.
- **[high]** AI capabilities undecided (§4.1) — “suggest appropriate schemes” and “Generate possible interventions… using AI” have no acceptance criteria, data sources, or human-in-the-loop rules. *Fix:* Define v1 AI scope or mark non-goals; reference governance (advisory-only, org data vs external).

## Substance over theater — adequate

Core case management (§5.2–5.7) and master data (§5.10) carry real domain substance—six-stage lifecycle, field catalog, court and travel workflows. Weak areas are AI and integrations: single-line bullets without mechanism or success tests read as innovation theater. Executive summary problem statement (§2) is specific and earned, not generic vision filler.

### Findings
- **[medium]** AI bullets are theater without substance (§4.1) — No inputs, outputs, guardrails, or failure modes for scheme/intervention AI. *Fix:* Expand or defer to non-goals with pointer to future spec.
- **[low]** Dashboard metrics listed without thresholds (§5.9) — “Real time” and chart types named but no performance or freshness bounds. *Fix:* Add NFR for dashboard data latency (e.g. supervisor sees field update within N seconds).

## Strategic coherence — adequate

Thesis is clear: replace offline Excel/WhatsApp with cloud-first real-time case management across web (supervisors) and mobile (field). Features largely serve that arc. Coherence breaks where scope expands to budget, beneficiary portal, and external integrations without linking to the core coordination pain. Success is implied (no manual Excel) but not measured.

### Findings
- **[high]** Problem → solution gap on duplicate cases (§2.1 vs §5.2) — §2.1 names duplicate registration as a top pain; §5.2 search/filter exists but no duplicate-prevention or merge requirement. *Fix:* Add FR for crime/ST uniqueness enforcement and Coordinator merge workflow.
- **[medium]** No success metrics section — Activity implied (reports, dashboards) but no measurable outcomes (e.g. coordinator visibility without weekly Excel export). *Fix:* Add §7 Success Metrics tied to problem statement.

## Done-ness clarity — thin

Requirements are feature lists, not FRs with testable consequences. Court sitting (§5.6) specifies reminder timing (24h push, email) — good. Most other areas lack verifiable “done” conditions. Visit scheduling, offline behavior, notification delivery guarantees, and report export correctness are unspecified.

### Findings
- **[critical]** No testable acceptance per requirement (§5 overall) — e.g. §5.3 “Visits auto-suggested based on… GPS proximity” has no behavior when GPS unverified; §5.8 SMS/WhatsApp in scope at §4.1 but not specified in §5.8 notification list. *Fix:* Restructure as numbered FRs each with Consequences bullets.
- **[high]** Court miss handling incomplete (§5.6) — Reminders and scheduled vs attended reports exist; no escalation when sitting date passes with status still Upcoming. *Fix:* Add FR for post-date miss flag and Coordinator alert.
- **[high]** Case handover not specified (§2.1) — Information loss on transfer is a stated problem; no FR for handoff summary or transfer workflow. *Fix:* Add handoff/transfer requirements.
- **[medium]** Travel claim mandatory receipts (§5.7) — Stated for bus/auto/petrol; no consequence for submit without receipt. *Fix:* Add validation rule FR.

## Scope honesty — broken

No dedicated Non-Goals or MVP Out of Scope section. §4.1 interleaves in-scope features with negations (“Native biometric login… used instead,” “Excel file import”) without clarifying intent. Deferred items (portal, budget, integrations) use weak qualifiers (“If possible”) rather than explicit deferral. Reader must infer omissions.

### Findings
- **[critical]** Missing explicit non-goals (document) — Biometric out is buried in scope list; third-party integrations listed as in-scope and aspirational simultaneously. *Fix:* §5 Non-Goals + §6.2 MVP Out of Scope.
- **[high]** Connectivity model ambiguous (§1) — “Always connected” vs field reality (patchy rural signal) not addressed; brainstorm/SPEC flag offline trust as open question. *Fix:* State online-only vs offline-capable visit capture decision.

## Downstream usability — broken

No glossary (CCL, ICP, PMA, ST Number, Legends used without definition). No FR/UJ/SM IDs. No user journeys despite five roles and two surfaces. Cross-references impossible. UX and architecture skills cannot trace requirements.

### Findings
- **[critical]** No stable requirement IDs (§5) — Features numbered 5.1–5.10 but FRs not globally numbered. *Fix:* Adopt BMAD FR-1…FR-N pattern.
- **[high]** No user journeys (document) — Multi-stakeholder field + supervisor product needs named-protagonist UJs (Priya visit day, Ravi morning triage). *Fix:* Add §2.3 Key User Journeys.
- **[high]** No glossary (document) — Domain acronyms and role names need §3 Glossary. *Fix:* Define Case, Sitting, Legend, stage names, offence classifications.

## Shape fit — thin

Product is multi-stakeholder B2B with meaningful UX (web + React Native mobile)—shape expects journeys and role-specific flows. PRD is a flat functional catalog closer to an internal capability spec missing the narrative layer. Role table (§3) is appropriate; rest under-formalized for chain-top PRD feeding UX → architecture.

### Findings
- **[medium]** Shape mismatch for UX-heavy product — Five roles, two surfaces, field GPS, discreet capture scenarios absent. *Fix:* Journey-led sections or explicit UX handoff noting PRD is domain draft only.
- **[low]** §4.1 formatting mixes scope bullets — Some lines read as out-of-scope but sit under “In Scope.” *Fix:* Editorial pass separating in/out lists.

## Mechanical notes

- No YAML frontmatter, created/updated dates, or document purpose section.
- Crime Number and ST Number marked unique in §5.2 fields but duplicate-prevention behavior not specified—internal inconsistency.
- “CCLs” in AI bullet (§4.1) undefined—likely Case Workers or similar; glossary drift risk.
- Comparison to `_bmad-output/specs/spec-kaval-online/SPEC.md`: SPEC resolves 16 capabilities with success criteria and guardrails; PDF PRD missing CAP-3 duplicate prevention, CAP-8 court miss escalation, CAP-15 handoff, CAP-13 Field Memory AI nuance.
