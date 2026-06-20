import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel: string;
}

@Component({
  selector: 'app-legend-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button mat-flat-button color="warn" [mat-dialog-close]="true">
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-dialog-content { min-width: 360px; }
  `,
})
export class LegendConfirmDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<LegendConfirmDialogComponent, boolean>);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);

  cancel(): void {
    this.dialogRef.close(false);
  }
}
