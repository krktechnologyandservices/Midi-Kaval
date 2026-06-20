---
name: Kaval
description: Trust-first operations design for NGO case management — field-durable mobile and supervisor-focused web. shadcn brand-layer on web; platform-native typography on mobile.
status: final
updated: 2026-06-12
sources:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md
  - _bmad-output/specs/spec-kaval-online/SPEC.md
colors:
  status-critical: '#B42318'
  status-critical-foreground: '#FFFFFF'
  status-warning: '#B54708'
  status-warning-foreground: '#FFFFFF'
  status-info: '#175CD3'
  status-info-foreground: '#FFFFFF'
  status-neutral: '#667085'
  status-neutral-foreground: '#FFFFFF'
  sync-local: '#5925DC'
  sync-synced: '#027A48'
  sync-pending: '#B54708'
  sync-error: '#B42318'
  primary: '#0D6E6E'
  primary-foreground: '#FFFFFF'
  accent: '#0D6E6E'
  accent-foreground: '#FFFFFF'
  primary-dark: '#5CB8B8'
  primary-foreground-dark: '#042F2F'
  surface-base: '#F8FAFC'
  surface-raised: '#FFFFFF'
  ink-primary: '#101828'
  ink-secondary: '#475467'
  ink-disabled: '#98A2B3'
  border-hairline: '#EAECF0'
  surface-base-dark: '#101828'
  surface-raised-dark: '#1D2939'
  ink-primary-dark: '#F9FAFB'
  ink-secondary-dark: '#98A2B3'
  border-hairline-dark: '#344054'
typography:
  display:
    note: 'Web only — shadcn display: Inter 600, 30px/36px'
  body:
    note: 'Web — Geist Sans / shadcn body. Mobile — platform native Body.'
  meta:
    note: 'Web — shadcn muted. Mobile — Footnote / Body Small.'
  title:
    note: 'Mobile — iOS Title 2 · Android Title Medium'
rounded:
  sm: 6px
  md: 8px
  lg: 12px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
components:
  crisis-row-critical:
    borderLeft: '4px solid {colors.status-critical}'
    background: '#FEF3F2'
  crisis-row-warning:
    borderLeft: '4px solid {colors.status-warning}'
    background: '#FFFAEB'
  crisis-row-info:
    borderLeft: '4px solid {colors.status-info}'
    background: '#EFF8FF'
  crisis-row-neutral:
    borderLeft: '4px solid {colors.status-neutral}'
    background: '{colors.surface-raised}'
  command-strip-card:
    background: '{colors.surface-raised}'
    radius: '{rounded.lg}'
    border: '1px solid {colors.border-hairline}'
  sync-chip-synced:
    background: '#ECFDF3'
    foreground: '{colors.sync-synced}'
  sync-chip-local:
    background: '#F4F3FF'
    foreground: '{colors.sync-local}'
  sync-chip-pending:
    background: '#FFFAEB'
    foreground: '{colors.sync-pending}'
  sync-chip-error:
    background: '#FEF3F2'
    foreground: '{colors.sync-error}'
  handoff-whisper:
    background: '#EFF8FF'
    borderLeft: '3px solid {colors.status-info}'
  duplicate-match-sheet:
    background: '{colors.surface-raised}'
    radius: '{rounded.lg}'
    shadow: 'shadcn Dialog elevation'
    primaryAction: '{colors.primary}'
  court-sitting-row:
    border: '1px solid {colors.border-hairline}'
    statusUpcoming: '{colors.status-info}'
    statusOverdue: '{colors.status-critical}'
  discreet-header:
    foreground: '{colors.ink-secondary}'
    crimeNumber: '{colors.ink-primary}'
    expandControl: '{colors.primary}'
  experience-brief-card:
    background: '#F8FAFC'
    border: '1px solid {colors.border-hairline}'
    advisoryText: '{colors.ink-secondary}'
  button-primary:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    radius: '{rounded.md}'
---

## Brand & Style

Kaval serves social work teams in the field and supervisors at desks. The visual posture is **operational trust** — clear status, no gamification, no clinical hospital aesthetic. Workers must read a screen in sunlight on a bus; coordinators must triage risk in seconds. Hierarchy comes from **status color and layout**, not decoration.

Web inherits **shadcn/ui** defaults; this file specifies brand deltas only. Mobile inherits **platform typography**; shared semantic colors apply to status and sync.

Spines win on conflict with mockups. Visual references: `mockups/command-strip-today.html`, `mockups/crisis-queue.html`.

## Colors

- **Teal Primary (`#0D6E6E`)** — Web primary actions, active nav, links.
- **Status Critical (`#B42318`)** — Overdue visits, missed court escalations.
- **Status Warning (`#B54708`)** — Court within 48h without prep, sync pending.
- **Status Info (`#175CD3`)** — Recent handoffs, informational queue rows.
- **Status Neutral (`#667085`)** — Pending travel claims, non-urgent queue items.
- **Sync Local (`#5925DC`)** — Saved on device.
- **Sync Synced (`#027A48`)** — Confirmed cloud persistence.

**Contrast (WCAG AA verified pairs):** `#B42318` on `#FEF3F2` (crisis critical row); `#B54708` on `#FFFAEB` (warning row); `#175CD3` on `#EFF8FF` (info row); `#0D6E6E` on `#FFFFFF` (primary button).

Avoid: gradients, streak badges, performance leaderboards, clinical pastel palettes.

## Typography

Web: shadcn body stack; section titles semibold sans. Mobile: platform native scales; crime/ST numbers in tabular figures.

Command Strip headlines use `title`; discreet mode beneficiary display uses `meta` only until expand.

## Layout & Spacing

Scale 4–32px. Mobile: single-column; bottom tab bar (Today / Cases / More). Web: sidebar + main; Crisis Queue `max-w-4xl`. Web reflows at 200% browser zoom without horizontal scroll on queue rows.

Touch targets ≥ 44pt mobile / 40px web.

## Elevation & Depth

Minimal shadows. Cards use `surface-raised` + hairline border. Crisis rows use left border accent per severity. Duplicate match sheet uses single shadcn Dialog elevation.

## Shapes

`rounded/md` (8px) default. `rounded/lg` (12px) for Command Strip cards. Severity badges rectangular with `rounded/sm`, not pills.

## Components

- **Command Strip card** (`command-strip-card`) — Visit order, crime/ST, sync chip, Navigate + Start Visit. → `mockups/command-strip-today.html`
- **Crisis Queue row** (`crisis-row-*`) — Severity badge + label + case line + worker + chevron. → `mockups/crisis-queue.html`
- **Handoff Whisper** (`handoff-whisper`) — Three-line summary on Case detail.
- **Sync chip** (`sync-chip-*`) — Local, Synced, Pending, Error variants.
- **Duplicate match sheet** (`duplicate-match-sheet`) — Blocking sheet: match summary, Open existing (secondary), Merge (Coordinator, primary), Cancel. No "create duplicate" action.
- **Court sitting row** (`court-sitting-row`) — Date, court, status chip; overdue uses critical accent.
- **Discreet header** (`discreet-header`) — Initials + crime number; expand control for full detail after re-auth.
- **Experience brief card** (`experience-brief-card`) — v1.1 only; stats block + mandatory advisory footer.

## Do's and Don'ts

**Do:** Use status colors only for operational meaning. Show sync state on field surfaces. Pair color with text badge on crisis rows.

**Don't:** Celebratory animations, worker rankings, diagnostic iconography, silent sync failures, red primary buttons.
