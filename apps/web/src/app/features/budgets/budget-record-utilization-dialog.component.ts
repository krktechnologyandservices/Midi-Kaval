import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { CaseApiService } from '../cases/services/case-api.service';
import { AttachmentApiService } from '../cases/services/attachment-api.service';
import { CaseSummaryDto, MAX_ATTACHMENT_BYTES } from '../cases/models/case.models';
import { BudgetsApiService } from './services/budgets-api.service';
import { BudgetLineItemDto, BudgetUtilizationListDto, formatAmount } from './budget.models';

export interface RecordUtilizationDialogData {
  budgetId: string;
  lineItems: BudgetLineItemDto[];
  existing?: BudgetUtilizationListDto;
}

const ALLOWED_ATTACHMENT_CONTENT_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'] as const;

// Date.toISOString() converts through UTC, which shifts local midnight back a day in
// timezones ahead of UTC (e.g. IST) — format from local Y/M/D components instead.
function toLocalDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function parseLocalDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

@Component({
  selector: 'app-budget-record-utilization-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatAutocompleteModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit Utilization' : 'Record Utilization' }}</h2>
    <mat-dialog-content>
      @if (errorMessage) {
        <div class="error-line" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage }}</span>
        </div>
      }

      <div class="form-grid">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Budget Head</mat-label>
          <mat-select [(ngModel)]="budgetLineItemId" required [disabled]="isEdit">
            @for (li of lineItems; track li.id) {
              <mat-option [value]="li.id">
                {{ li.budgetHead }} — balance {{ formatAmount(li.amountAllocated - li.amountUtilized) }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (remainingBalance() !== null) {
          <p class="balance-hint">
            Remaining balance for this head: <strong>{{ formatAmount(remainingBalance()!) }}</strong>
          </p>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Amount</mat-label>
          <input matInput type="number" min="0.01" step="0.01" [(ngModel)]="amountUtilized" required />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="utilPicker" [(ngModel)]="utilizationDate" required />
          <mat-datepicker-toggle matSuffix [for]="utilPicker" />
          <mat-datepicker #utilPicker />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Description / Activity</mat-label>
          <textarea matInput [(ngModel)]="description" rows="3" maxlength="500" required></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Case (optional)</mat-label>
          <input
            matInput
            [(ngModel)]="caseSearchTerm"
            (ngModelChange)="onCaseSearchChange($event)"
            [matAutocomplete]="caseAuto"
            placeholder="Search crime number or beneficiary name"
          />
          @if (caseSearching) {
            <mat-spinner matSuffix diameter="18" />
          } @else if (selectedCase) {
            <button matSuffix mat-icon-button type="button" (click)="clearCase()" aria-label="Clear case">
              <mat-icon>close</mat-icon>
            </button>
          }
          <mat-autocomplete #caseAuto="matAutocomplete" (optionSelected)="onCaseSelected($event.option.value)">
            @for (c of caseOptions; track c.id) {
              <mat-option [value]="c">{{ c.crimeNumber }} — {{ c.beneficiaryName }}</mat-option>
            }
          </mat-autocomplete>
        </mat-form-field>

        <div class="attachment-section">
          <p class="attachment-label">Receipt / Bill (optional)</p>
          @if (existingAttachments.length > 0) {
            <ul class="attachment-list">
              @for (a of existingAttachments; track a.id) {
                <li>
                  <mat-icon>description</mat-icon>
                  <span>{{ a.originalFileName }}</span>
                </li>
              }
            </ul>
          }
          <input
            #fileInput
            type="file"
            accept="image/jpeg,image/png,image/webp,application/pdf"
            (change)="onFileSelected($event)"
            hidden
          />
          <button mat-stroked-button type="button" (click)="fileInput.click()" [disabled]="uploading">
            <mat-icon>attach_file</mat-icon>
            {{ selectedFile ? selectedFile.name : 'Attach receipt' }}
          </button>
          @if (uploadErrorMessage) {
            <p class="upload-error">{{ uploadErrorMessage }}</p>
          }
        </div>
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="!isValid() || saving" (click)="save()">
        {{ saving ? 'Saving...' : 'Save' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    mat-dialog-content { min-width: 440px; padding-top: 8px; }
    .form-grid { display: flex; flex-direction: column; gap: 0.75rem; }
    .balance-hint { margin: -0.5rem 0 0.25rem; font-size: 0.8125rem; color: var(--mat-sys-on-surface-variant, #475467); }
    .error-line { display: flex; align-items: center; gap: 0.5rem; color: #991B1B; font-size: 0.875rem; margin-bottom: 0.5rem; }
    .error-line mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .attachment-section { display: flex; flex-direction: column; gap: 0.375rem; margin-top: 0.25rem; }
    .attachment-label { font-size: 0.8125rem; color: var(--mat-sys-on-surface-variant, #475467); margin: 0; }
    .attachment-list { list-style: none; margin: 0 0 0.25rem; padding: 0; display: flex; flex-direction: column; gap: 0.25rem; }
    .attachment-list li { display: flex; align-items: center; gap: 0.375rem; font-size: 0.8125rem; }
    .attachment-list mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    .upload-error { color: #991B1B; font-size: 0.8125rem; margin: 0.25rem 0 0; }
  `,
})
export class BudgetRecordUtilizationDialogComponent {
  private readonly api = inject(BudgetsApiService);
  private readonly caseApi = inject(CaseApiService);
  private readonly attachmentApi = inject(AttachmentApiService);
  private readonly dialogRef = inject(MatDialogRef<BudgetRecordUtilizationDialogComponent, boolean>);

  readonly formatAmount = formatAmount;
  readonly lineItems: BudgetLineItemDto[];
  readonly isEdit: boolean;

  budgetLineItemId = '';
  amountUtilized: number | null = null;
  utilizationDate: Date | null = new Date();
  description = '';
  errorMessage = '';
  saving = false;

  caseSearchTerm = '';
  selectedCase: CaseSummaryDto | null = null;
  caseOptions: CaseSummaryDto[] = [];
  caseSearching = false;
  private caseSearchTimer: ReturnType<typeof setTimeout> | null = null;
  private caseSearchGeneration = 0;

  selectedFile: File | null = null;
  uploading = false;
  uploadErrorMessage = '';
  existingAttachments: { id: string; originalFileName: string }[] = [];

  constructor(@Inject(MAT_DIALOG_DATA) private readonly data: RecordUtilizationDialogData) {
    this.lineItems = data.lineItems;
    this.isEdit = !!data.existing;

    if (data.existing) {
      const e = data.existing;
      this.budgetLineItemId = e.budgetLineItemId;
      this.amountUtilized = e.amountUtilized;
      this.utilizationDate = parseLocalDate(e.utilizationDate);
      this.description = e.description;
      this.existingAttachments = e.attachments ?? [];
      // Preserve the link even if crimeNumber can't be displayed (e.g. a dangling case reference) —
      // otherwise saving any other field on this entry would silently unlink the case, since
      // save() sends `selectedCase?.id` and the backend applies CaseId unconditionally on update.
      if (e.caseId) {
        this.selectedCase = { id: e.caseId, crimeNumber: e.caseCrimeNumber ?? '(unavailable)' } as CaseSummaryDto;
        this.caseSearchTerm = e.caseCrimeNumber ?? '(unavailable)';
      }
    }
  }

  remainingBalance(): number | null {
    const li = this.lineItems.find((l) => l.id === this.budgetLineItemId);
    return li ? li.amountAllocated - li.amountUtilized : null;
  }

  isValid(): boolean {
    return !!this.budgetLineItemId
      && this.amountUtilized != null && this.amountUtilized > 0
      && this.utilizationDate != null
      && this.description.trim().length > 0;
  }

  onCaseSearchChange(term: string): void {
    this.selectedCase = null;
    if (this.caseSearchTimer) clearTimeout(this.caseSearchTimer);
    if (!term || term.trim().length < 2) {
      this.caseOptions = [];
      return;
    }
    this.caseSearchTimer = setTimeout(() => void this.runCaseSearch(term.trim()), 350);
  }

  private async runCaseSearch(term: string): Promise<void> {
    // Guard against out-of-order responses: if a newer search has started by the time this one
    // resolves, discard it instead of overwriting caseOptions with stale results.
    const generation = ++this.caseSearchGeneration;
    this.caseSearching = true;
    try {
      const { result } = await this.caseApi.searchCases({ q: term, page: 1, pageSize: 10 });
      if (generation !== this.caseSearchGeneration) return;
      this.caseOptions = result.items ?? [];
    } catch {
      if (generation !== this.caseSearchGeneration) return;
      this.caseOptions = [];
    } finally {
      if (generation === this.caseSearchGeneration) {
        this.caseSearching = false;
      }
    }
  }

  onCaseSelected(caseSummary: CaseSummaryDto): void {
    this.selectedCase = caseSummary;
    this.caseSearchTerm = `${caseSummary.crimeNumber} — ${caseSummary.beneficiaryName}`;
    this.caseOptions = [];
  }

  clearCase(): void {
    this.selectedCase = null;
    this.caseSearchTerm = '';
    this.caseOptions = [];
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.uploadErrorMessage = '';

    if (!file) {
      this.selectedFile = null;
      return;
    }

    if (!ALLOWED_ATTACHMENT_CONTENT_TYPES.includes(file.type as (typeof ALLOWED_ATTACHMENT_CONTENT_TYPES)[number])) {
      this.uploadErrorMessage = 'File type not allowed. Use JPEG, PNG, WebP, or PDF.';
      this.selectedFile = null;
      input.value = '';
      return;
    }

    if (file.size > MAX_ATTACHMENT_BYTES) {
      this.uploadErrorMessage = `File is too large. Maximum size is ${Math.round(MAX_ATTACHMENT_BYTES / (1024 * 1024))}MB.`;
      this.selectedFile = null;
      input.value = '';
      return;
    }

    this.selectedFile = file;
  }

  private async uploadAttachmentIfSelected(utilizationId: string): Promise<void> {
    if (!this.selectedFile) return;
    this.uploading = true;
    try {
      const presign = await this.attachmentApi.presign({
        resourceType: 'BudgetUtilization',
        resourceId: utilizationId,
        fileName: this.selectedFile.name,
        contentType: this.selectedFile.type,
        fileSizeBytes: this.selectedFile.size,
      });

      if (!presign.uploadUrl || !presign.attachmentId) {
        throw new Error('Presign failed');
      }

      await this.attachmentApi.uploadToPresignedUrl(
        presign.uploadUrl,
        this.selectedFile,
        presign.requiredHeaders ?? { 'x-ms-blob-type': 'BlockBlob', 'Content-Type': this.selectedFile.type },
      );

      await this.attachmentApi.confirm({ attachmentId: presign.attachmentId });
    } catch (error) {
      this.uploadErrorMessage = this.attachmentApi.extractErrorMessage(error);
    } finally {
      this.uploading = false;
    }
  }

  async save(): Promise<void> {
    if (!this.isValid()) return;
    this.saving = true;
    this.errorMessage = '';

    try {
      let utilizationId: string;
      if (this.isEdit && this.data.existing) {
        const updated = await this.api.updateUtilization(this.data.budgetId, this.data.existing.id, {
          caseId: this.selectedCase?.id,
          amountUtilized: this.amountUtilized!,
          utilizationDate: toLocalDateString(this.utilizationDate!),
          description: this.description.trim(),
        });
        utilizationId = updated.id;
      } else {
        const created = await this.api.createUtilization(this.data.budgetId, {
          budgetLineItemId: this.budgetLineItemId,
          caseId: this.selectedCase?.id,
          amountUtilized: this.amountUtilized!,
          utilizationDate: toLocalDateString(this.utilizationDate!),
          description: this.description.trim(),
        });
        utilizationId = created.id;
      }

      await this.uploadAttachmentIfSelected(utilizationId);
      this.dialogRef.close(true);
    } catch (error) {
      this.errorMessage = this.api.extractErrorMessage(error);
    } finally {
      this.saving = false;
    }
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
