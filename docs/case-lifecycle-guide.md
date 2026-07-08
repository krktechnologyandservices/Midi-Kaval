# How a Case Travels From Creation to Close-Out

> A plain-language walkthrough of the Case Management workflow — what happens at each stage, who
> does it, what data is captured, and where in the app it happens. Grounded directly in the actual
> stage data models and business rules in the codebase (file references included), not a generic
> description.

---

## The short version

A case moves through **6 stages, strictly in order, one at a time**:

```
1. Process Initiation → 2. Maintenance & Development → 3. Inter-Sectoral Approach
   → 4. Rehabilitation → 5. Reintegration → 6. Termination / Exclusion
```

Two different roles do two different kinds of work on the same case, at the same time:

- **Field workers** (SocialWorker / CaseWorker, on the **mobile app**) do the on-the-ground work:
  home visits, case notes, interventions, court appearances. This never stops throughout the
  case's life — it's not tied to a specific stage.
- **Coordinators and Directors** (on the **web app**, via the Case Detail page) own the formal
  case record: they enter each stage's specific data, decide when the case is ready to move to the
  next stage, and handle transfers between field workers.

Both feed into the *same* case, but only Coordinator/Director actions actually move it through the
6-stage progression.

---

## Step 1 — Creating the case

**Where**: Web app → Cases → New Case (`case-create.component`)
**Who**: Director or Coordinator

A case starts life with these fields (this is *all* Stage 1 asks for — everything else comes
later, stage by stage):

| Field | What it's for |
|-------|----------------|
| Crime Number | The police record's case number |
| ST Number | Sessions Trial number (assigned by the court) |
| Beneficiary name / age / contact | The child at the center of the case — encrypted at rest |
| Type of offence | Free text description |
| Offence classification | Petty / Serious / Heinous |
| Domicile | Urban / Rural / Coastal / Tribal / Slum |
| First-time offender | Yes/No |

The case is created at stage **Process Initiation** and must then be **assigned to a field
worker** — that assignment is what makes the case show up on that worker's mobile app.

---

## Step 2 — The stage-by-stage record

This is the part the redesigned Case Detail page (the stepper you now see) is built around. Each
stage below has its **own dedicated data form**, and — this is the important part — **that form is
only reachable while the case is actually sitting at that exact stage**. Once the case moves
forward, the previous stage's form becomes inaccessible (the API itself enforces this: e.g.
`CaseStage2DataService.GetAsync` throws "not found" the instant `currentStage` no longer equals
`MaintainAndDevelopment`). So there's no going back to tidy up an earlier stage's paperwork after
the fact — get it right before moving on.

### Stage 1 — Process Initiation
Already covered above — this *is* stage 1. The case sits here until a Coordinator/Director judges
enough is known to formally open the file and advance it.

### Stage 2 — Maintenance & Development
*"Is the case being actively managed and developed?"*

| Field | Plain meaning |
|-------|----------------|
| Bio-psycho-social assessment | The child's biological, psychological, and social situation |
| ICP records | Individual Care Plan — the personalized plan of action |
| Life skill training | Training/skills programs the child is enrolled in |
| Parent management | Work being done with the parents/guardians |
| Group work | Group-based interventions |
| Community program attendance | Participation in community programs |
| PMA status | Post-mediation assessment status |
| Overall progress | Free-text summary of how things are going |

All fields are optional text fields — fill in whatever applies; there's no single required field
that blocks you.

### Stage 3 — Inter-Sectoral Approach
*"Who else is involved in supporting this child?"*

Unlike the other stages, this one is a **list**, not a single form — you can add as many support
records as apply:

| Field | Plain meaning |
|-------|----------------|
| Support type | e.g. medical, educational, legal, psychological (required) |
| Provider name | Who's providing it |
| Notes | Free text |
| Provided status | Whether the support has actually been delivered yet |

This is where coordination across departments/agencies (health, education, police, etc.) gets
recorded.

### Stage 4 — Rehabilitation
*"Where is the child being placed for rehabilitation?"*

| Field | Plain meaning |
|-------|----------------|
| Placement type | Required — the kind of placement (shelter/foster/institutional/etc.) |
| Institution name | Which facility |
| Address | Where |
| Start date | When the placement began |

### Stage 5 — Reintegration
*"How is the child returning to family/community?"*

| Field | Plain meaning |
|-------|----------------|
| Reintegration level | Required — how the reintegration is being handled |
| Institution details | Any relevant institutional context |

### Stage 6 — Termination / Exclusion
*"The case is closing — why and how?"*

| Field | Plain meaning |
|-------|----------------|
| Termination/exclusion type | Required — the formal closure category |
| JJB details | Juvenile Justice Board details, if applicable |
| Exclusion reason | Why the case is being excluded/closed (if applicable) |
| Report attachment | An optional final report document |

**This is the terminal stage** — once here, there is no "next stage," and the transition form
disappears from the Case Detail page entirely (this is enforced by `stageIsTerminal()` checking
`currentStage === 'TerminationExclusion'`).

---

## Step 3 — Moving to the next stage

**Where**: Web app → Case Detail → "Change stage" section
**Who**: Director or Coordinator

Moving a case forward is a separate, deliberate action from filling in a stage's data — the system
doesn't force you to complete Stage 2's fields before letting you advance to Stage 3. Practically,
though, you should finish a stage's data entry *before* advancing, since you lose access to that
form afterward (see above).

To advance:
1. Open the case → the stepper shows your current position and an info card explaining what the
   *current* stage means.
2. The "Change stage" panel always offers exactly one option: the single valid next stage (the
   system won't let you skip stages).
3. Add an optional note explaining the transition.
4. Submit — the case's `currentStage` updates immediately and the stepper advances.

---

## Meanwhile — the work that happens regardless of stage

These aren't stage-gated at all — they're available for the whole life of the case:

- **Visits** (mobile) — field workers schedule, start, and complete home/field visits at any time.
- **Case notes** (web + mobile) — a running timeline of observations, always open for new entries.
- **Interventions** — counselling, training, referrals — logged as they happen.
- **Court sittings** — scheduled and tracked independently of the case's stage.
- **Transfer** — a case can be reassigned to a different field worker at any point, regardless of
  stage, via the same Case Detail page's "Transfer case" panel. This also captures a handoff note
  (prior actions, open items, next visit purpose) so the new worker isn't starting blind.

All four of these live in the expandable sections below the stepper on the redesigned Case Detail
page — they're utilities you reach for as needed, not steps you walk through in order.

---

## Quick reference — who does what

| Action | Role | App |
|--------|------|-----|
| Create a case | Director, Coordinator | Web |
| Assign/transfer a case | Director, Coordinator | Web |
| Enter stage 2–6 data | Director, Coordinator | Web |
| Move a case to its next stage | Director, Coordinator | Web |
| Conduct visits, add notes, log interventions | SocialWorker, CaseWorker | Mobile |
| View case list, search cases | Director, Coordinator | Web |
| View assigned cases | SocialWorker, CaseWorker | Mobile |

---

## Where to go for more detail

- [User Manual §5 — Case Management](user-manual.md#5-case-management): the click-by-click,
  feature-by-feature reference for every button on these screens.
- [UX Review & Redesign Plan](ux-review-and-redesign-plan.md): the reasoning behind the stepper +
  expandable-sections layout, and the roadmap for anything not yet built.
