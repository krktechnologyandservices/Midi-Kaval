# Midi-Kaval — User Guide

This guide explains how to access, install, and use Midi-Kaval — the case management
system for child-protection case work (DCPU/NGO field operations). It covers the web
app, the mobile app (currently distributed as an APK, not yet on the Play Store), user
roles, onboarding, and how a case actually moves through the system end to end.

No real account credentials are included anywhere in this guide — wherever a login is
referenced, it uses a placeholder like `your-email@example.org`. Actual login details
are provided to each user separately when their account is created.

---

## 1. Accessing the application

### Web app

The web app is where Directors, Coordinators, Accountants, and Vendors do most of
their work — case management, budgets, reports, admin, and reviewing/approving things
submitted by field staff.

Open a modern browser (Chrome, Edge, Firefox, Safari) and go to one of the following,
depending on which environment your administrator tells you is the "live" one for daily
work:


- **Self-hosted (Oracle Cloud)**: https://midi-kaval.duckdns.org

Both point at independent deployments with their own separate database — they are not
kept in sync with each other, so use whichever one your organisation has told you is
current, not both interchangeably.

No installation is needed for the web app — just a browser and an internet connection.

### Mobile app (Android)

The mobile app is built for field workers (Social Workers / Case Workers) doing home
visits, logging court sittings, and submitting travel claims from the field, including
while offline.

It is **not yet published on the Google Play Store** — for now it's distributed as a
direct APK file. To install it:

