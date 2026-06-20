---
stepsCompleted: [1, 2]
inputDocuments:
  - docs/Kaaval_Online_PRD.docx.pdf
session_topic: 'Kaval Online — cloud-first case management platform for social work teams (web + React Native mobile)'
session_goals: 'Surface innovative ideas beyond the PRD; explore implementation options for aspirational scope (AI, beneficiary portal, budget); identify risks, UX wins, and MVP priorities'
selected_approach: 'ai-recommended'
techniques_used: ['Role Playing', 'Cross-Pollination', 'Reverse Brainstorming']
ideas_generated: 17
context_file: 'docs/Kaaval_Online_PRD.docx.pdf'
---

# Brainstorming Session Results

**Facilitator:** Admin
**Date:** 2026-06-12

## Session Overview

**Topic:** Kaval Online — cloud-first case management platform for social work teams (web + React Native mobile)

**Goals:** Surface innovative ideas beyond the PRD; explore implementation options for aspirational scope (AI, beneficiary portal, budget); identify risks, UX wins, and MVP priorities

### Context Guidance

Loaded from `docs/Kaaval_Online_PRD.docx.pdf`. Key themes: real-time collaboration replacing Excel/WhatsApp; 5 roles (Project Director, Coordinator, Social Worker, Case Worker); 6-stage case lifecycle; field visits with GPS; interventions, court sittings, travel claims; dashboards and reporting; aspirational AI for government schemes and intervention suggestions; optional beneficiary portal and budget management.

### Session Setup

User selected **[2] AI-Recommended Techniques** for technique selection tailored to a detailed PRD with complex multi-stakeholder domain and ambiguous v1 scope.

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** Kaval Online PRD with focus on innovation, MVP clarity, and multi-stakeholder needs

**Recommended Techniques:**

- **Role Playing:** Ground ideas in the lived experience of Directors, Coordinators, Social Workers, Case Workers, and beneficiaries
- **Cross-Pollination:** Borrow proven patterns from healthcare, field service, and gov-tech domains
- **Reverse Brainstorming:** Surface failure modes and flip them into requirements and guardrails

**AI Rationale:** Complex regulated domain with five roles and aspirational AI scope — empathy first, then innovation, then risk refinement.

## Technique Execution — Role Playing (partial complete)

### Persona: Priya — Social Worker (field)

**Scenario:** Tuesday 7:30 AM, 3 visits + court Thursday, patchy connectivity in one area.

#### Ideas Generated

