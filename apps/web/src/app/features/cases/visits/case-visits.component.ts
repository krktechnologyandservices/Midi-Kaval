import { Component, computed, inject, input, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  AddVisitPlaceRequest,
  FieldWorkerUserDto,
  GeocodingResultDto,
  RescheduleVisitRequest,
  ScheduleVisitRequest,
  UpdateVisitPlaceRequest,
  VisitListItemDto,
  VisitPlaceDto,
} from '../models/case.models';
import { CaseApiService } from '../services/case-api.service';
import {
  buildTimeOfDayOptions,
  combineDateAndTime,
  defaultScheduleDate,
  startOfToday,
} from '../../../shared/utils/schedule-time.util';

const ACTIVE_STATUSES = ['Scheduled', 'InProgress'];
const DEFAULT_SCHEDULE_TIME = '10:00';

@Component({
  selector: 'app-case-visits',
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './case-visits.component.html',
  styleUrl: './case-visits.component.scss',
})
export class CaseVisitsComponent implements OnInit, OnDestroy {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();
  readonly fieldWorkers = input<FieldWorkerUserDto[]>([]);
  readonly assignedWorkerId = input<string | null | undefined>(null);
  readonly stageIsTerminal = input(false);

  readonly items = signal<VisitListItemDto[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly cancellingId = signal<string | null>(null);
  readonly reschedulingId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly formErrorMessage = signal<string | null>(null);
  readonly cancelFormErrorMessage = signal<string | null>(null);
  readonly rescheduleFormErrorMessage = signal<string | null>(null);

  readonly showNoMatchHint = computed(
    () =>
      !this.placeSearchLoading()
      && this.placeSearchResults().length === 0
      && !this.selectedPlaceResult()
      && this.placeSearchQuery().trim().length >= 3,
  );

  readonly activeVisit = computed(
    () => this.items().find((item) => ACTIVE_STATUSES.includes(item.status)) ?? null,
  );

  readonly minScheduleDate = startOfToday();
  readonly timeOptions = buildTimeOfDayOptions(15);

  readonly addForm = this.fb.nonNullable.group({
    scheduledDate: this.fb.control<Date | null>(defaultScheduleDate(), Validators.required),
    scheduledTime: [DEFAULT_SCHEDULE_TIME, Validators.required],
    assigneeUserId: [''],
  });

  readonly cancelForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });

  readonly rescheduleForm = this.fb.nonNullable.group({
    scheduledDate: this.fb.control<Date | null>(defaultScheduleDate(), Validators.required),
    scheduledTime: [DEFAULT_SCHEDULE_TIME, Validators.required],
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });

  readonly addingPlaceForVisitId = signal<string | null>(null);
  readonly placeSearchQuery = signal('');
  readonly placeSearchResults = signal<GeocodingResultDto[]>([]);
  readonly placeSearchLoading = signal(false);
  readonly selectedPlaceResult = signal<GeocodingResultDto | null>(null);
  readonly placeFormErrorMessage = signal<string | null>(null);
  readonly addingPlaceSubmitting = signal(false);

  readonly editingPlaceId = signal<string | null>(null);
  readonly editPlaceForm = this.fb.nonNullable.group({
    address: ['', [Validators.required, Validators.maxLength(500)]],
  });
  readonly editPlaceFormErrorMessage = signal<string | null>(null);
  readonly editPlaceSubmitting = signal(false);

  readonly removingPlaceId = signal<string | null>(null);
  readonly removePlaceErrorMessage = signal<string | null>(null);
  readonly removePlaceSubmitting = signal(false);

  private placeSearchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  private placeSearchSequence = 0;

  async ngOnInit(): Promise<void> {
    await this.loadVisits();
  }

  ngOnDestroy(): void {
    if (this.placeSearchDebounceTimer) {
      clearTimeout(this.placeSearchDebounceTimer);
    }
  }

  async loadVisits(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      this.items.set(await this.caseApi.listVisits(this.caseId()));
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  statusChipClass(status: string): string {
    switch (status) {
      case 'InProgress':
        return 'chip-in-progress';
      case 'Completed':
        return 'chip-completed';
      case 'Cancelled':
        return 'chip-cancelled';
      default:
        return 'chip-scheduled';
    }
  }

  isActive(item: VisitListItemDto): boolean {
    return ACTIVE_STATUSES.includes(item.status);
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

    if (scheduled.getTime() <= Date.now()) {
      this.formErrorMessage.set('Visits must be scheduled in the future.');
      return;
    }

    if (!value.assigneeUserId && !this.assignedWorkerId()) {
      this.formErrorMessage.set(
        'Pick a field worker — this case has no assigned worker to default to.',
      );
      return;
    }

    const request: ScheduleVisitRequest = {
      scheduledAtUtc: scheduled.toISOString(),
      assigneeUserId: value.assigneeUserId || null,
    };

    this.submitting.set(true);
    this.formErrorMessage.set(null);
    try {
      await this.caseApi.scheduleVisit(this.caseId(), request);
      this.addForm.reset({
        scheduledDate: defaultScheduleDate(),
        scheduledTime: DEFAULT_SCHEDULE_TIME,
        assigneeUserId: '',
      });
      await this.loadVisits();
    } catch (error) {
      this.formErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  startCancel(item: VisitListItemDto): void {
    this.cancellingId.set(item.id);
    this.cancelForm.reset({ reason: '' });
    this.cancelFormErrorMessage.set(null);
  }

  cancelCancelling(): void {
    this.cancellingId.set(null);
  }

  async submitCancel(item: VisitListItemDto): Promise<void> {
    if (this.cancellingId() !== item.id || this.cancelForm.invalid) {
      this.cancelForm.markAllAsTouched();
      return;
    }

    const reason = this.cancelForm.getRawValue().reason.trim();

    this.submitting.set(true);
    this.cancelFormErrorMessage.set(null);
    try {
      await this.caseApi.cancelVisit(this.caseId(), item.id, { reason });
      this.cancellingId.set(null);
      await this.loadVisits();
    } catch (error) {
      this.cancelFormErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  startReschedule(item: VisitListItemDto): void {
    this.reschedulingId.set(item.id);
    this.rescheduleForm.reset({
      scheduledDate: defaultScheduleDate(),
      scheduledTime: DEFAULT_SCHEDULE_TIME,
      reason: '',
    });
    this.rescheduleFormErrorMessage.set(null);
  }

  cancelRescheduling(): void {
    this.reschedulingId.set(null);
  }

  async submitReschedule(item: VisitListItemDto): Promise<void> {
    if (this.reschedulingId() !== item.id || this.rescheduleForm.invalid) {
      this.rescheduleForm.markAllAsTouched();
      return;
    }

    const value = this.rescheduleForm.getRawValue();
    const scheduled = combineDateAndTime(value.scheduledDate, value.scheduledTime);
    if (!scheduled || Number.isNaN(scheduled.getTime())) {
      this.rescheduleFormErrorMessage.set('Pick a scheduled date and time.');
      return;
    }

    if (scheduled.getTime() <= Date.now()) {
      this.rescheduleFormErrorMessage.set('Visits must be scheduled in the future.');
      return;
    }

    const request: RescheduleVisitRequest = {
      scheduledAtUtc: scheduled.toISOString(),
      reason: value.reason.trim(),
    };

    this.submitting.set(true);
    this.rescheduleFormErrorMessage.set(null);
    try {
      await this.caseApi.rescheduleVisit(item.id, request);
      this.reschedulingId.set(null);
      await this.loadVisits();
    } catch (error) {
      this.rescheduleFormErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  formatTimestamp(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    return new Date(value).toLocaleString();
  }

  startAddPlace(visitId: string): void {
    this.addingPlaceForVisitId.set(visitId);
    this.placeSearchQuery.set('');
    this.placeSearchResults.set([]);
    this.selectedPlaceResult.set(null);
    this.placeFormErrorMessage.set(null);
  }

  cancelAddPlace(): void {
    this.addingPlaceForVisitId.set(null);
    this.placeSearchResults.set([]);
    this.selectedPlaceResult.set(null);
  }

  onPlaceSearchInput(rawValue: string): void {
    this.placeSearchQuery.set(rawValue);
    this.selectedPlaceResult.set(null);

    if (this.placeSearchDebounceTimer) {
      clearTimeout(this.placeSearchDebounceTimer);
    }

    const query = rawValue.trim();
    if (query.length < 3) {
      this.placeSearchResults.set([]);
      this.placeSearchLoading.set(false);
      return;
    }

    this.placeSearchLoading.set(true);
    const sequence = ++this.placeSearchSequence;
    this.placeSearchDebounceTimer = setTimeout(() => {
      void this.runPlaceSearch(query, sequence);
    }, 350);
  }

  private async runPlaceSearch(query: string, sequence: number): Promise<void> {
    try {
      const results = await this.caseApi.searchGeocodingAddresses(query);
      // Discard if a newer keystroke has already started a later search — otherwise a
      // slow earlier response could overwrite the results of what the user typed next.
      if (sequence !== this.placeSearchSequence) {
        return;
      }
      this.placeSearchResults.set(results);
    } catch (error) {
      if (sequence !== this.placeSearchSequence) {
        return;
      }
      this.placeFormErrorMessage.set(this.caseApi.extractErrorMessage(error));
      this.placeSearchResults.set([]);
    } finally {
      if (sequence === this.placeSearchSequence) {
        this.placeSearchLoading.set(false);
      }
    }
  }

  selectPlaceResult(result: GeocodingResultDto): void {
    this.selectedPlaceResult.set(result);
    this.placeSearchQuery.set(result.displayName);
    this.placeSearchResults.set([]);
  }

  async submitAddPlace(item: VisitListItemDto): Promise<void> {
    if (this.addingPlaceForVisitId() !== item.id) {
      return;
    }

    const selected = this.selectedPlaceResult();
    const typedAddress = this.placeSearchQuery().trim();
    if (!typedAddress) {
      this.placeFormErrorMessage.set('Search for and pick an address.');
      return;
    }

    const request: AddVisitPlaceRequest = {
      address: selected?.displayName ?? typedAddress,
      osmReference: selected?.osmReference ?? null,
      plannedLatitude: selected?.latitude ?? null,
      plannedLongitude: selected?.longitude ?? null,
    };

    this.addingPlaceSubmitting.set(true);
    this.placeFormErrorMessage.set(null);
    try {
      await this.caseApi.addVisitPlace(this.caseId(), item.id, request);
      this.cancelAddPlace();
      await this.loadVisits();
    } catch (error) {
      this.placeFormErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.addingPlaceSubmitting.set(false);
    }
  }

  canEditPlace(item: VisitListItemDto, place: VisitPlaceDto): boolean {
    // A place a field worker has already logged is a real record of what happened —
    // only not-yet-visited places on an active visit stay editable/removable.
    return this.isActive(item) && !place.loggedAtUtc;
  }

  startEditPlace(place: VisitPlaceDto): void {
    this.removingPlaceId.set(null);
    this.editingPlaceId.set(place.id);
    this.editPlaceForm.reset({ address: place.address });
    this.editPlaceFormErrorMessage.set(null);
  }

  cancelEditPlace(): void {
    this.editingPlaceId.set(null);
  }

  async submitEditPlace(item: VisitListItemDto, place: VisitPlaceDto): Promise<void> {
    if (this.editingPlaceId() !== place.id || this.editPlaceForm.invalid) {
      this.editPlaceForm.markAllAsTouched();
      return;
    }

    const request: UpdateVisitPlaceRequest = {
      address: this.editPlaceForm.getRawValue().address.trim(),
      osmReference: place.osmReference,
      plannedLatitude: place.plannedLatitude,
      plannedLongitude: place.plannedLongitude,
    };

    this.editPlaceSubmitting.set(true);
    this.editPlaceFormErrorMessage.set(null);
    try {
      await this.caseApi.updateVisitPlace(this.caseId(), item.id, place.id, request);
      this.editingPlaceId.set(null);
      await this.loadVisits();
    } catch (error) {
      this.editPlaceFormErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.editPlaceSubmitting.set(false);
    }
  }

  startRemovePlace(place: VisitPlaceDto): void {
    this.editingPlaceId.set(null);
    this.removingPlaceId.set(place.id);
    this.removePlaceErrorMessage.set(null);
  }

  cancelRemovePlace(): void {
    this.removingPlaceId.set(null);
  }

  async confirmRemovePlace(item: VisitListItemDto, place: VisitPlaceDto): Promise<void> {
    if (this.removingPlaceId() !== place.id) {
      return;
    }

    this.removePlaceSubmitting.set(true);
    this.removePlaceErrorMessage.set(null);
    try {
      await this.caseApi.removeVisitPlace(this.caseId(), item.id, place.id);
      this.removingPlaceId.set(null);
      await this.loadVisits();
    } catch (error) {
      this.removePlaceErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.removePlaceSubmitting.set(false);
    }
  }

  viewOnMapUrl(place: VisitPlaceDto): string | null {
    const lat = place.loggedLatitude ?? place.plannedLatitude;
    const lon = place.loggedLongitude ?? place.plannedLongitude;
    if (lat == null || lon == null) {
      return null;
    }
    return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}#map=18/${lat}/${lon}`;
  }
}
