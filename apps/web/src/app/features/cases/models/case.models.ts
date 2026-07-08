import type { components } from '@midi-kaval/api-client';

export type CreateCaseRequest = components['schemas']['CreateCaseRequest'];
export type CaseDto = components['schemas']['CaseDto'];
export type CaseSummaryDto = components['schemas']['CaseSummaryDto'];
export type CaseSearchResultDto = components['schemas']['CaseSearchResultDto'];
export type CaseSearchFiltersDto = components['schemas']['CaseSearchFiltersDto'];
export type CaseSearchPresetDto = components['schemas']['CaseSearchPresetDto'];
export type CreateCaseSearchPresetRequest = components['schemas']['CreateCaseSearchPresetRequest'];
export type CheckCaseDuplicateRequest = components['schemas']['CheckCaseDuplicateRequest'];
export type CheckCaseDuplicateResultDto = components['schemas']['CheckCaseDuplicateResultDto'];
export type CaseDuplicateMatchDto = components['schemas']['CaseDuplicateMatchDto'];
export type CaseDetailDto = components['schemas']['CaseDetailDto'];
export type HandoffWhisperDto = components['schemas']['HandoffWhisperDto'];
export type TransferCaseRequest = components['schemas']['TransferCaseRequest'];
export type FieldWorkerUserDto = components['schemas']['FieldWorkerUserDto'];
export type TransitionCaseStageRequest = components['schemas']['TransitionCaseStageRequest'];
export type CaseNoteDto = components['schemas']['CaseNoteDto'];
export type CaseNoteListResultDto = components['schemas']['CaseNoteListResultDto'];
export type CreateCaseNoteRequest = components['schemas']['CreateCaseNoteRequest'];
export type InterventionDto = components['schemas']['InterventionDto'];
export type InterventionListResultDto = components['schemas']['InterventionListResultDto'];
export type CreateInterventionRequest = components['schemas']['CreateInterventionRequest'];
export type UpdateInterventionRequest = components['schemas']['UpdateInterventionRequest'];
export type CourtSittingDto = components['schemas']['CourtSittingDto'];
export type CourtSittingListResultDto = components['schemas']['CourtSittingListResultDto'];
export type CreateCourtSittingRequest = components['schemas']['CreateCourtSittingRequest'];
export type UpdateCourtSittingRequest = components['schemas']['UpdateCourtSittingRequest'];
export type NotificationDto = components['schemas']['NotificationDto'];
export type NotificationListResultDto = components['schemas']['NotificationListResultDto'];
export type UnreadCountDto = { count: number };
export type AttachmentSummaryDto = components['schemas']['AttachmentSummaryDto'];
export type AttachmentPresignRequest = components['schemas']['AttachmentPresignRequest'];
export type AttachmentPresignResultDto = components['schemas']['AttachmentPresignResultDto'];
export type AttachmentConfirmRequest = components['schemas']['AttachmentConfirmRequest'];
export type AttachmentDto = components['schemas']['AttachmentDto'];
export type AttachmentDownloadUrlDto = components['schemas']['AttachmentDownloadUrlDto'];

// Hand-written rather than pulled from the generated client: the client's VisitListItemDto
// predates the assigneeUserId/assigneeEmail/cancellationReason fields and has no
// CancelVisitRequest schema at all, since the cancel endpoint is new.
export interface VisitListItemDto {
  id: string;
  assigneeUserId: string;
  assigneeEmail?: string | null;
  scheduledAtUtc: string;
  status: string;
  isOverdue: boolean;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  completionNote?: string | null;
  lastRescheduleReason?: string | null;
  cancellationReason?: string | null;
  places: VisitPlaceDto[];
}

export interface VisitPlaceDto {
  id: string;
  visitId: string;
  address: string;
  osmReference?: string | null;
  plannedLatitude?: number | null;
  plannedLongitude?: number | null;
  createdAtUtc: string;
  loggedLatitude?: number | null;
  loggedLongitude?: number | null;
  loggedAtUtc?: string | null;
  loggedByEmail?: string | null;
}

