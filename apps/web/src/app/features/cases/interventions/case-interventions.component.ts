import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  CreateInterventionRequest,
  FieldWorkerUserDto,
  INTERVENTION_DIRECTIONS,
  INTERVENTION_PRIORITIES,
  INTERVENTION_STATUSES,
  InterventionDirection,
  InterventionDto,
  InterventionPriority,
  InterventionStatus,
  UpdateInterventionRequest,
} from '../models/case.models';
import { CaseApiService } from '../services/case-api.service';

@Component({
  selector: 'app-case-interventions',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './case-interventions.component.html',
  styleUrl: './case-interventions.component.scss',
})
export class CaseInterventionsComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();
  readonly fieldWorkers = input<FieldWorkerUserDto[]>([]);

  readonly directions = INTERVENTION_DIRECTIONS;
  readonly priorities = INTERVENTION_PRIORITIES;
  readonly statuses = INTERVENTION_STATUSES;

  readonly items = signal<InterventionDto[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly updatingId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly formErrorMessage = signal<string | null>(null);

  readonly addForm = this.fb.nonNullable.group({
    direction: ['Needed' as InterventionDirection, Validators.required],
    categoryName: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    priority: ['Medium' as InterventionPriority, Validators.required],
    assignedStaffUserId: ['', Validators.required],
    dueAtLocal: [''],
    providedAtLocal: [''],
  });

  readonly updateForm = this.fb.nonNullable.group({
    status: ['Open' as InterventionStatus, Validators.required],
    outcome: ['', Validators.maxLength(2000)],
  });

  async ngOnInit(): Promise<void> {
    await this.loadInterventions();
  }

  async loadInterventions(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      this.items.set(await this.caseApi.listInterventions(this.caseId()));
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  isOverdue(item: InterventionDto): boolean {
    return item.direction === 'Needed'
      && item.status === 'Open'
      && !!item.dueAtUtc
      && new Date(item.dueAtUtc).getTime() < Date.now();
  }

  async submitAdd(): Promise<void> {
    if (this.submitting() || this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }

    const value = this.addForm.getRawValue();
    let dueAtUtc: string | null = null;
    let providedAtUtc: string | null = null;

    if (value.direction === 'Needed') {
      if (!value.dueAtLocal) {
        this.formErrorMessage.set('Due date is required for Needed interventions.');
        return;
      }
      const due = new Date(value.dueAtLocal);
      if (Number.isNaN(due.getTime()) || due.getTime() <= Date.now()) {
        this.formErrorMessage.set('Due date must be in the future.');
        return;
      }
      dueAtUtc = due.toISOString();
    } else {
      if (!value.providedAtLocal) {
        this.formErrorMessage.set('Provided date is required for Provided interventions.');
        return;
      }
      providedAtUtc = new Date(value.providedAtLocal).toISOString();
    }

    const request: CreateInterventionRequest = {
      direction: value.direction,
      categoryName: value.categoryName.trim(),
      description: value.description.trim(),
      priority: value.priority,
      status: 'Open',
      assignedStaffUserId: value.assignedStaffUserId,
      dueAtUtc,
      providedAtUtc,
    };

    this.submitting.set(true);
    this.formErrorMessage.set(null);
    try {
      await this.caseApi.createIntervention(this.caseId(), request);
      this.addForm.reset({
        direction: 'Needed',
        categoryName: '',
        description: '',
        priority: 'Medium',
        assignedStaffUserId: '',
        dueAtLocal: '',
        providedAtLocal: '',
      });
      await this.loadInterventions();
    } catch (error) {
      this.formErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  startUpdate(item: InterventionDto): void {
    this.updatingId.set(item.id ?? null);
    this.updateForm.setValue({
      status: (item.status as InterventionStatus) ?? 'Open',
      outcome: item.outcome ?? '',
    });
  }

  cancelUpdate(): void {
    this.updatingId.set(null);
  }

  async submitUpdate(item: InterventionDto): Promise<void> {
    if (!item.id || this.updatingId() !== item.id) {
      return;
    }

    const value = this.updateForm.getRawValue();
    if (
      (value.status === 'Completed' || value.status === 'Cancelled')
      && !value.outcome.trim()
    ) {
      this.formErrorMessage.set('Outcome is required when status is Completed or Cancelled.');
      this.updateForm.controls.outcome.markAsTouched();
      return;
    }

    const request: UpdateInterventionRequest = {
      status: value.status,
      outcome: value.outcome.trim() || null,
    };

    if (value.status === 'Completed' || value.status === 'Cancelled') {
      request.providedAtUtc = new Date().toISOString();
    }

    this.submitting.set(true);
    this.formErrorMessage.set(null);
    try {
      await this.caseApi.updateIntervention(this.caseId(), item.id, request);
      this.updatingId.set(null);
      await this.loadInterventions();
    } catch (error) {
      this.formErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  formatTimestamp(value: string | undefined | null): string {
    if (!value) {
      return '';
    }
    return new Date(value).toLocaleString();
  }
}
