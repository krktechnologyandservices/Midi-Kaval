# Adversarial Review — Kaval Online PRD

**Target:** `docs/Kaaval_Online_PRD.docx.pdf`

## Verdict

This PRD would pass a stakeholder demo and fail a skeptical engineering kickoff. It describes *what the NGO wants in the universe* more than *what v1 must do Monday*.

## Findings

- **[critical]** POCSO and minor data — POCSO listed as offence type; no data protection, access logging nuance, or field-worker discreet-capture consideration for sensitive cases. A court-ready audit log is in scope; privacy minimization on mobile is not. *Fix:* Security/privacy NFR section for beneficiary PII and minor cases.

- **[high]** WhatsApp as coordination enemy and channel — §2 blames WhatsApp for coordination failure; §4.1 adds SMS/WhatsApp notifications without defining what moves off WhatsApp vs what still happens there. *Fix:* State operational policy: system replaces Excel handoffs; WhatsApp remains informal only.

- **[high]** “Staff-wise workload and performance” report (§5.9) — In a social-work context, ranking performance from visit counts risks perverse incentives and worker distrust. *Fix:* Clarify report is workload distribution only, not performance scoring; align with AI advisory non-punitive stance.

- **[medium]** Heinous case PMA tracking — PMA status fields exist; no workflow tying PMA court orders to visit/intervention plans. Gap between data fields and process.

- **[medium]** Related cases cross-links — Mentioned in fields; no merge/link UX or dedup relationship to duplicate registration problem.

- **[low]** Page 4 of 6 PDF truncation — Document may be incomplete export (ends at master data); verify no missing sections (security, deployment, NFRs).
