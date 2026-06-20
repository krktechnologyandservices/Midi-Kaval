---
name: Kaval
status: final
updated: 2026-06-12
sources:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md
  - _bmad-output/specs/spec-kaval-online/SPEC.md
---

# Kaval — Experience Spine

> Multi-surface: React Native mobile (field) + Angular PWA web (supervisors). `DESIGN.md` owns visual tokens; this spine owns behavior. **Spines win on conflict** with `mockups/`.

## Foundation

| Surface | Users | Stack |
|---------|-------|-------|
| Mobile app | Social Worker, Case Worker | React Native, iOS + Android |
| Web app | Project Coordinator, Project Director | Angular PWA + Angular Material, responsive ≥768px primary |

Light mode default `[ASSUMPTION]`; dark supported. Regulated posture: pathway, reintegration, support — not clinical scoring.

## Information Architecture

### Mobile

| Surface | Reached from | Purpose | PRD | Mock |
|---------|--------------|---------|-----|------|
| Today (Command Strip) | App open / Today tab | Visit queue, court countdown, sync | FR-12, UJ-1 | `mockups/command-strip-today.html` |
| Active visit | Start Visit | GPS, notes, discreet mode | FR-8–11 | — |
| Case detail | Cases / strip tap | Profile, Handoff Whisper, timeline | FR-7, FR-13 | — |
| Court schedule | Today header / filter | Upcoming sittings | FR-15 | — |
| Travel claim | More tab | Draft/submit + receipts | FR-18 | — |
| Notifications | Bell | Read/unread items | FR-19 | — |
| Login / OTP | App launch | Email/password + OTP 2FA | FR-1 | — |
| Sync queue | More → Sync | Retry failed uploads | FR-11 | — |

Tabs: **Today · Cases · More**.

### Web

| Surface | Reached from | Purpose | PRD | Mock |
|---------|--------------|---------|-----|------|
| Crisis Queue | App open (Coordinator) | Triage risks | FR-21, UJ-2 | `mockups/crisis-queue.html` |
| Dashboard | Sidebar | Charts (secondary) | FR-20 | — |
| Case registry | Sidebar | Search, create, merge | FR-3–6, UJ-3 | — |
| Case detail | Registry row | Full case, assign | FR-3, FR-7 | — |
| Reports | Sidebar | Generate/export | FR-22 | — |
| Legends | Sidebar | Master data CRUD | FR-23 | — |
| Admin | Sidebar (Director) | Users, audit, approvals | FR-1, FR-25 | — |
| Login / OTP | App launch | Email/password + OTP | FR-1 | — |

## Voice and Tone

| Do | Don't |
|----|-------|
| "Saved on this device." | "Offline mode engaged" |
| "Possible match — review before saving." | "Error: duplicate" |
| "Enter the 6-digit code from your email." | "OTP verification required!!!" |
| "Court sitting in 2 days — no prep note yet." | "Warning!!!" |
| "Workload by area" | "Top performer" |

## Component Patterns

| Component | Surface | Behavioral rules |
|-----------|---------|------------------|
| Command Strip row | Mobile Today | Wraps `{components.command-strip-card}`. One row per visit + overdue. `{colors.sync-local}` when unsynced. |
| Crisis Queue row | Web home | Uses `crisis-row-{severity}`. Badge text + icon: Overdue, Court 48h, Handoff, Claim. Sorted critical → warning → info → neutral. |
| Handoff Whisper | Case detail | ≤7 days post-transfer. Max 3 lines + "View full timeline." |
| Duplicate match sheet | Create Case | **Blocks save.** Actions: Open existing, Merge (Coordinator only), Cancel. No duplicate create path. Focus trap; `aria-modal`. |
| Court sitting row | Both | Upcoming / Attended / Postponed. Past-due Upcoming → critical queue. |
| Discreet header | Mobile visit | Uses `{components.discreet-header}`. Default on POCSO/sensitive. Expand requires OTP re-entry (same session OTP acceptable within 5 min). |
| Experience brief card | Mobile v1.1 | `{components.experience-brief-card}`. Advisory footer mandatory. |
| Sync chip | Mobile field | Maps to `sync-chip-{state}`. Error tap → Sync queue. |
| OTP input | Both login | Single accessible field or 6-box with group label; errors announced via `aria-live`. |

## State Patterns

