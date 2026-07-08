# Mobile & Web UX Review, Redesign, and Implementation Plan

> Kaval Online — Travel Claim Module (mobile) & Case Management (web)
> Prepared as a senior product/UX/frontend review. All findings below are grounded in direct
> inspection of the actual source (file paths and line references included) — not generic advice.

---

## How to read this document

- **Part 1 (Mobile Travel Claim)** — issues found were fixed directly in this pass. Each item is
  marked `[FIXED]` or `[OPEN — Phase N]`.
- **Part 2 (Web Case Management)** — this is a **proposal**, not yet implemented. The Case Detail
  page is a core, already-in-use feature; rewriting it into a stepper is a meaningful architecture
  change with real blast radius (existing child components, existing tests, existing muscle memory
  for current users). The plan is complete and ready to execute, but implementation should start
  only after you confirm the direction in [Section 3](#3-proposed-redesign).

---

## 1. UX Review

### 1.1 Mobile — Travel Claim Module

| # | Issue | Severity | Screen(s) | Root Cause | Status |
|---|-------|----------|-----------|------------|--------|
| 1 | No icon library anywhere in the mobile app | High | All screens | `react-native-vector-icons` (or any icon lib) was never added as a dependency; zero icon imports existed in the codebase | **[FIXED]** |
| 2 | Bottom tab bar (Today/Cases/More) has no icons | High | Global nav | Consequence of #1 — `MainTabNavigator.tsx` had no `tabBarIcon` | **[FIXED]** |
| 3 | Travel claim form has no page heading/context | Medium | `TravelClaimFormScreen` | Screen jumped straight into "Claim date" with no title, no distinction between create/edit/view | **[FIXED]** |
| 4 | Native date picker always mounted inline | Medium | `TravelClaimFormScreen` | `<DateTimePicker>` rendered unconditionally instead of behind a styled trigger, visually clashing with the surrounding custom-styled inputs (raw OS widget vs. app theme) | **[FIXED]** — tap-to-open pattern with icon |
| 5 | No required-field indicators | Medium | `TravelClaimFormScreen` | Labels gave no visual cue about which fields are mandatory (Start location, Destination, Transport mode, Amount, Linked cases, Claim date) | **[FIXED]** — `*` markers + "Fields marked * are required" subtitle |
| 6 | No placeholder/example text | Low | `TravelClaimFormScreen` | Text inputs had no `placeholder`, so a first-time user has no idea what format is expected | **[FIXED]** for Start location/Destination/Auto number/Amount |
| 7 | No currency symbol on Amount field | Medium | `TravelClaimFormScreen` | Raw numeric input with no unit context (list screen already showed ₹, form didn't — inconsistent) | **[FIXED]** — ₹ prefix, matches list screen |
| 8 | No empty state for "Linked cases" | Medium | `TravelClaimFormScreen` | If `assignedCases` is empty, the section rendered a label and nothing else | **[FIXED]** |
| 9 | No explanation of *why* a case must be linked | Low | `TravelClaimFormScreen` | Business rule (claim must relate to a case) was silently enforced by validation with no upfront context | **[FIXED]** — helper text added |
| 10 | Save/Submit buttons buried at the bottom of a scrollable form | Medium | `TravelClaimFormScreen` | Buttons were the last elements inside the `ScrollView` instead of a sticky footer — on a form with many linked cases, users must scroll to find the primary action | **[FIXED]** — moved to a fixed footer outside the scroll area |
| 11 | Receipt/attachment rows show plain filename text with no success affordance | Low | `TravelClaimFormScreen` | No icon/color to confirm "this file is attached and good" | **[FIXED]** — check icon, green tint |
| 12 | "Take photo" / "Choose file" buttons visually identical except for text | Medium | `TravelClaimFormScreen` | No icon differentiation between two adjacent, similarly-styled buttons | **[FIXED]** — camera / file icons |
| 13 | Validation only surfaces in one error region at the bottom, after a submit attempt | Medium | `TravelClaimFormScreen` | `validateForm()` runs once at save-time; there's no per-field, real-time feedback (e.g. red border on an invalid amount while typing) | **[OPEN — Phase 2]**, see 5.2 |
| 14 | Linked-case picker has no search and silently caps at 100 cases | Medium | `TravelClaimFormScreen` | `caseApiService.listAssignedCases(1, 100)` — a field worker with >100 assigned cases would never see the rest, with no indication | **[OPEN — Phase 2]** |
| 15 | No pull-to-refresh / search on the Travel Claims list | Low | `TravelClaimsListScreen` | List screen already does empty-state, retry-on-error, and pull-to-refresh well (good pattern) — search would help at scale but isn't urgent given typical claim volume | **[OPEN — Phase 4]** |
| 16 | No contextual offline banner | Low | `TravelClaimFormScreen` | Only a small `SyncChip` shows sync state; no upfront "You're offline — this will save to your device" messaging when connectivity drops mid-edit | **[OPEN — Phase 2]** |
| 17 | Jest test suite for this exact screen was silently broken | High (infra) | `TravelClaimFormScreen.test.tsx` | `jest.config.js`'s `transformIgnorePatterns` never accounted for `react-native-image-picker` (added earlier this session) — the whole suite failed to *parse*, so no one could have seen a red test for this screen | **[FIXED]** |

### 1.2 Web — Case Management

| # | Issue | Severity | Screen(s) | Root Cause | Status |
|---|-------|----------|-----------|------------|--------|
| 1 | Case Detail page is one long flat scroll of unrelated sub-features | High | `case-detail-placeholder.component.html` | Confirmed by direct inspection: a single `mat-card` stacks summary fields → stage-transition form → assigned-worker/match info → handoff whisper → case notes timeline → interventions → court sittings → transfer form, with only plain `<h3>` section headers and no visual grouping, tabs, or stepper | **[OPEN]** — see redesign proposal |
| 2 | No visual indication of case-lifecycle progress | High | Case Detail | `currentStage` renders as a plain `<p><strong>Stage:</strong> X</p>` line — no progress bar, no stepper, no sense of "5 of 7 stages complete" | **[OPEN]** |
| 3 | Case Create is a flat, unexplained form | Medium | `case-create.component.html` | 9 fields (crime number, ST number, beneficiary name/age/contact, offence type/classification, domicile, first-time-offender) in one form, no section grouping, no inline help explaining POCSO-specific terms (e.g. "offence classification: Heinous/Serious/Petty") to a first-time user | **[OPEN]** |
| 4 | No terminology help for domain-specific fields | Medium | Case Create, Case Detail | Terms like "ST Number", "Domicile", "Offence Classification" are presented with zero explanation — a new user with no POCSO/child-protection background has no way to know what's expected | **[OPEN]** |
| 5 | Stage transition has no explanation of what the next stage entails | Medium | Case Detail → "Change stage" section | The form just offers a `<mat-select>` with one option (the only valid next stage) and a notes field — no description of what that stage means or what will be expected next | **[OPEN]** |
| 6 | No draft/auto-save on the create form | Low | Case Create | If a user's session drops or they navigate away mid-form, all entered data is lost | **[OPEN — Phase 4]** |

---

## 2. UI Review

### 2.1 Mobile

- **Icons**: fixed — `react-native-vector-icons` (MaterialCommunityIcons family) added, Android font
  linking wired via `fonts.gradle`, and a single `Icon` wrapper component
  (`src/components/Icon.tsx`) centralizes font-family choice so the whole app draws from one
  family — this also gives a design-system choke point for future icon work.
- **Color/typography**: reasonably consistent *within* Travel Claim (`#0d6e6e` primary,
  `#344054`/`#101828`/`#475467` text scale, 8px-based spacing) — the problem was never inconsistent
  *values*, it was **missing elements** (no heading scale beyond one `label` style, no icon-driven
  visual hierarchy).
- **Responsiveness**: screens use `ScrollView` + flex layout with no fixed pixel widths — this
  already adapts reasonably across phone sizes. Not tested against tablet layouts (see QA section).
  No `SafeAreaView` used, but React Navigation's stack header already accounts for safe-area insets,
  so this is low-risk, not zero-risk on notched devices in full-screen modals.
- **Accessibility**: existing screens already do many things right — `accessibilityRole`,
  `accessibilityLabel`, and `accessibilityState` are used consistently on interactive elements, and
  there's a dedicated `AccessibleErrorRegion` component for screen-reader-friendly error
  announcements. Gaps: decorative icons added in this pass have no `accessibilityLabel` (correct —
  they're paired with text, not standalone controls) but should be spot-checked with a screen reader
  once the rebuild is testable on-device.

### 2.2 Web — Case Management

- **Layout**: `case-detail-placeholder.component.html` (120 lines) is a single `mat-card` with
  6 stacked sub-sections and no `mat-tab`/`mat-stepper`/`mat-expansion-panel` anywhere. This is the
  literal, confirmed cause of "everything expands vertically."
  Note: the component is named `case-detail-*placeholder*` — the placeholder naming plus its
  minimal structure suggests it may itself have been intended as a temporary stand-in that never
  got replaced; that context is directly relevant to how comfortable it should be to redesign.
- **Create form**: `case-create.component.html` (94 lines) — flat, single-column
  `mat-form-field` list, no visual grouping between "case identifiers" (crime/ST number) vs.
  "beneficiary details" vs. "offence details," even though those are conceptually distinct groups.

---

## 3. Proposed Redesign

### 3.1 Mobile Travel Claim — already applied in this pass

```
┌─────────────────────────────────┐
│ 🚌  New travel claim            │  ← icon + title + "* required" subtitle
│     Fields marked * required    │
├─────────────────────────────────┤
│ Claim date *        📅 12/07/26 │  ← tap target, opens native picker on demand
│ Start location *    [_________] │
│ Destination *       [_________] │
│ Transport mode *     (chips)    │
│ Amount *            ₹[______]   │  ← currency-prefixed
│ Linked cases *                  │
│   ℹ Select case(s) this trip... │  ← helper text
│   [empty state if none assigned]│
│   ☑ CR-1 · ST-1                 │
│ Notes                           │
│ Receipt (optional/required)     │
│   ✓ receipt.jpg                 │  ← success icon
│   [📷 Take photo] [📄 Choose file]│
├─────────────────────────────────┤  ← sticky footer, not part of scroll
│ [💾 Save draft]  [✈ Submit]     │
└─────────────────────────────────┘
```

### 3.2 Web Case Management — proposed (not yet implemented)

**Recommended pattern: Stepper Wizard for the case lifecycle, Tabs for the supporting sub-features.**

Rationale: the case *lifecycle* (Process Initiation → Preliminary Assessment → Social Investigation
→ Community Based Intervention → Placement → Reintegration → Termination/Exclusion) is genuinely
sequential and benefits from a stepper. The *supporting* features currently stacked on the same page
(Notes, Interventions, Court Sittings, Transfer) are **not** sequential — they're always-available
utilities a user might reach for at any point in a case's life. Forcing those into stepper steps
would be wrong; they belong in tabs alongside the stepper, not inside it.

```
┌────────────────────────────────────────────────────────────┐
│ Case CR-1 / ST-1 — Priya's case                    [⋮ menu]│
│ ●───●───●───○───○───○───○   Stage 3 of 7: Social Investig. │  ← stepper + progress
├────────────────────────────────────────────────────────────┤
│ [ Case Timeline ] [ Notes ] [ Interventions ] [ Court ]     │  ← tabs for utilities
│ [ Transfer ]                                                │
├────────────────────────────────────────────────────────────┤
│  (selected tab content, or current-stage form when          │
│   "Case Timeline" tab is active)                            │
│                                                              │
│  ℹ What is Social Investigation?                             │
│  A structured assessment of the child's family, social,      │
│  and living context, completed by the assigned worker.       │
│                                                              │
│  Required for this stage:                                    │
│  ☐ Family structure recorded    ☐ Home visit completed        │
│  ☐ Risk assessment submitted                                  │
├────────────────────────────────────────────────────────────┤
│                            [Back]   [Save draft]  [Next →]  │
└────────────────────────────────────────────────────────────┘
```

Key elements:
- **Stepper header** — always visible, shows completed (●), current (●, highlighted), and pending
  (○) stages, plus "Stage N of 7" and the stage name in plain language.
- **"What is this stage?" info card** — one or two sentences, sourced from a small static lookup
  table (`STAGE_DESCRIPTIONS`), not a new CMS — this alone directly answers the brief's requirement
  that every stage communicate "what it is, why it exists, what's required, what happens next."
  See [4.3 Workflow Documentation](#4-workflow-analysis) for the actual stage content to use.
- **Tabs for utilities** — Notes/Interventions/Court/Transfer move from stacked sections to tabs,
  each keeping its *existing* child component (`app-case-notes-timeline`, `app-case-interventions`,
  `app-case-court-sittings`) unchanged internally — this is a wrapping/layout change, not a rewrite
  of those features.
- **Sticky footer** — Back / Save draft / Next, always visible, mirroring the mobile pattern above.

**What does *not* change:** the backend stage-transition API, validation rules, or the underlying
child components' internals. This is a presentation-layer reorganization, not a data-model change.

---

## 4. Workflow Analysis — Case Lifecycle

| Stage | Plain-language purpose | Typical inputs | Who acts | Notes |
|-------|------------------------|----------------|----------|-------|
| Process Initiation | The case is opened and basic identifying facts are recorded | Crime number, ST number, beneficiary details, offence classification, domicile | Director/Coordinator | Entry point; case is assigned to a field worker here |
| Preliminary Assessment | An initial, quick read on urgency and risk | Risk indicators, immediate safety concerns | Assigned worker | Feeds the crisis-queue prioritization |
| Social Investigation | A structured assessment of the child's family/social context | Home visit notes, family structure, risk assessment | Assigned worker | Usually the most time-consuming stage |
| Community Based Intervention | Support services are engaged while the child stays in their community | Intervention records (counselling, training, referral) | Assigned worker + external providers | Maps to the existing Interventions feature |
| Placement | If needed, formal placement (shelter/foster/institutional) is recorded | Placement type, facility, dates | Coordinator/Director approval likely needed | Highest-sensitivity stage |
| Reintegration | Preparing the child's return to family/community | Reintegration plan, follow-up schedule | Assigned worker | |
| Termination/Exclusion | Case is formally closed | Closure reason, final outcome | Director | Terminal — no further transitions (already enforced: `stageIsTerminal()`) |

This table is the literal content to put behind the "What is this stage?" info cards proposed in
3.2 — it doesn't require new backend fields, just a static frontend lookup keyed by stage name.

---

## 5. Design System Recommendations

### 5.1 Mobile

| Component | Purpose | Status |
|-----------|---------|--------|
| `Icon` (`src/components/Icon.tsx`) | Single choke point for all icon rendering | **Added this pass** |
| `StatusChip` | The Draft/Submitted/Approved/Returned chip pattern is duplicated identically in `TravelClaimFormScreen` and `TravelClaimsListScreen` (`statusStyle()` function copy-pasted) | Recommend extracting to a shared `StatusChip` component — same visual language already exists, just not reused |
| `EmptyState` | The empty-state pattern added to Travel Claim's linked-cases section should become a shared component (icon + message), reused across Cases list, Notifications, etc. | Recommend for Phase 2 |
| `StickyFooter` | The sticky action-button footer pattern (used now in Travel Claim) is a good candidate for reuse in Case Create/Detail forms | Recommend for Phase 2 |

### 5.2 Web

| Component | Purpose |
|-----------|---------|
| `Stepper` | Angular Material's `MatStepper` — use directly for the case-lifecycle redesign (3.2) rather than a custom build |
| `StageInfoCard` | New, small reusable card component for "what is this stage" content, driven by the lookup table in Section 4 |
| `StatusBadge` | Consolidate the several ad hoc status-chip CSS blocks already scattered across budget/invitation/travel-claim pages (see earlier session work on invitation status chips) into one shared component |

---

## 6. Implementation Plan

### Phase 1 — Critical UI fixes *(this pass)*
- [x] Mobile icon system (library + Android linking + tab bar + Travel Claim icons)
- [x] Travel Claim: heading, required markers, currency prefix, empty state, sticky footer,
      tap-to-open date picker, receipt icons
- [x] Fixed broken Jest transform config that had silently disabled this screen's test coverage

### Phase 2 — UX improvements *(proposed, not started)*
- Per-field real-time validation on Travel Claim (highlight invalid field inline, not just a
  bottom error region)
- Search/pagination for the linked-case picker (currently hard-capped at 100)
- Shared `StatusChip`/`EmptyState`/`StickyFooter` components (mobile)
- Contextual offline banner on Travel Claim
- `StageInfoCard` content lookup table (Section 4) — buildable independently of the stepper itself

### Phase 3 — Workflow redesign *(proposed, requires sign-off before starting)*
- Case Detail → Stepper + Tabs redesign (Section 3.2)
- Case Create → grouped sections with inline terminology help
- Stage-transition UX: show the next stage's requirements before submitting the transition

### Phase 4 — Advanced enterprise enhancements
- Draft/auto-save on Case Create
- Activity/audit timeline surfaced directly on Case Detail (data already exists via audit log —
  this is a presentation layer addition)
- Travel Claims list search/filter
- Keyboard shortcuts (web)

---

## 7. Developer Tasks

**Task: Case Detail stepper + tabs redesign**
- Files likely affected: `case-detail-placeholder.component.ts/html/scss`, new
  `stage-info-card.component.ts`, possibly a new `case-detail-shell.component.ts` wrapping the
  existing child components (`case-notes-timeline`, `case-interventions`, `case-court-sittings`)
  unchanged.
- Acceptance criteria: existing stage-transition, transfer, notes/interventions/court-sitting
  functionality all continue to work unchanged; case lifecycle progress is visible at a glance;
  each stage shows a plain-language description before the user acts.
- Testing scenarios: case at each of the 7 stages renders the correct stepper position; terminal
  stage shows no "next" step; existing `case-detail-placeholder.component.spec.ts` assertions
  continue to pass (may need selector updates for the new wrapper markup).
  Prerequisite/dependency: none — the underlying API is unchanged, this is presentation-only.
- Priority: High (this is the core reported pain point) — but **do not start without confirming
  direction**, given the blast radius on a used feature.

**Task: Shared mobile design-system components**
- Files likely affected: new `src/components/StatusChip.tsx`, `EmptyState.tsx`,
  `StickyFooter.tsx`; refactor `TravelClaimFormScreen.tsx`/`TravelClaimsListScreen.tsx` to use them.
- Acceptance criteria: no visual regression on either screen; `statusStyle()` duplication removed.
- Testing: existing Jest suites for both screens continue to pass unchanged.
- Priority: Medium. Dependency: none.

**Task: Linked-case picker search/pagination**
- Files likely affected: `TravelClaimFormScreen.tsx`, `CaseApiService.ts` (mobile).
- Acceptance criteria: a field worker with >100 assigned cases can find and link any of them, not
  just the first 100.
- Testing: mock >100 cases, confirm search filters correctly and no case is unreachable.
- Priority: Medium (currently a silent data-loss-adjacent bug, not just cosmetic). Dependency: none.

---

## 8. QA Test Cases

### Mobile — Travel Claim
1. **Icons render** — open the app on a real/emulated Android device after rebuild; confirm tab bar
   icons (calendar/folder/dots) and Travel Claim icons (camera, file, save, send, calendar) all
   render as glyphs, not empty boxes.
2. **Required-field markers** — confirm `*` appears next to Claim date, Start location, Destination,
   Transport mode, Amount, Linked cases; confirm Auto number only shows `*` when Transport mode = Auto.
3. **Empty state** — mock a field worker with zero assigned cases; confirm the empty-state message
   renders instead of a blank section.
4. **Sticky footer** — on a device with many linked cases (long scroll), confirm Save/Submit buttons
   remain visible without scrolling.
5. **Date picker** — tap the claim-date field; confirm the native picker opens on demand and closes
   after selection (not permanently visible).
6. **Receipt success state** — attach a receipt via camera and via file picker separately; confirm
   the check-icon + filename appears for both paths.
7. **Accessibility** — with TalkBack/VoiceOver enabled, confirm all buttons announce their
   `accessibilityLabel` correctly and the new icons don't introduce redundant announcements.
8. **Regression** — full existing Jest suite (`npx jest`) passes; offline-draft creation,
   auto-number validation, and read-only status views all still work as before.

### Web — Case Management (once Phase 3 is implemented)
1. Case at each of the 7 stages shows correct stepper position and progress percentage.
2. Terminal-stage case shows no "Next" action, matches existing `stageIsTerminal()` behavior.
3. Switching tabs (Notes/Interventions/Court/Transfer) preserves scroll position and doesn't
   reload unrelated data.
4. Stage-info card content matches the Section 4 table for all 7 stages.
5. Existing case-detail component spec tests pass (or are updated 1:1 for new markup, not
   deleted/skipped).
6. Responsive: stepper collapses to a compact/vertical form on tablet and mobile-web widths.
7. Accessibility: stepper is keyboard-navigable, each step announces its name and state
   (completed/current/pending) to screen readers.

---

## 9. Final Recommendation — Executive Summary

**Current maturity**: functionally solid, visually unfinished. The underlying data model, API
surface, and business-rule enforcement (validation, role gating, audit trail) are mature and
correct — the gaps found in this review are almost entirely in the *presentation* layer, not the
domain logic. That's a good position to redesign from: the risk of a UI/UX pass breaking core
business behavior is low.

**Key usability risks**:
1. The Case Detail page's flat, unstructured layout is the single biggest source of "I don't know
   what to do next" — confirmed directly in the markup, not just user perception.
2. The mobile app's total absence of icons made it look meaningfully less finished than its actual
   functional completeness (2FA, offline sync, camera capture, sync conflict handling are all
   already implemented — they just don't *look* implemented).
3. A silently-broken test suite (Jest transform config) meant regressions on this exact screen
   could have shipped undetected — now fixed as a side effect of this review.

**Highest-impact improvements** (in order):
1. Case Detail stepper + tabs (Section 3.2) — directly answers the primary complaint.
2. Mobile icon system (done) — highest visual-impact-per-effort ratio of anything in this review.
3. Stage-info cards (Section 4) — cheap to build (static content), directly serves the
   "zero business knowledge" requirement.

**Expected benefit after redesign**: a first-time user should be able to look at the Case Detail
page and, within seconds, answer "what stage is this case at, what's done, what's next, and where
do I go for notes/court dates/etc." — none of which is currently answerable without prior training.

**Recommendation for next step**: proceed with Phase 2 (mobile design-system extraction) as
low-risk follow-up work, and get explicit sign-off on the Case Detail stepper direction (Section
3.2) before Phase 3 implementation begins, given it touches a core, currently-used feature.
