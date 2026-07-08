import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ReportsApiService } from '../services/reports-api.service';
import { SocioDemographicReportDialogComponent } from '../../reports/socio-demographic-report-dialog.component';
import {
  REPORT_TYPES,
  ReportExportJobDto,
  ReportTypeInfo,
  displayFormat,
  getReportTypeInfo,
} from '../reports.models';

const POLL_INTERVAL_MS = 10000;

function toIsoDateOnly(date: Date | null): string | null {
  if (!date) return null;
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
const SKELETON_ROW_COUNT = 5;

@Component({
  selector: 'app-reports-page',
  providers: [provideNativeDateAdapter()],
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './reports-page.component.html',
  styleUrl: './reports-page.component.scss',
})
export class ReportsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(ReportsApiService);
  private readonly dialog = inject(MatDialog);
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private loadingGuard = false;

  // Template helpers (expose imports for AOT)
  readonly reportTypes = REPORT_TYPES;
  protected readonly isNaN = globalThis.isNaN;
  protected readonly Number = globalThis.Number;

  // Shared state
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Report type selection form
  readonly selectedType = signal<string | null>(null);
  readonly selectedFormat = signal<'excel' | 'pdf' | null>(null);
  readonly fromDate = signal<string | null>(null);
  readonly toDate = signal<string | null>(null);
  readonly year = signal<number | null>(null);
  readonly exporting = signal(false);
  readonly exportError = signal<string | null>(null);

  readonly selectedTypeInfo = computed<ReportTypeInfo | undefined>(() => {
    const key = this.selectedType();
    if (!key) return undefined;
    return getReportTypeInfo(key);
  });

  readonly canStartExport = computed(() => {
    if (this.exporting()) return false;
    if (!this.selectedType() || !this.selectedFormat()) return false;
    const info = this.selectedTypeInfo();
    if (!info) return false;
    return true;
  });

  readonly showDateRange = computed(() => this.selectedTypeInfo()?.supportsDateRange ?? false);
  readonly showYear = computed(() => this.selectedTypeInfo()?.supportsYear ?? false);

  // Export jobs list
  readonly jobs = signal<ReportExportJobDto[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(20);

  readonly skeletonCount = SKELETON_ROW_COUNT;

  readonly subtitle = computed(() => {
    const count = this.jobs().length;
    if (count > 0) {
      return `${this.totalCount()} export${this.totalCount() === 1 ? '' : 's'}`;
    }
    return 'Standard operational reports';
  });

  readonly displayedColumns = ['reportType', 'format', 'createdAt', 'status', 'actions'];

  async ngOnInit(): Promise<void> {
    await this.loadJobs();
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  async loadJobs(): Promise<void> {
    if (this.loadingGuard) {
      return;
    }
    this.loadingGuard = true;
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const result = await this.api.listExports(this.page(), this.pageSize());
      this.jobs.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
      this.loadingGuard = false;
    }
  }

  async startExport(): Promise<void> {
    const type = this.selectedType();
    const format = this.selectedFormat();
    if (!type || !format) return;

    this.exporting.set(true);
    this.exportError.set(null);
    try {
      const job = await this.api.startExport(type, {
        format,
        from: this.fromDate(),
        to: this.toDate(),
        year: this.year(),
      });
      this.jobs.update((prev) => [job, ...prev]);
      this.totalCount.update((c) => c + 1);
      // Reset optional fields (keep type and format for convenience)
      this.fromDate.set(null);
      this.toDate.set(null);
      this.year.set(null);
    } catch (error) {
      this.exportError.set(this.api.extractErrorMessage(error));
    } finally {
      this.exporting.set(false);
    }
  }

  openSocioDemographicDialog(): void {
    this.dialog.open(SocioDemographicReportDialogComponent, {
      width: '450px',
    });
  }

  downloadJob(job: ReportExportJobDto): void {
    if (job.downloadUrl) {
      window.open(job.downloadUrl, '_blank');
    }
  }

  onPageChange(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    void this.loadJobs();
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'pending':
        return 'Pending';
      case 'processing':
        return 'Processing';
      case 'completed':
        return 'Completed';
      case 'failed':
        return 'Failed';
      default:
        return status;
    }
  }

  getReportDisplayName(reportType: string): string {
    const info = getReportTypeInfo(reportType);
    return info?.displayName ?? reportType;
  }

  formatReportFormat(format: string): string {
    return displayFormat(format);
  }

  onFromDateChange(date: Date | null): void {
    this.fromDate.set(toIsoDateOnly(date));
  }

  onToDateChange(date: Date | null): void {
    this.toDate.set(toIsoDateOnly(date));
  }

  formatTimestamp(utc: string | null): string {
    if (!utc) return '—';
    const date = new Date(utc);
    if (isNaN(date.getTime())) return utc;
    return date.toLocaleString('en-IN', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  private startPolling(): void {
    this.pollTimer = setInterval(async () => {
      const active = this.jobs().filter(
        (j) => j.status === 'pending' || j.status === 'processing',
      );
      if (active.length === 0) return;
      await this.refreshActiveJobs();
    }, POLL_INTERVAL_MS);
  }

  private async refreshActiveJobs(): Promise<void> {
    try {
      const result = await this.api.listExports(1, 100);
      const activeIds = this.jobs()
        .filter((j) => j.status === 'pending' || j.status === 'processing')
        .map((j) => j.jobId);
      if (activeIds.length === 0) return;
      this.jobs.update((prev) =>
        prev.map(
          (job) =>
            result.items.find((u) => u.jobId === job.jobId && activeIds.includes(u.jobId)) ??
            job,
        ),
      );
    } catch {
      // Silently ignore polling errors — keep stale data visible
    }
  }

  private stopPolling(): void {
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
