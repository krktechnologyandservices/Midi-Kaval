import { Component, inject, OnInit, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { HandoffWhisperComponent } from '../handoff-whisper/handoff-whisper.component';
import { CaseNotesTimelineComponent } from '../notes-timeline/case-notes-timeline.component';
import { CaseInterventionsComponent } from '../interventions/case-interventions.component';
import { CaseVisitsComponent } from '../visits/case-visits.component';
import { CaseCourtSittingsComponent } from '../court-sittings/case-court-sittings.component';
import { Stage2DataComponent } from '../stage-data/stage2-data.component';
import { Stage3SupportsComponent } from '../stage-data/stage3-supports.component';
import { Stage4PlacementComponent } from '../stage-data/stage4-placement.component';
import { Stage5ReintegrationComponent } from '../stage-data/stage5-reintegration.component';
import { Stage6TerminationComponent } from '../stage-data/stage6-termination.component';
import {
  CASE_STAGE_INFO,
  CASE_STAGES,
  CaseDetailDto,
  CaseDuplicateMatchDto,
  CaseDto,
  CaseStageInfo,
  CaseSummaryDto,
  caseStageIndex,
  caseStageLabel,
  FieldWorkerUserDto,
  nextCaseStage,
  TransferCaseRequest,
  TransitionCaseStageRequest,
} from '../models/case.models';
import { CaseApiService } from '../services/case-api.service';
import { formatMatchedOn } from '../utils/matched-on-label';

interface CaseDetailState {
  summary?: CaseDuplicateMatchDto | CaseDto | CaseSummaryDto;
  fromCreate?: boolean;
}

@Component({
  selector: 'app-case-detail-placeholder',
  imports: [
    MatCardModule,
    MatButtonModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    HandoffWhisperComponent,
    CaseNotesTimelineComponent,
    CaseInterventionsComponent,
    CaseVisitsComponent,
    CaseCourtSittingsComponent,
    Stage2DataComponent,
    Stage3SupportsComponent,
    Stage4PlacementComponent,
    Stage5ReintegrationComponent,
    Stage6TerminationComponent,
  ],
  templateUrl: './case-detail-placeholder.component.html',
  styleUrl: './case-detail-placeholder.component.scss',
})
export class CaseDetailPlaceholderComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly notesTimeline = viewChild(CaseNotesTimelineComponent);
  readonly state = this.router.lastSuccessfulNavigation?.extras?.state as CaseDetailState | undefined;
  readonly formatMatchedOn = formatMatchedOn;

  readonly detail = signal<CaseDetailDto | null>(null);
  readonly fieldWorkers = signal<FieldWorkerUserDto[]>([]);
  readonly fieldWorkersError = signal<string | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly stageSubmitting = signal(false);
  readonly stageErrorMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly transferForm = this.fb.nonNullable.group({
    assigneeUserId: ['', Validators.required],
    priorActions: ['', [Validators.required, Validators.maxLength(500)]],
    openItems: ['', [Validators.required, Validators.maxLength(500)]],
    nextVisitPurpose: ['', [Validators.required, Validators.maxLength(500)]],
  });

  readonly stageForm = this.fb.nonNullable.group({
    targetStage: ['', Validators.required],
    notes: [''],
  });

  readonly stages = CASE_STAGES;
  readonly stageInfo = CASE_STAGE_INFO;
  readonly caseStageLabel = caseStageLabel;

  get summary(): CaseDuplicateMatchDto | CaseDto | CaseSummaryDto | CaseDetailDto | undefined {
    return this.detail() ?? this.state?.summary;
  }

  currentStageIndex(): number {
    return caseStageIndex(this.detail()?.currentStage);
  }

  currentStageInfo(): CaseStageInfo | null {
    const stage = this.detail()?.currentStage;
    return stage ? this.stageInfo[stage as keyof typeof this.stageInfo] ?? null : null;
  }

  stageStatus(index: number): 'completed' | 'current' | 'pending' {
    const current = this.currentStageIndex();
    if (current < 0) {
      return 'pending';
    }
    if (index < current) {
      return 'completed';
    }
    return index === current ? 'current' : 'pending';
  }

  assignedWorkerLabel(): string | null {
    const summary = this.detail();
    const workerId = summary?.assignedWorkerUserId;
    if (!workerId) {
      return null;
    }

    const worker = this.fieldWorkers().find((candidate) => candidate.id === workerId);
    return worker?.email ?? workerId;
  }

  nextForwardStage(): string | null {
    return nextCaseStage(this.detail()?.currentStage ?? null);
  }

  stageIsTerminal(): boolean {
    return this.detail()?.currentStage === 'TerminationExclusion';
  }

  matchedOnLabel(): string | null {
    const summary = this.state?.summary;
    if (!summary || !('matchedOn' in summary) || !summary.matchedOn) {
      return null;
    }

    return this.formatMatchedOn(summary.matchedOn);
  }

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadDetail(), this.loadFieldWorkers()]);
  }

  async loadDetail(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const data = await this.caseApi.getCaseDetail(this.caseId);
      this.detail.set(data);

      if (!this.stageIsTerminal()) {
        const nextStage = nextCaseStage(data.currentStage);
        this.stageForm.patchValue({
          targetStage: nextStage ?? '',
          notes: '',
        });
      }
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  async loadFieldWorkers(): Promise<void> {
    this.fieldWorkersError.set(null);

    try {
      const workers = await this.caseApi.listFieldWorkers();
      this.fieldWorkers.set(workers);
    } catch (error) {
      this.fieldWorkers.set([]);
      this.fieldWorkersError.set(this.caseApi.extractErrorMessage(error));
    }
  }

  async submitTransfer(): Promise<void> {
    if (this.transferForm.invalid || this.submitting()) {
      this.transferForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const value = this.transferForm.getRawValue();
    const request: TransferCaseRequest = {
      assigneeUserId: value.assigneeUserId,
      priorActions: value.priorActions.trim(),
      openItems: value.openItems.trim(),
      nextVisitPurpose: value.nextVisitPurpose.trim(),
    };

    try {
      const updated = await this.caseApi.transferCase(this.caseId, request);
      this.detail.set(updated);
      this.transferForm.reset();
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  async submitStageTransition(): Promise<void> {
    if (this.stageSubmitting() || this.stageIsTerminal()) {
      return;
    }

    const nextStage = this.nextForwardStage();
    if (!nextStage) {
      return;
    }

    if (this.stageForm.invalid) {
      this.stageForm.markAllAsTouched();
      return;
    }

    this.stageSubmitting.set(true);
    this.stageErrorMessage.set(null);

    const value = this.stageForm.getRawValue();
    const request: TransitionCaseStageRequest = {
      targetStage: nextStage,
      notes: value.notes.trim() ? value.notes.trim() : null,
    };

    try {
      await this.caseApi.transitionStage(this.caseId, request);
      await this.loadDetail();
    } catch (error) {
      this.stageErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.stageSubmitting.set(false);
    }
  }

  backHome(): void {
    void this.router.navigate(['/crisis-queue']);
  }

  onViewFullTimeline(): void {
    this.notesTimeline()?.scrollIntoView();
  }
}
