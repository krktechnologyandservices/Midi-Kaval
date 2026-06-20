# PRD Addendum — Kaval Online

Technical and mechanism details that do not belong in the PRD kernel.

## Platform stack (confirmed)

- **Web:** Angular PWA — supervisors (Director, Coordinator): case management, Crisis Queue, dashboards, reporting, admin. Installable; bounded read-only offline for queue/dashboard snapshot.
- **Mobile:** React Native — iOS and Android for field staff (Social Worker, Case Worker). Primary offline visit capture (FR-11).
- **API:** Secure cloud API; all clients consume same REST/GraphQL surface `[ASSUMPTION: REST unless architecture decides otherwise]`.
- **Maps:** Google Maps for navigation.
- **Storage:** Cloud object storage for receipts and note attachments.

## Excel migration

One-time import tool maps legacy Excel columns to Case fields. Not an ongoing sync channel. Post-migration, Excel exports are **read-only reports** from Kaval only.

## WhatsApp operational policy

Kaval replaces Excel handoffs and status chasing via WhatsApp. Informal WhatsApp may continue for human coordination but is **not** a system integration in v1.

## Field Memory AI mechanism (v1.1 preview)

- Pattern clustering: offence type, age band, domicile, family type, stage at tag time.
- Learning pool: Coordinator-approved outcome tags only.
- Display: aggregate stats on field UI; staff bridge on Coordinator UI only.

## Reference

Canonical spec: `_bmad-output/specs/spec-kaval-online/SPEC.md`
