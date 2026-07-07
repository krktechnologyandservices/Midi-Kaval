import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { BudgetsApiService } from './services/budgets-api.service';
import {
  BUDGET_SOURCE_OPTIONS,
  BUDGET_HEAD_OPTIONS,
  CreateBudgetRequest,
  CreateBudgetLineItemRequest,
} from './budget.models';

interface LineItemForm {
  budgetHead: string;
  amountAllocated: number | null;
}

// Date.toISOString() converts through UTC, which shifts local midnight back a day in
// timezones ahead of UTC (e.g. IST) — format from local Y/M/D components instead.
function toLocalDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

@Component({
  selector: 'app-budget-create-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>Create Budget</h2>
    <mat-dialog-content>
      <div class="form-grid">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Source</mat-label>
          <mat-select [(ngModel)]="source" required>
            @for (opt of sourceOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Financial Year Start</mat-label>
          <input matInput [matDatepicker]="startPicker" [(ngModel)]="financialYearStart" required />
          <mat-datepicker-toggle matSuffix [for]="startPicker" />
          <mat-datepicker #startPicker startView="multi-year" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Financial Year End</mat-label>
          <input matInput [matDatepicker]="endPicker" [(ngModel)]="financialYearEnd" required />
          <mat-datepicker-toggle matSuffix [for]="endPicker" />
          <mat-datepicker #endPicker startView="multi-year" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Notes (optional)</mat-label>
          <textarea matInput [(ngModel)]="notes" rows="3"></textarea>
        </mat-form-field>
      </div>

      <h3>Line Items</h3>
      @if (errorMessage) {
        <div class="error-line" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage }}</span>
        </div>
      }

      @for (item of lineItems; track item; let i = $index) {
        <div class="line-item-row">
          <mat-form-field appearance="outline" class="head-field">
            <mat-label>Budget Head</mat-label>
            <mat-select [(ngModel)]="item.budgetHead" required>
              @for (opt of headOptions; track opt.value) {
                <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="amount-field">
            <mat-label>Amount</mat-label>
            <input matInput type="number" min="0" step="0.01" [(ngModel)]="item.amountAllocated" required />
          </mat-form-field>

          <button mat-icon-button color="warn" (click)="removeLineItem(i)" [disabled]="lineItems.length <= 1" aria-label="Remove line item">
            <mat-icon>remove_circle</mat-icon>
          </button>
        </div>
      }

      <button mat-stroked-button (click)="addLineItem()" class="add-line-btn">
        <mat-icon>add</mat-icon>
        Add line item
      </button>
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
    mat-dialog-content { min-width: 480px; padding-top: 8px; }
    .form-grid { display: flex; flex-direction: column; gap: 0.75rem; margin-bottom: 1rem; }
    .line-item-row { display: flex; gap: 0.75rem; align-items: flex-start; margin-bottom: 0.5rem; }
    .head-field { flex: 1; }
    .amount-field { width: 160px; }
    .add-line-btn { margin-top: 0.5rem; }
    .error-line { display: flex; align-items: center; gap: 0.5rem; color: #991B1B; font-size: 0.875rem; margin-bottom: 0.5rem; }
    .error-line mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    h3 { margin: 0 0 0.5rem; font-size: 0.875rem; font-weight: 500; color: var(--mat-sys-on-surface-variant, #475467); }
  `,
})
export class BudgetCreateDialogComponent {
  private readonly api = inject(BudgetsApiService);
  private readonly dialogRef = inject(MatDialogRef<BudgetCreateDialogComponent, boolean>);

  readonly sourceOptions = BUDGET_SOURCE_OPTIONS;
  readonly headOptions = BUDGET_HEAD_OPTIONS;

  source = '';
  financialYearStart: Date | null = null;
  financialYearEnd: Date | null = null;
  notes = '';
  lineItems: LineItemForm[] = [{ budgetHead: '', amountAllocated: null }];
  errorMessage = '';
  saving = false;

  addLineItem(): void {
    this.lineItems.push({ budgetHead: '', amountAllocated: null });
  }

  removeLineItem(index: number): void {
    if (this.lineItems.length > 1) {
      this.lineItems.splice(index, 1);
    }
  }

  isValid(): boolean {
    if (!this.source || !this.financialYearStart || !this.financialYearEnd) return false;
    if (this.financialYearStart > this.financialYearEnd) return false;
    if (this.lineItems.length === 0) return false;
    if (this.lineItems.some((li) => !li.budgetHead || li.amountAllocated == null || li.amountAllocated <= 0)) return false;
    const heads = this.lineItems.map((li) => li.budgetHead).filter((h) => h);
    if (new Set(heads).size !== heads.length) return false;
    return true;
  }

  async save(): Promise<void> {
    if (!this.isValid()) return;
    this.saving = true;
    this.errorMessage = '';

    const request: CreateBudgetRequest = {
      source: this.source,
      financialYearStart: toLocalDateString(this.financialYearStart!),
      financialYearEnd: toLocalDateString(this.financialYearEnd!),
      notes: this.notes || undefined,
      lineItems: this.lineItems.map(
        (li) =>
          ({ budgetHead: li.budgetHead, amountAllocated: li.amountAllocated ?? 0 }) satisfies CreateBudgetLineItemRequest,
      ),
    };

    try {
      await this.api.create(request);
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
