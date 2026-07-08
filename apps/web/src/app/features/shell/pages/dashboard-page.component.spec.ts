import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardApiService } from '../services/dashboard-api.service';
import { DashboardResultDto } from '../shell.models';
import { DashboardPageComponent } from './dashboard-page.component';

const fullDashboardData: DashboardResultDto = {
  casesByStage: [
    { stage: 'ProcessInitiation', count: 12 },
    { stage: 'MaintainAndDevelopment', count: 8 },
  ],
  casesByOffenceClassification: [
    { offenceClassification: 'Petty', count: 5 },
    { offenceClassification: 'Serious', count: 10 },
  ],
  casesByDomicile: [
    { domicile: 'Urban', count: 9 },
    { domicile: 'Rural', count: 6 },
  ],
  casesByStaff: [
    { workerName: 'Anita', workerId: 'w1', caseCount: 7 },
    { workerName: 'Raj', workerId: 'w2', caseCount: 4 },
  ],
  overdueVisits: { totalOverdue: 3, uniqueCasesAffected: 2 },
  interventionsGauge: { inProgress: 5, overdue: 1, completedThisMonth: 8 },
  courtThisWeek: { totalUpcoming: 4, attendedSoFar: 1, totalCasesWithSittings: 3 },
  pendingClaims: { pendingCount: 6, totalAmountPending: 12500, oldestPendingDays: 14 },
  budgetHealth: {
    totalAllocated: 500000,
    totalUtilized: 320000,
    totalBalance: 180000,
    overallUtilizationPercentage: 64,
    headsNearingLimit: [],
  },
  intakeTrend: [
    { month: '2025-07', count: 10 },
    { month: '2025-08', count: 0 },
    { month: '2025-09', count: 15 },
    { month: '2025-10', count: 7 },
    { month: '2025-11', count: 12 },
    { month: '2025-12', count: 5 },
    { month: '2026-01', count: 8 },
    { month: '2026-02', count: 0 },
    { month: '2026-03', count: 11 },
    { month: '2026-04', count: 9 },
    { month: '2026-05', count: 6 },
    { month: '2026-06', count: 4 },
  ],
};

const emptyDashboardData: DashboardResultDto = {
  casesByStage: [],
  casesByOffenceClassification: [],
  casesByDomicile: [],
  casesByStaff: [],
  overdueVisits: { totalOverdue: 0, uniqueCasesAffected: 0 },
  interventionsGauge: { inProgress: 0, overdue: 0, completedThisMonth: 0 },
  courtThisWeek: { totalUpcoming: 0, attendedSoFar: 0, totalCasesWithSittings: 0 },
  pendingClaims: { pendingCount: 0, totalAmountPending: 0, oldestPendingDays: 0 },
  budgetHealth: {
    totalAllocated: 0,
    totalUtilized: 0,
    totalBalance: 0,
    overallUtilizationPercentage: 0,
    headsNearingLimit: [],
  },
  intakeTrend: [],
};

