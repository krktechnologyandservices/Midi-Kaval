import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  COURT_SITTING_STATUSES,
  CourtSittingDto,
  CourtSittingStatus,
  CreateCourtSittingRequest,
  UpdateCourtSittingRequest,
} from '../models/case.models';
import { CaseApiService } from '../services/case-api.service';
import {
  buildTimeOfDayOptions,
  combineDateAndTime,
  defaultScheduleDate,
  ensureTimeOptionPresent,
  splitIsoToDateAndTime,
  startOfToday,
  TimeOfDayOption,
} from '../../../shared/utils/schedule-time.util';

const DEFAULT_SCHEDULE_TIME = '10:00';

export function isCourtSittingPastDue(item: CourtSittingDto): boolean {
  return item.status === 'Upcoming'
    && !!item.scheduledAtUtc
    && new Date(item.scheduledAtUtc).getTime() < Date.now();
}

@Component({
  selector: 'app-case-court-sittings',
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './case-court-sittings.component.html',
  styleUrl: './case-court-sittings.component.scss',
})
export class CaseCourtSittingsComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();
  readonly statuses = COURT_SITTING_STATUSES;

  readonly items = signal<CourtSittingDto[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly updatingId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly formErrorMessage = signal<string | null>(null);

  readonly minScheduleDate = startOfToday();
  readonly timeOptions = buildTimeOfDayOptions(15);
  readonly updateScheduledTimeOptions = signal<TimeOfDayOption[]>(this.timeOptions);
  readonly updateNextCourtTimeOptions = signal<TimeOfDayOption[]>(this.timeOptions);

  readonly addForm = this.fb.nonNullable.group({
    scheduledDate: this.fb.control<Date | null>(defaultScheduleDate(), Validators.required),
    scheduledTime: [DEFAULT_SCHEDULE_TIME, Validators.required],
    courtName: ['', [Validators.required, Validators.maxLength(256)]],
    purpose: ['', [Validators.required, Validators.maxLength(512)]],
    status: ['Upcoming' as CourtSittingStatus, Validators.required],
    notes: ['', Validators.maxLength(2000)],
    outcome: ['', Validators.maxLength(2000)],
  });

  readonly updateForm = this.fb.nonNullable.group({
    status: ['Upcoming' as CourtSittingStatus, Validators.required],
    scheduledDate: this.fb.control<Date | null>(null, Validators.required),
    scheduledTime: ['', Validators.required],
    courtName: ['', [Validators.required, Validators.maxLength(256)]],
    purpose: ['', [Validators.required, Validators.maxLength(512)]],
    notes: ['', Validators.maxLength(2000)],
    outcome: ['', Validators.maxLength(2000)],
    nextCourtDate: this.fb.control<Date | null>(null),
    nextCourtTime: [''],
  });

  async ngOnInit(): Promise<void> {
    await this.loadCourtSittings();
  }

  async loadCourtSittings(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      this.items.set(await this.caseApi.listCourtSittings(this.caseId()));
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  isPastDue(item: CourtSittingDto): boolean {
    return isCourtSittingPastDue(item);
  }

  statusChipClass(status: string | null | undefined): string {
    switch (status) {
      case 'Attended':
        return 'chip-attended';
      case 'Postponed':
        return 'chip-postponed';
      default:
        return 'chip-upcoming';
    }
  }

  async submitAdd(): Promise<void> {
    if (this.submitting() || this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }

    const value = this.addForm.getRawValue();
    const scheduled = combineDateAndTime(value.scheduledDate, value.scheduledTime);
    if (!scheduled || Number.isNaN(scheduled.getTime())) {
      this.formErrorMessage.set('Pick a scheduled date and time.');
      return;
    }

    if (value.status === 'Upcoming' && scheduled.getTime() <= Date.now()) {
      this.formErrorMessage.set('Upcoming sittings must be scheduled in the future.');
      return;
    }

    if (value.status === 'Attended' && !value.outcome.trim()) {
      this.formErrorMessage.set('Outcome is required when status is Attended.');
      this.addForm.controls.outcome.markAsTouched();
      return;
    }

    const request: CreateCourtSittingRequest = {
      scheduledAtUtc: scheduled.toISOString(),
      courtName: value.courtName.trim(),
      purpose: value.purpose.trim(),
      status: value.status,
      notes: value.notes.trim() || null,
      outcome: value.outcome.trim() || null,
    };

    this.submitting.set(true);
    this.formErrorMessage.set(null);
    try {
      await this.caseApi.createCourtSitting(this.caseId(), request);
      this.addForm.reset({
        scheduledDate: defaultScheduleDate(),
        scheduledTime: DEFAULT_SCHEDULE_TIME,
        courtName: '',
        purpose: '',
        status: 'Upcoming',
        notes: '',
        outcome: '',
      });
      await this.loadCourtSittings();
    } catch (error) {
      this.formErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  startUpdate(item: CourtSittingDto): void {
    this.updatingId.set(item.id ?? null);
    const scheduled = splitIsoToDateAndTime(item.scheduledAtUtc);
    const nextCourt = splitIsoToDateAndTime(item.nextCourtAtUtc);
    // The stored time may not land on the fixed step (e.g. legacy data at :07) — merge it
    // in as an extra option so the select shows the real value instead of going blank.
    this.updateScheduledTimeOptions.set(ensureTimeOptionPresent(this.timeOptions, scheduled.time));
    this.updateNextCourtTimeOptions.set(ensureTimeOptionPresent(this.timeOptions, nextCourt.time));
    this.updateForm.setValue({
      status: (item.status as CourtSittingStatus) ?? 'Upcoming',
      scheduledDate: scheduled.date,
      scheduledTime: scheduled.time,
      courtName: item.courtName ?? '',
      purpose: item.purpose ?? '',
      notes: item.notes ?? '',
      outcome: item.outcome ?? '',
      nextCourtDate: nextCourt.date,
      nextCourtTime: nextCourt.time,
    });
  }

  cancelUpdate(): void {
    this.updatingId.set(null);
  }

  async submitUpdate(item: CourtSittingDto): Promise<void> {
    if (!item.id || this.updatingId() !== item.id || this.updateForm.invalid) {
      this.updateForm.markAllAsTouched();
      return;
    }

    const value = this.updateForm.getRawValue();
    const scheduled = combineDateAndTime(value.scheduledDate, value.scheduledTime);
    if (!scheduled || Number.isNaN(scheduled.getTime())) {
      this.formErrorMessage.set('Pick a scheduled date and time.');
      return;
    }

    if (value.status === 'Upcoming' && scheduled.getTime() <= Date.now()) {
      this.formErrorMessage.set('Upcoming sittings must be scheduled in the future.');
      return;
    }

    if (value.status === 'Attended' && !value.outcome.trim()) {
      this.formErrorMessage.set('Outcome is required when status is Attended.');
      this.updateForm.controls.outcome.markAsTouched();
      return;
    }

    const request: UpdateCourtSittingRequest = {
      status: value.status,
      scheduledAtUtc: scheduled.toISOString(),
      courtName: value.courtName.trim(),
      purpose: value.purpose.trim(),
      notes: value.notes.trim() || null,
      outcome: value.outcome.trim() || null,
    };

    const nextCourt = combineDateAndTime(value.nextCourtDate, value.nextCourtTime);
    if (value.status === 'Postponed' && nextCourt) {
      request.nextCourtAtUtc = nextCourt.toISOString();
    } else if (value.status === 'Postponed') {
      request.nextCourtAtUtc = null;
    }

    this.submitting.set(true);
    this.formErrorMessage.set(null);
    try {
      await this.caseApi.updateCourtSitting(this.caseId(), item.id, request);
      this.updatingId.set(null);
      await this.loadCourtSittings();
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
