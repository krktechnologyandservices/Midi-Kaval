import type {components} from '@midi-kaval/api-client';

export type CreateCaseRequest = components['schemas']['CreateCaseRequest'];
export type CaseDto = components['schemas']['CaseDto'];
export type CheckCaseDuplicateRequest =
  components['schemas']['CheckCaseDuplicateRequest'];
export type CheckCaseDuplicateResultDto =
  components['schemas']['CheckCaseDuplicateResultDto'];
export type CaseDuplicateMatchDto =
  components['schemas']['CaseDuplicateMatchDto'];
export type CaseSummaryDto = components['schemas']['CaseSummaryDto'] & {
  sensitivityLevel?: string;
};
export type CaseSearchResultDto = components['schemas']['CaseSearchResultDto'];
export type CaseDetailDto = components['schemas']['CaseDetailDto'] & {
  sensitivityLevel?: string;
  domicile?: string;
};
// currentStage is transmitted as the raw backend enum key (e.g. "MaintainAndDevelopment") and
// was being shown to field workers verbatim with zero translation. Mirrors the same lookup
// added to the web app's case.models.ts (apps/web/.../features/cases/models/case.models.ts) so
// stage terminology is consistent between web and mobile.
const CASE_STAGE_LABELS: Record<string, string> = {
  ProcessInitiation: 'Process Initiation',
  MaintainAndDevelopment: 'Maintenance & Development',
  InterSectoralApproach: 'Inter-Sectoral Approach',
  Rehabilitation: 'Rehabilitation',
  Reintegration: 'Reintegration',
  TerminationExclusion: 'Termination / Exclusion',
};

export function caseStageLabel(stage: string | null | undefined): string {
  if (!stage) {
    return '—';
  }
  return CASE_STAGE_LABELS[stage] ?? stage;
}

export type HandoffWhisperDto = components['schemas']['HandoffWhisperDto'];
export type VerifyCaseGpsRequest = components['schemas']['VerifyCaseGpsRequest'];
export type CaseGpsDto = components['schemas']['CaseGpsDto'];

export type RevealCasePiiResponse = {
  beneficiaryName: string;
  beneficiaryAge?: number | null;
  beneficiaryContact?: string | null;
};

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
export type CourtSittingScheduleItemDto = components['schemas']['CourtSittingScheduleItemDto'];
export type CourtSittingUpcomingListResultDto =
  components['schemas']['CourtSittingUpcomingListResultDto'];
export type FieldWorkerUserDto = components['schemas']['FieldWorkerUserDto'];
// Hand-written rather than pulled from the generated client: the generated schema
// still has the old presign/confirm/downloadUrl shape from before attachments were
// switched to a single encrypted upload/download flow through the API.
export interface AttachmentSummaryDto {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  confirmedAtUtc: string;
}

export interface AttachmentDto {
  id: string;
  resourceType: string;
  resourceId: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: string;
  uploadedByUserId: string;
  createdAtUtc: string;
  confirmedAtUtc?: string | null;
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

export const OFFENCE_CLASSIFICATIONS = ['Petty', 'Serious', 'Heinous'] as const;
export const DOMICILE_OPTIONS = [
  'Urban',
  'Rural',
  'Coastal',
  'Tribal',
  'Slum',
] as const;

export function buildDuplicateCheckRequest(
  crimeNumber: string,
  stNumber: string,
): CheckCaseDuplicateRequest {
  const body: CheckCaseDuplicateRequest = {};
  const crime = crimeNumber.trim();
  const st = stNumber.trim();
  if (crime) {
    body.crimeNumber = crime;
  }
  if (st) {
    body.stNumber = st;
  }
  return body;
}
