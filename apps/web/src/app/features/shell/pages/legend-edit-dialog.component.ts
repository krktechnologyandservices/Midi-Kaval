import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface LegendEditData {
  title: string;
  currentName: string;
}

export interface LegendEditResult {
  name: string;
}

@Component({
  selector: 'app-legend-edit-dialog',
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, FormsModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Name</mat-label>
        <input matInput [(ngModel)]="name" (keydown.enter)="save()" />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="!name.trim()" (click)="save()">
        Save
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    mat-dialog-content { min-width: 360px; padding-top: 8px; }
  `,
})
export class LegendEditDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<LegendEditDialogComponent, LegendEditResult>);
  readonly data = inject<LegendEditData>(MAT_DIALOG_DATA);
  name = this.data.currentName;

  save(): void {
    const trimmed = this.name.trim();
    if (!trimmed) {
      this.dialogRef.close();
      return;
    }
    this.dialogRef.close({ name: trimmed });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
