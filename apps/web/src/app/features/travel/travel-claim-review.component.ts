import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CaseDetailDto } from '../cases/models/case.models';
import { attachmentBasename } from '../cases/models/case.models';
import { AttachmentApiService } from '../cases/services/attachment-api.service';
import { CaseApiService } from '../cases/services/case-api.service';
import { TravelClaimApiService } from './services/travel-claim-api.service';
import { TravelClaimDto } from './travel.models';

@Component({
  selector: 'app-travel-claim-review',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './travel-claim-review.component.html',
  styleUrl: './travel-claim-review.component.scss',
})
export class TravelClaimReviewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly travelApi = inject(TravelClaimApiService);
  private readonly caseApi = inject(CaseApiService);
  private readonly attachmentApi = inject(AttachmentApiService);
  private readonly fb = inject(FormBuilder);

  readonly claim = signal<TravelClaimDto | null>(null);
  readonly linkedCases = signal<CaseDetailDto[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly showApproveForm = signal(false);
  readonly showReturnForm = signal(false);

  readonly readOnly = signal(false);
  readonly backLink = signal('/admin/travel-claims');

  readonly approveForm = this.fb.nonNullable.group({
    comment: ['', Validators.maxLength(2000)],
  });

  readonly returnForm = this.fb.nonNullable.group({
    comment: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  async ngOnInit(): Promise<void> {
    this.readOnly.set(this.route.snapshot.data['readOnly'] === true);
    this.backLink.set(
      this.readOnly() ? '/crisis-queue' : '/admin/travel-claims',
    );
    await this.load();
  }

  async load(): Promise<void> {
    const claimId = this.route.snapshot.paramMap.get('id');
    if (!claimId) {
      this.errorMessage.set('Claim not found.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const dto = this.readOnly()
        ? await this.travelApi.getForSupervisorReview(claimId)
        : await this.travelApi.getForDirectorReview(claimId);
      this.claim.set(dto);

      const cases = (await Promise.allSettled(
        (dto.caseIds ?? []).map((caseId) => this.caseApi.getCaseDetail(caseId)),
      ))
        .filter(
          (result): result is PromiseFulfilledResult<CaseDetailDto> =>
            result.status === 'fulfilled',
        )
        .map((result) => result.value);
      this.linkedCases.set(cases);
    } catch (error) {
      this.errorMessage.set(this.travelApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  async openReceipt(attachmentId: string, fileName: string | null | undefined): Promise<void> {
    try {
      const blob = await this.attachmentApi.download(attachmentId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = attachmentBasename(fileName ?? 'receipt');
      anchor.rel = 'noopener';
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 0);
    } catch (error) {
      this.actionError.set(this.attachmentApi.extractDownloadErrorMessage(error));
    }
  }

  async submitApprove(): Promise<void> {
    const current = this.claim();
    if (!current) {
      return;
    }

    this.submitting.set(true);
    this.actionError.set(null);
    try {
      await this.travelApi.approve(current.id, {
        comment: this.approveForm.controls.comment.value || null,
      });
      await this.router.navigateByUrl(this.backLink());
    } catch (error) {
      this.actionError.set(this.travelApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  async submitReturn(): Promise<void> {
    const current = this.claim();
    if (!current || this.returnForm.invalid) {
      this.returnForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.actionError.set(null);
    try {
      await this.travelApi.returnClaim(current.id, {
        comment: this.returnForm.controls.comment.value,
      });
      await this.router.navigateByUrl(this.backLink());
    } catch (error) {
      this.actionError.set(this.travelApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  basename(fileName: string): string {
    return attachmentBasename(fileName);
  }
}
