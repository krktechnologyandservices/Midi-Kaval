# Implementation Plan: KAVAL User Story Gaps & Enhancements

**Based on:** `docs/KAVAL User Story_260616_224459.pdf` + `docs/KavalSample.xlsx`
**Generated:** 2026-06-21

---

## Overview

The new user story document reveals case data fields and sub-stage tracking that were not included in the original MVP PRD. The Excel report (`KavalSample.xlsx`) confirms the exact columns expected in the socio-demographic profile report. This plan defines 6 new epics to fill the gaps.

---

## Epic 11: Enhanced Case Socio-Demographic Fields

### Story 11.1 — Gender, Family Type & Economic Status

**As a** Project Coordinator,
**I want** to record Gender, Family Type, and Economic Status for each child,
**So that** the socio-demographic profile report is complete.

**Acceptance Criteria:**

1. **Given** the Case entity
   **When** creating/updating a Case
   **Then** the following enum fields are available:
   - `Gender` — Male / Female / Transgender
   - `FamilyType` — Joint / Nuclear / SingleParent / Others
   - `EconomicStatus` — APL / BPL
   **And** each field has a corresponding DB column with enum-to-string conversion
   **And** each new field is nullable (not required for backward compatibility)

2. **Given** existing Cases
   **When** EF Core migration runs
   **Then** existing rows get default/null values for new columns, no data loss

3. **Given** the CreateCase API
   **When** creating a Case
   **Then** the optional fields gender, familyType, economicStatus are accepted in the request DTO
   **And** invalid enum values return 400 Bad Request

**Files to change:**
- `apps/api/Domain/Enums/Gender.cs` — **new**
- `apps/api/Domain/Enums/FamilyType.cs` — **new**
- `apps/api/Domain/Enums/EconomicStatus.cs` — **new**
- `apps/api/Domain/Entities/Case.cs` — add 3 new properties
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs` — configure new columns
- `apps/api/Models/Cases/CaseDtos.cs` — add to CreateCaseRequest, CaseDetailDto, CaseSummaryDto
- Create EF Core migration

---

### Story 11.2 — Occupation and Education Level on Case

**As a** Project Coordinator,
**I want** the Occupation and Education Level of each child stored on the Case record,
**So that** the socio-demographic profile can be aggregated.

**Acceptance Criteria:**

1. **Given** the `occupations` and `education_levels` Legend tables already exist
   **When** I create/update a Case
   **Then** I can set `OccupationId` and `EducationLevelId` as optional FK references
   **And** the Case entity has navigation properties (not required, for query convenience)

2. **Given** a Case with Occupation set
   **When** the profile report queries
   **Then** the Occupation name is available via the FK join

3. **Given** invalid FK values
   **When** saving
   **Then** DB referential integrity returns error (FK constraint)

**Files to change:**
- `apps/api/Domain/Entities/Case.cs` — add `OccupationId?`, `EducationLevelId?`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs` — configure FKs to occupations/education_levels tables
- `apps/api/Models/Cases/CaseDtos.cs` — add optional fields
- Create EF Core migration

---

### Story 11.3 — Recidivism and Family History Tracking

**As a** Project Coordinator,
**I want** to track history of crime in family and recidivism counts,
**So that** the profile report shows repeat/first-time and recidivism data.

**Acceptance Criteria:**

1. **Given** the Case entity
   **When** viewing/editing a Case
   **Then** the following fields are available:
   - `FamilyHistoryOfCrime` — bool (default false)
   - `RecidivismBeforeCount` — int? (number of re-offences before intervention)
   - `RecidivismAfterCount` — int? (number of re-offences after intervention)
   **And** `IsFirstTimeOffender` (existing field) maps to "Frequency of offences: Repeat/First time"

2. **Given** existing data
   **When** migration runs
   **Then** `IsFirstTimeOffender = true` implies display as "First time"; `false` as "Repeat"

