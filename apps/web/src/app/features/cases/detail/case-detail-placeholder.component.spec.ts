import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CaseApiService } from '../services/case-api.service';
import { CaseDetailPlaceholderComponent } from './case-detail-placeholder.component';
import { CaseDetailDto, CaseDto, FieldWorkerUserDto } from '../models/case.models';

describe('CaseDetailPlaceholderComponent', () => {
  let fixture: ComponentFixture<CaseDetailPlaceholderComponent>;
  let caseApi: jasmine.SpyObj<CaseApiService>;

  const caseId = '11111111-1111-4111-8111-111111111111';
  const worker: FieldWorkerUserDto = {
    id: '22222222-2222-4222-8222-222222222222',
    email: 'social@rbac.test',
    role: 'SocialWorker',
  };

  const detailWithWhisper: CaseDetailDto = {
    id: caseId,
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    beneficiaryName: 'Test User',
    currentStage: 'ProcessInitiation',
    visitCount: 0,
    assignedWorkerUserId: worker.id,
    assignedAtUtc: '2026-06-15T00:00:00Z',
    createdAtUtc: '2026-06-15T00:00:00Z',
    updatedAtUtc: '2026-06-15T00:00:00Z',
    handoffWhisper: {
      priorActions: 'Visited home',
      openItems: 'Open school task',
      nextVisitPurpose: 'Check progress',
      transferredAtUtc: '2026-06-15T00:00:00Z',
    },
  };

  const detailWithoutWhisper: CaseDetailDto = {
    ...detailWithWhisper,
    handoffWhisper: undefined,
  };

  beforeEach(async () => {
    caseApi = jasmine.createSpyObj<CaseApiService>('CaseApiService', [
      'getCaseDetail',
      'listFieldWorkers',
      'transferCase',
      'transitionStage',
      'listCaseNotes',
      'createCaseNote',
      'listInterventions',
      'listCourtSittings',
      'extractErrorMessage',
    ]);

    caseApi.getCaseDetail.and.returnValue(Promise.resolve(detailWithWhisper));
    caseApi.listFieldWorkers.and.returnValue(Promise.resolve([worker]));
    caseApi.listCaseNotes.and.returnValue(Promise.resolve([]));
    caseApi.listInterventions.and.returnValue(Promise.resolve([]));
    caseApi.listCourtSittings.and.returnValue(Promise.resolve([]));
    caseApi.transferCase.and.callFake(async () => detailWithoutWhisper);
    caseApi.extractErrorMessage.and.returnValue('Transfer failed');

    await TestBed.configureTestingModule({
      imports: [CaseDetailPlaceholderComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CaseApiService, useValue: caseApi },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => caseId } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CaseDetailPlaceholderComponent);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('shows handoff whisper when API returns whisper', async () => {
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Prior actions:');
    expect(text).toContain('Visited home');
  });

  it('includes notes timeline section', async () => {
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Notes timeline');
  });

  it('includes court sittings section', async () => {
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Court sittings');
  });

  it('hides handoff whisper when API returns null', async () => {
    caseApi.getCaseDetail.and.returnValue(Promise.resolve(detailWithoutWhisper));
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Prior actions:');
  });

  it('shows assignee email instead of raw user id', async () => {
    await settle();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('social@rbac.test');
    expect(text).not.toContain(worker.id!);
  });

  it('surfaces field workers load failure in transfer section', async () => {
    caseApi.listFieldWorkers.and.returnValue(Promise.reject(new Error('Forbidden')));
    caseApi.extractErrorMessage.and.returnValue('Could not load field workers');

    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Could not load field workers');
    expect(text).toContain('Retry');
  });

  it('submits transfer form', async () => {
    await settle();

    fixture.componentInstance.transferForm.setValue({
      assigneeUserId: worker.id!,
      priorActions: 'Done intake',
      openItems: 'Schedule visit',
      nextVisitPurpose: 'Home check',
    });

    await fixture.componentInstance.submitTransfer();
    await settle();

    expect(caseApi.transferCase).toHaveBeenCalledWith(caseId, {
      assigneeUserId: worker.id!,
      priorActions: 'Done intake',
      openItems: 'Schedule visit',
      nextVisitPurpose: 'Home check',
    });
  });

  it('shows stage transition control for non-terminal stage', async () => {
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Change stage');
    expect(text).toContain('Update stage');

    expect(fixture.componentInstance.nextForwardStage()).toBe('MaintainAndDevelopment');
    expect(fixture.componentInstance.stageIsTerminal()).toBeFalse();
  });

  it('hides stage transition control when stage is TerminationExclusion', async () => {
    const terminalDetail: CaseDetailDto = {
      ...detailWithWhisper,
      currentStage: 'TerminationExclusion',
    };

    caseApi.getCaseDetail.and.returnValue(Promise.resolve(terminalDetail));

    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Change stage');
    expect(fixture.componentInstance.stageIsTerminal()).toBeTrue();
  });

  it('submits stage transition with next forward stage', async () => {
    caseApi.transitionStage.and.returnValue(Promise.resolve({} as unknown as CaseDto));

    await settle();

    fixture.componentInstance.stageForm.setValue({
      targetStage: 'MaintainAndDevelopment',
      notes: 'Follow up note',
    });

    await fixture.componentInstance.submitStageTransition();
    await settle();

    expect(caseApi.transitionStage).toHaveBeenCalledWith(caseId, {
      targetStage: 'MaintainAndDevelopment',
      notes: 'Follow up note',
    });
  });

  it('surfaces stage transition errors inline without hiding detail', async () => {
    caseApi.transitionStage.and.returnValue(Promise.reject(new Error('422')));
    caseApi.extractErrorMessage.and.returnValue('Stage update failed');

    await settle();

    fixture.componentInstance.stageForm.setValue({
      targetStage: 'MaintainAndDevelopment',
      notes: '',
    });

    await fixture.componentInstance.submitStageTransition();
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Stage update failed');
    expect(text).toContain('Transfer case');
    expect(fixture.componentInstance.stageErrorMessage()).toBe('Stage update failed');
  });
});
