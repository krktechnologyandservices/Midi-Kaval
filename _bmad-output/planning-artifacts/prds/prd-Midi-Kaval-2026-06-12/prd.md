---
title: Kaval Online Case Management Platform
status: final
created: 2026-06-12
updated: 2026-06-12
sources:
  - docs/Kaaval_Online_PRD.docx.pdf
  - _bmad-output/specs/spec-kaval-online/SPEC.md
  - _bmad-output/brainstorming/brainstorming-session-2026-06-12-1530.md
---

# PRD: Kaval Online Case Management Platform

## 0. Document Purpose

This PRD is for NGO product owners, supervisors, and downstream workflow owners (UX, architecture, epics). It supersedes the informal PDF (`docs/Kaaval_Online_PRD.docx.pdf`) as the build contract. Canonical machine contract: `_bmad-output/specs/spec-kaval-online/SPEC.md`. Technical stack choices live in `addendum.md`.

## 1. Vision

Kaval Online replaces offline Excel-and-WhatsApp coordination with a **cloud-first** case management platform for social work teams. Project Coordinators and Directors gain **real-time visibility** into field visits, interventions, court sittings, and travel claims. Social Workers and Case Workers use a **mobile app** in the field; supervisors use a **web application** for oversight, reporting, and configuration.

The platform enforces **one source of truth**: all authorized changes persist to a central database via a secure API and are visible immediately—no manual Excel synchronisation as an operational workflow.

## 2. Target User

### 2.1 Jobs To Be Done

- **Project Director** — Approve travel claims, review organisation-wide reports, manage users and audit compliance.
- **Project Coordinator** — Triage overdue visits and court risks, resolve duplicate cases, manage staff and master data, export reports without chasing Excel files.
- **Social Worker** — Execute today's visits, capture GPS and notes, submit travel claims, see handoff context on transferred cases.
- **Case Worker** — Track interventions and court sittings, record outcomes, respond to overdue intervention alerts.
- **Organisation (implicit)** — Demonstrate case progress, court attendance, and intervention delivery to funders and courts with auditable records.

### 2.2 Non-Users (v1)

- Beneficiaries and families (no public portal in v1).
- External police or court systems (no third-party integration in v1).

### 2.3 Key User Journeys

- **UJ-1. Priya starts a visit day without opening a dashboard.**
  - **Persona + context:** Priya, Social Worker, 18 active cases, patchy signal in one area.
  - **Entry state:** Authenticated on mobile; Tuesday 7:30 AM.
  - **Path:** Opens app → Morning Command Strip shows visit 1 with navigate + Start Visit → sees Offline Trust indicator and Handoff Whisper on transferred case → completes visit offline → syncs when signal returns.
  - **Climax:** Visit count updates; Coordinator sees completion on web without WhatsApp message.
  - **Resolution:** Remaining visits in strip; court countdown visible.
  - **Edge case:** GPS unverified — app flags landmark capture required before navigation confidence.

- **UJ-2. Ravi prevents a court miss before it happens.**
  - **Persona + context:** Ravi, Project Coordinator, 12 field staff.
  - **Entry state:** Authenticated on web; Tuesday 9:00 AM.
  - **Path:** Crisis Queue shows court sitting in 48h without prep note (amber) → taps case → reviews sitting details → assigns follow-up note to Case Worker → receives email when sitting completes.
  - **Climax:** Past-due Upcoming sitting would appear red with escalation if worker had not updated status.
  - **Resolution:** Queue cleared for resolved items; dashboard reflects court sittings this week.

- **UJ-3. Coordinator blocks a duplicate registration.**
  - **Persona + context:** Ravi registering a case from paper intake.
  - **Path:** Enters Crime Number → system shows possible match → reviews existing case → cancels create or initiates merge workflow.
  - **Climax:** No second record for same Crime/ST number.
  - **Resolution:** Single case record maintained.

## 3. Glossary

- **Beneficiary** — Individual receiving social work services under a Case.
- **Case** — Primary record for one beneficiary's engagement, identified by unique Crime Number and ST Number.
- **Case Worker** — Mobile role focused on interventions and court sittings.
- **Court Sitting** — Scheduled court appearance for a Case with status Upcoming, Attended, or Postponed.
- **Crime Number** — Police/legal identifier; unique per Case in the organisation.
- **Handoff Whisper** — Compressed transfer summary on a recently reassigned Case.
- **ICP** — Individual Care Plan.
- **Intervention** — Support action needed or provided (vocational, legal, medical, etc.).
- **Legend** — Master data entry managed by Coordinator (offence types, categories, outcomes).
- **PMA** — Parent Management Approach; tracked for heinous cases.
- **Project Coordinator** — Unit-level supervisor on web.
- **Project Director** — Organisation-level administrator on web.
- **Sitting** — See Court Sitting.
- **Social Worker** — Mobile role focused on field visits and travel claims.
- **ST Number** — Secondary unique case identifier.
- **Stage** — One of six Case lifecycle phases (Process Initiation through Termination/Exclusion).