export interface AddVisitPlaceRequest {
  address: string;
  osmReference?: string | null;
  plannedLatitude?: number | null;
  plannedLongitude?: number | null;
}

export interface ScheduleVisitRequest {
  scheduledAtUtc: string;
  assigneeUserId?: string | null;
}

export interface CancelVisitRequest {
  reason: string;
}

export interface GeocodingResultDto {
  displayName: string;
  latitude: number;
  longitude: number;
  osmReference?: string | null;
}

export const CASE_NOTE_TYPES = ['Visit', 'Court', 'Intervention', 'General'] as const;
export type CaseNoteType = (typeof CASE_NOTE_TYPES)[number];

export const INTERVENTION_DIRECTIONS = ['Needed', 'Provided'] as const;
export type InterventionDirection = (typeof INTERVENTION_DIRECTIONS)[number];
export const INTERVENTION_PRIORITIES = ['High', 'Medium', 'Low'] as const;
export type InterventionPriority = (typeof INTERVENTION_PRIORITIES)[number];
export const INTERVENTION_STATUSES = ['Open', 'InProgress', 'Completed', 'Cancelled'] as const;
export type InterventionStatus = (typeof INTERVENTION_STATUSES)[number];

export const COURT_SITTING_STATUSES = ['Upcoming', 'Attended', 'Postponed'] as const;
export type CourtSittingStatus = (typeof COURT_SITTING_STATUSES)[number];

export const ALLOWED_ATTACHMENT_CONTENT_TYPES = [
  'image/jpeg',
  'image/png',
  'image/webp',
  'application/pdf',
] as const;

export const MAX_ATTACHMENT_BYTES = 10485760;

export function attachmentBasename(fileName: string): string {
  const normalized = fileName.replace(/\\/g, '/');
  const segments = normalized.split('/');
  return segments[segments.length - 1] ?? fileName;
}

export interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string; totalCount?: number | null };
}

export const OFFENCE_CLASSIFICATIONS = ['Petty', 'Serious', 'Heinous'] as const;
export const DOMICILE_OPTIONS = ['Urban', 'Rural', 'Coastal', 'Tribal', 'Slum'] as const;
export const CASE_STAGES = [
  'ProcessInitiation',
  'MaintainAndDevelopment',
  'InterSectoralApproach',
  'Rehabilitation',
  'Reintegration',
  'TerminationExclusion',
] as const;

export function nextCaseStage(currentStage: string | null | undefined): string | null {
  if (!currentStage) {
    return null;
  }

  const index = CASE_STAGES.indexOf(currentStage as (typeof CASE_STAGES)[number]);
  if (index < 0 || index >= CASE_STAGES.length - 1) {
    return null;
  }

  return CASE_STAGES[index + 1];
}

export function caseStageIndex(stage: string | null | undefined): number {
  if (!stage) {
    return -1;
  }
  return CASE_STAGES.indexOf(stage as (typeof CASE_STAGES)[number]);
}

// currentStage is stored/transmitted as the raw enum key (e.g. "MaintainAndDevelopment") and,
// until this lookup existed, was rendered to users verbatim — a first-time user has no way to
// know what that key means. This maps each key to a short display name and one-line
// description shown alongside the stage stepper.
export interface CaseStageInfo {
  key: (typeof CASE_STAGES)[number];
  label: string;
  description: string;
}

