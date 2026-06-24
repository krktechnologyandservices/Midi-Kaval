---
name: Midi-Kaval Role Management
description: Enterprise admin skin for the Role Management & Registration System — bank/corporate posture for NGO trust buyers. Angular Material theme layer; this DESIGN.md specifies the brand-layer delta. Paired with EXPERIENCE.md.
status: final
updated: 2026-06-23
sources:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md
colors:
  # Angular Material theme overrides. Unlisted tokens inherit from Material defaults.
  # This skin is deliberately distinct from the main Kaval teal identity (#0D6E6E).
  # Navy-deep enterprise palette signals trust, stability, and bank-grade security.
  primary: '#1B2A4A'
  primary-foreground: '#FFFFFF'
  primary-hover: '#243A60'
  accent: '#2E7D8F'
  accent-foreground: '#FFFFFF'
  accent-hover: '#3A95A8'
  primary-dark: '#4A6B96'
  primary-foreground-dark: '#E8EDF5'
  accent-dark: '#5BA3B5'
  accent-foreground-dark: '#0F1A2B'
  surface-base: '#F5F6FA'
  surface-raised: '#FFFFFF'
  surface-card: '#FFFFFF'
  ink-primary: '#121926'
  ink-secondary: '#4B5565'
  ink-disabled: '#9DA4B3'
  ink-muted: '#697586'
  border-hairline: '#E2E5EB'
  border-focus: '#3A95A8'
  surface-base-dark: '#0F172A'
  surface-raised-dark: '#1E293B'
  ink-primary-dark: '#F1F5F9'
  ink-secondary-dark: '#94A3B8'
  border-hairline-dark: '#334155'
  status-active: '#0F6E4A'
  status-active-foreground: '#FFFFFF'
  status-pending: '#B45309'
  status-pending-foreground: '#FFFFFF'
  status-suspended: '#991B1B'
  status-suspended-foreground: '#FFFFFF'
  status-neutral: '#6B7280'
  status-neutral-foreground: '#FFFFFF'
typography:
  # Body, label, and caption inherit from Angular Material typography. Only display is overridden.
  display:
    fontFamily: 'Inter'
    fontWeight: '600'
    fontSize: 28px
    lineHeight: '1.3'
    letterSpacing: '-0.02em'
  display-sm:
    fontFamily: 'Inter'
    fontWeight: '600'
    fontSize: 20px
    lineHeight: '1.4'
  title:
    fontWeight: '600'
    fontSize: 16px
    lineHeight: '1.5'
  mono:
    fontFamily: 'JetBrains Mono, monospace'
    fontSize: 13px
    lineHeight: '1.5'
rounded:
  # Corporate precision — Angular Material shape overrides.
  sm: 4px
  md: 6px
  lg: 8px
  xl: 12px
spacing:
  # Angular Material spacing scale inherited; no overrides.
components:
  button-primary:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    radius: '{rounded.md}'
    hover: '{colors.primary-hover}'
  button-secondary:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '1px solid {colors.border-hairline}'
    radius: '{rounded.md}'
  table-header:
    background: '{colors.surface-base}'
    foreground: '{colors.ink-muted}'
    borderBottom: '1px solid {colors.border-hairline}'
    fontWeight: '500'
    fontSize: '12px'
    textTransform: 'uppercase'
    letterSpacing: '0.05em'
  table-row:
    borderBottom: '1px solid {colors.border-hairline}'
    hover: '{colors.surface-base}'
  status-badge-active:
    background: '#ECFDF5'
    foreground: '{colors.status-active}'
    radius: '{rounded.sm}'
  status-badge-pending:
    background: '#FFFBEB'
    foreground: '{colors.status-pending}'
    radius: '{rounded.sm}'
  status-badge-suspended:
    background: '#FEF2F2'
    foreground: '{colors.status-suspended}'
    radius: '{rounded.sm}'
  invite-card:
    background: '{colors.surface-raised}'
    radius: '{rounded.lg}'
    border: '1px solid {colors.border-hairline}'
    shadow: '0 1px 3px 0 rgba(0, 0, 0, 0.06)'
  sidebar-nav:
    background: '{colors.primary}'
    foreground: '#FFFFFF'
    activeIndicator: '{colors.accent}'
    width: '256px'
  sidebar-nav-item:
    foreground: '#C8D0DD'
    foreground-active: '#FFFFFF'
    background-active: 'rgba(255, 255, 255, 0.08)'
    radius: '{rounded.md}'
  modal-overlay:
    background: 'rgba(18, 25, 38, 0.5)'
  input:
    radius: '{rounded.md}'
    border: '1px solid {colors.border-hairline}'
    focus: '2px solid {colors.border-focus}'
  role-pill:
    foreground: '{colors.primary}'
    background: '#E8EDF5'
    radius: '{rounded.sm}'
    fontSize: '12px'
