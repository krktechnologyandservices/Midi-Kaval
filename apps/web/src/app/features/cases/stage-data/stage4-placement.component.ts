import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CaseApiService } from '../services/case-api.service';

function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

@Component({
  selector: 'app-stage4-placement',
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './stage4-placement.component.html',
  styleUrl: './stage-data-shared.scss',
})
export class Stage4PlacementComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly savedMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    placementType: ['', [Validators.required, Validators.maxLength(32)]],
    institutionName: ['', Validators.maxLength(500)],
    address: ['', Validators.maxLength(2000)],
    startDate: [new Date(), Validators.required],
  });

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await this.caseApi.getStage4Placement(this.caseId());
      this.form.patchValue({
        placementType: data.placementType ?? '',
        institutionName: data.institutionName ?? '',
        address: data.address ?? '',
        startDate: data.startDate ? new Date(data.startDate) : new Date(),
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
      await this.caseApi.upsertStage4Placement(this.caseId(), {
        placementType: value.placementType.trim(),
        institutionName: value.institutionName.trim() || null,
        address: value.address.trim() || null,
        startDate: toIsoDate(value.startDate),
      });
      this.savedMessage.set('Saved.');
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
