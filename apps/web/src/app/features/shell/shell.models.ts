export interface CasesByStageDto {
  stage: string;
  count: number;
}

export interface CasesByOffenceClassificationDto {
  offenceClassification: string;
  count: number;
}

export interface CasesByDomicileDto {
  domicile: string;
  count: number;
}

export interface CasesByStaffDto {
  workerName: string;
  workerId: string;
  caseCount: number;
}

export interface OverdueVisitsDto {
  totalOverdue: number;
  uniqueCasesAffected: number;
}

export interface InterventionsGaugeDto {
  inProgress: number;
  overdue: number;
  completedThisMonth: number;
}

export interface CourtThisWeekDto {
  totalUpcoming: number;
  attendedSoFar: number;
  totalCasesWithSittings: number;
}

export interface PendingClaimsDto {
  pendingCount: number;
  totalAmountPending: number;
  oldestPendingDays: number;
}

export interface IntakeTrendPointDto {
  month: string;
  count: number;
}

export interface DashboardResultDto {
  casesByStage: CasesByStageDto[];
  casesByOffenceClassification: CasesByOffenceClassificationDto[];
  casesByDomicile: CasesByDomicileDto[];
  casesByStaff: CasesByStaffDto[];
  overdueVisits: OverdueVisitsDto;
  interventionsGauge: InterventionsGaugeDto;
  courtThisWeek: CourtThisWeekDto;
  pendingClaims: PendingClaimsDto;
  intakeTrend: IntakeTrendPointDto[];
}
