# AI and Guardrails

## Two AI capability streams

### Field Memory AI (CAP-13)

Draws from the organisation’s own tagged case outcomes—not generic web advice.

**Behaviors:**
- Pre-visit experience brief: 30-second pattern summary from similar cases (offence, age, domicile, family type, etc.).
- Pattern confidence badge: “Based on N approved outcomes in your unit” with anonymized stats drill-down.
- Coordinator outcome-tag approval before tags enter learning pool.
- Experience bridge: Coordinator connects field worker to prior handler when pattern is unit-unique.
- Care pathway memory adapted from discharge planning: cluster → pathway → outcome tag at Reintegration/Termination.

**User requirement (brainstorming):** AI is acceptable when it applies previous field experiences from the same organisation.

### Scheme and law AI (CAP-14)

- Load/update government schemes in system.
- Suggest appropriate schemes to case workers.
- Generate possible interventions and actions by age, offence type, and applicable law.

## Guardrails (load-bearing constraints)

| Guardrail | Rule |
|-----------|------|
| Anti-stereotype floor | Never recommend from domicile + offence alone; minimum three factors; low-confidence below five similar approved outcomes |
| Redaction-by-design | Pattern cards show aggregates only; no re-identifying addresses, dates, or case labels on field UI |
| Tag quality gate | Coordinator approves outcome tags; approved-only badge; 24-month tag expiry |
| Advisory only | Fixed copy on every AI surface; no supervisor AI-compliance scoring |
| Social work language | No clinical diagnostic or punitive readmission framing; worker judgment overrides pathway |
| Staff bridge privacy | Internal staff lookup for Coordinators; field UI never says “like Ramesh’s case on MG Road” |

## Non-goals for AI

- Generic internet-sourced recommendations disconnected from unit history.
- Punitive ranking of workers by suggestion adherence.
