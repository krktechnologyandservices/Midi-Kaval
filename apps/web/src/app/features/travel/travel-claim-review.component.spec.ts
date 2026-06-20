import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute, Router } from '@angular/router';
import { TravelClaimReviewComponent } from './travel-claim-review.component';
import { TravelClaimApiService } from './services/travel-claim-api.service';
import { CaseApiService } from '../cases/services/case-api.service';
import { AttachmentApiService } from '../cases/services/attachment-api.service';

describe('TravelClaimReviewComponent', () => {
  let fixture: ComponentFixture<TravelClaimReviewComponent>;
  let travelApi: jasmine.SpyObj<TravelClaimApiService>;
  let router: Router;

  const submittedClaim = {
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
    attachments: [],
  };

  beforeEach(async () => {
    travelApi = jasmine.createSpyObj('TravelClaimApiService', [
      'getForDirectorReview',
      'getForSupervisorReview',
      'approve',
      'returnClaim',
      'extractErrorMessage',
    ]);
    travelApi.getForDirectorReview.and.resolveTo(submittedClaim);
    travelApi.getForSupervisorReview.and.resolveTo(submittedClaim);
    travelApi.extractErrorMessage.and.returnValue('Error');

    const caseApi = jasmine.createSpyObj('CaseApiService', ['getCaseDetail']);
    caseApi.getCaseDetail.and.resolveTo({
      id: 'case-1',
      crimeNumber: 'CR-1',
      stNumber: 'ST-1',
    });

    const attachmentApi = jasmine.createSpyObj('AttachmentApiService', [
      'getDownloadUrl',
      'extractErrorMessage',
    ]);

    await TestBed.configureTestingModule({
      imports: [TravelClaimReviewComponent],
      providers: [
        provideRouter([]),
        { provide: TravelClaimApiService, useValue: travelApi },
        { provide: CaseApiService, useValue: caseApi },
        { provide: AttachmentApiService, useValue: attachmentApi },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => 'claim-1' },
              data: { readOnly: false },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TravelClaimReviewComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('shows approve and return actions for submitted claim', async () => {
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Approve');
    expect(text).toContain('Return');
  });

  it('hides approve and return actions in read-only mode', async () => {
    TestBed.resetTestingModule();
    travelApi = jasmine.createSpyObj('TravelClaimApiService', [
      'getForSupervisorReview',
      'extractErrorMessage',
    ]);
    travelApi.getForSupervisorReview.and.resolveTo(submittedClaim);
    travelApi.extractErrorMessage.and.returnValue('Error');

    const caseApi = jasmine.createSpyObj('CaseApiService', ['getCaseDetail']);
    caseApi.getCaseDetail.and.resolveTo({
      id: 'case-1',
      crimeNumber: 'CR-1',
      stNumber: 'ST-1',
    });

    await TestBed.configureTestingModule({
      imports: [TravelClaimReviewComponent],
      providers: [
        provideRouter([]),
        { provide: TravelClaimApiService, useValue: travelApi },
        { provide: CaseApiService, useValue: caseApi },
        {
          provide: AttachmentApiService,
          useValue: jasmine.createSpyObj('AttachmentApiService', ['getDownloadUrl']),
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => 'claim-1' },
              data: { readOnly: true },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TravelClaimReviewComponent);
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Director approval required');
    expect(text).not.toContain('Confirm approve');
    expect(text).not.toContain('Confirm return');
  });

  it('does not submit return when comment is empty', async () => {
    await settle();
    fixture.componentInstance.showReturnForm.set(true);
    fixture.detectChanges();

    await fixture.componentInstance.submitReturn();

    expect(travelApi.returnClaim).not.toHaveBeenCalled();
  });

  it('navigates to back link after approve', async () => {
    travelApi.approve.and.resolveTo(submittedClaim);
    await settle();

    fixture.componentInstance.showApproveForm.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.submitApprove();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/travel-claims');
  });
});
