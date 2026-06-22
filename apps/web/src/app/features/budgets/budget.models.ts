export interface BudgetListDto {
  id: string;
  source: string;
  financialYearStart: string;
  financialYearEnd: string;
  approvalStatus: string;
  totalAllocated: number;
  totalUtilized: number;
  createdAtUtc: string;
}

export interface BudgetDetailDto {
  id: string;
  source: string;
  financialYearStart: string;
  financialYearEnd: string;
  approvalStatus: string;
  notes?: string;
  lineItems: BudgetLineItemDto[];
  createdByUserId: string;
  approvedByUserId?: string;
  decisionComment?: string;
  decidedAtUtc?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface BudgetLineItemDto {
  id: string;
  budgetHead: string;
  amountAllocated: number;
  amountUtilized: number;
}

export interface CreateBudgetRequest {
  source: string;
  financialYearStart: string;
  financialYearEnd: string;
  notes?: string;
  lineItems: CreateBudgetLineItemRequest[];
}

export interface CreateBudgetLineItemRequest {
  budgetHead: string;
  amountAllocated: number;
}

export interface UpdateBudgetRequest {
  notes?: string;
  lineItems: UpdateBudgetLineItemRequest[];
}

export interface UpdateBudgetLineItemRequest {
  budgetHead: string;
  amountAllocated: number;
}

export interface ApproveBudgetRequest {
  decisionComment?: string;
}

export interface ReturnBudgetRequest {
  decisionComment: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BudgetUtilizationListDto {
  id: string;
  budgetLineItemId: string;
  budgetHead: string;
  caseId?: string;
  caseCrimeNumber?: string;
  amountUtilized: number;
  utilizationDate: string;
  description: string;
  createdAtUtc: string;
}

export interface BudgetUtilizationSummaryDto {
  budgetId: string;
  headSummaries: BudgetHeadSummaryDto[];
  totalAllocated: number;
  totalUtilized: number;
  totalBalance: number;
  overallUtilizationPercentage: number;
}

export interface BudgetHeadSummaryDto {
  budgetHead: string;
  allocated: number;
  utilized: number;
  balance: number;
  utilizationPercentage: number;
}

export function formatFinancialYear(start: string, end: string): string {
  const startYear = start.substring(0, 4);
  const endShort = end.substring(2, 4);
  return `${startYear}-${endShort}`;
}

export function formatAmount(amount: number): string {
  return amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export const BUDGET_SOURCE_OPTIONS = [
  { value: 'DCPU', label: 'DCPU' },
  { value: 'NPO (Dale View)', label: 'NPO (Dale View)' },
] as const;

export const BUDGET_HEAD_OPTIONS = [
  { value: 'Honorarium', label: 'Honorarium' },
  { value: 'TravelExpenses', label: 'Travel Expenses' },
  { value: 'ParentManagementTraining', label: 'Parent Management Training' },
  { value: 'LifeSkillsTraining', label: 'Life Skills Training' },
  { value: 'PsychosocialSupport', label: 'Psychosocial Support' },
  { value: 'AdministrativeExpenses', label: 'Administrative Expenses' },
  { value: 'StationeryExpenses', label: 'Stationery Expenses' },
] as const;

export const BUDGET_APPROVAL_STATUS_LABELS: Record<string, string> = {
  Draft: 'Draft',
  Proposed: 'Proposed',
  Returned: 'Returned',
  Approved: 'Approved',
  Executed: 'Executed',
};

export const BUDGET_STATUS_COLORS: Record<string, string> = {
  Draft: '#9E9E9E',
  Proposed: '#1976D2',
  Returned: '#F57C00',
  Approved: '#388E3C',
  Executed: '#00897B',
};
