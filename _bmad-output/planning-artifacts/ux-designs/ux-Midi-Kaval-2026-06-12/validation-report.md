# Validation Report — Kaval Online UX

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md`
- **Run at:** 2026-06-12

## Overall verdict

Core journeys (field visit day, crisis triage, duplicate prevention) are well-specified and aligned with PRD UJ-1–3. The pair **fails visual reference coverage** (no mocks) and is **thin on flow/state coverage** for secondary surfaces (travel approval, interventions, auth, admin). Token and component gaps on sync/crisis-neutral/duplicate sheet should be fixed before architecture. Accessibility review flags OTP and modal focus gaps for regulated use.

## Category verdicts

- Flow coverage — thin
- Token completeness — adequate
- Component coverage — thin
- State coverage — thin
- Visual reference coverage — broken
- Bloat & overspecification — strong
- Inheritance discipline — adequate
- Shape fit — strong

## Findings by severity

### Critical (1)

**[Visual reference]** — No mockups/wireframes/imports  
Command Strip and Crisis Queue are spine-only; highest-risk layouts lack visual anchor.  
*Fix:* Add `mockups/` HTML for Today + Crisis Queue or run Stitch handoff; link in EXPERIENCE IA.

### High (9)

**[Flow coverage]** — Director travel approval missing (FR-18)  
*Fix:* Add Key Flow with Director protagonist.

**[Flow coverage]** — Case Worker intervention overdue missing (FR-14)  
*Fix:* Add Key Flow from push alert to resolution.

**[Token completeness]** — sync-chip-pending/error components missing  
*Fix:* Extend DESIGN.md frontmatter components.

**[Token completeness]** — crisis-row-neutral missing  
*Fix:* Add neutral crisis row token.

**[Component coverage]** — Duplicate match sheet has no DESIGN spec  
*Fix:* Add visual component definition.

**[Component coverage]** — Court sitting row has no DESIGN spec  
*Fix:* Add court-sitting-row component.

**[State coverage]** — Web Dashboard/Reports/Legends/Admin states missing  
*Fix:* Extend State Patterns table.

**[State coverage]** — Mobile Travel/Notifications/Court schedule states missing  
*Fix:* Extend State Patterns table.

**[Accessibility]** — OTP/2FA accessibility unspecified  
*Fix:* Auth IA + a11y rules.

### Medium (8)

**[Flow coverage]** — Auth flow missing  
**[Flow coverage]** — Notifications centre flow missing  
**[Component coverage]** — Discreet header / toggle name mismatch  
**[Component coverage]** — Experience brief card DESIGN stub for v1.1  
**[State coverage]** — Role-based denied / forced logout  
**[Token completeness]** — Contrast pairs not documented  
**[Inheritance]** — DESIGN.md sources missing SPEC  
**[Accessibility]** — Duplicate sheet focus trap; Crisis icon vocabulary; discreet expand re-auth; web 200% zoom  

### Low (5)

**[Flow coverage]** — Flow 3 "Create anyway blocked" wording  
**[Token completeness]** — accent-dark omitted  
**[Component coverage]** — Command Strip naming drift  
**[Bloat]** — IA PRD column duplication  
**[Shape fit]** — Assumptions Index non-template  
**[Accessibility]** — prefers-reduced-motion  

## Reviewer files

- `review-rubric.md`
- `review-accessibility.md`
