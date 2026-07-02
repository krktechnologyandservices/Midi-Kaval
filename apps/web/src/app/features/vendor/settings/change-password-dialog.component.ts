import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { VendorApiService } from '../vendor-api.service';

@Component({
  selector: 'app-change-password-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Update Password</h2>

    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="error-banner">{{ errorMessage() }}</div>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Current Password</mat-label>
          <input matInput type="password" formControlName="currentPassword" />
          @if (form.controls.currentPassword.invalid && form.controls.currentPassword.touched) {
            <mat-error>Current password is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>New Password</mat-label>
          <input matInput type="password" formControlName="newPassword" />
          @if (form.controls.newPassword.invalid && form.controls.newPassword.touched) {
            <mat-error>Password must be at least 8 characters.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Confirm New Password</mat-label>
          <input matInput type="password" formControlName="confirmNewPassword" />
          @if (form.controls.confirmNewPassword.invalid && form.controls.confirmNewPassword.touched) {
            @if (form.controls.confirmNewPassword.errors?.['required']) {
              <mat-error>Please confirm your new password.</mat-error>
            }
            @if (form.controls.confirmNewPassword.errors?.['passwordMismatch']) {
              <mat-error>Passwords do not match.</mat-error>
            }
          }
        </mat-form-field>
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" [disabled]="submitting()" mat-dialog-close>Cancel</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Saving...' : 'Save' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [
    `
      .full-width {
        width: 100%;
        margin-bottom: 8px;
      }
      .error-banner {
        padding: 8px 12px;
        background: #ffebee;
        color: #c62828;
        border-radius: 4px;
        margin-bottom: 12px;
        font-size: 0.875rem;
      }
      @media (prefers-color-scheme: dark) {
        .error-banner {
          background: #4a1c1c;
          color: #ef9a9a;
        }
      }
    `,
  ],
})
export class ChangePasswordDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly vendorApi = inject(VendorApiService);
  private readonly dialogRef = inject(MatDialogRef<ChangePasswordDialogComponent>);
  private readonly snackBar = inject(MatSnackBar);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmNewPassword: ['', Validators.required],
    },
    { validators: this.passwordMatchValidator },
  );

  private passwordMatchValidator(group: { get: (key: string) => { value: string } | null }): Record<string, boolean> | null {
    const newPwd = group.get('newPassword')?.value;
    const confirm = group.get('confirmNewPassword')?.value;
    return newPwd === confirm ? null : { passwordMismatch: true };
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    try {
      await this.vendorApi.changePassword(
        this.form.value.currentPassword!,
        this.form.value.newPassword!,
        this.form.value.confirmNewPassword!,
      );
      this.dialogRef.close(true);
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        this.errorMessage.set(
          error.error?.detail ?? 'Failed to update password. Please try again.',
        );
      } else {
        this.errorMessage.set('Failed to update password. Please try again.');
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
