import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { DuplicateMatchSheetComponent } from './duplicate-match-sheet.component';

describe('DuplicateMatchSheetComponent', () => {
  let fixture: ComponentFixture<DuplicateMatchSheetComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<DuplicateMatchSheetComponent>>;

  const match = {
    caseId: '11111111-1111-4111-8111-111111111111',
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    beneficiaryName: 'Ravi',
    currentStage: 'ProcessInitiation',
    matchedOn: 'Both',
  };

  beforeEach(async () => {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [DuplicateMatchSheetComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRef },
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            matches: [match],
            canMerge: true,
            triggerFieldId: 'crimeNumber',
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DuplicateMatchSheetComponent);
    fixture.detectChanges();
  });

  it('renders headline and matched-on label', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Possible match — review before saving.');
    expect(compiled.textContent).toContain('Matched on Crime and ST number');
  });

  it('does not render create duplicate action', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent?.toLowerCase()).not.toContain('create anyway');
    expect(compiled.textContent?.toLowerCase()).not.toContain('save duplicate');
  });

  it('closes with open action', () => {
    fixture.componentInstance.openExisting(match);
    expect(dialogRef.close).toHaveBeenCalledWith(
      jasmine.objectContaining({ action: 'open' }),
    );
  });

  it('closes with merge action after inline confirm', () => {
    fixture.componentInstance.startMergeConfirm(match);
    fixture.detectChanges();
    fixture.componentInstance.confirmMerge(match);
    expect(dialogRef.close).toHaveBeenCalledWith(
      jasmine.objectContaining({ action: 'merge' }),
    );
  });

  it('announces match count in aria-live region', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const live = compiled.querySelector('[aria-live="polite"]');
    expect(live?.textContent).toContain('1 possible match found');
  });
});
