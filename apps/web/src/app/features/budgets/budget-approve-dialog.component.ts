import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ApproveDialogData {
  mode: 'approve' | 'return';
  budgetSource: string;
}

export interface ApproveDialogResult {
  comment: string;
}

@Component({
  selector: 'app-budget-approve-dialog',
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, FormsModule],
  template: `
    <h2 mat-dialog-title>{{ data.mode === 'approve' ? 'Approve' : 'Return' }} Budget</h2>
    <mat-dialog-content>
      <p>
        {{ data.mode === 'approve'
            ? 'Approve budget "' + data.budgetSource + '"? This will transition it to Approved status.'
            : 'Return budget "' + data.budgetSource + '"? This will transition it to Returned status.'
        }}
      </p>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Comment {{ data.mode === 'approve' ? '(optional)' : '(required)' }}</mat-label>
        <textarea
          matInput
          [(ngModel)]="comment"
          rows="3"
          [required]="data.mode === 'return'"
          placeholder="Reason for {{ data.mode === 'approve' ? 'approval' : 'return' }}"
        ></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button
        mat-flat-button
        [color]="data.mode === 'approve' ? 'primary' : 'warn'"
        [disabled]="data.mode === 'return' && !comment.trim()"
        (click)="confirm()"
      >
        {{ data.mode === 'approve' ? 'Approve' : 'Return' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    mat-dialog-content { min-width: 360px; }
    mat-dialog-content p { margin-top: 0; color: var(--mat-sys-on-surface-variant, #475467); }
  `,
})
export class BudgetApproveDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<BudgetApproveDialogComponent, ApproveDialogResult | false>);
  readonly data = inject<ApproveDialogData>(MAT_DIALOG_DATA);

  comment = '';

  confirm(): void {
    if (this.data.mode === 'return' && !this.comment.trim()) return;
    this.dialogRef.close({ comment: this.comment.trim() });
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
