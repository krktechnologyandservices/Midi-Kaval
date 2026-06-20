# Accessibility Review — Kaval UX Spines

**Target:** DESIGN.md + EXPERIENCE.md  
**Lens:** Regulated social-work context (POCSO minors, field use, supervisor triage)

## Verdict

Accessibility floor is **started but incomplete** for a regulated multi-surface product. Color+text rule is stated; OTP/auth, modal focus traps, and discreet-mode screen-reader behavior need explicit commitment before build.

## Findings

- **[high]** **OTP / 2FA login (FR-1)** — No accessibility spec for OTP input (digit boxes vs single field), error announcement, or timeout. *Fix:* Add auth surface to IA + Accessibility Floor entries.

- **[high]** **Duplicate match sheet** — Modal focus trap, escape behavior, and screen reader announcement of match severity not specified. *Fix:* Interaction Primitives + A11y for blocking sheets.

- **[medium]** **Crisis Queue severity** — Claims "icon + text" but icon vocabulary not defined in DESIGN.md. *Fix:* Name icons or aria-label pattern per severity.

- **[medium]** **Discreet mode vs screen reader** — EXPERIENCE says full detail behind expand; expand auth mechanism unspecified (biometric out of scope — PIN re-auth?). *Fix:* Define re-auth primitive for POCSO expand.

- **[medium]** **Dynamic type on web** — Mobile covered; web Crisis Queue at `max-w-4xl` — no large-text reflow rules. *Fix:* Responsive & Platform row for browser zoom 200%.

- **[low]** **Reduce Motion** — Not mentioned for sync chip or crisis row transitions. *Fix:* Respect `prefers-reduced-motion` for non-critical animations only.
