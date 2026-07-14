import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  CaseCourtSittingsComponent,
  isCourtSittingPastDue,
} from './case-court-sittings.component';
import { CaseApiService } from '../services/case-api.service';
import { CourtSittingDto } from '../models/case.models';

describe('CaseCourtSittingsComponent', () => {
  let fixture: ComponentFixture<CaseCourtSittingsComponent>;
  let caseApi: jasmine.SpyObj<CaseApiService>;

  const caseId = '11111111-1111-4111-8111-111111111111';

  const courtSittings: CourtSittingDto[] = [
    {
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      caseId,
      scheduledAtUtc: '2020-01-01T10:00:00Z',
      courtName: 'District Court',
      purpose: 'Hearing',
      status: 'Upcoming',
    },
  ];

  beforeEach(async () => {
    caseApi = jasmine.createSpyObj('CaseApiService', [
      'listCourtSittings',
      'createCourtSitting',
      'updateCourtSitting',
      'extractErrorMessage',
    ]);

    caseApi.listCourtSittings.and.returnValue(Promise.resolve(courtSittings));
    caseApi.createCourtSitting.and.returnValue(Promise.resolve(courtSittings[0]));
    caseApi.updateCourtSitting.and.returnValue(
      Promise.resolve({
        ...courtSittings[0],
        status: 'Attended',
        outcome: 'Completed',
      }),
    );
    caseApi.extractErrorMessage.and.returnValue('Save failed');

    await TestBed.configureTestingModule({
      imports: [CaseCourtSittingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CaseApiService, useValue: caseApi },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CaseCourtSittingsComponent);
    fixture.componentRef.setInput('caseId', caseId);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('surfaces list load failure with retry', async () => {
    caseApi.listCourtSittings.and.returnValue(Promise.reject(new Error('Forbidden')));
    caseApi.extractErrorMessage.and.returnValue('Could not load court sittings');

    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Could not load court sittings');
    expect(text).toContain('Retry');
  });

  it('renders court sittings with past-due styling', async () => {
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('District Court');
    expect(text).toContain('Overdue');
    expect(isCourtSittingPastDue(courtSittings[0])).toBeTrue();
  });

  it('creates a court sitting via CaseApiService', async () => {
    await settle();

    const future = new Date(Date.now() + 86400000);

    fixture.componentInstance.addForm.setValue({
      scheduledDate: future,
      scheduledTime: '10:00',
      courtName: 'Family Court',
      purpose: 'Review',
      status: 'Upcoming',
      notes: '',
      outcome: '',
    });

    await fixture.componentInstance.submitAdd();
    await settle();

    expect(caseApi.createCourtSitting).toHaveBeenCalled();
    const [, request] = caseApi.createCourtSitting.calls.mostRecent().args;
    expect(request.courtName).toBe('Family Court');
    expect(request.status).toBe('Upcoming');
  });

  it('blocks Attended create without outcome', async () => {
    await settle();

    const future = new Date(Date.now() + 86400000);

    fixture.componentInstance.addForm.setValue({
      scheduledDate: future,
      scheduledTime: '10:00',
      courtName: 'Family Court',
      purpose: 'Review',
      status: 'Attended',
      notes: '',
      outcome: '',
    });

    await fixture.componentInstance.submitAdd();
    await settle();

    expect(caseApi.createCourtSitting).not.toHaveBeenCalled();
    expect(fixture.componentInstance.formErrorMessage()).toContain('Outcome is required');
  });

  it('updates a court sitting via CaseApiService', async () => {
    await settle();

    fixture.componentInstance.startUpdate(courtSittings[0]);
    fixture.componentInstance.updateForm.setValue({
      status: 'Attended',
      scheduledDate: new Date('2026-12-01T00:00:00'),
      scheduledTime: '10:00',
      courtName: 'District Court',
      purpose: 'Hearing',
      notes: '',
      outcome: 'Completed',
      nextCourtDate: null,
      nextCourtTime: '',
    });

    await fixture.componentInstance.submitUpdate(courtSittings[0]);
    await settle();

    expect(caseApi.updateCourtSitting).toHaveBeenCalled();
  });
});
