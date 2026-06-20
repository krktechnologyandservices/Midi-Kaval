import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CaseInterventionsComponent } from './case-interventions.component';
import { CaseApiService } from '../services/case-api.service';
import { InterventionDto } from '../models/case.models';

describe('CaseInterventionsComponent', () => {
  let fixture: ComponentFixture<CaseInterventionsComponent>;
  let caseApi: jasmine.SpyObj<CaseApiService>;

  const caseId = '11111111-1111-4111-8111-111111111111';
  const workerId = '22222222-2222-4222-8222-222222222222';

  const interventions: InterventionDto[] = [
    {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      caseId,
      direction: 'Needed',
      categoryName: 'Counselling',
      description: 'Weekly session',
      priority: 'High',
      status: 'Open',
      dueAtUtc: '2020-01-01T00:00:00Z',
      assignedStaffUserId: workerId,
      assignedStaffEmail: 'worker@test',
    },
  ];

  beforeEach(async () => {
    caseApi = jasmine.createSpyObj('CaseApiService', [
      'listInterventions',
      'createIntervention',
      'updateIntervention',
      'extractErrorMessage',
    ]);

    caseApi.listInterventions.and.returnValue(Promise.resolve(interventions));
    caseApi.createIntervention.and.returnValue(Promise.resolve(interventions[0]));
    caseApi.updateIntervention.and.returnValue(
      Promise.resolve({
        ...interventions[0],
        status: 'Completed',
        outcome: 'Done',
      }),
    );
    caseApi.extractErrorMessage.and.returnValue('Save failed');

    await TestBed.configureTestingModule({
      imports: [CaseInterventionsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CaseApiService, useValue: caseApi },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CaseInterventionsComponent);
    fixture.componentRef.setInput('caseId', caseId);
    fixture.componentRef.setInput('fieldWorkers', [{ id: workerId, email: 'worker@test' }]);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('renders interventions with overdue styling', async () => {
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Counselling');
    expect(text).toContain('Overdue');
  });

  it('creates an intervention via CaseApiService', async () => {
    await settle();

    const future = new Date(Date.now() + 86400000);
    const local = new Date(future.getTime() - future.getTimezoneOffset() * 60000)
      .toISOString()
      .slice(0, 16);

    fixture.componentInstance.addForm.setValue({
      direction: 'Needed',
      categoryName: 'Legal aid',
      description: 'Court support',
      priority: 'Medium',
      assignedStaffUserId: workerId,
      dueAtLocal: local,
      providedAtLocal: '',
    });

    await fixture.componentInstance.submitAdd();
    await settle();

    expect(caseApi.createIntervention).toHaveBeenCalled();
    const [, request] = caseApi.createIntervention.calls.mostRecent().args;
    expect(request.categoryName).toBe('Legal aid');
    expect(request.assignedStaffUserId).toBe(workerId);
  });

  it('blocks terminal update without outcome', async () => {
    await settle();

    fixture.componentInstance.startUpdate(interventions[0]);
    fixture.componentInstance.updateForm.setValue({
      status: 'Completed',
      outcome: '',
    });

    await fixture.componentInstance.submitUpdate(interventions[0]);
    await settle();

    expect(caseApi.updateIntervention).not.toHaveBeenCalled();
    expect(fixture.componentInstance.formErrorMessage()).toContain('Outcome is required');
  });

  it('updates intervention when outcome provided', async () => {
    await settle();

    fixture.componentInstance.startUpdate(interventions[0]);
    fixture.componentInstance.updateForm.setValue({
      status: 'Completed',
      outcome: 'Session completed',
    });

    await fixture.componentInstance.submitUpdate(interventions[0]);
    await settle();

    expect(caseApi.updateIntervention).toHaveBeenCalledWith(
      caseId,
      interventions[0].id!,
      jasmine.objectContaining({
        status: 'Completed',
        outcome: 'Session completed',
      }),
    );
  });
});
