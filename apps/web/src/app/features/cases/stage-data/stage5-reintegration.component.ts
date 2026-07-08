import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CaseApiService } from '../services/case-api.service';

@Component({
  selector: 'app-stage5-reintegration',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './stage5-reintegration.component.html',
  styleUrl: './stage-data-shared.scss',
})
export class Stage5ReintegrationComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly savedMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    reintegrationLevel: ['', [Validators.required, Validators.maxLength(32)]],
    institutionDetails: ['', Validators.maxLength(2000)],
  });

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await this.caseApi.getStage5Reintegration(this.caseId());
      this.form.patchValue({
        reintegrationLevel: data.reintegrationLevel ?? '',
        institutionDetails: data.institutionDetails ?? '',
      });
    } catch (error) {
      if (!this.caseApi.isStageDataNotFound(error)) {
        this.errorMessage.set(this.caseApi.extractErrorMessage(error));
      }
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.savedMessage.set(null);

    const value = this.form.getRawValue();
    try {
      await this.caseApi.upsertStage5Reintegration(this.caseId(), {
        reintegrationLevel: value.reintegrationLevel.trim(),
        institutionDetails: value.institutionDetails.trim() || null,
      });
      this.savedMessage.set('Saved.');
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
