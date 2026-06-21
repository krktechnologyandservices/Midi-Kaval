import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { ImportApiService, MigrationImportResult } from '../services/import-api.service';

@Component({
  selector: 'app-import-page',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatChipsModule,
    MatIconModule,
    RouterLink,
  ],
  template: `
    <header class="page-header">
      <div>
        <h1>Legacy Import</h1>
        <p class="subtitle">Import cases from legacy Excel export — one-time migration tool</p>
      </div>
      <a mat-stroked-button routerLink="/admin">Back to Admin</a>
    </header>

    <mat-card>
      <mat-card-header>
        <mat-card-title>Upload Excel File</mat-card-title>
        <mat-card-subtitle>
          Select the legacy cases export (.xlsx) file. The file must match the mapping specification
          defined in Story 10.1.
        </mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <div
          class="drop-zone"
          [class.dragging]="isDragging()"
          (dragover)="onDragOver($event)"
          (dragleave)="onDragLeave($event)"
          (drop)="onDrop($event)"
          (click)="fileInput.click()"
          role="button"
          tabindex="0"
          aria-label="Click or drag to upload Excel file"
        >
          @if (!selectedFile()) {
            <mat-icon class="upload-icon">cloud_upload</mat-icon>
            <p>Drag & drop your .xlsx file here, or click to browse</p>
          } @else {
            <mat-icon class="file-icon">description</mat-icon>
            <p class="file-name">{{ selectedFile()!.name }}</p>
            <p class="file-size">{{ (selectedFile()!.size / 1024).toFixed(1) }} KB</p>
            <button mat-stroked-button color="warn" (click)="clearFile($event)" type="button">
              Remove
            </button>
          }
        </div>
        <input
          #fileInput
          type="file"
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          (change)="onFileSelected($event)"
          hidden
        />

        <div class="options-row">
          <mat-checkbox [(ngModel)]="dryRun">Dry run (preview only, no data written)</mat-checkbox>
        </div>

        @if (errorMessage()) {
          <div class="error-banner">
            <mat-icon>error</mat-icon>
            <span>{{ errorMessage() }}</span>
          </div>
        }

        <button
          mat-flat-button
          color="primary"
          class="run-button"
          [disabled]="!selectedFile() || loading()"
          (click)="runImport()"
        >
          @if (loading()) {
            <mat-progress-spinner mode="indeterminate" diameter="20" aria-label="Importing..."></mat-progress-spinner>
            Importing...
          } @else {
            <mat-icon>play_arrow</mat-icon>
            Run Import
          }
        </button>
      </mat-card-content>
    </mat-card>

    @if (result()) {
      <mat-card class="results-card">
        <mat-card-header>
          <mat-card-title>Import Results</mat-card-title>
          <mat-card-subtitle>
            {{ result()!.importedAtUtc | date: 'medium' }}
            @if (dryRun) {
              <span class="dry-run-badge">Dry Run</span>
            }
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <div class="summary-row">
            <div class="summary-item">
              <span class="summary-value">{{ result()!.totalRows }}</span>
              <span class="summary-label">Total Rows</span>
            </div>
            <div class="summary-item created">
              <span class="summary-value">{{ result()!.created }}</span>
              <span class="summary-label">Created</span>
            </div>
            <div class="summary-item skipped">
              <span class="summary-value">{{ result()!.skipped.length }}</span>
              <span class="summary-label">Skipped</span>
            </div>
            <div class="summary-item errors-count">
              <span class="summary-value">{{ result()!.errors.length }}</span>
              <span class="summary-label">Errors</span>
            </div>
          </div>

          @if (result()!.skipped.length > 0 || result()!.errors.length > 0) {
            <h3>Row Details</h3>
            <div class="table-wrapper">
              <table mat-table [dataSource]="detailRows()" class="results-table">
                <ng-container matColumnDef="rowIndex">
                  <th mat-header-cell *matHeaderCellDef>Row</th>
                  <td mat-cell *matCellDef="let row">{{ row.rowIndex }}</td>
                </ng-container>

                <ng-container matColumnDef="crimeNumber">
                  <th mat-header-cell *matHeaderCellDef>Crime #</th>
                  <td mat-cell *matCellDef="let row">{{ row.crimeNumber || '-' }}</td>
                </ng-container>

                <ng-container matColumnDef="stNumber">
                  <th mat-header-cell *matHeaderCellDef>ST #</th>
                  <td mat-cell *matCellDef="let row">{{ row.stNumber || '-' }}</td>
                </ng-container>

                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let row">
                    <mat-chip-row
                      [class.status-created]="row.status === 'created'"
                      [class.status-skipped]="row.status === 'skipped'"
                      [class.status-error]="row.status === 'error'"
                    >
                      {{ row.status }}
                    </mat-chip-row>
                  </td>
                </ng-container>

                <ng-container matColumnDef="reason">
                  <th mat-header-cell *matHeaderCellDef>Reason</th>
                  <td mat-cell *matCellDef="let row">{{ row.reason || '-' }}</td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
              </table>
            </div>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1.5rem;
    }

    .page-header h1 {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 500;
    }

    .subtitle {
      color: var(--mat-sys-on-surface-variant, #475467);
      font-size: 0.875rem;
      margin: 0.25rem 0 0;
    }

    .drop-zone {
      border: 2px dashed var(--mat-sys-outline, #d0d5dd);
      border-radius: 8px;
      padding: 2.5rem 1rem;
      text-align: center;
      cursor: pointer;
      transition: border-color 0.2s, background-color 0.2s;
      margin-bottom: 1rem;
    }

    .drop-zone:hover,
    .drop-zone.dragging {
      border-color: var(--mat-sys-primary, #0D6E6E);
      background-color: rgba(13, 110, 110, 0.04);
    }

    .upload-icon,
    .file-icon {
      font-size: 2.5rem;
      width: 2.5rem;
      height: 2.5rem;
      color: var(--mat-sys-on-surface-variant, #475467);
      margin-bottom: 0.5rem;
    }

    .file-name {
      font-weight: 500;
      margin: 0;
    }

    .file-size {
      font-size: 0.8rem;
      color: var(--mat-sys-on-surface-variant, #475467);
      margin: 0.25rem 0;
    }

    .options-row {
      margin-bottom: 1rem;
    }

    .error-banner {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.75rem;
      background: var(--mat-sys-error-container, #fce4ec);
      color: var(--mat-sys-on-error-container, #c62828);
      border-radius: 4px;
      margin-bottom: 1rem;
      font-size: 0.875rem;
    }

    .run-button {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      width: 100%;
      justify-content: center;
      padding: 0.75rem;
    }

    .results-card {
      margin-top: 1.5rem;
    }

    .dry-run-badge {
      display: inline-block;
      background: var(--mat-sys-secondary-container, #e8f5e9);
      color: var(--mat-sys-on-secondary-container, #2e7d32);
      font-size: 0.75rem;
      padding: 0.125rem 0.5rem;
      border-radius: 4px;
      margin-left: 0.5rem;
      font-weight: 500;
    }

    .summary-row {
      display: flex;
      gap: 1.5rem;
      margin-bottom: 1.5rem;
      flex-wrap: wrap;
    }

    .summary-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 100px;
      padding: 1rem;
      background: var(--mat-sys-surface-container-low, #f5f5f5);
      border-radius: 8px;
    }

    .summary-value {
      font-size: 2rem;
      font-weight: 600;
      line-height: 1;
    }

    .summary-label {
      font-size: 0.8rem;
      color: var(--mat-sys-on-surface-variant, #475467);
      margin-top: 0.25rem;
    }

    .created .summary-value { color: var(--status-info, #1565c0); }
    .skipped .summary-value { color: var(--status-warning, #f57f17); }
    .errors-count .summary-value { color: var(--status-critical, #c62828); }

    .table-wrapper {
      overflow-x: auto;
    }

    .results-table {
      width: 100%;
    }

    .status-created {
      --mat-chip-container-color: var(--status-info, #e3f2fd);
      color: var(--status-info, #1565c0);
    }

    .status-skipped {
      --mat-chip-container-color: var(--status-warning, #fff8e1);
      color: var(--status-warning, #f57f17);
    }

    .status-error {
      --mat-chip-container-color: var(--status-critical, #ffebee);
      color: var(--status-critical, #c62828);
    }
  `,
})
export class ImportPageComponent {
  private readonly importApi = inject(ImportApiService);

