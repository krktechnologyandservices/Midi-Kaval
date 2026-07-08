import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CaseApiService } from '../services/case-api.service';

@Component({
  selector: 'app-stage2-data',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './stage2-data.component.html',
  styleUrl: './stage-data-shared.scss',
})
export class Stage2DataComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly savedMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    bioPsychoSocialAssessment: [''],
    icpRecords: [''],
    lifeSkillTraining: [''],
    parentManagement: [''],
    groupWork: [''],
    communityProgramAttendance: [''],
    pmaStatus: [''],
    overallProgress: [''],
  });

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await this.caseApi.getStage2Data(this.caseId());
      this.form.patchValue({
        bioPsychoSocialAssessment: data.bioPsychoSocialAssessment ?? '',
        icpRecords: data.icpRecords ?? '',
        lifeSkillTraining: data.lifeSkillTraining ?? '',
        parentManagement: data.parentManagement ?? '',
        groupWork: data.groupWork ?? '',
        communityProgramAttendance: data.communityProgramAttendance ?? '',
        pmaStatus: data.pmaStatus ?? '',
        overallProgress: data.overallProgress ?? '',
      });
    } catch (error) {
      // A case that just arrived at this stage has no data yet — that's normal, not an error.
      if (!this.caseApi.isStageDataNotFound(error)) {
        this.errorMessage.set(this.caseApi.extractErrorMessage(error));
      }
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.errorMessage.set(null);
    this.savedMessage.set(null);

    const value = this.form.getRawValue();
    try {
      await this.caseApi.upsertStage2Data(this.caseId(), {
        bioPsychoSocialAssessment: value.bioPsychoSocialAssessment.trim() || null,
        icpRecords: value.icpRecords.trim() || null,
        lifeSkillTraining: value.lifeSkillTraining.trim() || null,
        parentManagement: value.parentManagement.trim() || null,
        groupWork: value.groupWork.trim() || null,
        communityProgramAttendance: value.communityProgramAttendance.trim() || null,
        pmaStatus: value.pmaStatus.trim() || null,
        overallProgress: value.overallProgress.trim() || null,
      });
      this.savedMessage.set('Saved.');
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
