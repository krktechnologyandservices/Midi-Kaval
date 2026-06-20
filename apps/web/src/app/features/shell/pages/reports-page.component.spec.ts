import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReportsApiService } from '../services/reports-api.service';
import { ReportExportJobDto } from '../reports.models';
import { ReportsPageComponent } from './reports-page.component';

const pendingJob: ReportExportJobDto = {
  jobId: 'job-1',
  status: 'pending',
  reportType: 'daily-work',
  format: 'excel',
  createdAtUtc: '2026-06-20T10:00:00Z',
  completedAtUtc: null,
  downloadUrl: null,
  errorMessage: null,
};

const completedJob: ReportExportJobDto = {
  jobId: 'job-2',
  status: 'completed',
  reportType: 'yearly-work',
  format: 'pdf',
  createdAtUtc: '2026-06-19T14:30:00Z',
  completedAtUtc: '2026-06-19T14:35:00Z',
  downloadUrl: 'https://blob.example.com/report.pdf?sig=abc',
  errorMessage: null,
};

const failedJob: ReportExportJobDto = {
  jobId: 'job-3',
  status: 'failed',
  reportType: 'workload-distribution',
  format: 'excel',
  createdAtUtc: '2026-06-18T09:00:00Z',
  completedAtUtc: '2026-06-18T09:00:05Z',
  downloadUrl: null,
  errorMessage: 'Failed to generate report',
};

const processingJob: ReportExportJobDto = {
  jobId: 'job-4',
  status: 'processing',
  reportType: 'interventions',
  format: 'pdf',
  createdAtUtc: '2026-06-20T11:00:00Z',
  completedAtUtc: null,
  downloadUrl: null,
  errorMessage: null,
};

