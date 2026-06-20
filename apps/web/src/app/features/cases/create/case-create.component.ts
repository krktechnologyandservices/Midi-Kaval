import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import {
  DOMICILE_OPTIONS,
  OFFENCE_CLASSIFICATIONS,
  CaseDuplicateMatchDto,
} from '../models/case.models';
import {
  buildDuplicateCheckRequest,
  CaseApiService,
} from '../services/case-api.service';
import {
  DuplicateMatchSheetComponent,
  DuplicateMatchSheetData,
  DuplicateMatchSheetResult,
} from '../duplicate-match-sheet/duplicate-match-sheet.component';

type DuplicateCheckOutcome = 'proceed' | 'sheet-shown' | 'blocked';

@Component({
  selector: 'app-case-create',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatCheckboxModule,
    MatSnackBarModule,
  ],
  templateUrl: './case-create.component.html',
  styleUrl: './case-create.component.scss',
})
export class CaseCreateComponent {
  private readonly fb = inject(FormBuilder);
  private readonly caseApi = inject(CaseApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly crimeInput = viewChild<ElementRef<HTMLInputElement>>('crimeInput');
  readonly stInput = viewChild<ElementRef<HTMLInputElement>>('stInput');

  readonly offenceClassifications = OFFENCE_CLASSIFICATIONS;
  readonly domicileOptions = DOMICILE_OPTIONS;

  readonly checkingDuplicate = signal(false);
  readonly merging = signal(false);
  readonly saveBlocked = signal(false);
  readonly sheetOpen = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly lastTriggerFieldId = signal('crimeNumber');

  private identifiersLockedAfterCancel = false;
  private blurTimer: ReturnType<typeof setTimeout> | null = null;

  readonly form = this.fb.nonNullable.group({
    crimeNumber: ['', [Validators.required, Validators.maxLength(64)]],
    stNumber: ['', [Validators.required, Validators.maxLength(64)]],
    beneficiaryName: ['', [Validators.required, Validators.maxLength(256)]],
    beneficiaryAge: [null as number | null],
    beneficiaryContact: ['', [Validators.maxLength(32)]],
    typeOfOffence: ['', [Validators.required, Validators.maxLength(128)]],
    offenceClassification: ['Petty', [Validators.required]],
    domicile: ['Urban', [Validators.required]],
    isFirstTimeOffender: [true],
  });

  constructor() {
    this.form.controls.crimeNumber.valueChanges.subscribe(() => this.onIdentifierEdited());
    this.form.controls.stNumber.valueChanges.subscribe(() => this.onIdentifierEdited());
  }

  saveLabel(): string {
    if (this.merging()) {
      return 'Merging…';
    }
    if (this.checkingDuplicate()) {
      return 'Checking…';
    }
    return 'Save case';
  }

  saveDisabled(): boolean {
    return (
      this.checkingDuplicate()
      || this.merging()
      || this.saveBlocked()
      || this.sheetOpen()
      || this.identifiersLockedAfterCancel
      || this.form.invalid
    );
  }

  onIdentifierBlur(field: 'crimeNumber' | 'stNumber'): void {
    if (this.blurTimer) {
      clearTimeout(this.blurTimer);
    }

    this.blurTimer = setTimeout(() => {
      void this.runOptionalBlurCheck(field);
    }, 400);
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    await this.runSaveFlow(this.lastTriggerFieldId());
  }

  private async runOptionalBlurCheck(field: 'crimeNumber' | 'stNumber'): Promise<void> {
    const value = this.form.controls[field].value.trim();
    if (!value || this.sheetOpen() || this.checkingDuplicate()) {
      return;
    }

    await this.runDuplicateCheck(field, false);
  }

  private async runSaveFlow(triggerFieldId: string): Promise<void> {
    this.lastTriggerFieldId.set(triggerFieldId);
    const outcome = await this.runDuplicateCheck(triggerFieldId, true);
    if (outcome === 'proceed') {
      await this.createAfterCleanCheck();
    }
  }

  private async runDuplicateCheck(
    triggerFieldId: string,
    allowCreateOnNoMatch: boolean,
  ): Promise<DuplicateCheckOutcome> {
    const checkBody = buildDuplicateCheckRequest(
      this.form.controls.crimeNumber.value,
      this.form.controls.stNumber.value,
    );

    if (!checkBody.crimeNumber && !checkBody.stNumber) {
      this.errorMessage.set('At least one of crimeNumber or stNumber is required.');
      return 'blocked';
    }

    this.checkingDuplicate.set(true);
    try {
      const result = await this.caseApi.checkDuplicate(checkBody);
      if (result.hasMatch) {
        if (!result.matches?.length) {
          this.errorMessage.set(
            'Possible duplicate detected. Verify Crime/ST and try again.',
          );
          return 'blocked';
        }

        await this.openDuplicateSheet(result.matches, triggerFieldId);
        return 'sheet-shown';
      }

      return allowCreateOnNoMatch ? 'proceed' : 'blocked';
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
      return 'blocked';
    } finally {
      this.checkingDuplicate.set(false);
    }
  }

  private async createAfterCleanCheck(): Promise<void> {
    this.checkingDuplicate.set(true);
    try {
      const created = await this.caseApi.createCase(this.buildCreateRequest());
      this.snackBar.open('Case created.', 'Dismiss', { duration: 3000 });
      await this.router.navigate(['/cases', created.id], {
        state: { summary: created, fromCreate: true },
      });
    } catch (error) {
      if (this.caseApi.isConflict(error)) {
        const outcome = await this.runDuplicateCheck(this.lastTriggerFieldId(), false);
        if (outcome === 'blocked') {
          this.errorMessage.set(
            this.caseApi.extractErrorMessage(error)
            || 'This Crime or ST number is already in use.',
          );
        }
        return;
      }

      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.checkingDuplicate.set(false);
    }
  }

  private buildCreateRequest() {
    const raw = this.form.getRawValue();
    return {
      crimeNumber: raw.crimeNumber.trim(),
      stNumber: raw.stNumber.trim(),
      beneficiaryName: raw.beneficiaryName.trim(),
      beneficiaryAge: raw.beneficiaryAge ?? undefined,
      beneficiaryContact: raw.beneficiaryContact.trim() || undefined,
      typeOfOffence: raw.typeOfOffence.trim(),
      offenceClassification: raw.offenceClassification,
      domicile: raw.domicile,
      isFirstTimeOffender: raw.isFirstTimeOffender,
    };
  }

  private async openDuplicateSheet(
    matches: CaseDuplicateMatchDto[],
    triggerFieldId: string,
  ): Promise<void> {
    this.sheetOpen.set(true);
    this.saveBlocked.set(true);

    const data: DuplicateMatchSheetData = {
      matches,
      canMerge: this.auth.isSupervisorRole(),
      triggerFieldId,
    };

    const dialogRef = this.dialog.open(DuplicateMatchSheetComponent, {
      data,
      disableClose: true,
      autoFocus: 'first-tabbable',
      ariaModal: true,
      role: 'dialog',
      ariaLabelledBy: 'duplicate-match-sheet-title',
    });

    const result = await firstValueFrom(dialogRef.afterClosed());
    this.sheetOpen.set(false);

    if (!result) {
      this.handleCancel(triggerFieldId);
      return;
    }

    await this.handleSheetResult(result, triggerFieldId);
  }

  private async handleSheetResult(
    result: DuplicateMatchSheetResult,
    triggerFieldId: string,
  ): Promise<void> {
    if (result.action === 'open') {
      this.saveBlocked.set(false);
      this.identifiersLockedAfterCancel = false;
      await this.router.navigate(['/cases', result.caseId], {
        state: { summary: result.match },
      });
      return;
    }

    if (result.action === 'merge') {
      await this.mergeFromSheet(result.caseId);
      return;
    }

    this.handleCancel(triggerFieldId);
  }

  private async mergeFromSheet(caseId: string): Promise<void> {
    this.errorMessage.set(null);
    this.merging.set(true);
    try {
      const merged = await this.caseApi.mergeCase(caseId, this.buildCreateRequest());
      this.snackBar.open('Intake merged into existing case.', 'Dismiss', { duration: 3000 });
      this.saveBlocked.set(false);
      this.identifiersLockedAfterCancel = false;
      await this.router.navigate(['/cases', merged.id], {
        state: { summary: merged, fromCreate: true },
      });
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
      this.saveBlocked.set(false);
    } finally {
      this.merging.set(false);
    }
  }

  private handleCancel(triggerFieldId: string): void {
    this.identifiersLockedAfterCancel = true;
    this.saveBlocked.set(false);
    this.focusTriggerField(triggerFieldId);
  }

  private focusTriggerField(triggerFieldId: string): void {
    queueMicrotask(() => {
      if (triggerFieldId === 'stNumber') {
        this.stInput()?.nativeElement.focus();
      } else {
        this.crimeInput()?.nativeElement.focus();
      }
    });
  }

  private onIdentifierEdited(): void {
    if (this.identifiersLockedAfterCancel) {
      this.identifiersLockedAfterCancel = false;
    }
  }
}