## 4. Features

### 4.1 Authentication and Access Control

**Description:** All users authenticate with email/password and OTP 2FA. Five roles are enforced **only on the server**. Realizes UJ-1, UJ-2.

#### FR-1: User authentication

Users can log in with email, password, and email OTP. Realizes UJ-1.

**Consequences:**
- OTP required on login after password validation.
- Password reset completes via email link.
- Admin can force password reset and deactivate accounts.

#### FR-2: Server-side authorization

The API enforces role permissions for every protected action.

**Consequences:**
- Requests without valid session return unauthorized.
- Role change or deactivation forces logout on next request.
- Client UI hiding controls does not grant access if API would deny.

### 4.2 Case Management

**Description:** Full beneficiary Case profile across six Stages with search, filter, export, duplicate prevention, and transfer handoff. Realizes UJ-1, UJ-3.

#### FR-3: Case lifecycle

Staff can create and progress Cases through six Stages with defined sub-step data.

**Consequences:**
- All core fields from source PRD §5.2 are capturable (socio-demographic, offence, domicile, ICP, PMA, GPS, etc.).
- Stage transitions persist with audit trail.

#### FR-4: Unique identifiers

The system enforces unique Crime Number and ST Number per organisation.

**Consequences:**
- Save rejected if duplicate Crime or ST number exists (unless merge workflow).
- Search returns case by Crime or ST in under 2 seconds `[ASSUMPTION: <2s at 10k cases]`.

#### FR-5: Duplicate prevention

Users receive possible-match warning before creating a Case when Crime or ST matches an existing record. Realizes UJ-3.

**Consequences:**
- Warning shown pre-save with link to existing Case.
- Coordinator can execute merge workflow preserving audit history.

#### FR-6: Case search and export

Users can search and filter Cases and export results.

**Consequences:**
- Full-text search on crime, ST, name, contact, area.
- Filters: stage, offence, classification, staff, overdue, district.
- Saved filter presets per user.
- Bulk export to Excel or PDF matches current filter.

#### FR-7: Case transfer handoff

Receiving worker sees Handoff Whisper on Cases transferred within the last seven days. Realizes UJ-1.

**Consequences:**
- Summary shows prior actions, open items, next visit purpose.
- Full notes timeline remains available but is not required reading for first visit.

### 4.3 Visit Scheduling and Field Operations

**Description:** Mobile-first visit execution with GPS, proximity grouping, and supervisor visibility. Realizes UJ-1.

#### FR-8: Visit scheduler

Field staff view Today, Weekly, and Overdue visits.

**Consequences:**
- Completing visit increments visit count on Case in real time.
- Reschedule requires reason visible to supervisor.
- Push notification for today's visits and overdue visits.

#### FR-9: Visit navigation and GPS

Staff can navigate to visits and capture or verify location.

**Consequences:**
- Google Maps navigation from mobile.
- GPS coordinates, landmark, verified date/by stored on Case.
- Unverified GPS flagged; proximity grouping warns before suggesting route.

#### FR-10: Proximity visit grouping

System suggests grouping same-day visits by GPS cluster with reorder option.

**Consequences:**
- Suggestion excludes cases with unverified GPS until landmark captured.

#### FR-11: Offline visit capture

`[ASSUMPTION: v1 includes brief offline tolerance]` Field staff can start and complete visits with notes while offline; sync status visible.

**Consequences:**
- Local save indicator shown ("saved locally" / "synced").
- Failed sync queued visibly; no silent data loss.
- Cloud remains authoritative when connectivity returns.

#### FR-12: Morning Command Strip (mobile home)

Mobile home presents time-ordered today queue as primary surface. Realizes UJ-1.

**Consequences:**
- First screen shows visits, overdue badge, court countdown, one-tap Start Visit.
- No dashboard required for daily field workflow.

### 4.4 Notes and Activities

#### FR-13: Case notes timeline

Staff add typed notes with optional attachments on Cases.