---

## Brand & Style

The Role Management system serves NGO trust administrators — Executive Directors, programme operations leads, and grant compliance officers. The visual posture is **institutional trust**: clear hierarchy, no decoration, bank-grade predictability. Every screen must communicate "your data is safe, your team is visible, your controls are here."

This is a deliberate departure from the main Kaval field app's warm teal identity. The enterprise admin skin uses a **deep navy primary (`#1B2A4A`)** with a **cool teal accent (`#2E7D8F`)** — the relationship is sibling products, not identical twins. The navy signals stability and seriousness; the accent brings a touch of operational clarity without warmth.

Web inherits **Angular Material** theme defaults; this DESIGN.md specifies the brand-layer delta only. The majority of components (MatButton, MatTable, MatDialog, MatBottomSheet, MatSelect, MatInput, MatBadge, MatChips, MatMenu, MatSnackBar, MatTabs, MatSidenav, MatProgressSpinner, MatSort, MatPaginator) inherit Material's visual specs via the Angular Material theming system.

Spines win on conflict with mockups or wireframes.

## Colors

The palette is intentionally restrained — two brand colours plus status semantics. Everything else inherits from Angular Material defaults.

- **Primary Navy (`#1B2A4A` light / `#4A6B96` dark)** — The brand colour. Configures `$primary` palette via Angular Material `mat.define-palette`. Used on: primary buttons, sidebar nav background, active nav items, link underlines. Never decorative — every navy pixel has a structural job.
- **Accent Teal (`#2E7D8F` light / `#5BA3B5` dark)** — The operational accent. Configures `$accent` palette. Used exclusively for: active state indicators in the sidebar, focus borders (`border-focus`), interactive hover highlights, empty-state CTAs. Teal means "you are here" or "act here."
- **Surface Base (`#F5F6FA`)** — A cooler, more corporate gray than Material's default. Used as the main page background for admin surfaces. Reads "professional" against the slightly warmer main app background.
- **Status colours** — Active (green, `#0F6E4A`), Pending (amber, `#B45309`), Suspended (red, `#991B1B`), Neutral (gray, `#6B7280`). All paired with WCAG AA-compliant foregrounds. Used via custom `$status` palette or as overrides on MatChip/MatBadge.
- **All other tokens** (`background`, `foreground`, `warn`, `surface`, `background-card`, etc.) inherit from Angular Material defaults.

Avoid: gradients, celebratory animations, decorative iconography, warmth (no oranges or pinks), the main app's teal used in admin context.

**Contrast (WCAG AA verified pairs):** `#1B2A4A` on `#FFFFFF` (primary button); `#2E7D8F` on `#FFFFFF` (accent CTA); `#0F6E4A` on `#ECFDF5` (active badge); `#B45309` on `#FFFBEB` (pending badge); `#991B1B` on `#FEF2F2` (suspended badge).

## Typography

Body / label / caption inherit Angular Material typography (Inter via the Material `typography` config). Only `display` and `title` levels are brand-overridden.

- **Display (28px, Semibold, Inter)** — Dashboard headlines, page titles. Maps to Material `headline-1` level.
- **Display-sm (20px, Semibold, Inter)** — Section headers within surfaces, modal titles. Maps to `headline-2`.
- **Title (16px, Semibold, Inter)** — Card headers, table column groups. Maps to `title-medium`.
- **Mono (13px, JetBrains Mono)** — Invitation tokens, audit event IDs, API identifiers. Maps to a custom `mono` level.

## Layout & Spacing

Angular Material spacing scale inherited (8px-based). Content surfaces follow a **structured grid** — tables, card lists, and detail panels use consistent column alignment.

- **Sidebar nav** — Fixed `256px` wide, navy background. Implemented as `MatSidenav` in `mode="side"`. All admin navigation passes through the sidebar.
- **Main content** — Centered at `1280px` max width. Admin surfaces benefit from width for data tables, roster lists, and audit views.
- **Detail panels** — Slide-in `MatBottomSheet` or side panel from right for user detail, invitation review, audit event expand. Never full-screen. Uses Angular Material `panelClass` for custom elevation.
- **Table density** — 12px vertical row padding, 16px horizontal cell padding. Dense enough for at-a-glance scanning, spacious enough for legibility.

Touch targets ≥ 44px (consistent with Material accessibility guidance).

## Elevation & Depth

