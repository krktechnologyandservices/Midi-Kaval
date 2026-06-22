import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { BudgetsApiService } from './services/budgets-api.service';
import {
  BUDGET_HEAD_OPTIONS,
  BudgetDetailDto,
  UpdateBudgetRequest,
  UpdateBudgetLineItemRequest,
} from './budget.models';

interface LineItemForm {
  budgetHead: string;
  amountAllocated: number | null;
}

export interface BudgetEditData {
  budgetId: string;
}

@Component({
  selector: 'app-budget-edit-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>Edit Budget</h2>
    <mat-dialog-content>
      @if (loading) {
        <div class="loading-state">Loading budget details...</div>
      } @else {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Notes</mat-label>
          <textarea matInput [(ngModel)]="notes" rows="3"></textarea>
        </mat-form-field>

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
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button
        mat-flat-button color="primary"
        [disabled]="loading || hasLoadError || !isValid() || saving"
        (click)="save()"
      >
        {{ saving ? 'Saving...' : 'Save' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    mat-dialog-content { min-width: 480px; padding-top: 8px; }
    .line-item-row { display: flex; gap: 0.75rem; align-items: flex-start; margin-bottom: 0.5rem; }
    .head-field { flex: 1; }
    .amount-field { width: 160px; }
    .add-line-btn { margin-top: 0.5rem; }
    .error-line { display: flex; align-items: center; gap: 0.5rem; color: #991B1B; font-size: 0.875rem; margin-bottom: 0.5rem; }
    .error-line mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .loading-state { padding: 2rem 0; text-align: center; color: var(--mat-sys-on-surface-variant, #475467); }
    h3 { margin: 0 0 0.5rem; font-size: 0.875rem; font-weight: 500; color: var(--mat-sys-on-surface-variant, #475467); }
  `,
})
export class BudgetEditDialogComponent implements OnInit {
  private readonly api = inject(BudgetsApiService);
  private readonly dialogRef = inject(MatDialogRef<BudgetEditDialogComponent, boolean>);
  readonly data = inject<BudgetEditData>(MAT_DIALOG_DATA);

  readonly headOptions = BUDGET_HEAD_OPTIONS;

  notes = '';
  lineItems: LineItemForm[] = [];
  errorMessage = '';
  loading = true;
  saving = false;

  async ngOnInit(): Promise<void> {
    try {
      const budget = await this.api.getById(this.data.budgetId);
      this.notes = budget.notes ?? '';
      this.lineItems = budget.lineItems.map((li) => ({
        budgetHead: li.budgetHead,
        amountAllocated: li.amountAllocated,
      }));
    } catch (error) {
      this.errorMessage = this.api.extractErrorMessage(error);
    } finally {
      this.loading = false;
    }
  }

  get hasLoadError(): boolean { return !!this.errorMessage && !this.loading; }

  addLineItem(): void {
    this.lineItems.push({ budgetHead: '', amountAllocated: null });
  }

  removeLineItem(index: number): void {
    if (this.lineItems.length > 1) {
      this.lineItems.splice(index, 1);
    }
  }

  isValid(): boolean {
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

    const request: UpdateBudgetRequest = {
      notes: this.notes || undefined,
      lineItems: this.lineItems.map(
        (li) =>
          ({ budgetHead: li.budgetHead, amountAllocated: li.amountAllocated ?? 0 }) satisfies UpdateBudgetLineItemRequest,
      ),
    };

    try {
      await this.api.update(this.data.budgetId, request);
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
