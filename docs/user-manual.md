# Kaval Online — User Manual

> Case Management Platform for POCSO & Child Protection Services  
> Version 1.0.0

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Getting Started](#2-getting-started)
3. [Login & Authentication](#3-login--authentication)
4. [Dashboard](#4-dashboard)
5. [Case Management](#5-case-management)
6. [Visits & Field Operations](#6-visits--field-operations)
7. [Case Notes & Interventions](#7-case-notes--interventions)
8. [Court Management](#8-court-management)
9. [Travel Claims](#9-travel-claims)
10. [Budget Management](#10-budget-management)
11. [Staff & User Management](#11-staff--user-management)
12. [Reports & Exports](#12-reports--exports)
13. [Notifications](#13-notifications)
14. [Vendor Portal](#14-vendor-portal)
15. [Mobile App Guide](#15-mobile-app-guide)
16. [Troubleshooting](#16-troubleshooting)
17. [Glossary](#17-glossary)

---

## 1. Introduction

### 1.1 What is Kaval Online?

Kaval Online is a comprehensive case management platform designed for child protection services, POCSO (Protection of Children from Sexual Offences) case workers, and juvenile justice professionals. It provides end-to-end case lifecycle management, field visit tracking, court management, and reporting — all accessible via web and mobile.

### 1.2 Who Uses the System?

| Role | Description |
|------|-------------|
| **Director** | Top-level administrator. Manages staff, approves budgets and claims, views audit logs, manages user accounts. |
| **Coordinator** | Mid-level supervisor. Oversees cases, reviews field worker activities, manages crisis queue. |
| **SocialWorker** | Field worker assigned to POCSO cases. Conducts visits, creates interventions, manages court sittings, works offline. |
| **CaseWorker** | Field worker with same capabilities as SocialWorker. |
| **Accountant** | Manages budgets — creates, proposes, and executes budgets; tracks utilizations. |
| **Vendor** | Backstage system role for vendor onboarding and organisation management. |

### 1.3 Supported Platforms

- **Web App**: Chrome, Firefox, Edge (recommended) — `http://localhost:4200`
- **Mobile App**: Android (React Native) — installed via APK
- **API Swagger**: `http://localhost:5049/swagger`

---

## 2. Getting Started

### 2.1 System Requirements

- Modern web browser (Chrome 90+, Edge 90+, Firefox 88+)
- For mobile: Android 8.0+ device with USB debugging enabled
- Google Authenticator (or compatible) app for 2FA

### 2.2 Accessing the Platform

1. Open your browser to `http://localhost:4200`
2. The login page will appear
3. Sign in with your credentials (provided by your Director)

### 2.3 First-Time Login

**Step 1 — Enter Credentials:**

| User Type | Email | Password (default) |
|-----------|-------|-------------------|
| Director | director@pilot.example | CHANGE_ME |
| Vendor | karthik.k.82@outlook.com | CHANGE_ME |

**Step 2 — OTP Verification:**

A 6-digit code is sent to your registered email address. Enter the code within 5 minutes.

**Step 3 — Set 2FA (Recommended for Directors):**

After first login, you will be prompted to set up Two-Factor Authentication:
1. Scan the QR code with Google Authenticator or Authy
2. Enter the 6-digit code from the app
3. On subsequent logins, you'll use the authenticator code instead of email OTP

**Step 4 — Change Password:**

Navigate to your profile settings to update your password from the default.

---

## 3. Login & Authentication

### 3.1 Standard Login Flow

```
Email + Password → OTP via Email → Access Granted
```

1. Enter your registered email and password
2. Check your email inbox for a 6-digit verification code
3. Enter the code on the OTP screen
4. You are now logged in and redirected to the dashboard

> Note: OTP codes expire after 5 minutes. Click "Resend Code" if needed.

### 3.2 Two-Factor Authentication (2FA) — Directors

If you have enrolled in 2FA (recommended):

```
Email + Password → TOTP Code → Access Granted
```

1. Enter your email and password
2. Instead of email OTP, you'll be asked for a 6-digit TOTP code
3. Open your authenticator app and enter the current code
4. You are now logged in

**To enroll in 2FA:**
- Go to **Admin → My Profile → Enable 2FA**
- Scan the QR code with your authenticator app
- Enter the verification code to confirm

### 3.3 Password Reset

1. On the login page, click **Forgot Password?**
2. Enter your registered email address
3. Check your email for a password reset link
4. Click the link and enter your new password (minimum 8 characters)
5. Log in with your new password

### 3.4 Session Management

- Access tokens expire after **15 minutes**
- Refresh tokens expire after **7 days**
- You'll be automatically logged out after prolonged inactivity
- Log out manually using the profile menu

### 3.5 Step-Up Authentication (Field Workers)

When revealing personally identifiable information (PII) for a case:
1. Click **Reveal PII** on a case detail page
2. A step-up OTP will be sent to your email
3. Enter the code to temporarily authorize PII access

---

## 4. Dashboard

### 4.1 Director / Coordinator Dashboard

The dashboard provides a real-time overview of your organisation's operations:

- **Crisis Queue**: Prioritized list of high-risk cases requiring immediate attention
- **Case Overview**: Total cases by stage, new cases this week, overdue items
- **Visit Metrics**: Today's scheduled visits, completion rate, overdue visits
- **Budget Snapshot**: Budget utilization summary, pending approvals
- **Travel Claims**: Pending claim approvals, monthly totals
- **Recent Activity**: Latest case updates, notes, and interventions

### 4.2 Field Worker Dashboard

- **My Assigned Cases**: List of cases assigned to you
- **Today's Visits**: Scheduled visits for the day with grouping suggestions
- **Weekly Schedule**: Full week view of all visits
- **Overdue Items**: Visits and interventions past their due date
- **Upcoming Court**: Court sittings scheduled for your cases

---

## 5. Case Management

### 5.1 Case Lifecycle

A case progresses through these stages:

```
Process Initiation → Preliminary Assessment → Social Investigation →
Community Based Intervention → Placement → Reintegration → Termination/Exclusion
```

Each stage has its own data collection form capturing stage-specific information.

### 5.2 Creating a New Case

**Who can create cases:** Directors, Coordinators

1. Navigate to **Cases → Create New Case**
2. Fill in required fields:

| Field | Description |
|-------|-------------|
| Crime Number | Unique case identifier from police records |
| ST Number | Sessions Trial number |
| Beneficiary Name | Child's name (encrypted at rest) |
| Beneficiary Age | Age of the child |
| Type of Offence | Classification of the offence |
| Offence Classification | Heinous, Serious, or Petty |
| Domicile | Urban or Rural |
| Is First-Time Offender | Yes/No |

3. Click **Save**. The case is created with current stage set to `ProcessInitiation`
4. Assign the case to a field worker

### 5.3 Searching for Cases

1. Go to **Cases → Case Registry**
2. Use the search panel to filter by:
   - Crime number, ST number
   - Beneficiary name
   - Current stage
   - Assigned worker
   - Date range
   - Offence type, classification
   - Domicile
3. Results can be **exported to Excel or PDF**

### 5.4 Transferring a Case

1. Open the case detail page
2. Click **Transfer**
3. Select the new assigned worker
4. (Optional) Add a transfer note
5. Confirm the transfer

### 5.5 Merging Duplicate Cases

If duplicate cases are detected:
1. Open either case
2. Click **Merge Duplicates**
3. Select the target case to merge into
4. Choose which data to keep from each case
5. Confirm — the duplicate case is deactivated

### 5.6 Stage Transitions

1. On the case detail page, locate the **Stage** section
2. Click **Transition Stage**
3. Select the target stage (must follow the defined order)
4. Add any transition notes
5. Confirm — the stage is updated and the event is logged

### 5.7 Checking for Duplicates

Before creating a new case, use the **Check Duplicate** feature:
1. Go to **Cases → Check Duplicate**
2. Enter crime number/ST number
3. The system checks for existing matches and warns you

### 5.8 GPS Verification (Mobile)

When visiting a case location:
1. Open the case on the mobile app
2. Tap **Verify GPS**
3. The app captures current coordinates (latitude, longitude)
4. Coordinates are stored for audit trail

### 5.9 PII Reveal (Step-Up)

Protected personal data includes beneficiary name, contact, landmark:
1. Click **Reveal PII** on case detail
2. A step-up OTP is sent to your email
3. Enter the code to temporarily view the data
4. The session authorizes PII view for a limited time

### 5.10 GDPR — Data Erasure & Portability

**Directors only:**
- **Export PII**: Download all personal data for a case as JSON
- **Erase PII**: Irreversibly remove personal data for a case

---

## 6. Visits & Field Operations

### 6.1 Scheduling a Visit

1. Open a case → **Visits** tab
2. Click **Schedule Visit**
3. Select the visit date/time
4. (Optional) Add visit notes
5. The visit appears on the worker's dashboard

### 6.2 Starting a Visit

1. From dashboard or visit list, tap **Start Visit**
2. The visit status changes to "In Progress"
3. Record time, observations, photos (mobile)

### 6.3 Completing a Visit

1. Fill in the visit report:
   - Observations
   - Child's wellbeing status
   - Home environment assessment
   - Follow-up actions required
2. Tap **Complete Visit**
3. The visit is logged with timestamp

### 6.4 Rescheduling a Visit

1. Open the visit details
2. Click **Reschedule**
3. Select new date and time
4. Provide a reason for rescheduling

### 6.5 Visit Grouping (Mobile)

For efficient route planning:
1. On the mobile app, view **Today's Visits**
2. Tap **Grouping Suggestion**
3. The app suggests visits to group based on proximity
4. Follow the suggested route for optimal efficiency

### 6.6 Offline Mode (Mobile)

The mobile app supports offline operation:
1. **Sync data** while connected
2. Work offline — visits, notes, GPS capture all work
3. When reconnected, tap **Push Sync**
4. All offline data is uploaded to the server

---

## 7. Case Notes & Interventions

### 7.1 Creating Case Notes

1. Open a case → **Notes** tab
2. Click **Add Note**
3. Enter the note content
4. (Optional) Mark as internal or visible to all
5. Save — notes appear in chronological order

### 7.2 Attachments

1. Click **Attach File** on a note or case
2. Select a file (PDF, image, document)
3. File uploads to secure storage (Azure Blob)
4. A download link is generated with time-limited access

### 7.3 Creating Interventions

1. Open a case → **Interventions** tab
2. Click **Create Intervention**
3. Fill in:
   - Intervention type (counselling, training, referral, etc.)
   - Provider name
   - Notes
   - Provided status (completed or pending)
4. Save

### 7.4 Overdue Interventions

The system monitors intervention schedules:
- Overdue interventions appear on the dashboard
- Directors receive automated alerts for overdue items
- Escalation triggers after defined thresholds

---

## 8. Court Management

### 8.1 Scheduling a Court Sitting

1. Open a case → **Court Sittings** tab
2. Click **Schedule Court Sitting**
3. Enter:
   - Court date and time
   - Court name/location
   - Judge name
   - Case outcome (if known)
4. Save

### 8.2 Upcoming Court Sittings

Your upcoming court dates appear on:
- Dashboard court widget
- **Court Sittings** page with filter options
- Mobile app notifications (24 hours before)

### 8.3 Reminders & Escalations

- **24-hour reminder**: Automatic SMS/email reminder before the sitting
- **Missed court**: If a sitting is missed, escalation process triggers
- **Crisis queue**: Repeated missed court appearances escalate to crisis

### 8.4 Court Outcome Recording

1. Open the court sitting
2. Update with the outcome:
   - Case disposed
   - Adjourned
   - Other
3. Add judge's remarks
4. System logs the update

---

## 9. Travel Claims

### 9.1 Creating a Travel Claim

Field workers can submit travel claims for work-related travel:

1. Go to **Travel → New Claim**
2. Enter claim details:
   - Date of travel
   - From/To locations
   - Purpose (visit, court, training)
   - Mode of transport
   - Amount
3. Attach receipt images (captured via mobile)
4. Save as Draft or Submit

### 9.2 Submitting a Claim

1. Complete all required fields
2. Click **Submit for Approval**
3. The claim enters the Director's approval queue
4. Status changes to "Pending Approval"

### 9.3 Approving/Returning Claims (Director)

1. Go to **Travel → Pending Approvals**
2. Review claim details and receipts
3. **Approve**: Claim is marked approved, worker notified
4. **Return**: Add a comment explaining why, worker can edit and resubmit

### 9.4 Monthly Totals

Directors and Coordinators can view monthly travel claim totals:
1. Go to **Travel → Monthly Totals**
2. View summarized amounts by worker
3. Export for record-keeping

---

## 10. Budget Management

### 10.1 Budget Lifecycle

```
Draft → Proposed → Approved → Executed
```

| Stage | Done By | Description |
|-------|---------|-------------|
| Draft | Accountant | Initial budget creation |
| Proposed | Accountant | Submit for Director review |
| Approved | Director | Budget is authorized |
| Executed | Accountant | Budget is active and utilizable |

### 10.2 Creating a Budget

1. Go to **Budgets → Create Budget**
2. Fill in:
   - Financial year (start/end dates)
   - Source of funds
   - Budget line items with allocated amounts
3. Save as Draft

### 10.3 Proposing a Budget

1. Open the draft budget
2. Click **Propose**
3. Budget enters review queue for Director

### 10.4 Approving a Budget (Director)

1. Go to **Budgets → Pending Approval**
2. Review line items and amounts
3. **Approve** or **Return** with comment

### 10.5 Tracking Utilization

1. Open an executed budget
2. View **Utilizations** tab for:
   - Each line item's allocated vs utilized amounts
   - Individual utilization entries with dates and descriptions
   - Running balance per budget head

### 10.6 Budget Reports

1. Go to **Budgets → Reports**
2. View consolidated budget report
3. Export to Excel for offline review

---

## 11. Staff & User Management

### 11.1 Viewing Staff Directory (Director)

1. Go to **Admin → Staff Directory**
2. View all staff members in your organisation
3. Filter by role, status, or name

### 11.2 Adding Staff Members

1. Click **Add Staff Member**
2. Fill in:
   - Name, email
   - Role (SocialWorker, CaseWorker, Coordinator, Accountant)
   - Phone number
3. System sends an invitation email with setup instructions

### 11.3 Suspending/Reactivating Users

1. Open user profile
2. Click **Suspend** — user cannot log in
3. To restore access, click **Reactivate**

### 11.4 Resetting User 2FA (Director)

If a user loses access to their authenticator app:
1. Open user profile
2. Click **Reset 2FA**
3. User's 2FA enrollment is cleared
4. User can re-enroll on next login

### 11.5 Force Password Reset

1. Open user profile
2. Click **Force Reset**
3. User receives a password reset email
4. Current session is invalidated

### 11.6 Sending Invitations (Director with 2FA)

1. Go to **Admin → Invitations**
2. Click **Send Invitation**
3. Enter email and role
4. System sends an invitation link
5. Recipient creates their account via the link

---

## 12. Reports & Exports

### 12.1 Available Reports

| Report | Access | Format |
|--------|--------|--------|
| Case Search Export | Coordinator+ | Excel, PDF |
| Socio-Demographic Profile | Coordinator+ | Excel |
| Budget Report | Director | Excel |
| Crisis Queue | Coordinator+ | Web |
| Audit Log | Director only | Web (paginated) |
| Travel Claim Totals | Coordinator+ | Web |

### 12.2 Exporting Case Search Results

1. Perform a case search with your desired filters
2. Click **Export**
3. Choose format (Excel or PDF)
4. Download the generated file

### 12.3 Socio-Demographic Report

Generate a socio-demographic profile report:
1. Go to **Reports → Socio-Demographic Profile**
2. Report is generated as Excel
3. Contains aggregated data on beneficiary demographics

### 12.4 Audit Log

Directors can view all system activity:
1. Go to **Admin → Audit Log**
2. Filter by:
   - Date range
   - Event type (login, case create, visit, etc.)
   - User
3. Paginated results with timestamp and actor details

---

## 13. Notifications

### 13.1 In-App Notifications

The bell icon in the header shows:
- Unread notification count
- Click to expand the notification list
- Notifications for: case assignments, visit reminders, claim approvals, system alerts

### 13.2 Push Notifications (Mobile)

When the mobile app is installed and push tokens registered:
- **Visit reminders** — 24 hours before scheduled visit
- **Court reminders** — 24 hours before court sitting
- **Intervention overdue** — alert for past-due interventions
- **Claim updates** — approval/rejection notifications
- **Crisis alerts** — urgent notifications for escalated cases

### 13.3 Email Notifications

System sends emails for:
- OTP verification codes
- Password reset links
- Welcome emails for new users
- Court sitting reminders
- Travel claim status updates

### 13.4 Notification Preferences

1. Go to **Settings → Notification Preferences**
2. Toggle notification types on/off
3. Preferences sync across devices

---

## 14. Vendor Portal

### 14.1 What is Vendor Access?

The Vendor role provides backstage access for managing organisations and the activation flow. It is used by system administrators for:
- Creating new organisations
- Managing activation tokens
- Generating activation links for Directors

### 14.2 Vendor Endpoints (via Swagger UI)

Vendor operations are API-only (no web UI). Access via Swagger:
1. Open `http://localhost:5049/swagger`
2. Authorize with your vendor JWT token
3. Use the Organisations and Admin endpoints

### 14.3 2FA Requirement

All vendor API operations require Two-Factor Authentication enrollment. To enroll:
1. Login via Swagger UI
2. Call `POST /api/v1/auth/enroll-2fa`
3. Scan the provisioning URI with your authenticator app
4. Call `POST /api/v1/auth/verify-enroll-2fa` with the code
5. You can now access vendor-protected endpoints

---

## 15. Mobile App Guide

### 15.1 Installation

For development builds:
1. Ensure USB debugging is enabled on your Android device
2. Connect the device via USB
3. Run: `npx react-native run-android`
4. The app installs and launches automatically

### 15.2 Setting Up USB Connection

1. Enable **Developer Options** on your Android device
2. Enable **USB Debugging**
3. Connect via USB cable
4. When prompted, **Allow USB debugging** on the device
5. Run: `adb reverse tcp:5049 tcp:5049` (API proxy)
6. The mobile app can now communicate with the API

### 15.3 App Navigation

- **Bottom tabs**: Dashboard, Cases, Visits, More
- **Dashboard tab**: Today's visits, weekly schedule, overdue items
- **Cases tab**: Your assigned cases list
- **Visits tab**: Visit management, grouping suggestions
- **More tab**: Profile, settings, sync status

### 15.4 Offline Sync

The mobile app supports offline-first operations:
1. Ensure you sync before going offline
2. Work normally — visits, GPS, notes all work offline
3. When back online, the app auto-syncs
4. Check sync status in the More tab

### 15.5 Discreet Mode (POCSO)

For sensitive field operations:
1. Enable **Discreet Mode** in settings
2. The app icon changes to a neutral icon
3. Notification previews are hidden
4. Quick-exit button (tapping exits app immediately)

---

## 16. Troubleshooting

### 16.1 Login Issues

| Problem | Solution |
|---------|----------|
| "Invalid email or password" | Check caps lock. Reset password via Forgot Password link. |
| OTP not received | Check spam folder. Wait 30 seconds, click Resend. |
| "Account suspended" | Contact your Director. |
| "Contact your coordinator" | Your account has been deactivated. |
| "Token version mismatch" | Your session was revoked (password change, 2FA reset). Log in again. |

### 16.2 API Not Responding (Web)

1. Check that Docker containers are running: `docker ps`
2. Check API is running: open `http://localhost:5049/swagger`
3. If not, run `scripts\start-api.bat`
4. Check console for error messages

### 16.3 API Not Responding (Mobile)

1. Ensure ADB reverse proxy is set: `adb reverse tcp:5049 tcp:5049`
2. Confirm API is running on your computer
3. Reconnect USB cable

### 16.4 "Two-Factor Authentication Required"

You attempted an action that requires 2FA enrollment:
1. Log in to the web app
2. Go to **Admin → Enable 2FA**
3. Follow the setup instructions
4. Retry the action

### 16.5 Page Not Loading / Blank Screen

1. Check browser console for errors (F12)
2. Ensure the API is running
3. Clear browser cache and reload
4. Check CSP violations in browser console

---

## 17. Glossary

| Term | Definition |
|------|------------|
| **POCSO** | Protection of Children from Sexual Offences Act |
| **OTP** | One-Time Password — 6-digit code sent via email |
| **TOTP** | Time-based One-Time Password — 6-digit code from authenticator app |
| **2FA** | Two-Factor Authentication — second layer of security |
| **JWT** | JSON Web Token — used for API authentication |
| **ST Number** | Sessions Trial number assigned by the court |
| **GPS** | Global Positioning System — for visit location verification |
| **PII** | Personally Identifiable Information |
| **SAS** | Shared Access Signature — time-limited URL for file access |
| **RBAC** | Role-Based Access Control — permission system by roles |
| **CSP** | Content Security Policy — security headers for web app |
| **Stage** | Current phase in the case lifecycle (1-6) |
| **Crisis Queue** | Prioritized list of cases needing urgent attention |
| **Hangfire** | Background job processing library for scheduled tasks |
| **Azurite** | Local Azure Blob Storage emulator for development |