export const CASE_STAGE_INFO: Record<(typeof CASE_STAGES)[number], CaseStageInfo> = {
  ProcessInitiation: {
    key: 'ProcessInitiation',
    label: 'Process Initiation',
    description: 'The case has been opened and the initial case details have been recorded.',
  },
  MaintainAndDevelopment: {
    key: 'MaintainAndDevelopment',
    label: 'Maintenance & Development',
    description:
      'The assigned worker maintains regular contact and develops the ongoing intervention plan.',
  },
  InterSectoralApproach: {
    key: 'InterSectoralApproach',
    label: 'Inter-Sectoral Approach',
    description:
      'Support is coordinated across the departments and agencies involved in the case (health, education, police, etc.).',
  },
  Rehabilitation: {
    key: 'Rehabilitation',
    label: 'Rehabilitation',
    description: 'Rehabilitation services and support are actively provided to the beneficiary.',
  },
  Reintegration: {
    key: 'Reintegration',
    label: 'Reintegration',
    description: "Preparing and supporting the beneficiary's return to family and community.",
  },
  TerminationExclusion: {
    key: 'TerminationExclusion',
    label: 'Termination / Exclusion',
    description: 'The case is formally closed. No further stage transitions are possible.',
  },
};

export function caseStageLabel(stage: string | null | undefined): string {
  if (!stage) {
    return '—';
  }
  return CASE_STAGE_INFO[stage as (typeof CASE_STAGES)[number]]?.label ?? stage;
}

// Stage 2–6 data models. Hand-written (not from the generated @midi-kaval/api-client) because
// the generated client has never been regenerated against these endpoints — same staleness
// issue documented elsewhere in this codebase (see auth.models.ts). Field names/shapes mirror
// apps/api/Models/Cases/Stage{2..6}DataDtos.cs exactly.

export interface Stage2DataDto {
  id?: string;
  caseId?: string;
  bioPsychoSocialAssessment?: string | null;
  icpRecords?: string | null;
  lifeSkillTraining?: string | null;
  parentManagement?: string | null;
  groupWork?: string | null;
  communityProgramAttendance?: string | null;
  pmaStatus?: string | null;
  overallProgress?: string | null;
  createdByUserId?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpsertStage2DataRequest {
  bioPsychoSocialAssessment?: string | null;
  icpRecords?: string | null;
  lifeSkillTraining?: string | null;
  parentManagement?: string | null;
  groupWork?: string | null;
  communityProgramAttendance?: string | null;
  pmaStatus?: string | null;
  overallProgress?: string | null;
}

export interface Stage3SupportDto {
  id?: string;
  caseId?: string;
  supportType: string;
  providerName?: string | null;
  notes?: string | null;
  providedStatus: boolean;
  createdByUserId?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface Stage3SupportItemRequest {
  supportType: string;
  providerName?: string | null;
  notes?: string | null;
  providedStatus: boolean;
}

export interface UpsertStage3SupportsRequest {
  items: Stage3SupportItemRequest[];
}

export interface Stage4PlacementDto {
  id?: string;
  caseId?: string;
  placementType: string;
  institutionName?: string | null;
  address?: string | null;
  startDate: string;
  createdByUserId?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpsertStage4PlacementRequest {
  placementType: string;
  institutionName?: string | null;
  address?: string | null;
  startDate: string;
}

export interface Stage5ReintegrationDto {
  id?: string;
  caseId?: string;
  reintegrationLevel: string;
  institutionDetails?: string | null;
  createdByUserId?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpsertStage5ReintegrationRequest {
  reintegrationLevel: string;
  institutionDetails?: string | null;
}

export interface Stage6TerminationExclusionDto {
  id?: string;
  caseId?: string;
  terminationExclusionType: string;
  jjbDetails?: string | null;
  exclusionReason?: string | null;
  reportAttachmentId?: string | null;
  createdByUserId?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpsertStage6TerminationExclusionRequest {
  terminationExclusionType: string;
  jjbDetails?: string | null;
  exclusionReason?: string | null;
  reportAttachmentId?: string | null;
}

export interface CaseSearchParams {
  q?: string;
  currentStage?: string;
  typeOfOffence?: string;
  offenceClassification?: string;
  domicile?: string;
  createdByUserId?: string;
  assignedWorkerUserId?: string;
  overdue?: boolean;
  page?: number;
  pageSize?: number;
}