describe('ReportsPageComponent', () => {
  let fixture: ComponentFixture<ReportsPageComponent>;
  let reportsApi: jasmine.SpyObj<ReportsApiService>;

  function getJobList(items: ReportExportJobDto[]) {
    return { items, totalCount: items.length };
  }

  beforeEach(async () => {
    reportsApi = jasmine.createSpyObj('ReportsApiService', [
      'startExport',
      'getExportStatus',
      'listExports',
      'extractErrorMessage',
    ]);
    reportsApi.listExports.and.resolveTo(getJobList([pendingJob, completedJob, failedJob]));
    reportsApi.extractErrorMessage.and.returnValue('Failed to load exports');
    reportsApi.startExport.and.resolveTo(pendingJob);
    reportsApi.getExportStatus.and.resolveTo({ status: 'completed', downloadUrl: null, errorMessage: null });

    await TestBed.configureTestingModule({
      imports: [ReportsPageComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ReportsApiService, useValue: reportsApi },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ReportsPageComponent);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  describe('loading state', () => {
    it('shows skeleton rows while loading', () => {
      const component = fixture.componentInstance;
      component.loading.set(true);
      fixture.detectChanges();

      const skeletonTable = fixture.nativeElement.querySelector('.skeleton-table');
      expect(skeletonTable).toBeTruthy();
      const skeletonRows = skeletonTable.querySelectorAll('.skeleton-row');
      expect(skeletonRows.length).toBe(5);
    });

    it('hides skeleton after loading completes', async () => {
      await settle();
      const skeletonTable = fixture.nativeElement.querySelector('.skeleton-table');
      expect(skeletonTable).toBeNull();
    });
  });

  describe('error state', () => {
    it('shows error message and retry button for jobs list', async () => {
      reportsApi.listExports.and.rejectWith(new Error('Network error'));
      await settle();

      const error = fixture.nativeElement.querySelector('[role="alert"]');
      expect(error).toBeTruthy();
      expect(error.textContent).toContain('Failed to load exports');

      const retryButton = fixture.nativeElement.querySelector('.error-state button');
      expect(retryButton).toBeTruthy();
    });

    it('retry button calls loadJobs() again', async () => {
      reportsApi.listExports.and.rejectWith(new Error('Network error'));
      await settle();

      reportsApi.listExports.and.resolveTo(getJobList([pendingJob]));
      const retryButton = fixture.nativeElement.querySelector('.error-state button') as HTMLButtonElement;
      retryButton.click();

      await settle();
      expect(reportsApi.listExports).toHaveBeenCalledTimes(2);
      const rows = fixture.nativeElement.querySelectorAll('mat-row');
      expect(rows.length).toBe(1);
    });
  });

  describe('empty state', () => {
    it('shows empty state when no exports exist', async () => {
      reportsApi.listExports.and.resolveTo(getJobList([]));
      await settle();

      const emptyState = fixture.nativeElement.querySelector('.empty-state');
      expect(emptyState).toBeTruthy();
      expect(emptyState.textContent).toContain('No exports yet');
    });
  });

  describe('export form', () => {
    it('renders report type selector', async () => {
      await settle();
      const select = fixture.nativeElement.querySelector('.field-type');
      expect(select).toBeTruthy();
    });

    it('renders format toggle group', async () => {
      await settle();
      const toggleGroup = fixture.nativeElement.querySelector('mat-button-toggle-group');
      expect(toggleGroup).toBeTruthy();
    });

    it('start export button is disabled when no type selected', async () => {
      await settle();
      const button = fixture.nativeElement.querySelector('.export-actions button') as HTMLButtonElement;
      expect(button.disabled).toBeTrue();
    });

    it('calls startExport when form is valid and button clicked', async () => {
      const component = fixture.componentInstance;
      component.selectedType.set('daily-work');
      component.selectedFormat.set('excel');
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector('.export-actions button') as HTMLButtonElement;
      expect(button.disabled).toBeFalse();

      button.click();
      await settle();

      expect(reportsApi.startExport).toHaveBeenCalledWith('daily-work', {
        format: 'excel',
        from: null,
        to: null,
        year: null,
      });
    });

    it('shows export error when startExport fails', async () => {
      reportsApi.startExport.and.rejectWith(new Error('Export failed'));
      const component = fixture.componentInstance;
      component.selectedType.set('daily-work');
      component.selectedFormat.set('excel');
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector('.export-actions button') as HTMLButtonElement;
      button.click();
      await settle();

      const exportError = fixture.nativeElement.querySelector('.export-error');
      expect(exportError).toBeTruthy();
      expect(exportError.textContent).toContain('Failed to load exports');
    });

    it('start export button is disabled during export', async () => {
      reportsApi.startExport.and.callFake(async () => {
        return new Promise<ReportExportJobDto>(() => {}); // never resolves
      });
      const component = fixture.componentInstance;
      component.selectedType.set('daily-work');
      component.selectedFormat.set('excel');
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector('.export-actions button') as HTMLButtonElement;
      button.click();
      fixture.detectChanges();

      expect(button.disabled).toBeTrue();
    });
  });

  describe('jobs table', () => {
    it('renders all jobs from API', async () => {
      await settle();
      const rows = fixture.nativeElement.querySelectorAll('mat-row');
      expect(rows.length).toBe(3);
    });

    it('shows status chips with correct labels', async () => {
      await settle();
      const chips = fixture.nativeElement.querySelectorAll('.status-chip');
      expect(chips.length).toBe(3);
      expect(chips[0].textContent).toContain('Pending');
      expect(chips[1].textContent).toContain('Completed');
      expect(chips[2].textContent).toContain('Failed');
    });

    it('shows download button for completed jobs', async () => {
      await settle();
      const buttons = fixture.nativeElement.querySelectorAll('.jobs-table button');
      const downloadButtons = Array.from(buttons).filter(
        (b) => (b as HTMLButtonElement).textContent?.trim() === 'Download',
      );
      expect(downloadButtons.length).toBe(1);
    });

    it('shows error detail for failed jobs', async () => {
      await settle();
      const errors = fixture.nativeElement.querySelectorAll('.error-detail');
      expect(errors.length).toBe(1);
      expect(errors[0].textContent).toContain('Failed to generate report');
    });

    it('shows expired label for completed jobs without downloadUrl', async () => {
      reportsApi.listExports.and.resolveTo(
        getJobList([
          {
            ...completedJob,
            downloadUrl: null,
          },
        ]),
      );
      await settle();

      const expiredLabel = fixture.nativeElement.querySelector('.expired-label');
      expect(expiredLabel).toBeTruthy();
      expect(expiredLabel.textContent).toContain('Expired');
    });
  });

  describe('report type display', () => {
    it('shows correct display name for report types', async () => {
      await settle();
      const cells = fixture.nativeElement.querySelectorAll('.cell-report-type');
      expect(cells[0].textContent).toContain('Daily Work');
      expect(cells[1].textContent).toContain('Yearly Work');
    });
  });

  describe('page title', () => {
    it('shows page header', async () => {
      await settle();
      const header = fixture.nativeElement.querySelector('h1');
      expect(header.textContent).toContain('Reports');
    });
  });
});
