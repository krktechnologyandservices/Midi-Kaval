# Field and Court Operations

## Visit scheduling (CAP-4)

- Views: Today, Weekly calendar, Overdue.
- Auto-suggest visits by next visit date and GPS proximity; flag unverified GPS for manual landmark entry.
- Accept/complete updates visit count in real time.
- Reschedule requires reason visible to supervisor.
- Visit location entry; Google Maps navigation from mobile.
- Push: today's visits, overdue visits.

### Mobile field UX (from brainstorming — load-bearing for CAP-4)

- **Morning command strip:** time-ordered today queue with tap-to-navigate, overdue badge, court countdown, one-tap start visit.
- **Offline trust indicator:** visible local-save vs synced state; failed sync queued visibly, never silent.
- **Proximity day pack:** cluster nearby visits with reorder; warn when GPS unverified.

## Notes (CAP-5)

Types: Visit, Court sitting, Intervention, General follow-up. Fields: date/time, author, text, action required, action due date, attachments to cloud storage. Chronological timeline on case detail.

## Interventions (CAP-6)

Separate needed vs provided per case. Fields: type, description, priority (High/Medium/Low), status, provided date, outcome, assigned staff. Overdue alerts to Case Worker.

## Court sittings (CAP-7, CAP-8)

Per case: date/time, court name, purpose, status (Upcoming/Attended/Postponed). Notes and outcome per sitting; next court date on completion. Schedule screen for assigned worker.

**Reminders:** Mobile push 24h before; email to Coordinator; dashboard “court sittings this week.”

**Miss escalation (CAP-8):** If sitting date passes while status remains Upcoming, flag case, notify Coordinator, surface on crisis queue until resolved.

## Travel allowance (CAP-9)

Fields: date, staff, linked cases, start, destination, transport mode, bus/auto/petrol fares, auto number, notes. Receipt image mandatory for bus, auto, petrol. Workflow: Draft → Submitted → Approved (Director). Monthly totals per staff for supervisors.

## Notifications summary (CAP-10)

See CAP-10 in SPEC.md kernel. SMS/WhatsApp channel in scope per PRD; court reminders via push + email minimum; SMS/WhatsApp for court TBD (open question).
