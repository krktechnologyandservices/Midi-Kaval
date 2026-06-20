import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TravelClaimsPendingListComponent } from './travel-claims-pending-list.component';
import { TravelClaimApiService } from './services/travel-claim-api.service';
import { TravelClaimDto } from './travel.models';

describe('TravelClaimsPendingListComponent', () => {
  let fixture: ComponentFixture<TravelClaimsPendingListComponent>;
  let travelApi: jasmine.SpyObj<TravelClaimApiService>;

  const sampleClaim: TravelClaimDto = {
    id: 'claim-1',
    claimDate: '2026-06-15',
    startLocation: 'Office',
    destination: 'Court',
    transportMode: 'Bus',
    amount: 100,
    status: 'Submitted',
    claimantUserId: 'user-1',
    claimantEmail: 'worker@example.com',
    createdAtUtc: '2026-06-15T00:00:00Z',
    updatedAtUtc: '2026-06-15T00:00:00Z',
    caseIds: ['case-1'],
    attachments: [{ id: 'att-1', originalFileName: 'receipt.jpg', contentType: 'image/jpeg', fileSizeBytes: 10, confirmedAtUtc: '2026-06-15T00:00:00Z' }],
  };

  beforeEach(async () => {
    travelApi = jasmine.createSpyObj('TravelClaimApiService', [
      'listPending',
      'extractErrorMessage',
    ]);
    travelApi.listPending.and.resolveTo([]);
    travelApi.extractErrorMessage.and.returnValue('Failed to load claims');

    await TestBed.configureTestingModule({
      imports: [TravelClaimsPendingListComponent],
      providers: [provideRouter([]), { provide: TravelClaimApiService, useValue: travelApi }],
    }).compileComponents();

    fixture = TestBed.createComponent(TravelClaimsPendingListComponent);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('shows empty state when no pending claims', async () => {
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('No claims awaiting approval');
  });

  it('shows loading spinner while fetching', () => {
    travelApi.listPending.and.returnValue(new Promise(() => undefined));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('mat-progress-spinner')).not.toBeNull();
  });

  it('shows error and retry when load fails', async () => {
    travelApi.listPending.and.rejectWith(new Error('network'));
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Failed to load claims');
    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Retry');
  });

  it('renders pending claim rows', async () => {
    travelApi.listPending.and.resolveTo([sampleClaim]);
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('worker@example.com');
    expect(text).toContain('Court');
    expect(text).toContain('1 receipt(s)');
  });
});