  readonly selectedFile = signal<File | null>(null);
  readonly isDragging = signal(false);
  readonly dryRun = signal(true);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly result = signal<MigrationImportResult | null>(null);

  readonly displayedColumns = ['rowIndex', 'crimeNumber', 'stNumber', 'status', 'reason'];

  get detailRows() {
    return () => {
      const r = this.result();
      if (!r) return [];
      return [...r.skipped, ...r.errors];
    };
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files[0];
    if (file) this.validateAndSetFile(file);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.validateAndSetFile(file);
  }

  clearFile(event: MouseEvent): void {
    event.stopPropagation();
    this.selectedFile.set(null);
    this.result.set(null);
    this.errorMessage.set(null);
  }

  private validateAndSetFile(file: File): void {
    if (!file.name.endsWith('.xlsx')) {
      this.errorMessage.set('Please select an .xlsx file.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.errorMessage.set('File size must not exceed 10 MB.');
      return;
    }
    this.selectedFile.set(file);
    this.errorMessage.set(null);
    this.result.set(null);
  }

  async runImport(): Promise<void> {
    const file = this.selectedFile();
    if (!file) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    this.result.set(null);

    try {
      const result = await this.importApi.import(file, this.dryRun());
      this.result.set(result);
    } catch (error: unknown) {
      this.errorMessage.set(this.importApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
}
