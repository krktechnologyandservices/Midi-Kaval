# Validation Report — Kaval Online

- **PRD:** `docs/Kaaval_Online_PRD.docx.pdf`
- **Rubric:** `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-06-12
- **Grade:** Poor

## Overall verdict

The source PRD captures a real NGO case-management domain—roles, lifecycle stages, visits, court, travel, and reporting are directionally sound and match the coordination pains in §2. It is **not build-ready**: scope honesty and downstream usability are broken (no MVP cuts, no FR IDs, no glossary, no journeys), and several problem-statement items (duplicate cases, handovers) never become testable requirements. Internal contradictions (Excel import vs no Excel sync) must be resolved before architecture. Adversarial review adds POCSO/minor-data privacy and “performance reporting” risks that the PRD does not address.

Cross-reference: `_bmad-output/specs/spec-kaval-online/SPEC.md` already closes many gaps (duplicate prevention, court-miss escalation, handoff, Field Memory AI guardrails)—an **Update** pass should reconcile the PDF into a formal `prd.md` using SPEC as change signal.

## Dimension verdicts

- Decision-readiness — thin
- Substance over theater — adequate
- Strategic coherence — adequate
- Done-ness clarity — thin
- Scope honesty — broken
- Downstream usability — broken
- Shape fit — thin

## Findings by severity

### Critical (5)

**[Scope honesty]** Scope lacks MVP decision boundary (§4.1)  
§4.1 mixes v1 core with AI, portal, budget, and integrations without phasing.  
*Fix:* Add §6 MVP In/Out Scope with explicit deferrals.

**[Done-ness]** No testable acceptance per requirement (§5)  
Feature bullets lack FR IDs and verifiable consequences.  
*Fix:* Restructure as FR-1…FR-N with Consequences per BMAD template.

**[Scope honesty]** Missing explicit non-goals (document)  
Omissions must be inferred; integrations and portal ambiguous.  
*Fix:* Dedicated Non-Goals + MVP Out of Scope sections.

**[Downstream usability]** No stable requirement IDs (§5)  
UX/architecture/epics cannot trace requirements.  
*Fix:* Global FR numbering and cross-refs.

**[Adversarial]** POCSO and minor data (§5.2 / offence types)  
Sensitive minor cases lack privacy/discreet-capture requirements.  
*Fix:* Security/privacy NFR section for PII and POCSO cases.

### High (8)

**[Decision-readiness]** Excel synchronisation contradiction (§1 vs §4.1)  
*Fix:* Decide migration-only import vs ongoing sync.

**[Decision-readiness]** AI capabilities undecided (§4.1)  
*Fix:* Define v1 AI scope, governance, and acceptance criteria.

**[Strategic coherence]** Duplicate cases in problem but not in FRs (§2.1 vs §5.2)  
*Fix:* Add duplicate alert + merge workflow FRs.

**[Done-ness]** Court miss escalation missing (§5.6)  
*Fix:* Post-date Upcoming → flag + Coordinator alert.

**[Done-ness]** Case handover not specified (§2.1)  
*Fix:* Handoff summary on transfer FR.

**[Downstream usability]** No user journeys (document)  
*Fix:* Named-protagonist UJs for field and supervisor flows.

**[Downstream usability]** No glossary (document)  
*Fix:* §3 Glossary for ICP, PMA, ST, Legends, roles.

**[Adversarial]** WhatsApp channel ambiguity (§2 vs §4.1)  
*Fix:* Clarify what the system replaces vs informal channels.

### Medium (5)

**[Substance]** AI bullets lack substance (§4.1)  
**[Strategic coherence]** No success metrics section  
**[Done-ness]** Travel receipt validation unspecified (§5.7)  
**[Scope honesty]** Connectivity / offline model ambiguous (§1)  
**[Shape fit]** Shape mismatch for UX-heavy product  

### Low (3)

**[Substance]** Dashboard latency thresholds missing (§5.9)  
**[Shape fit]** §4.1 in/out list formatting  
**[Adversarial]** PDF may be truncated export (6 pages)  

## Mechanical notes

- No frontmatter, assumptions index, or open questions section.
- “CCLs” in AI bullet undefined.
- SPEC companion artifacts absorb brainstorming guardrails not present in PDF.

## Reviewer files

- `review-rubric.md`
- `review-adversarial-general.md`
