import { Component, WritableSignal, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BudgetsApiService } from './services/budgets-api.service';

interface FrequencyOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-budget-report-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>Export Budget Report</h2>

    <mat-dialog-content>
      @if (errorMessage()) {
        <div class="error-banner" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage() }}</span>
        </div>
      }

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Report Frequency</mat-label>
        <mat-select [(ngModel)]="frequency" required>
          @for (opt of frequencyOptions; track opt.value) {
            <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Year</mat-label>
        <input matInput type="number" [(ngModel)]="year" required min="2000" max="2100" />
      </mat-form-field>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="cancel()" [disabled]="generating()">Cancel</button>
      @if (generating()) {
        <button mat-flat-button color="primary" disabled>
          <mat-spinner diameter="20" /> Generating...
        </button>
      } @else {
        <button mat-flat-button color="primary" (click)="generate()" [disabled]="!isValid()">
          <mat-icon>download</mat-icon> Generate
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; margin-bottom: 0.5rem; }
    .error-banner {
      display: flex; align-items: center; gap: 0.75rem;
      padding: 0.75rem 1rem; background: #FEF2F2; color: #991B1B;
      border-radius: 0.5rem; margin-bottom: 1rem;
    }
    .error-banner span { flex: 1; }
    mat-spinner { display: inline-block; margin-right: 0.5rem; }
  `,
})
export class BudgetReportDialogComponent {
  private readonly api = inject(BudgetsApiService);
  private readonly dialogRef = inject(MatDialogRef<BudgetReportDialogComponent>);

  readonly frequencyOptions: FrequencyOption[] = [
    { value: 'Monthly', label: 'Monthly' },
    { value: 'Quarterly', label: 'Quarterly' },
    { value: 'HalfYearly', label: 'Half-Yearly' },
    { value: 'Annually', label: 'Annually' },
  ];

  frequency = 'Quarterly';
  year = new Date().getFullYear();
  readonly generating: WritableSignal<boolean> = signal(false);
  readonly errorMessage: WritableSignal<string | null> = signal(null);

  isValid(): boolean {
    return !!this.frequency && this.year >= 2000 && this.year <= 2100;
  }

  cancel(): void {
    this.dialogRef.close();
  }

  async generate(): Promise<void> {
    this.generating.set(true);
    this.errorMessage.set(null);

    try {
      const blob = await this.api.exportReport(this.frequency, this.year);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Budget-Report-${this.frequency}-${this.year}.xlsx`;
      a.click();
      window.URL.revokeObjectURL(url);
      this.dialogRef.close(true);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.generating.set(false);
    }
  }
}