**Consequences:**
- Note types: Visit, Court sitting, Intervention, General follow-up.
- Fields: date/time, author, text, action required, action due date.
- Attachments upload to secured cloud storage with role-based access.
- Chronological timeline on case detail.

### 4.5 Interventions Management

#### FR-14: Interventions tracking

Case Workers track interventions needed and provided separately per Case.

**Consequences:**
- Fields: type, description, priority, status, provided date, outcome, assigned staff.
- Categories from Legends apply immediately.
- Overdue intervention alerts to assigned Case Worker.

### 4.6 Court Sitting Management

**Description:** Schedule, remind, record, and escalate court sittings. Realizes UJ-2.

#### FR-15: Court sitting records

Staff manage sittings per Case: date/time, court name, purpose, status.

**Consequences:**
- Status values: Upcoming, Attended, Postponed.
- Notes and outcome per sitting; next court date set on completion.
- Sittings appear on assigned worker schedule.

#### FR-16: Court reminders

System notifies field staff and Coordinators before sittings.

**Consequences:**
- Mobile push 24 hours before sitting.
- Email to Project Coordinator.
- Email to web user for court sitting tomorrow.

#### FR-17: Court miss escalation

System escalates likely missed sittings. Realizes UJ-2.

**Consequences:**
- If sitting date passes with status still Upcoming, Case flagged.
- Coordinator Crisis Queue shows item; notification sent.
- Flag clears when status updated to Attended or Postponed.

### 4.7 Travel Allowance

#### FR-18: Travel claims

Field staff submit travel allowance claims with receipt evidence.

**Consequences:**
- Fields per source PRD §5.7.
- Receipt image mandatory for bus, auto, petrol before submit.
- Workflow: Draft → Submitted → Approved (Director).
- Claimant notified on approval status.
- Monthly totals per staff visible to supervisor.

### 4.8 Notifications

#### FR-19: Multi-channel notifications

Users receive actionable notifications per role with preferences.

**Consequences:**
- Mobile push: visits, overdue, interventions, court, claim approval.
- Web email: report due, claim status, new assignment, court tomorrow.
- In-app centre with read/unread state.
- SMS/WhatsApp for court and overdue visits in v1.1 `[ASSUMPTION: push+email v1; SMS/WhatsApp v1.1]`.

### 4.9 Reports and Dashboards

**Description:** Supervisor visibility and reporting. Realizes UJ-2. Workload report is **distribution only**, not performance scoring.

#### FR-20: Real-time dashboard

Coordinators and Directors view operational dashboard.

**Consequences:**
- Widgets: cases by stage/offence/area/staff, overdue visits, interventions gauge, court this week, pending claims, 12-month intake trend.
- Data reflects field updates within 60 seconds of sync `[ASSUMPTION]`.

#### FR-21: Crisis Queue (Coordinator home)

Coordinator primary view is prioritized action queue, not charts. Realizes UJ-2.

**Consequences:**
- Rows: overdue visits (critical), court <48h without prep (warning), recent handoffs, pending claims.
- One-tap to Case and assigned worker.

#### FR-22: Standard reports

Users generate and export standard operational reports.

**Consequences:**
- Daily through yearly work reports; visits planned vs completed; interventions; court scheduled vs attended; offence/area counts; first-time vs repeat; staff workload distribution; travel totals with ticket compliance.
- Export to Excel (.xlsx) or PDF.

### 4.10 Master Data and Administration

#### FR-23: Legends administration

Coordinators manage master data via web without Excel import for legends.

**Consequences:**
- CRUD for offence types, classifications, intervention categories, education, occupation, visit/court outcomes, areas, designations, police stations.
- Changes effective on next client fetch for all users.

#### FR-24: Staff directory

Admins manage staff directory.

#### FR-25: Audit log

Directors retrieve audit history of data-changing events.

**Consequences:**
- Who, what, when recorded for mutations.

### 4.11 Field Memory AI (v1.1)

`[ASSUMPTION: ships v1.1 after core platform stable]`

**Description:** AI suggestions from organisation's own tagged outcomes—not generic web advice. User requirement: apply previous field experiences.

#### FR-26: Pre-visit experience brief

Field staff receive pattern summary before visit from similar approved outcomes.

**Consequences:**
- Brief cites aggregate stats and sample size.
- Low confidence when <5 similar approved outcomes.
- Advisory copy on every surface; worker override default.

#### FR-27: Outcome-tagged learning

Coordinators approve outcome tags at Reintegration/Termination before tags enter learning pool.