**[Field UX #1]**: Morning Command Strip
_Concept_: First screen is not a menu — it's a single scrollable "today strip": visit 1 with tap-to-navigate, overdue badge, court countdown, and one-tap "start visit" that works offline and syncs when signal returns.
_Novelty_: Replaces dashboard browsing with a time-ordered action queue; supervisor visibility without Priya opening reports.

**[Field UX #2]**: Offline Trust Card
_Concept_: Each case shows a tiny sync status chip ("saved locally 6:12 AM" / "synced to cloud") so Priya knows her notes won't vanish. Failed sync queues visibly, never silent.
_Novelty_: Addresses the #1 reason field workers revert to notebooks — fear of data loss, not missing features.

**[Field UX #3]**: Visit Handoff Whisper
_Concept_: If a case was transferred from another worker in the last 7 days, the top of case detail shows a 3-line "handoff whisper" — what the previous worker did, what's still open, next visit reason — not the full notes timeline.
_Novelty_: Directly attacks PRD pain of information loss on case transfer; compressed context for bus-stop reading.

**[Field UX #4]**: Proximity Day Pack
_Concept_: App auto-suggests grouping today's visits by GPS cluster with "reorder route" — but also flags "this case has no verified GPS yet" so Priya knows she'll need manual landmark entry.
_Novelty_: PRD mentions proximity grouping; adding the unverified-GPS warning prevents false confidence in rural/slum addresses.

**[AI #5]**: Field Memory AI (Organizational Experience Engine)
_Concept_: AI suggestions draw from anonymized patterns across the NGO's own history — which interventions worked for similar offence type + age + domicile + family type, what visit cadence preceded successful reintegration, which scheme combinations were approved before. Surfaces as "3 workers in your unit handled similar cases — here's what tended to work" with links to de-identified case summaries, not generic internet advice.
_Novelty_: PRD AI focuses on government schemes and intervention generation by law; this anchors AI in lived field outcomes within the organization — "same field previous experiences applied."

**[AI #6]**: Pre-Visit Experience Brief
_Concept_: Before Priya starts a visit, AI compiles a 30-second brief from unit history: "Similar petty theft, age 16, slum domicile — past cases often needed vocational referral before family mediation worked; average 4 visits to stage transition."
_Novelty_: Proactive coaching card, not a chatbot — distilled from internal precedents.

**[AI #7]**: Outcome-Tagged Learning Loop
_Concept_: When cases reach Reintegration or Termination, workers tag which interventions actually moved the needle. AI retrains on tagged outcomes only — avoids learning from abandoned or half-entered notes.
_Novelty_: Quality gate on institutional memory; prevents garbage-in from polluting suggestions.

**User insight (Admin):** AI is acceptable when it reuses and applies previous field experiences from the same organization/context, not abstract recommendations.

### Persona: Ravi — Project Coordinator (supervisor)

**Scenario:** Tuesday 9:00 AM; needs crisis prevention, not dashboards.

**[Supervisor UX #8]**: Crisis Queue, Not Dashboard
_Concept_: Ravi's home screen is a prioritized action queue: overdue visits (red), court in 48h without prep note (amber), handoffs <7 days (blue), travel claims pending (grey). Each row one-tap to case + assigned worker.
_Novelty_: Replaces "active cases by stage" pie charts with actionable triage — matches how coordinators actually work.

**[Supervisor UX #9]**: Experience Bridge Button
_Concept_: When a case matches a pattern only one other worker has handled, Ravi gets "Bridge to Suresh" — one click schedules a 5-min internal handoff call and logs it on the case timeline.
_Novelty_: Operationalizes Field Memory AI for supervisors; turns institutional memory into human connection.

**[Supervisor UX #10]**: Pattern Confidence Badge
_Concept_: Every AI suggestion shows "Based on N tagged outcomes in your unit" with drill-down to anonymized pattern stats — Ravi can approve or override with reason, feeding the learning loop.
_Novelty_: Supervisor becomes quality gate for AI trust; overrides improve future suggestions.

**Role Playing wrap:** Priya (field) + Ravi (supervisor) covered. Director, Case Worker, beneficiary personas deferred.

---

## Technique Execution — Cross-Pollination (partial complete)

**Domain explored:** Hospital discharge planning & care coordination

**[Cross-Poll #11]**: Care Pathway Memory (adapted)
_Concept_: Map hospital "similar patient pathway" model to Kaval — cluster by offence+age+domicile, bundle handoff summary + open tasks + escalation contact, tag outcomes at Reintegration/Termination to feed Field Memory AI.
_Novelty_: Validates Field Memory AI against a mature industry pattern; not invented from scratch.

**[Cross-Poll #12]**: Social Work ≠ Clinical Guardrail
_Concept_: Explicitly do NOT copy healthcare features: no diagnostic language, no treatment compliance scoring, no "readmission penalty" framing on beneficiaries. Use "pathway" and "reintegration support" language; worker judgment always overrides AI pathway.
_Novelty_: Prevents harmful transplants — social work cases are voluntary, stigmatized, and legally sensitive unlike clinical episodes.

**Deferred domains:** Field service routing (Uber/DoorDash), gov benefit portals (scheme matching) — revisit if needed.

---

## Technique Execution — Reverse Brainstorming (partial complete)

**Facilitator note:** User unsure on failure prioritization — facilitator proposed **B (gossip leak)** and **A (stereotype engine)** as highest-risk for NGO social work context.

**[Guardrail #13]**: Anti-Stereotype Floor
_Concept_: Field Memory AI must never recommend solely on domicile + offence cluster; minimum 3 distinguishing factors required; show "low confidence — only 2 similar cases" when sample size <5; worker override is default, not exception.
_Novelty_: Flips Failure A — prevents pathway laziness on marginalized communities.

**[Guardrail #14]**: Redaction-by-Design Patterns
_Concept_: Pattern cards show stats only (e.g. "3/5 vocational-first") — no addresses, worker names, or dates that could re-identify. Coordinator "Bridge to Suresh" is internal staff lookup, never shown to field worker as "like Ramesh's case."
_Novelty_: Flips Failure B — privacy as architecture, not policy PDF.

**[Guardrail #15]**: Tag Quality Gate
_Concept_: Outcome tags require Coordinator approval before entering learning pool; AI badge shows "approved outcomes only." Stale or disputed tags age out after 24 months.
_Novelty_: Flips Failure C — garbage-in blocked at source.

**[Guardrail #16]**: AI Is Advisory Copy
_Concept_: Every AI surface includes fixed copy: "Suggestion only — worker judgment prevails." Supervisor dashboard cannot rank workers by "AI compliance score."
_Novelty_: Flips Failure D — prevents punitive use of pattern stats.

**[Guardrail #17]**: Duplicate Registration Nightmare (second failure angle)
_Concept_: Reverse: make duplicate cases easiest — no search on create, no crime/ST uniqueness enforced, offline creates siloed copies. **Flip:** real-time duplicate alert on crime/ST number entry; "possible match" before save; merge workflow for Coordinators.
_Novelty_: Directly addresses PRD problem 2.1; MVP-critical guardrail.
