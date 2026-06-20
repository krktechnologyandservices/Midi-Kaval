import type { ApiEnvelope } from '../cases/models/case.models';

export interface AttachmentSummaryDto {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  confirmedAtUtc: string;
}

export interface TravelClaimDto {
  id: string;
  claimDate: string;
  startLocation: string;
  destination: string;
  transportMode: string;
  amount: number;
  autoNumber?: string | null;
  notes?: string | null;
  status: string;
  claimantUserId: string;
  claimantEmail?: string | null;
  submittedAtUtc?: string | null;
  decisionComment?: string | null;
  decidedAtUtc?: string | null;
  decidedByUserId?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  caseIds: string[];
  attachments: AttachmentSummaryDto[];
}

export interface TravelClaimListResultDto {
  items: TravelClaimDto[];
}

export interface ApproveTravelClaimRequest {
  comment?: string | null;
}

export interface ReturnTravelClaimRequest {
  comment?: string | null;
}

export interface CrisisQueueItemDto {
  rowType: string;
  severity: string;
  badgeLabel: string;
  caseId: string;
  courtSittingId?: string | null;
  travelClaimId?: string | null;
  assignedWorkerUserId?: string | null;
  claimantUserId?: string | null;
  claimantEmail?: string | null;
  amount?: number | null;
  receiptCount?: number | null;
  crimeNumber?: string | null;
  stNumber?: string | null;
  scheduledAtUtc?: string | null;
  title: string;
  detail: string;
  visitId?: string | null;
  overdueVisitCount?: number | null;
  visitScheduledAtUtc?: string | null;
  transferredAtUtc?: string | null;
  previousWorkerName?: string | null;
  courtSittingStatus?: string | null;
}

export interface CrisisQueueListResultDto {
  items: CrisisQueueItemDto[];
}
