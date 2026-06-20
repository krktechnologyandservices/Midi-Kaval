export interface ReportTypeInfo {
  key: string;
  displayName: string;
  description: string;
  supportsDateRange: boolean;
  supportsYear: boolean;
}

export const REPORT_TYPES: ReportTypeInfo[] = [
  {
    key: 'daily-work',
    displayName: 'Daily Work',
    description: 'Cases with visits scheduled or completed today, grouped by worker',
    supportsDateRange: false,
    supportsYear: false,
  },
  {
    key: 'yearly-work',
    displayName: 'Yearly Work',
    description: 'Aggregate case and work statistics for a given year',
    supportsDateRange: false,
    supportsYear: true,
  },
  {
    key: 'visits-planned-vs-completed',
    displayName: 'Visits Planned vs Completed',
    description: 'Visit counts by worker, planned versus completed, for a date range',
    supportsDateRange: true,
    supportsYear: false,
  },
  {
    key: 'interventions',
    displayName: 'Interventions',
    description: 'Interventions by status, outcome, and worker for a date range',
    supportsDateRange: true,
    supportsYear: false,
  },
  {
    key: 'court-summary',
    displayName: 'Court Summary',
    description: 'Court sittings by status and outcome for a date range',
    supportsDateRange: true,
    supportsYear: false,
  },
  {
    key: 'offence-area-counts',
    displayName: 'Offence Area Counts',
    description: 'Case counts grouped by offence classification and domicile',
    supportsDateRange: false,
    supportsYear: false,
  },
  {
    key: 'workload-distribution',
    displayName: 'Workload Distribution',
    description: 'Active case counts per worker — distribution only',
    supportsDateRange: false,
    supportsYear: false,
  },
  {
    key: 'travel-totals',
    displayName: 'Travel Totals',
    description: 'Travel claim totals by worker for a date range',
    supportsDateRange: true,
    supportsYear: false,
  },
];

export interface ReportExportJobDto {
  jobId: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  reportType: string;
  format: string;
  createdAtUtc: string;
  completedAtUtc: string | null;
  downloadUrl: string | null;
  errorMessage: string | null;
}

export interface ReportExportStatusDto {
  status: 'pending' | 'processing' | 'completed' | 'failed';
  downloadUrl: string | null;
  errorMessage: string | null;
}

export interface ReportExportRequest {
  format: 'excel' | 'pdf';
  from?: string | null;
  to?: string | null;
  year?: number | null;
}

export function getReportTypeInfo(key: string): ReportTypeInfo | undefined {
  return REPORT_TYPES.find((t) => t.key === key);
}

export function displayFormat(format: string): string {
  return format === 'excel' ? 'Excel' : 'PDF';
}
