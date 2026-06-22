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
import { ReportsApiService } from './services/reports-api.service';

interface MonthOption {
  value: number;
  label: string;
}

@Component({
  selector: 'app-socio-demographic-report-dialog',
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
    <h2 mat-dialog-title>Generate Socio-Demographic Profile</h2>

    <mat-dialog-content>
      @if (errorMessage()) {
        <div class="error-banner" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage() }}</span>
        </div>
      }

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Month</mat-label>
        <mat-select [(ngModel)]="month" required>
          @for (opt of monthOptions; track opt.value) {
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
export class SocioDemographicReportDialogComponent {
  private readonly api = inject(ReportsApiService);
  private readonly dialogRef = inject(MatDialogRef<SocioDemographicReportDialogComponent>);

  readonly monthOptions: MonthOption[] = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' },
  ];

  month = new Date().getMonth() + 1;
  year = new Date().getFullYear();
  readonly generating: WritableSignal<boolean> = signal(false);
  readonly errorMessage: WritableSignal<string | null> = signal(null);

  isValid(): boolean {
    return this.month >= 1 && this.month <= 12
      && Number.isInteger(this.year) && this.year >= 2000 && this.year <= 2100;
  }

  cancel(): void {
    this.dialogRef.close();
  }

  async generate(): Promise<void> {
    this.generating.set(true);
    this.errorMessage.set(null);

    try {
      const blob = await this.api.exportSocioDemographicProfile(this.month, this.year);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Socio-Demographic-Profile-${this.month}-${this.year}.xlsx`;
      a.click();
      // Delay revoke to allow browser to start the download
      setTimeout(() => window.URL.revokeObjectURL(url), 100);
      this.dialogRef.close(true);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.generating.set(false);
    }
  }
}