**Consequences:**
- AI badge shows "approved outcomes only."
- Tags expire after 24 months.
- Minimum three factors for pattern match; never domicile+offence alone.

#### FR-28: Experience bridge

Coordinator can connect worker to prior handler when pattern is unit-unique.

**Consequences:**
- Bridge logged on Case timeline; staff names internal only.

### 4.12 Government Scheme and Law AI (v2)

`[NON-GOAL for MVP]` Deferred to v2 with scheme/law corpus maintenance.

## 5. Non-Goals (Explicit)

- Offline-first with Excel as operational sync mechanism.
- Native biometric mobile login.
- Third-party police or court system integrations (v1).
- Public beneficiary-facing portal (v1).
- Budget allocation and expenditure management (v1).
- Punitive staff performance scoring or AI-compliance rankings.
- Generic internet AI advice disconnected from organisation history.
- Clinical diagnostic language or treatment-compliance scoring for beneficiaries.

## 6. MVP Scope

### 6.1 In Scope (v1)

- Angular PWA web (Director, Coordinator) + React Native mobile (Social Worker, Case Worker)
- FR-1 through FR-25
- One-time Excel import for legacy migration only
- Court miss escalation (FR-17)
- Duplicate prevention and merge (FR-5)
- Handoff Whisper (FR-7)
- Crisis Queue (FR-21)
- POCSO/minor data privacy controls (see §10)

### 6.2 Out of Scope for MVP

| Item | Target | Reason |
|------|--------|--------|
| Field Memory AI | v1.1 | De-risk v1; requires outcome-tag discipline |
| Scheme/law AI | v2 | Requires curated corpus and governance |
| SMS/WhatsApp notifications | v1.1 | Push + email sufficient for launch `[ASSUMPTION]` |
| Beneficiary portal | v2 | PRD "if possible"; not blocking coordination pain |
| Budget management | v2 | Aspirational in source PRD |
| Third-party integrations | v2 | External dependency |
| Biometric login | — | Explicitly excluded in source PRD |

## 7. Success Metrics

**Primary**

- **SM-1:** Coordinators obtain overdue visit and court-sitting status without requesting Excel exports from field staff — measured by zero weekly Excel coordination requests in pilot unit after 30 days. Validates FR-20, FR-21.
- **SM-2:** Duplicate Crime/ST registrations blocked at entry — zero duplicate active cases with same Crime or ST in pilot. Validates FR-4, FR-5.
- **SM-3:** Court sitting reminders delivered 24h before scheduled time for ≥95% of upcoming sittings in pilot. Validates FR-16.

**Secondary**

- **SM-4:** Field visit completion visible to supervisor within 60s of sync for ≥90% of completed visits. Validates FR-8, FR-11.

**Counter-metrics (do not optimize)**

- **SM-C1:** Raw visit count per worker — must not drive performance reviews; workload distribution only. Counterbalances misread of SM-4.

## 8. Open Questions

1. Confirm v1.1 timing for Field Memory AI vs parallel track with v1 hardening.
2. SMS/WhatsApp provider and template approval for court/overdue messages.
3. Pilot unit size and case volume for performance assumptions.
4. Legal review of POCSO field capture and retention periods.
5. Exact legacy Excel import field mapping for migration.

## 9. Assumptions Index

- §4.3 FR-11 — v1 includes brief offline visit/note capture with visible sync.
- §4.8 FR-19 — SMS/WhatsApp deferred to v1.1; push + email for v1 launch.
- §4.2 FR-4 — Search <2s at 10k cases.
- §4.9 FR-20 — Dashboard freshness within 60s of field sync.
- §6.1 — Angular PWA web + React Native mobile (architecture decision 2026-06-12).
- §6.1 — Excel import is one-time migration only, not ongoing sync.
- §4.11 — Field Memory AI ships v1.1, not v1.

## 10. Cross-Cutting NFRs

### Security and privacy

- All API traffic over TLS.
- Role-based access enforced server-side (FR-2).
- POCSO and minor Cases: field app supports discreet capture mode (minimal on-screen beneficiary detail during visit in public settings) `[ASSUMPTION: UX detail in bmad-ux]`.
- Beneficiary PII access logged in audit trail for sensitive Case classifications.
- Attachments stored in secured cloud storage with role-scoped URLs.

### Reliability

- Cloud database is source of truth; offline mobile is temporary buffer only.
- Failed syncs surface to user; support can inspect sync queue `[ASSUMPTION: ops detail in architecture]`.

### Performance

- Dashboard and search meet FR-4 and FR-20 latency assumptions under pilot load.