1. Get the `app-release.apk` file from your administrator (downloaded from the
   project's build pipeline).
2. Transfer it to your Android phone (email attachment, cloud drive link, USB cable,
   whatever's easiest).
3. Tap the file to open it. Android will likely warn that installing apps from outside
   the Play Store is blocked by default — you'll be prompted to allow it for the app you
   used to open the file (e.g. your file manager, Gmail, Chrome). Follow the prompt to
   allow it, then tap **Install** again.
4. Once installed, open **Midi-Kaval** from your app drawer like any other app.
5. On first launch, grant the permissions it asks for — **Location** (needed for GPS
   verification during visits) and **Camera** (needed to photograph handwritten consent
   forms and landmark photos during a visit). You can decline these, but visit
   verification and photo capture won't work without them.

There's no auto-update mechanism yet — when a new version is released, you'll need to
repeat the install steps above with the new APK file (Android will treat it as an
update to the existing app, keeping your data, as long as it's signed with the same
key each time).

---

## 2. User roles

Every account has exactly one role, which determines what they can see and do:

| Role | Typical user | Can do |
|---|---|---|
| **Director** | NGO/DCPU head | Everything — full case access, admin (invite/suspend users), budgets, reports, org settings, 2FA policy |
| **Coordinator** | Supervisor over a team of field workers | Case oversight and transfer, approving travel claims, budget viewing, the Crisis Queue (urgent items across the org) |
| **Social Worker** / **Case Worker** | Field worker | Assigned cases, home visits, court sittings, case notes, travel claims — this is who the mobile app is built for |
| **Accountant** | Finance/budget staff | Budget records, utilization tracking, financial reports |
| **Vendor** | External partner organisation | A restricted, separate view — their own organisation info, 2FA settings, and (as of a recent change) the ability to invite the very first Director account when setting up a brand-new deployment |

---

## 3. Getting an account (onboarding)

There is no public "sign up" — every account is created by someone else already in the
system.

### If you're the very first person on a brand-new deployment

A fresh installation has no users at all. The **first** account (a Director) is created
automatically the first time the server starts, using configuration your administrator
sets up during deployment — it's not something you request through the app. Ask your
administrator for that initial login.

### Everyone else — invited by a Director

1. A Director goes to **Admin → Invitations** in the web app and sends an invite to your
   email address, choosing your role (Coordinator, Social Worker, Case Worker, or
   Accountant).
2. You receive an email with an activation link (valid for a limited time — currently a
   few days).
3. Click the link, set your own password, and your account is active.
4. If your organisation requires two-factor authentication, you'll be prompted to set
   that up (see [Section 7](#7-security-2fa-and-data-protection)) either immediately or
   on your first login.

### Vendor accounts

Vendor accounts are separate from the Director/Coordinator/field-worker hierarchy and
are set up directly by an administrator during deployment configuration, not through the
in-app invitation flow.

---

## 4. How a case travels through the system

This is the core workflow — understanding it explains almost everything else in the
app.

### Creating a case

A Coordinator or Social Worker starts a new case from **Cases → New Case**. Before the
case is actually created, the system runs a **duplicate check** — searching existing
cases by name, crime number, and other identifying details — to catch a case that's
already been entered under a slightly different spelling or reference number. If a
likely duplicate is found, you're shown the match and asked to confirm before
proceeding.

Some cases are flagged as sensitive (e.g. POCSO cases involving minors) — these get
extra display protection (beneficiary names are shown discreetly, PII fields require an
explicit "reveal" action that's logged, and additional access controls apply).

### The six stages

Every case moves through six stages in order, each capturing a different phase of the
casework:

1. **Process Initiation** — the initial intake: who the case is about, the incident,
   assigning a field worker.
2. **Maintain & Development** — ongoing support activities while the case is being
   actively worked.
3. **Inter-Sectoral Approach** — coordination with other agencies/departments involved
   in the case.
4. **Rehabilitation** — placement details (e.g. shelter home, foster arrangement).
5. **Reintegration** — the plan and progress for returning the beneficiary to a stable
   home/community setting.
6. **Termination & Exclusion** — formally closing the case, recording the outcome and
   reason for closure.

Each stage has its own data-entry form (visible under the case's detail page), and a
case is moved to the next stage explicitly — it doesn't advance automatically. Once a
case reaches **Termination & Exclusion** it's considered closed; some actions (like
scheduling new visits) become unavailable on a closed case.

### While a case is open

Alongside the six stages, several other things happen throughout a case's life,
independent of which stage it's currently in:

- **Assignment / handoff** — a case can be transferred from one field worker to another
  (e.g. reassignment, staff change). When this happens, the new worker sees a
  **"Handoff Whisper"** — a short structured note (prior actions taken, open items,
  purpose of the next visit) left by whoever made the transfer, so context isn't lost.
  This is visible for a limited window right after the handoff.
- **Visits** — a field worker schedules, starts, and completes home visits tied to the
  case, each with GPS verification and a completion note. Visits can be rescheduled
  (with a reason) if needed, and can include one or more specific "places to visit"
  (addresses) that get logged individually.
- **Case notes** — free-text notes of different types (general, visit-related,
  court-related, intervention-related) can be added to the case timeline, optionally
  with an attached file (photo, PDF).
- **Interventions** — discrete support actions/services provided to the beneficiary,
  tracked separately from notes.
- **Court sittings** — hearing dates tied to the case are scheduled and tracked, with
  automatic reminders as a date approaches and escalation if a hearing is missed.
- **Travel claims** — a field worker submits an expense claim (with an optional
  photographed receipt) for travel related to visits on this case; a Coordinator or
  Director reviews and approves/returns it.
- **Notifications** — relevant users get in-app notifications for things like an
  upcoming court date, a new handoff, or a travel claim needing review.

### Who sees the urgent stuff

The **Crisis Queue** (Coordinators and Directors) is a single prioritized list of
time-sensitive items across the whole organisation: cases with a court date in the next
48 hours, cases recently handed off to a new worker, and travel claims waiting on
approval — so a supervisor doesn't have to hunt through individual cases to find what
needs attention today.

---

## 5. Using the web app

The web app's main sections (left-hand navigation):

- **Dashboard** — an overview relevant to your role.
- **Cases** — search, create, and manage cases; each case's detail page has tabs for
  its stage data, notes, interventions, court sittings, and visits.
- **Crisis Queue** — urgent items (Coordinator/Director only).
- **Travel Claims** — review and approve submissions from field workers
  (Coordinator/Director), or track your own if you're field staff who also uses the web
  app.
- **Budgets** — allocations and utilization tracking, with an attachable receipt/bill
  per record (Director/Coordinator/Accountant).
- **Reports** — generate and export case/operational reports (PDF/Excel).
- **Admin** — user management, invitations, audit log, org-wide settings including
  2FA policy (Director only).
- **Settings** — your own account: change password, enable/manage 2FA.

### Editing a visit from the web

Field workers primarily manage visits from the mobile app, but a Coordinator or
Director can also **reschedule** an upcoming (not yet completed) visit from a case's
detail page — useful for correcting a scheduling mistake without needing the assigned
field worker to do it themselves. Open the case → **Visits** tab → **Reschedule** on the
visit in question, pick a new date/time, and give a reason (this is recorded and
visible to everyone on the case). Note: a **completed** visit can't be edited or
rescheduled — its record is final once submitted.

---

## 6. Using the mobile app

After installing (see [Section 1](#mobile-app-android)) and logging in, the main
screens (bottom navigation):

- **Today** — your visits scheduled for today, with GPS-verified start/complete
  actions, a live countdown banner for your next court sitting, and buttons to
  **Court this week** and **Upcoming visits** (the rest of this week's schedule beyond
  just today, so you can plan ahead).
- **Cases** — your assigned cases; search/filter, view details, add notes (with photo
  attachments), interventions, and court sittings.
- **Travel Claims** — submit a new claim with a photographed receipt (from the camera or
  gallery), and track the status of ones you've already submitted.
- **More** — your sync queue (see below), notifications, and account settings
  (password, 2FA).

### Working offline

The mobile app is built to keep working with a poor or absent connection: actions like
starting/completing a visit or submitting a travel claim are queued locally and
automatically synced once you're back online. You can check what's still waiting to
sync under **More → Sync Queue**.

### Viewing attachments

Tapping an attachment on a note (photo, PDF) downloads it and opens it in your phone's
normal viewer app (e.g. its default photo viewer or PDF reader) — the same file, same
encryption-at-rest protection as the web app, just displayed through whatever app your
phone already uses for that file type.

---

## 7. Security, 2FA, and data protection

- **Encryption** — sensitive personal fields (PII) and uploaded attachments are
  encrypted at rest in the database/storage; only the app itself (with the correct
  server-side key) can read them back.
- **Two-factor authentication (2FA)** — available to every account via **Settings →
  Two-Factor Authentication**, using an authenticator app (TOTP, e.g. Google
  Authenticator). A Director can make 2FA mandatory for the whole organisation.
- **Step-up authentication** — some sensitive actions (like revealing a hidden PII
  field on a POCSO case) require re-confirming your identity via a fresh OTP/TOTP code,
  even if you're already logged in — this is deliberate friction on genuinely sensitive
  actions, not a bug.
- **Audit log** — Directors can review a log of sensitive actions (PII reveals, user
  suspensions, role changes, etc.) under **Admin → Audit Log**.
- **Session handling** — logging out, or a Director suspending/deactivating your
  account, immediately invalidates your session on both web and mobile.

---

## 8. Getting help

If something in the app isn't behaving as expected, the most useful things to note down
before asking for help:

- Which app (web or mobile) and, for web, which browser.
- What you were trying to do, and the exact error message shown (if any).
- Your role and roughly what time it happened.

This helps whoever's maintaining the deployment find the relevant detail quickly rather
than guessing.
