import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { CrisisQueueApiService } from '../../travel/services/crisis-queue-api.service';
import { CrisisQueueItemDto } from '../../travel/travel.models';
import { CrisisQueuePageComponent } from './crisis-queue-page.component';

describe('CrisisQueuePageComponent', () => {
  let fixture: ComponentFixture<CrisisQueuePageComponent>;
  let crisisQueueApi: jasmine.SpyObj<CrisisQueueApiService>;
  let router: Router;
  let userSignal: ReturnType<typeof signal<{ role: AppRole } | null>>;

  const overdueRow: CrisisQueueItemDto = {
    rowType: 'visit_overdue',
    severity: 'critical',
    badgeLabel: 'Overdue',
    caseId: 'case-1',
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    title: 'Crime CR-1 — visit 12 days overdue',
    detail: 'Priya Sharma · Slum cluster A',
  };

  const court48hRow: CrisisQueueItemDto = {
    rowType: 'court_48h',
    severity: 'warning',
    badgeLabel: 'Court 48h',
    caseId: 'case-2',
    courtSittingId: 'sitting-1',
    crimeNumber: 'CR-2',
    title: 'Crime CR-2 — sitting Friday, no prep note',
    detail: 'Anil Kumar (Case Worker)',
  };

  const handoffRow: CrisisQueueItemDto = {
    rowType: 'handoff',
    severity: 'info',
    badgeLabel: 'Handoff',
    caseId: 'case-3',
    crimeNumber: 'CR-3',
    title: 'Crime CR-3 — transferred 3 days ago',
    detail: 'Review handoff summary',
    previousWorkerName: 'Ravi',
    transferredAtUtc: '2026-06-17T10:00:00Z',
  };

  const claimRow: CrisisQueueItemDto = {
    rowType: 'travel_claim_pending',
    severity: 'neutral',
    badgeLabel: 'Claim',
    caseId: 'case-4',
    travelClaimId: 'claim-1',
    claimantUserId: 'user-1',
    claimantEmail: 'priya@pilot.example',
    amount: 250,
    receiptCount: 2,
    title: 'Travel claim pending approval — priya',
    detail: '₹250 · 2 receipt(s)',
  };

  const courtMissRow: CrisisQueueItemDto = {
    rowType: 'court_miss',
    severity: 'critical',
    badgeLabel: 'Court miss',
    caseId: 'case-5',
    courtSittingId: 'sitting-2',
    crimeNumber: 'CR-5',
    title: 'Court miss — sitting unattended',
    detail: 'Worker did not attend',
  };

  beforeEach(async () => {
    userSignal = signal<{ role: AppRole } | null>({ role: AppRole.Director });

    crisisQueueApi = jasmine.createSpyObj('CrisisQueueApiService', [
      'list',
      'extractErrorMessage',
    ]) as jasmine.SpyObj<CrisisQueueApiService>;
    crisisQueueApi.list.and.resolveTo([overdueRow, court48hRow, handoffRow, claimRow, courtMissRow]);
    crisisQueueApi.extractErrorMessage.and.returnValue('Failed to load crisis queue');

    const authStub = {
      currentUser: userSignal,
    } as unknown as AuthSessionService;

    await TestBed.configureTestingModule({
      imports: [CrisisQueuePageComponent],
      providers: [
        provideRouter([]),
        { provide: CrisisQueueApiService, useValue: crisisQueueApi },
        { provide: AuthSessionService, useValue: authStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CrisisQueuePageComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  describe('loading state', () => {
    it('shows skeleton rows while loading', () => {
      crisisQueueApi.list.and.resolveTo([]); // Will be overridden but keeps pending
      const component = fixture.componentInstance;
      component.loading.set(true);
      fixture.detectChanges();

      const skeletonList = fixture.nativeElement.querySelector('.skeleton-list');
      expect(skeletonList).toBeTruthy();
      const skeletonRows = skeletonList.querySelectorAll('.skeleton-row');
      expect(skeletonRows.length).toBe(3);
    });

    it('hides skeleton after loading completes', async () => {
      await settle();
      const skeletonList = fixture.nativeElement.querySelector('.skeleton-list');
      expect(skeletonList).toBeNull();
    });

    it('shows subtitle text when loading', () => {
      const component = fixture.componentInstance;
      component.loading.set(true);
      component.items.set([]);
      fixture.detectChanges();

      const subtitle = fixture.nativeElement.querySelector('.subtitle');
      expect(subtitle.textContent).toContain('Items needing supervisor attention');
    });
  });

  describe('error state', () => {
    it('shows error message and retry button', async () => {
      crisisQueueApi.list.and.rejectWith(new Error('Network error'));
      await settle();

      const error = fixture.nativeElement.querySelector('[role="alert"]');
      expect(error).toBeTruthy();
      expect(error.textContent).toContain('Failed to load crisis queue');

      const retryButton = fixture.nativeElement.querySelector('button');
      expect(retryButton).toBeTruthy();
      expect(retryButton.textContent).toContain('Retry');
    });

    it('retry button calls load() again', async () => {
      crisisQueueApi.list.and.rejectWith(new Error('Network error'));
      await settle();

      crisisQueueApi.list.and.resolveTo([overdueRow]);
      const retryButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
      retryButton.click();

      await settle();
      expect(crisisQueueApi.list).toHaveBeenCalledTimes(2);
      const rows = fixture.nativeElement.querySelectorAll('.queue-row');
      expect(rows.length).toBe(1);
    });
  });

  describe('empty state', () => {
    it('shows empty state with links to Dashboard and Cases', async () => {
      crisisQueueApi.list.and.resolveTo([]);
      await settle();

      const emptyState = fixture.nativeElement.querySelector('.empty-state');
      expect(emptyState).toBeTruthy();
      expect(emptyState.textContent).toContain('No urgent items');

      const links = emptyState.querySelectorAll('a');
      expect(links.length).toBe(2);
      expect(links[0].getAttribute('ng-reflect-router-link')).toBe('/dashboard');
      expect(links[1].getAttribute('ng-reflect-router-link')).toBe('/cases');
    });

    it('shows subtitle text when empty', async () => {
      crisisQueueApi.list.and.resolveTo([]);
      await settle();

      const subtitle = fixture.nativeElement.querySelector('.subtitle');
      expect(subtitle.textContent).toContain('Items needing supervisor attention');
    });
  });

  describe('row rendering', () => {
    it('renders all row types with severity classes', async () => {
      await settle();

      const rows = fixture.nativeElement.querySelectorAll('.queue-row');
      expect(rows.length).toBe(5);

      expect(rows[0].classList.contains('crisis-row-critical')).toBeTrue();
      expect(rows[1].classList.contains('crisis-row-warning')).toBeTrue();
      expect(rows[2].classList.contains('crisis-row-info')).toBeTrue();
      expect(rows[3].classList.contains('crisis-row-neutral')).toBeTrue();
      expect(rows[4].classList.contains('crisis-row-critical')).toBeTrue();
    });

    it('renders badge labels per row type', async () => {
      await settle();

      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Overdue');
      expect(text).toContain('Court 48h');
      expect(text).toContain('Handoff');
      expect(text).toContain('Claim');
      expect(text).toContain('Court miss');
    });

    it('renders badge severity classes', async () => {
      await settle();

      const badges = fixture.nativeElement.querySelectorAll('.badge');
      expect(badges.length).toBe(5);
      expect(badges[0].classList.contains('badge-critical')).toBeTrue();
      expect(badges[1].classList.contains('badge-warning')).toBeTrue();
      expect(badges[2].classList.contains('badge-info')).toBeTrue();
      expect(badges[3].classList.contains('badge-neutral')).toBeTrue();
      expect(badges[4].classList.contains('badge-critical')).toBeTrue();
    });

    it('shows subtitle with item count when items present', async () => {
      await settle();

      const subtitle = fixture.nativeElement.querySelector('.subtitle');
      expect(subtitle.textContent).toContain('5 items need attention');
    });
  });

  describe('row navigation', () => {
    it('navigates overdue rows to case detail', async () => {
      await settle();

      const firstRow = fixture.nativeElement.querySelectorAll('.queue-row')[0] as HTMLButtonElement;
      firstRow.click();

      expect(router.navigate).toHaveBeenCalledWith(['/cases', 'case-1']);
    });

    it('navigates court 48h rows to case detail', async () => {
      await settle();

      const warningRow = fixture.nativeElement.querySelectorAll('.queue-row')[1] as HTMLButtonElement;
      warningRow.click();

      expect(router.navigate).toHaveBeenCalledWith(['/cases', 'case-2']);
    });

    it('navigates handoff rows to case detail', async () => {
      await settle();

      const infoRow = fixture.nativeElement.querySelectorAll('.queue-row')[2] as HTMLButtonElement;
      infoRow.click();

      expect(router.navigate).toHaveBeenCalledWith(['/cases', 'case-3']);
    });

    it('navigates court miss rows to case detail', async () => {
      await settle();

      const missRow = fixture.nativeElement.querySelectorAll('.queue-row')[4] as HTMLButtonElement;
      missRow.click();

      expect(router.navigate).toHaveBeenCalledWith(['/cases', 'case-5']);
    });

    it('navigates Director claim rows to admin travel review', async () => {
      await settle();

      const claimButton = fixture.nativeElement.querySelectorAll('.queue-row')[3] as HTMLButtonElement;
      claimButton.click();

      expect(router.navigate).toHaveBeenCalledWith(['/admin/travel-claims', 'claim-1']);
    });

    it('navigates Coordinator claim rows to read-only crisis review', async () => {
      userSignal.set({ role: AppRole.Coordinator });
      await settle();

      const claimButton = fixture.nativeElement.querySelectorAll('.queue-row')[3] as HTMLButtonElement;
      claimButton.click();

      expect(router.navigate).toHaveBeenCalledWith(['/crisis-queue/travel-claims', 'claim-1']);
    });

    it('does not navigate claim row without travelClaimId', async () => {
      crisisQueueApi.list.and.resolveTo([
        { ...claimRow, travelClaimId: undefined },
      ]);
      await settle();

      const row = fixture.nativeElement.querySelector('.queue-row') as HTMLButtonElement;
      row.click();

      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('auto-refresh', () => {
    it('sets up 30s interval on init', fakeAsync(() => {
      const component = fixture.componentInstance;
      spyOn(component as never, 'autoRefresh');

      component.ngOnInit();
      tick(30000);

      expect((component as never)['autoRefresh']).toHaveBeenCalled();
    }));

    it('clears interval on destroy', fakeAsync(() => {
      const component = fixture.componentInstance;
      spyOn(window, 'clearInterval');

      component.ngOnDestroy();

      expect(window.clearInterval).toHaveBeenCalled();
    }));

    it('does not flash loading state on auto-refresh', async () => {
      crisisQueueApi.list.and.resolveTo([overdueRow, claimRow]);
      await settle();

      const component = fixture.componentInstance;
      expect(component.loading()).toBe(false);
      expect(component.refreshing()).toBe(false);

      // Simulate auto-refresh
      const apiSpy = crisisQueueApi.list as jasmine.Spy;
      apiSpy.calls.reset();
      apiSpy.and.resolveTo([overdueRow, court48hRow, handoffRow, claimRow]);

      await component['autoRefresh']();

      expect(component.loading()).toBe(false);
      expect(component.refreshing()).toBe(false);
      expect(component.items().length).toBe(4);
    });
  });

  describe('keyboard accessibility', () => {
    it('rows are button elements', async () => {
      await settle();

      const rows = fixture.nativeElement.querySelectorAll('.queue-row');
      rows.forEach((row: HTMLElement) => {
        expect(row.tagName).toBe('BUTTON');
      });
    });

    it('rows are focusable', async () => {
      await settle();

      const row = fixture.nativeElement.querySelector('.queue-row') as HTMLButtonElement;
      row.focus();
      expect(document.activeElement).toBe(row);
    });
  });
});
