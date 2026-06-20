# Roles and Access

## Roles

| Role | Primary surface | Key responsibilities |
|------|-----------------|----------------------|
| Project Director | Web | Approvals, all reports, organisation settings, user management, audit logs, travel claim approval |
| Project Coordinator | Web | Unit staff management, conflict resolution, report exports, case oversight, master data, outcome-tag approval for AI |
| Social Worker | Mobile | Assigned cases, visits, notes, GPS capture, travel claims |
| Case Worker | Mobile | Assigned cases, interventions, court sittings, notes, reports |

## Authentication flows

- Email + password login with OTP via email for 2FA.
- Password reset via email link.
- Admin may force password reset and deactivate accounts.
- Forced logout on role change or account deactivation.

## Authorization rule

Every protected action is verified server-side. Client UI may hide controls by role for usability, but the API is the authority.
