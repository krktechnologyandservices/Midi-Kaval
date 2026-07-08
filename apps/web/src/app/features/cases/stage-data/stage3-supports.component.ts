import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  Stage3SupportDto,
  Stage3SupportItemRequest,
} from '../models/case.models';
import { CaseApiService } from '../services/case-api.service';

@Component({
  selector: 'app-stage3-supports',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './stage3-supports.component.html',
  styleUrl: './stage-data-shared.scss',
})
export class Stage3SupportsComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();

  readonly items = signal<Stage3SupportDto[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly savedMessage = signal<string | null>(null);

  readonly addForm = this.fb.nonNullable.group({
    supportType: ['', [Validators.required, Validators.maxLength(50)]],
    providerName: ['', Validators.maxLength(200)],
    notes: ['', Validators.maxLength(2000)],
    providedStatus: [false],
  });

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await this.caseApi.getStage3Supports(this.caseId());
      this.items.set(data);
    } catch (error) {
      if (!this.caseApi.isStageDataNotFound(error)) {
        this.errorMessage.set(this.caseApi.extractErrorMessage(error));
      }
    } finally {
      this.loading.set(false);
    }
  }

  addItem(): void {
    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }

    const value = this.addForm.getRawValue();
    this.items.update((current) => [
      ...current,
      {
        supportType: value.supportType.trim(),
        providerName: value.providerName.trim() || null,
        notes: value.notes.trim() || null,
        providedStatus: value.providedStatus,
      },
    ]);
    this.addForm.reset({ supportType: '', providerName: '', notes: '', providedStatus: false });
  }

  removeItem(index: number): void {
    this.items.update((current) => current.filter((_, i) => i !== index));
  }

  toggleProvided(index: number): void {
    this.items.update((current) =>
      current.map((item, i) => (i === index ? { ...item, providedStatus: !item.providedStatus } : item)),
    );
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.errorMessage.set(null);
    this.savedMessage.set(null);

    const requestItems: Stage3SupportItemRequest[] = this.items().map((item) => ({
      supportType: item.supportType,
      providerName: item.providerName,
      notes: item.notes,
      providedStatus: item.providedStatus,
    }));

    try {
      const saved = await this.caseApi.upsertStage3Supports(this.caseId(), { items: requestItems });
      this.items.set(saved);
      this.savedMessage.set('Saved.');
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