describe('DashboardPageComponent', () => {
  let fixture: ComponentFixture<DashboardPageComponent>;
  let dashboardApi: jasmine.SpyObj<DashboardApiService>;

  beforeEach(async () => {
    dashboardApi = jasmine.createSpyObj('DashboardApiService', [
      'get',
      'extractErrorMessage',
    ]);
    dashboardApi.get.and.resolveTo(fullDashboardData);
    dashboardApi.extractErrorMessage.and.returnValue('Failed to load dashboard');

    await TestBed.configureTestingModule({
      imports: [DashboardPageComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardApiService, useValue: dashboardApi },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardPageComponent);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  describe('loading state', () => {
    it('shows 9 skeleton widgets while loading', () => {
      const component = fixture.componentInstance;
      component.loading.set(true);
      fixture.detectChanges();

      const skeletons = fixture.nativeElement.querySelectorAll('.skeleton-widget');
      expect(skeletons.length).toBe(9);
    });

    it('hides skeleton after loading completes', async () => {
      await settle();
      const skeletons = fixture.nativeElement.querySelectorAll('.skeleton-widget');
      expect(skeletons.length).toBe(0);
    });
  });

  describe('error state', () => {
    it('shows error message and retry button', async () => {
      dashboardApi.get.and.rejectWith(new Error('Network error'));
      await settle();

      const errorEl = fixture.nativeElement.querySelector('[role="alert"]');
      expect(errorEl).toBeTruthy();
      expect(errorEl.textContent).toContain('Failed to load dashboard');

      const retryButton = fixture.nativeElement.querySelector('button');
      expect(retryButton).toBeTruthy();
      expect(retryButton.textContent).toContain('Retry');
    });

    it('retry button calls load() again', async () => {
      dashboardApi.get.and.rejectWith(new Error('Network error'));
      await settle();

      dashboardApi.get.and.resolveTo(fullDashboardData);
      const retryButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
      retryButton.click();

      await settle();
      expect(dashboardApi.get).toHaveBeenCalledTimes(2);
    });
  });

  describe('widget rendering', () => {
    it('renders all 9 widget sections with data', async () => {
      await settle();

      const widgetTitles = fixture.nativeElement.querySelectorAll('.widget-title');
      expect(widgetTitles.length).toBe(9);
      expect(widgetTitles[0].textContent).toContain('Cases by Stage');
      expect(widgetTitles[1].textContent).toContain('Offence Classification');
      expect(widgetTitles[2].textContent).toContain('Cases by Domicile');
      expect(widgetTitles[3].textContent).toContain('Cases by Staff');
      expect(widgetTitles[4].textContent).toContain('Overdue Visits');
      expect(widgetTitles[5].textContent).toContain('Interventions');
      expect(widgetTitles[6].textContent).toContain('Court This Week');
      expect(widgetTitles[7].textContent).toContain('Pending Claims');
      expect(widgetTitles[8].textContent).toContain('Intake Trend');
    });

    it('renders distribution bar rows', async () => {
      await settle();

      const barRows = fixture.nativeElement.querySelectorAll('.bar-row');
      // 2 stage + 2 offence + 2 domicile + 2 staff = 8 bar rows
      expect(barRows.length).toBe(8);
    });

    it('renders metric cards with values', async () => {
      await settle();

      const metricPrimaries = fixture.nativeElement.querySelectorAll('.metric-primary');
      // 3 metric cards: overdue visits (3), court this week (4), pending claims (6)
      expect(metricPrimaries.length).toBe(3);
      expect(metricPrimaries[0].textContent).toContain('3');
      expect(metricPrimaries[1].textContent).toContain('4');
      expect(metricPrimaries[2].textContent).toContain('6');
    });

    it('renders interventions gauge with 3 sub-metrics', async () => {
      await settle();

      const metricItems = fixture.nativeElement.querySelectorAll('.metric-item');
      expect(metricItems.length).toBe(3);

      const metricSmValues = fixture.nativeElement.querySelectorAll('.metric-primary-sm');
      expect(metricSmValues.length).toBe(3);
      expect(metricSmValues[0].textContent).toContain('5'); // inProgress
      expect(metricSmValues[1].textContent).toContain('1'); // overdue
      expect(metricSmValues[2].textContent).toContain('8'); // completedThisMonth
    });

    it('renders intake trend bars', async () => {
      await settle();

      const trendBars = fixture.nativeElement.querySelectorAll('.trend-bar');
      expect(trendBars.length).toBe(12);
    });

    it('renders month labels on intake trend', async () => {
      await settle();

      const trendLabels = fixture.nativeElement.querySelectorAll('.trend-label');
      expect(trendLabels.length).toBe(12);
      expect(trendLabels[0].textContent).toContain('Jul');
      expect(trendLabels[11].textContent).toContain('Jun');
    });

    it('shows subtitle text', async () => {
      await settle();

      const subtitle = fixture.nativeElement.querySelector('.subtitle');
      expect(subtitle.textContent).toContain('Organisation status at a glance');
    });
  });

  describe('empty state', () => {
    it('shows "No data" placeholders for empty distributions', async () => {
      dashboardApi.get.and.resolveTo(emptyDashboardData);
      await settle();

      const emptyPlaceholders = fixture.nativeElement.querySelectorAll('.widget-empty');
      // 4 distribution widgets + intake trend
      expect(emptyPlaceholders.length).toBeGreaterThanOrEqual(4);
    });

    it('shows zero values on metric cards when empty', async () => {
      dashboardApi.get.and.resolveTo(emptyDashboardData);
      await settle();

      const metricPrimaries = fixture.nativeElement.querySelectorAll('.metric-primary');
      expect(metricPrimaries.length).toBe(3);
      metricPrimaries.forEach((el: HTMLElement) => {
        expect(el.textContent).toMatch(/^[0\—]$/);
      });
    });
  });

  describe('auto-refresh', () => {
    it('sets up 60s interval on init', () => {
      const component = fixture.componentInstance;
      spyOn(component as never, 'autoRefresh');

      component.ngOnInit();

      expect((component as never)['refreshTimer']).not.toBeNull();
    });

    it('clears interval on destroy', () => {
      const component = fixture.componentInstance;
      spyOn(window, 'clearInterval');

      component.ngOnDestroy();

      expect(window.clearInterval).toHaveBeenCalled();
    });

    it('does not flash loading state on auto-refresh', async () => {
      await settle();

      const component = fixture.componentInstance;
      expect(component.loading()).toBe(false);
      expect(component.refreshing()).toBe(false);

      // Simulate auto-refresh
      const apiSpy = dashboardApi.get as jasmine.Spy;
      apiSpy.calls.reset();
      apiSpy.and.resolveTo(fullDashboardData);

      await component['autoRefresh']();

      expect(component.loading()).toBe(false);
      expect(component.refreshing()).toBe(false);
    });

    it('shows "Updating…" indicator in DOM while refreshing', async () => {
      await settle();

      const component = fixture.componentInstance;
      component.refreshing.set(true);
      fixture.detectChanges();

      const indicator = fixture.nativeElement.querySelector('.refreshing-indicator');
      expect(indicator).toBeTruthy();
      expect(indicator.textContent).toContain('Updating');

      component.refreshing.set(false);
      fixture.detectChanges();

      const hidden = fixture.nativeElement.querySelector('.refreshing-indicator');
      expect(hidden).toBeFalsy();
    });
  });

  describe('responsive grid', () => {
    it('has widget-grid class with correct grid styling', async () => {
      await settle();

      const grid = fixture.nativeElement.querySelector('.widget-grid');
      expect(grid).toBeTruthy();
    });
  });

  describe('no gamification', () => {
    it('does not contain gamification text', async () => {
      await settle();

      const text = fixture.nativeElement.textContent as string;
      expect(text).not.toContain('leaderboard');
      expect(text).not.toContain('ranking');
      expect(text).not.toContain('score');
      expect(text).not.toContain('badge');
    });
  });
});
