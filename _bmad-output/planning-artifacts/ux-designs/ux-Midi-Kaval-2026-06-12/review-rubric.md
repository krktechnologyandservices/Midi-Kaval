# Spine Pair Review — Kaval Online (Midi-Kaval)

**Artifacts:** `DESIGN.md` + `EXPERIENCE.md`  
**Sources:** `prd-Midi-Kaval-2026-06-12/prd.md`, `spec-kaval-online/SPEC.md`

## Overall verdict

The spine pair is **strong on core field/supervisor journeys** (Priya visit day, Ravi crisis queue, duplicate block) and inherits discipline well (shadcn web delta, token cross-refs, anti-gamification). It is **not yet fully downstream-ready** for architecture/epics: many PRD v1 surfaces lack state coverage, several behavioral components lack matching DESIGN tokens, no visual references exist, and Director/Case Worker/travel/auth flows are absent from Key Flows. Fix high-severity gaps before `bmad-create-architecture`.

## 1. Flow coverage — thin

Checked PRD UJ-1–3 and FR references in IA tables against Key Flows.

### Findings
- **[high]** Director travel-claim approval (FR-18) has no Key Flow — Admin surface listed but no protagonist journey. *Fix:* Add Flow 5 (Director approves claim from queue).
- **[high]** Case Worker intervention overdue (FR-14) has no Key Flow. *Fix:* Add flow with alert → intervention update.
- **[medium]** Authentication / OTP (FR-1) — no flow or auth states. *Fix:* Short flow + State Patterns for OTP entry, forced logout.
- **[medium]** Notifications centre (FR-19) — IA lists bell; no open/read flow. *Fix:* Flow or state row for notification drill-down.
- **[low]** Flow 3 step 3 says "Create anyway blocked" — contradicts PRD Coordinator merge path; clarify merge vs block roles.

**UJ-1–3:** Present with protagonist, climax, failure where applicable — **adequate** for covered journeys.

## 2. Token completeness — adequate

Extracted YAML colors, typography, components; cross-checked `{colors.*}` references in EXPERIENCE.md.

### Findings
- **[high]** `Sync chip` states Pending and Error referenced behaviorally but `components` frontmatter lacks `sync-chip-pending` and `sync-chip-error` (only synced/local). *Fix:* Add component tokens using `{colors.sync-pending}` and `{colors.sync-error}`.
- **[high]** `crisis-row-neutral` missing from DESIGN frontmatter; EXPERIENCE sorts neutral queue rows. *Fix:* Add `crisis-row-neutral` component token.
- **[medium]** No explicit WCAG contrast verification for status-critical on `#FEF3F2` crisis row backgrounds. *Fix:* Note verified pairs in Colors or Accessibility Floor.
- **[low]** `accent-dark` / `accent-foreground-dark` omitted — acceptable if accent equals primary and shadcn inherits rest.

## 3. Component coverage — thin

Extracted component names from both spines.

### Findings
- **[high]** **Duplicate match sheet** — behavioral spec only; no DESIGN component (sheet/modal anatomy, button hierarchy). *Fix:* Add `duplicate-match-sheet` to DESIGN.Components + frontmatter.
- **[high]** **Court sitting row** — EXPERIENCE only; no DESIGN visual spec. *Fix:* Add `court-sitting-row` component.
- **[medium]** **Discreet header** — DESIGN prose only; EXPERIENCE names "Discreet mode toggle" — name mismatch. *Fix:* Align names; add behavioral + visual pairing.
- **[medium]** **Experience brief card** (v1.1) — EXPERIENCE only. *Fix:* Stub in DESIGN or mark spine-only until v1.1.
- **[low]** Command Strip row vs command-strip-card — naming drift (acceptable if documented as row wraps card).

## 4. State coverage — thin

Walked IA surfaces on mobile and web.

### Findings
- **[high]** Web **Dashboard, Reports, Legends, Admin** — no State Patterns (empty, loading, permission-denied, export in progress). *Fix:* Add rows per surface or global web loading/error pattern.
- **[high]** Mobile **Travel claim, Notifications, Court schedule** — no empty/error/permission states. *Fix:* Add state table rows.
- **[medium]** **Role-based denied action** (e.g. Social Worker on web admin) — not specified. *Fix:* State Pattern for unauthorized route.
- **[medium]** **Forced logout** (FR-1) — missing. *Fix:* Global session-expired pattern.

## 5. Visual reference coverage — broken

Listed `mockups/`, `wireframes/`, `imports/` — **none exist**.

### Findings
- **[critical]** No visual references for Command Strip or Crisis Queue — highest-risk layouts are spine-only. *Fix:* Generate key-screen HTML mocks or Stitch handoff; link from EXPERIENCE IA table.
- **[medium]** Open Question #2 acknowledges gap but spine claims `status: final`. *Fix:* Either produce mocks or set status draft until mock coverage confirmed.

## 6. Bloat & overspecification — strong

Spines are lean; PRD restatement limited to IA PRD column. EXPERIENCE prose operational, not decorative.

### Findings
- **[low]** PRD column in IA table may duplicate traceability already in reconcile-prd.md — acceptable for downstream.

## 7. Inheritance discipline — adequate

- `sources` frontmatter present; EXPERIENCE adds SPEC (good).
- UJ-1–3 names align with PRD.
- Glossary lives in PRD only — not duplicated in UX spines (acceptable if architecture reads PRD; optional UX glossary extract).

### Findings
- **[medium]** DESIGN.md `sources` omits SPEC though EXPERIENCE includes it. *Fix:* Add SPEC to DESIGN frontmatter for parity.

## 8. Shape fit — strong

DESIGN.md section order canonical. EXPERIENCE.md has Foundation, IA, Voice, Components, States, Primitives, A11y, Inspiration, Responsive, Key Flows. Required-when-applicable sections present.

### Findings
- **[low]** Assumptions Index in EXPERIENCE is non-template but earns place for Fast path.

## Mechanical notes

- No `mockups/` or `imports/` directories created in workspace.
- Flow 3 "Create anyway blocked" — verify against FR-5 Coordinator merge workflow.
- `status: final` on both spines conflicts with visual reference gap.