**Files to change:**
- `apps/api/Domain/Entities/Case.cs` — add 3 new fields
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs` — configure defaults
- `apps/api/Models/Cases/CaseDtos.cs` — expose in DTOs
- Create EF Core migration

---

## Epic 12: Case Stage Sub-Step Data Models

### Story 12.1 — Stage 2: Maintain & Development Sub-Step Data

**As a** Project Coordinator,
**I want** to record bio-psycho-social assessment, ICP, life skill training, PMA, and related data for Stage 2,
**So that** case development is tracked per the workflow.

**Acceptance Criteria:**

1. **Given** a Case in Stage 2 (MaintainAndDevelopment)
   **When** I view the stage details
   **Then** I can see and edit:
   - `case_bio_psycho_assessments`: IsCompleted (bool), CompletedAtUtc, Notes
   - `case_icp_records`: IsDocumented (bool), LifeSkillAttended (bool), ParentMgmtAttended (bool), GroupWorkAttended (bool), CommunityProgramAttended (bool), PmaStatus (enum: NotApplicable/Ordered/Scheduled/Completed)
   - Each record is linked to one Case via CaseId FK

2. **Given** the API
   **When** I call `GET /api/v1/cases/{id}/stage-details`
   **Then** Stage 2 detail includes all sub-step records
   **And** `PUT /api/v1/cases/{id}/stage-details` updates them

**New files:**
- `apps/api/Domain/Enums/PmaStatus.cs`
- `apps/api/Domain/Entities/CaseBioPsychoAssessment.cs`
- `apps/api/Domain/Entities/CaseIcpRecord.cs`
- `apps/api/Infrastructure/Persistence/CaseBioPsychoAssessmentConfiguration.cs`
- `apps/api/Infrastructure/Persistence/CaseIcpRecordConfiguration.cs`
- `apps/api/Models/Cases/StageDetailDtos.cs` — new DTOs for stage sub-step data

**Files to change:**
- `apps/api/Infrastructure/Persistence/AppDbContext.cs` — add DbSets
- Controllers — add stage-details endpoints
- Create EF Core migration

---

### Story 12.2 — Stage 3: Inter-Sectoral Approach Support Tracking

**As a** Project Coordinator,
**I want** to record which types of inter-sectoral support are provided to each child,
**So that** legal, police, education, and other supports are tracked.

**Acceptance Criteria:**

1. **Given** a Case in Stage 3 (InterSectoralApproach)
   **When** I view the stage details
   **Then** I can see and edit a list of support records, each with:
   - SupportType — enum: Legal/Police/Education/Vocational/Psychological/Deaddiction/MaterialFinancial/Medical
   - IsProvided (bool)
   - Notes (string?)
   - ProviderDetails (string?)

2. **Given** the API
   **When** I add/remove support records
   **Then** changes persist and are audited

**New files:**
- `apps/api/Domain/Enums/InterSectoralSupportType.cs`
- `apps/api/Domain/Entities/CaseInterSectoralSupport.cs`
- `apps/api/Infrastructure/Persistence/CaseInterSectoralSupportConfiguration.cs`
- DTOs and stage-details endpoint extension

**Files to change:**
- `AppDbContext.cs` — add DbSet
- `StageDetailDtos.cs` — extend with support records
- Create EF Core migration

---

### Story 12.3 — Stage 4: Rehabilitation Placement Records

**As a** Project Coordinator,
**I want** to record where a child is placed during rehabilitation,
**So that** placement type and institution details are documented.

**Acceptance Criteria:**

1. **Given** a Case in Stage 4 (Rehabilitation)
   **When** I view the stage details
   **Then** I can see and edit:
   - PlacementType — InHome / ObservationHome / SpecialHome
   - InstitutionName
   - InstitutionAddress
   - StartDate

2. **Given** the API
   **When** placement is recorded
   **Then** it is stored and audited

**New files:**
- `apps/api/Domain/Enums/PlacementType.cs`
- `apps/api/Domain/Entities/CasePlacement.cs`
- Configuration, DTOs, stage-details endpoint updates
- Create EF Core migration

---

### Story 12.4 — Stage 5: Reintegration Records

**As a** Project Coordinator,
**I want** to record reintegration level and institution details,
**So that** the child's reintegration path is documented.

**Acceptance Criteria:**

1. **Given** a Case in Stage 5 (Reintegration)
   **When** I view the stage details
   **Then** I can see and edit:
   - Level — Community / Institutional
   - InstitutionDetails (string?)

2. **Given** the API
   **When** reintegration is recorded
   **Then** it is stored and audited

**New files:**
- `apps/api/Domain/Enums/ReintegrationLevel.cs`
- `apps/api/Domain/Entities/CaseReintegration.cs`
- Configuration, DTOs, endpoint updates
- Create EF Core migration

---

### Story 12.5 — Stage 6: Termination/Exclusion Records

**As a** Project Coordinator,
**I want** to record termination from JJB or exclusion with reason and report attachment,
**So that** case closure is fully documented.

**Acceptance Criteria:**

1. **Given** a Case in Stage 6 (TerminationExclusion)
   **When** I view the stage details
   **Then** I can see and edit:
   - Type — Termination / Exclusion
   - TerminatedByJjbDetails (string?)
   - ExclusionReason (string?)
   - ReportAttachmentId (Guid?, FK to attachments)

2. **Given** the API
   **When** termination/exclusion is recorded
   **Then** it is stored and audited

**New files:**
- `apps/api/Domain/Enums/TerminationType.cs`
- `apps/api/Domain/Entities/CaseTermination.cs`
- Configuration, DTOs, endpoint updates
- Create EF Core migration

---

## Epic 13: Case Cross-Linking (Related Cases)

### Story 13.1 — Related Cases Data Model and API

**As a** Project Coordinator,
**I want** to link related cases (cross-links),
**So that** I can see connections between siblings, co-accused, or linked children.

**Acceptance Criteria:**

1. **Given** the `CaseRelatedCase` join table
   **When** I link two cases
   **Then** a bidirectional relationship is stored with a `RelationshipType` label

2. **Given** a Case with related cases
   **When** I view Case detail
   **Then** related cases are listed with Crime/ST numbers and relationship type

**New files:**
- `apps/api/Domain/Entities/CaseRelatedCase.cs`
- `apps/api/Infrastructure/Persistence/CaseRelatedCaseConfiguration.cs`
- DTOs, API endpoints (POST/GET/DELETE)
- Create EF Core migration

---

## Epic 14: Budget Allocation and Utilization Module

### Story 14.1 — Budget CRUD API and DB Schema

**As a** Project Director,
**I want** to create and manage budgets for the project,
**So that** financial allocation is tracked.

**Acceptance Criteria:**

1. **Given** the `budgets` table
   **When** Director creates a budget
   **Then** it stores: OrganisationId, FinancialYear, TotalAllocated, Description
   **And** Director can edit and deactivate budgets

2. **Given** the API
   **When** I call `GET /api/v1/budgets`
   **Then** budgets list returns with total allocated and utilized amounts (computed)

**New files:**
- `apps/api/Domain/Entities/Budget.cs`
- `apps/api/Infrastructure/Persistence/BudgetConfiguration.cs`
- `apps/api/Controllers/V1/BudgetsController.cs`
- `apps/api/Models/Budgets/BudgetDtos.cs`
- Registration in `Program.cs`
- Create EF Core migration

---

### Story 14.2 — Budget Utilization Tracking API

**As a** Project Coordinator,
**I want** to record budget utilization entries linked to Cases or general expenses,
**So that** expenditure is tracked against allocation.

**Acceptance Criteria:**

1. **Given** the `budget_utilizations` table
   **When** Coordinator adds a utilization entry
   **Then** it stores: BudgetId, CaseId? (optional), AmountUtilized, UtilizationDate, Category, Notes

2. **Given** utilization entries exist
   **When** I view budget detail
   **Then** total utilized is computed and remaining balance shown

**New files:**
- `apps/api/Domain/Entities/BudgetUtilization.cs`
- `apps/api/Infrastructure/Persistence/BudgetUtilizationConfiguration.cs`
- `apps/api/Models/Budgets/BudgetUtilizationDtos.cs`
- Create EF Core migration

---

### Story 14.3 — Budget Web UI

**As a** Project Director/Coordinator,
**I want** a web page to manage budgets and view utilization,
**So that** I can track finances without Excel.

**Acceptance Criteria:**

1. **Given** the Admin sidebar
   **When** Director clicks "Budgets"
   **Then** list view shows budgets with allocated vs utilized amounts

2. **Given** a budget selected
   **When** I open detail
   **Then** utilization entries are listed with total and remaining

**New files:**
- `apps/web/src/app/features/budgets/` — list and detail components
- Admin sidebar link
- Routes

---

### Story 14.4 — Budget Expenditure Report

**As a** Project Director,
**I want** to export a budget vs utilization report as Excel,
**So that** funders receive financial reports.

**Acceptance Criteria:**

1. **Given** budgets and utilization data
   **When** Director exports budget report
   **Then** Excel file is generated with allocation, utilization, and balance columns

**Files to change:**
- Reports infrastructure — add budget report type
- Report generation — ClosedXL export
- Report Web UI — add budget report option

---

## Epic 15: Socio-Demographic Profile Report

### Story 15.1 — Socio-Demographic Profile Report Generation

**As a** Project Coordinator,
**I want** to generate the monthly socio-demographic profile report matching the KavalSample.xlsx format,
**So that** DCPU receives the required reporting.

**Acceptance Criteria:**

1. **Given** Cases exist with the new socio-demographic fields (Gender, Occupation, Education, FamilyType, EconomicStatus, Recidivism, etc.)
   **When** I generate the report for a given month/year
   **Then** the Excel output has two sections:
   - **Section 1:** List of children with Sl No, Name, Age, Contact, Case Committed, Crime Number, Status, Present Stage
   - **Section 2:** Cross-tabulation count table with columns for Gender (M/F/TG), Age Group, Occupation, Domicile, Education, Family Type, Economic Status, Frequency, Family History, Recidivism (before/after), Nature of Offence

2. **Given** the report parameters (month, year)
   **When** the report is generated
   **Then** the file matches the exact layout and styling of `docs/KavalSample.xlsx`

**New files:**
- `apps/api/Infrastructure/Reports/ProfileReportService.cs` — report generation logic
- API endpoint in ReportsController
- Web UI option in reports page

---

## Epic 16: Excel Migration Mapping Update

### Story 16.1 — Update Migration Spec with New Fields

**As a** Project Director,
**I want** the legacy Excel import to support the new socio-demographic fields,
**So that** historical data for these fields can be imported.

**Acceptance Criteria:**

1. **Given** the new Case fields are added
   **When** I view `mapping-spec.json`
   **Then** it includes mapping rules for Gender, FamilyType, EconomicStatus, Occupation, EducationLevel, Recidivism fields, FamilyHistoryOfCrime

2. **Given** the updated spec
   **When** a legacy Excel contains these columns
   **Then** the import maps and stores them correctly

**Files to change:**
- `docs/excel-migration/mapping-spec.json` — add new field mappings
- `apps/api/Infrastructure/Migration/MappingSpecLoader.cs` — update hardcoded fallback defaults
- `apps/api/Infrastructure/Migration/MigrationImportService.cs` — handle new fields

---

## Summary: All Epics and Stories

| Epic | Stories | Priority |
|------|---------|----------|
| **Epic 11**: Enhanced Case Fields | 11.1 (Gender/Family/Economic), 11.2 (Occupation/Education), 11.3 (Recidivism/History) | **High** — blocks the report |
| **Epic 12**: Stage Sub-Step Data | 12.1 (Stage 2), 12.2 (Stage 3), 12.3 (Stage 4), 12.4 (Stage 5), 12.5 (Stage 6) | **Medium** |
| **Epic 13**: Case Cross-Linking | 13.1 (Related Cases) | **Low** |
| **Epic 14**: Budget Module | 14.1 (CRUD), 14.2 (Utilization), 14.3 (Web UI), 14.4 (Report) | **Medium** — entirely new feature |
| **Epic 15**: Profile Report | 15.1 (Excel report) | **High** — direct output from the sample |
| **Epic 16**: Migration Update | 16.1 (Update mapping spec) | **Medium** — after Epic 11 is done |

**Recommended order:** Epic 11 → Epic 15 → Epic 16 → Epic 12 → Epic 14 → Epic 13
