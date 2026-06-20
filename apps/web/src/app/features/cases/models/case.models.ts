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
