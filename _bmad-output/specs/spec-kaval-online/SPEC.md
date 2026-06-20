---
id: SPEC-kaval-online
companions:
  - roles-and-access.md
  - case-and-lifecycle.md
  - field-and-court-operations.md
  - reporting-and-admin.md
  - ai-guardrails.md
sources:
  - docs/Kaaval_Online_PRD.docx.pdf
  - _bmad-output/brainstorming/brainstorming-session-2026-06-12-1530.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate.

# Kaval Online Case Management Platform

## Why

Social work teams managing beneficiary cases, interventions, court sittings, and field visits are stuck coordinating through manual Excel exports over WhatsApp and email. Project Coordinators lack real-time visibility; case handovers lose context; duplicate registrations proliferate; court dates and overdue visits are tracked manually; and consolidated reporting consumes days each month. **Pain to solve** for NGO field and supervisory staff; **mandate to meet** the Kaval project’s move from offline-first to cloud-first operations. Kaval Online must centralize case data in real time so authorized roles collaborate on one source of truth across web (supervisors) and mobile (field staff).

## Capabilities

- id: CAP-1
  intent: Users authenticate with email/password and OTP 2FA, and the system enforces role-based access for five defined roles exclusively on the server.
  success: Deactivated users and role changes force logout; API rejects unauthorized actions regardless of client UI state; password reset and admin force-reset flows complete end-to-end.

- id: CAP-2
  intent: Staff create and manage beneficiary cases through a six-stage lifecycle with the full socio-demographic and legal profile defined for Kaval cases.
  success: Cases progress through all six stages with sub-step data captured; crime number and ST number remain unique; search and filters return correct cases by crime/ST, name, contact, stage, offence, staff, and overdue status; bulk export of filtered results produces valid Excel or PDF.

- id: CAP-3
  intent: The system prevents duplicate case registration by alerting users to possible matches before a new case is saved.
  success: Entering an existing crime or ST number surfaces a possible-match warning pre-save; Coordinators can merge duplicate records through a defined workflow.

- id: CAP-4
  intent: Assigned field staff schedule, navigate to, complete, and reschedule visits with GPS capture and visit-count updates visible in real time to supervisors.
  success: Scheduler shows today, weekly, and overdue visits; completing a visit increments visit count on the case; reschedules require a reason visible to supervisors; proximity-based visit grouping suggests same-day clusters and flags unverified GPS.

- id: CAP-5
  intent: Staff record typed notes and file attachments on cases in a chronological timeline linked to visit, court, intervention, or follow-up activity.
  success: Each note stores date/time, author, text, optional action and due date; attachments upload to secured cloud storage with role-controlled access; case detail shows ordered notes feed.

- id: CAP-6
  intent: Case Workers and Social Workers track interventions needed and provided per case with priority, status, outcome, and master-data categories.
  success: Needed and provided interventions are separately tracked; overdue interventions trigger alerts to assigned Case Worker; Coordinator-managed categories apply immediately.

- id: CAP-7
  intent: Staff manage court sittings per case with scheduling, attendance status, outcomes, and proactive reminders to field staff and Coordinators.
  success: Sittings record date/time, court, purpose, and status (Upcoming/Attended/Postponed); completing a sitting sets next court date; mobile push fires 24 hours before sitting; Coordinator receives email alert; sittings appear on worker schedule and dashboard “this week” view.

- id: CAP-8
  intent: The system escalates likely missed court sittings when a sitting date passes without Attended or Postponed status.
  success: Past-due Upcoming sittings appear on Coordinator crisis queue and trigger notification; case is flagged until status is resolved.

- id: CAP-9
  intent: Field staff submit travel allowance claims with receipt evidence and Directors approve them through a defined workflow.
  success: Claims move Draft → Submitted → Approved with mandatory receipt images for bus, auto, and petrol; monthly totals per staff are visible to supervisors; approval status notifies the claimant.

- id: CAP-10
  intent: Users receive push, email, SMS/WhatsApp, and in-app notifications for actionable events according to role and configurable preferences.
  success: Mobile push covers today’s visits, overdue visits, interventions, court sittings, and claim approval; web email covers report due, claim status, new assignment, and court tomorrow; notification centre tracks read/unread state.

- id: CAP-11
  intent: Supervisors and Directors view real-time dashboards and generate standard operational reports exportable to Excel or PDF.
  success: Dashboard shows active cases by stage/offence/area/staff, overdue visits, interventions gauge, court sittings this week, pending travel claims, and 12-month intake trend; daily through yearly reports and listed standard report types export correctly.

