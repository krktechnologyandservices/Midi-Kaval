---
title: Budget Plan — Open Points Resolution
status: captured
created: 2026-06-21
updated: 2026-06-21
source_session: brainstorming-session-2026-06-12-1530
related_epic: epic-14
---

# Budget Plan — Open Points Resolution

## Session Overview

**Topic:** Budget Allocation and Utilization Module — Domain Rules
**Goals:** Resolve open domain questions to inform Epic 14 story creation (budget CRUD API, utilization tracking, UI, expenditure reporting)
**Facilitator:** Admin

---

## Open Points & Answers

### 1. Budget Source

> **Only from DCPU and NPO (Dale View)**

- Budget allocation originates exclusively from two sources:
  - **DCPU** — District Child Protection Unit (government)
  - **NPO (Dale View)** — the non-profit organization operating the project
- No other funding sources in scope for v1.

### 2. Financial Year

> **PSR Financial Year (April to March)**

- The budget cycle follows the **Project- and Scheme-based Reporting (PSR)** fiscal calendar:
  - **Start:** 1st April
  - **End:** 31st March
- All budget periods, utilization tracking, and reporting align to this cycle.

### 3. Budget Heads (Categories)

> Honorarium, Travel expenses, Parent management training, Life skills training, Psychosocial support, Administrative expenses, Stationery expenses

Seven budget heads:

| # | Budget Head | Type |
|---|------------|------|
| 1 | Honorarium | Personnel / stipends |
| 2 | Travel expenses | Operational — field travel |
| 3 | Parent Management Training | Program — training & capacity building |
| 4 | Life Skills Training | Program — beneficiary development |
| 5 | Psychosocial Support | Program — counselling & support |
| 6 | Administrative Expenses | Operational — office/admin |
| 7 | Stationery Expenses | Operational — supplies & materials |

### 4. Budget Level

> **At project level**

- Budgets are allocated and tracked at the **project level** (not per-case or per-worker).
- Single project scope for v1 (multi-project/donor budget may be future scope).

### 5. Approval Flow

> **Accountant → Director and back**

```
Accountant (creates/proposes) → Director (approves/rejects) → Accountant (executes)
```

- **Accountant** initiates/recommends budget entries
- **Director** approves or returns with changes
- **Accountant** executes the approved budget
- Bidirectional flow: "and back" implies revision/return path.

### 6. (Binary Question) — Yes

> **Yes**

Confirmed affirmative for the associated capability (likely: is separate budget approval workflow needed).

### 7. Reporting Frequency

> Monthly, Quarterly, Half-yearly, Annually

Four reporting cadences:

| Frequency | Period Covered | Likely Audience |
|-----------|---------------|-----------------|
| Monthly | Single month | Internal / coordinator |
| Quarterly | 3 months (Apr-Jun, Jul-Sep, Oct-Dec, Jan-Mar) | DCPU / NPO management |
| Half-yearly | 6 months (Apr-Sep, Oct-Mar) | DCPU / board |
| Annually | Full financial year (Apr-Mar) | Statutory / annual report |

---

## Implications for Epic 14

### Story 14.1 — Budget CRUD API & DB Schema
- **Budget entity** needs: `Source` (DCPU/NPO), `FinancialYear` (start/end date derived from Apr–Mar), `Level` (project), `ApprovalStatus` (draft/proposed/approved/returned/executed)
- **BudgetLineItem entity** per head: amount allocated, amount utilized, head category
- **Approval flow**: `CreatedBy` (Accountant) → `ApprovedBy` (Director) with timestamp; rejection reason field

### Story 14.2 — Budget Utilization Tracking
- Track actual spend per budget head against allocation
- Monthly/quarterly/half-yearly/annual period comparisons

### Story 14.3 — Budget Web UI
- Accountant dashboard: create/propose budget, view utilization
- Director dashboard: approve/return budget, view reports

### Story 14.4 — Budget Expenditure Report
- Reporting periods aligned to PSR financial year
- Reports by frequency (monthly, quarterly, half-yearly, annual)
- Report format: per budget head (allocated vs utilized vs balance)
