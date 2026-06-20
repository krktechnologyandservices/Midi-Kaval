import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { CaseApiService, CaseApiError } from '../services/case-api.service';
import { CaseCreateComponent } from './case-create.component';
import { DuplicateMatchSheetComponent } from '../duplicate-match-sheet/duplicate-match-sheet.component';

describe('CaseCreateComponent', () => {
  let fixture: ComponentFixture<CaseCreateComponent>;
  let caseApi: jasmine.SpyObj<CaseApiService>;
  let dialog: jasmine.SpyObj<MatDialog>;
  let router: Router;

  beforeEach(async () => {
    caseApi = jasmine.createSpyObj('CaseApiService', [
      'checkDuplicate',
      'createCase',
      'mergeCase',
      'extractErrorMessage',
      'isConflict',
      'isNetworkError',
    ]);
    dialog = jasmine.createSpyObj('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [CaseCreateComponent],
      providers: [
        provideRouter([]),
        { provide: CaseApiService, useValue: caseApi },
        { provide: MatDialog, useValue: dialog },
        {
          provide: MatSnackBar,
          useValue: jasmine.createSpyObj('MatSnackBar', ['open']),
        },
        {
          provide: AuthSessionService,
          useValue: {
            isSupervisorRole: () => true,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CaseCreateComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture.componentInstance.form.patchValue({
      crimeNumber: 'CR-NEW',
      stNumber: 'ST-NEW',
      beneficiaryName: 'Test User',
      typeOfOffence: 'Theft',
      offenceClassification: 'Petty',
      domicile: 'Urban',
    });
  });

  it('disables save while checking duplicate', async () => {
    let resolveCheck!: (value: { hasMatch: boolean; matches: [] }) => void;
    caseApi.checkDuplicate.and.returnValue(
      new Promise((resolve) => {
        resolveCheck = resolve;
      }),
    );

    const submitPromise = fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(fixture.componentInstance.saveLabel()).toBe('Checking…');

    resolveCheck({ hasMatch: false, matches: [] });
    caseApi.createCase.and.returnValue(
      Promise.resolve({
        id: '22222222-2222-4222-8222-222222222222',
        crimeNumber: 'CR-NEW',
        stNumber: 'ST-NEW',
        beneficiaryName: 'Test User',
        currentStage: 'ProcessInitiation',
      }),
    );

    await submitPromise;
    expect(caseApi.checkDuplicate).toHaveBeenCalled();
    expect(caseApi.createCase).toHaveBeenCalled();
  });

  it('opens duplicate sheet when match found', async () => {
    caseApi.checkDuplicate.and.returnValue(
      Promise.resolve({
        hasMatch: true,
        matches: [
          {
            caseId: '11111111-1111-4111-8111-111111111111',
            crimeNumber: 'CR-1',
            matchedOn: 'CrimeNumber',
          },
        ],
      }),
    );

    dialog.open.and.returnValue({
      afterClosed: () => of({ action: 'cancel' }),
    } as never);

    await fixture.componentInstance.submit();

    expect(dialog.open).toHaveBeenCalledWith(
      DuplicateMatchSheetComponent,
      jasmine.objectContaining({ disableClose: true }),
    );
    expect(caseApi.createCase).not.toHaveBeenCalled();
  });

  it('navigates on successful create', async () => {
    caseApi.checkDuplicate.and.returnValue(
      Promise.resolve({ hasMatch: false, matches: [] }),
    );
    caseApi.createCase.and.returnValue(
      Promise.resolve({
        id: '22222222-2222-4222-8222-222222222222',
        crimeNumber: 'CR-NEW',
        stNumber: 'ST-NEW',
        beneficiaryName: 'Test User',
        currentStage: 'ProcessInitiation',
      }),
    );

    await fixture.componentInstance.submit();

    expect(router.navigate).toHaveBeenCalledWith(
      ['/cases', '22222222-2222-4222-8222-222222222222'],
      jasmine.objectContaining({ state: jasmine.objectContaining({ fromCreate: true }) }),
    );
  });

  it('re-opens duplicate sheet on create 409 when re-check finds match', async () => {
    caseApi.checkDuplicate.and.returnValues(
      Promise.resolve({ hasMatch: false, matches: [] }),
      Promise.resolve({
        hasMatch: true,
        matches: [
          {
            caseId: '11111111-1111-4111-8111-111111111111',
            crimeNumber: 'CR-1',
            matchedOn: 'CrimeNumber',
          },
        ],
      }),
    );
    caseApi.createCase.and.returnValue(
      Promise.reject(new CaseApiError('http', 409, {})),
    );
    caseApi.isConflict.and.returnValue(true);
    caseApi.extractErrorMessage.and.returnValue(
      'This Crime or ST number is already in use.',
    );

    dialog.open.and.returnValue({
      afterClosed: () => of({ action: 'cancel' }),
    } as never);

    await fixture.componentInstance.submit();

    expect(caseApi.checkDuplicate).toHaveBeenCalledTimes(2);
    expect(dialog.open).toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toBeNull();
  });

  it('blocks create when hasMatch is true but matches are empty', async () => {
    caseApi.checkDuplicate.and.returnValue(
      Promise.resolve({ hasMatch: true, matches: [] }),
    );

    await fixture.componentInstance.submit();

    expect(caseApi.createCase).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toContain('Possible duplicate');
  });

  it('merges intake when sheet returns merge action', async () => {
    caseApi.checkDuplicate.and.returnValue(
      Promise.resolve({
        hasMatch: true,
        matches: [
          {
            caseId: '11111111-1111-4111-8111-111111111111',
            crimeNumber: 'CR-1',
            matchedOn: 'CrimeNumber',
          },
        ],
      }),
    );
    caseApi.mergeCase.and.returnValue(
      Promise.resolve({
        id: '11111111-1111-4111-8111-111111111111',
        crimeNumber: 'CR-NEW',
        stNumber: 'ST-NEW',
        beneficiaryName: 'Test User',
        currentStage: 'ProcessInitiation',
      }),
    );

    dialog.open.and.returnValue({
      afterClosed: () =>
        of({
          action: 'merge',
          caseId: '11111111-1111-4111-8111-111111111111',
          match: { caseId: '11111111-1111-4111-8111-111111111111' },
        }),
    } as never);

    await fixture.componentInstance.submit();

    expect(caseApi.mergeCase).toHaveBeenCalledWith(
      '11111111-1111-4111-8111-111111111111',
      jasmine.objectContaining({
        crimeNumber: 'CR-NEW',
        stNumber: 'ST-NEW',
        beneficiaryName: 'Test User',
      }),
    );
    expect(caseApi.createCase).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(
      ['/cases', '11111111-1111-4111-8111-111111111111'],
      jasmine.objectContaining({ state: jasmine.objectContaining({ fromCreate: true }) }),
    );
  });

  it('surfaces merge error and unblocks save for re-check', async () => {
    caseApi.checkDuplicate.and.returnValue(
      Promise.resolve({
        hasMatch: true,
        matches: [
          {
            caseId: '11111111-1111-4111-8111-111111111111',
            crimeNumber: 'CR-1',
            matchedOn: 'CrimeNumber',
          },
        ],
      }),
    );
    caseApi.mergeCase.and.returnValue(Promise.reject(new CaseApiError('http', 409, {})));
    caseApi.extractErrorMessage.and.returnValue('Merge conflict');

    dialog.open.and.returnValue({
      afterClosed: () =>
        of({
          action: 'merge',
          caseId: '11111111-1111-4111-8111-111111111111',
          match: { caseId: '11111111-1111-4111-8111-111111111111' },
        }),
    } as never);

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorMessage()).toBe('Merge conflict');
    expect(fixture.componentInstance.saveDisabled()).toBe(false);
    expect(caseApi.createCase).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