Minimal shadows. Cards use `surface-raised` + hairline border. Only the sidebar nav and modals use elevation.

- **Sidebar** — Full-height, `mat-elevation-z0`. Visual separation via background colour, not depth.
- **Invite card** — `mat-elevation-z1` (`0 1px 3px 0 rgba(0, 0, 0, 0.06)`) — barely perceptible lift for interactive action cards.
- **Modal overlay** — `rgba(18, 25, 38, 0.5)` — deep navy-tinted overlay, not generic black. Dialog uses `mat-elevation-z24`.
- **Dropdown menus** — `MatMenu` default elevation (`mat-elevation-z8`).

## Shapes

Tighter than Material defaults: `rounded/sm` (4px) for status badges and input fields; `rounded/md` (6px) for buttons, cards, and table chips; `rounded/lg` (8px) for dialogs and slide-in panels; `rounded/xl` (12px) for invite cards. Applied via Angular Material `shape` overrides.

Pill shapes (`rounded/full`) appear only on role pills (`MatChip`), never on buttons or cards.

## Components

Role Management uses the following Angular Material components with their default visual specs unchanged: `MatButton`, `MatTable`, `MatDialog`, `MatBottomSheet`, `MatMenu`, `MatSnackBar`, `MatTabs`, `MatAvatar`, `MatBadge`, `MatChips`, `MatInput`, `MatSelect`, `MatSort`, `MatPaginator`, `MatSidenav`, `MatProgressSpinner`, `MatDivider`. The contract: don't theme-customise these beyond the brand palette.

Brand-layer-overridden components:

- **Button (primary variant)** — `{colors.primary}` fill via `[color]="primary"`, `{colors.primary-foreground}` text, `{rounded.md}`. Hover uses `{colors.primary-hover}` via custom `_mat-button-overrides` mixin. Flat button style (no elevation on default state).
- **Button (secondary variant)** — White fill via `[color]="basic"` with custom class `.btn-secondary`, navy text, hairline border. Used for "Cancel", "Back", and non-primary actions.
- **Table header** — `{components.table-header}` via `MatHeaderRowDef` custom styles. Thin uppercase label row, subtle base background, 12px semibold text with `0.05em` letter-spacing. Applied via `::ng-deep .mat-header-cell` scoped to admin module.
- **Table row** — `{components.table-row}`. Hover background shifts to `{colors.surface-base}` via custom row style. Clickable rows use `cursor: pointer`.
- **Status badge** — `status-badge-{state}` variant via `MatChip` with custom `color` input mapped. 4px rounded, state-colour text on state-tinted background. Never uses icons — colour + text label only.
- **Invite card** — `{components.invite-card}` via `MatCard` with `mat-elevation-z1`. 12px rounded, hairline border `1px solid {border-hairline}`.
- **Sidebar navigation** — `{components.sidebar-nav}` via `MatSidenav` custom theme. Full-height navy panel (`#1B2A4A`). Active item: accent teal left border indicator (`2px solid {accent}`) + white text. Inactive: muted gray-blue (`#C8D0DD`). Hover: `rgba(255, 255, 255, 0.08)` overlay.
- **Role pill** — `{components.role-pill}` via `MatChip` with `color="primary"` and custom class `.role-pill`. Navy text on `#E8EDF5` background, 12px font, 4px rounded.
- **Input** — `MatInput` with `appearance="outline"`. Focus border colour overridden to `{accent}` via custom `$accent` palette.
- **Modal overlay** — `MatDialog` backdrop colour: `rgba(18, 25, 38, 0.5)`. Applied via `overlayContainer` custom theme.

## Do's and Don'ts

| Do | Don't |
|----|-------|
| Inherit Angular Material defaults for everything not in the brand layer | Override Material's colour system beyond `primary` and `accent` palette tokens |
| Use `{colors.accent}` only for "you are here / act here" indicators | Use accent for decorative chrome, hover affordances, or status |
| Use status colour **with text label** on every badge | Rely on colour alone for status — NGO compliance officers may have accessibility needs |
| Tables (`MatTable`) for data-heavy surfaces; cards for action-oriented surfaces | Cards for user rosters (use `MatSort` + `MatPaginator` tables for scannability) |
| Structured grids with consistent column alignment | Freeform card layouts on admin surfaces |
| Mono type (`JetBrains Mono`) for tokens, IDs, and identifiers | Red or blue link colours on navigation elements |
| `display` typography sparingly — dashboard headlines and section titles only | Set body text in `display` levels |
| `MatSidenav` as primary navigation — predictable, always visible on `>=1024px` | Hamburger menu as primary nav on desktop widths |