- id: CAP-12
  intent: Project Coordinators administer master data (Legends), staff directory, police station records, and organisation settings via web UI with immediate effect for all users.
  success: Offence types, classifications, intervention categories, education/occupation types, visit/court outcomes, areas, designations, and police stations are CRUD-managed without Excel import; changes are live for all clients on next fetch.

- id: CAP-13
  intent: Field staff and supervisors receive AI suggestions derived from the organisation’s own tagged case outcomes and field patterns—not generic external advice.
  success: Pre-visit briefs and pattern cards cite anonymized unit statistics (sample size, approved outcomes only); workers can override any suggestion; Coordinators approve outcome tags before they enter the learning pool; all AI surfaces display advisory-only copy.

- id: CAP-14
  intent: The system suggests appropriate government schemes and legally grounded intervention actions based on beneficiary age, offence type, and applicable law.
  success: Scheme and intervention suggestions are generated for eligible cases; suggestions are traceable to loaded scheme/law reference data maintained in the system.

- id: CAP-15
  intent: Cases transferred between workers retain a compressed handoff summary so the receiving worker has immediate operational context.
  success: Within seven days of transfer, case detail shows a handoff summary (prior actions, open items, next visit purpose) without requiring the full notes timeline; Coordinators can bridge to prior handler when pattern is unique in the unit.

- id: CAP-16
  intent: All data-changing actions are recorded in an audit log accessible to authorized supervisory roles.
  success: Audit log captures who changed what and when for data mutations; Directors can retrieve audit history for compliance review.

## Constraints

- Role-based access is enforced only at the API layer; client-side role display is never sufficient for authorization.
- Crime number and ST number must remain unique across the organisation; duplicate prevention is mandatory at case creation.
- Cloud API is the single integration point for web and mobile; all authorized users see real-time persisted data—no manual Excel synchronisation as the operational workflow.
- Field Memory AI suggestions require minimum three distinguishing factors, sample-size disclosure, and cannot recommend from domicile + offence cluster alone; low-confidence state when fewer than five similar approved outcomes exist.
- AI pattern displays use aggregate statistics only—no beneficiary-re-identifying addresses, dates, or case labels on field-facing surfaces.
- Outcome tags enter the AI learning pool only after Coordinator approval; disputed or stale tags expire after twenty-four months.
- Supervisors must not rank or score staff by AI compliance; AI is advisory and worker judgment prevails on every surface.
- Social-work-appropriate language only—no clinical diagnostic framing, treatment-compliance scoring, or punitive “readmission” metaphors for beneficiaries.
- Standard credential login only; native biometric login is out of scope.
- Receipt images are mandatory for bus, auto, and petrol travel claims before submission.

## Non-goals

- Offline-first operation with periodic Excel sync as the primary data model (superseded by cloud-first real-time design).
- Native biometric authentication on mobile.
- Third-party integrations with police or court systems in initial release.
- Public beneficiary-facing portal unless explicitly scoped in a later phase.
- Budget allocation and expenditure management unless explicitly scoped in a later phase.
- Punitive supervisor dashboards or worker rankings based on AI suggestion adherence.
- Generic internet-sourced AI advice disconnected from the organisation’s own outcome-tagged case history.

## Success signal

A Project Coordinator opens the web app on a weekday morning and sees overdue visits, court sittings this week, and pending travel claims without requesting an Excel export from field staff; a Social Worker completes a visit on mobile and the supervisor sees updated visit count and notes within one refresh cycle; entering a duplicate crime or ST number is blocked before save; and a court sitting receives a mobile push twenty-four hours before the scheduled time with Coordinator email backup.

## Assumptions

- Web application serves Project Directors and Project Coordinators; React Native mobile serves Social Workers and Case Workers on iOS and Android.
- Field staff may operate in patchy connectivity; visit capture should tolerate brief offline periods with visible sync status even though PRD emphasizes always-connected cloud storage.
- Government scheme and law-based AI (CAP-14) depends on scheme/law reference data loaded by administrators; quality of suggestions is bounded by that corpus.
- Kaval project details, objectives, and police station directory are in scope as reference/admin content per PRD.

## Open Questions

- Does v1 require offline-capable visit and note capture with explicit sync indicators, or strict online-only per PRD executive summary?
- PRD lists both “no Excel synchronisation” and “Excel file import for sync”—is legacy import a one-time migration tool only?
- Is the public beneficiary portal in v1 or deferred?
- Is budget allocation and expenditure management in v1 or deferred?
- Which AI capabilities (CAP-13 Field Memory vs CAP-14 scheme/law suggestions) ship in v1 versus a later phase?
- Should court-miss escalation (CAP-8) also notify via SMS/WhatsApp in addition to push and email?