| State | Surface | Treatment |
|-------|---------|-----------|
| Cold open | Mobile Today | Cached Command Strip; skeleton if empty. Stale >24h → soft banner. |
| Offline visit | Mobile Active visit | `{colors.sync-local}` chip; More → Sync queue. |
| Sync pending | Mobile | `{colors.sync-pending}` chip while uploading. |
| Sync error | Mobile | `{colors.sync-error}` chip; tap retry. |
| Empty Crisis Queue | Web | "No urgent items." Links to Dashboard + Cases. |
| Duplicate on create | Web/mobile | Duplicate match sheet; save disabled until resolved. |
| Court miss escalation | Web Crisis Queue | `crisis-row-critical` until status fixed. |
| Empty court schedule | Mobile | "No sittings this week." |
| Empty travel drafts | Mobile More | "No claims yet" + create CTA. |
| Empty notifications | Mobile/web | "You're up to date." |
| Reports generating | Web Reports | Progress indicator; disable duplicate export. |
| Legends empty category | Web Legends | Empty table + add row CTA. |
| Admin forbidden | Web | Social Worker mobile-only roles: web login shows "Use the mobile app for your role." |
| Session expired | Both | Forced logout screen; return to Login/OTP. |
| Dashboard loading | Web | Skeleton widgets matching layout. |
| AI low confidence | Mobile v1.1 | "Only 2 similar outcomes — use judgment." |

## Interaction Primitives

**Mobile:** Tap primary actions. Pull-to-refresh on Today and Cases. **Banned:** infinite scroll on visits, gamified badges.

**Web:** Click row → Case. `/` focuses registry search. **Banned:** hover-only on touch widths; modal stack >1.

**Modals:** Duplicate sheet and OTP re-auth for discreet expand — focus trap, Esc closes only when non-blocking, return focus to trigger.

**Motion:** Respect `prefers-reduced-motion`; sync chip and crisis row transitions instant when reduced.

## Accessibility Floor

- WCAG 2.2 AA web; platform APIs on mobile.
- Crisis rows: severity **badge text** (Overdue, Court 48h, Handoff, Claim) — never color alone.
- Sync chips: state text in label ("Saved on this device", "Synced", "Uploading", "Sync failed").
- OTP: labelled input group, `aria-live="polite"` for errors, timeout announces "Code expired — request new code."
- Discreet expand: screen reader can access full detail after OTP re-auth; collapsed state announces "Limited detail mode."
- Dynamic type / 200% zoom: Command Strip wraps; crime numbers never truncate.
- Tap targets per `DESIGN.md` Layout.

## Inspiration & Anti-patterns

- **Lifted:** field-service today-queue home; healthcare handoff bundle (pattern only).
- **Rejected:** performance leaderboards, chatbot home, WhatsApp-style case threads.

## Responsive & Platform

| Breakpoint | Web behavior |
|------------|--------------|
| `≥1024px` | Sidebar + optional split detail |
| `768–1023px` | Collapsed sidebar |
| `<768px` | Emergency web read; mobile app primary for field |
| 200% zoom | Crisis queue rows wrap; no horizontal scroll |

## Key Flows

### Flow 1 — Priya's visit day (UJ-1)

1. Priya opens app (post-login).
2. **Today** Command Strip — visit 1, court banner, Handoff Whisper on expand.
3. GPS unverified → "Capture landmark before navigate."
4. Completes visit offline → `{colors.sync-local}`.
5. **Climax:** Sync → `{colors.sync-synced}`; Ravi's queue updates.
6. Visits 2–3 remain.

Failure: sync error chip + retry.

### Flow 2 — Ravi prevents court miss (UJ-2)

1. Crisis Queue default — amber Court 48h row.
2. Opens Case → adds prep note for worker.
3. **Climax:** Past-due → critical row + notifications.
4. Worker marks Attended → row clears.

### Flow 3 — Duplicate blocked (UJ-3)

1. Ravi types Crime Number on create.
2. Duplicate match sheet — existing Case summary.
3. **Open existing** or **Merge** (Coordinator). Field staff cannot create duplicate.
4. **Climax:** Single active Crime/ST record.

### Flow 4 — Discreet POCSO visit

1. POCSO Case opens — discreet header default.
2. Initials + crime number only on visit screen.
3. Voice note or minimal complete.
4. Full detail — expand after OTP re-entry.

### Flow 5 — Anil clears intervention overdue (FR-14)

1. Anil (Case Worker) receives push: intervention overdue.
2. Opens notification → Case → intervention row.
3. Updates status + outcome note.
4. **Climax:** Overdue alert clears; Crisis Queue intervention count drops if applicable.

### Flow 6 — Director approves travel claim (FR-18)

1. Meera (Director) opens Crisis Queue — neutral claim row.
2. Opens claim → reviews receipts (mandatory images).
3. Approves or returns with comment.
4. **Climax:** Priya receives approval push; monthly total updates.

### Flow 7 — Login with OTP (FR-1)

1. User enters email + password.
2. OTP screen — 6-digit code from email.
3. **Climax:** Valid OTP → role-appropriate home (Command Strip or Crisis Queue).
4. Invalid/expired → `aria-live` error; resend after 60s.

Failure: deactivated account → "Contact your coordinator" (no OTP sent).

## Open Questions

1. Hindi UI strings — v1 English only or bilingual?
2. Tab bar iconography — custom vs platform default.

## Assumptions Index

- Light mode default; English v1.
- Discreet expand re-auth = OTP within same login session rules.
- v1.1 Experience brief above Start Visit on strip row.
