import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { InvitationService } from '../../services/invitation.service';
import { SendInvitationRequest, SendInvitationResponse } from '../../models/admin.models';

interface RoleOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-invite-dialog',
  imports: [
    MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatCardModule,
    MatProgressSpinnerModule, FormsModule,
  ],
  template: `
    <h2 mat-dialog-title>Invite User</h2>

    @if (!showConfirmation()) {
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email</mat-label>
          <input
            matInput
            type="email"
            [(ngModel)]="email"
            [disabled]="submitting()"
            required
            email
          />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Role</mat-label>
          <mat-select [(ngModel)]="role" [disabled]="submitting()" required>
            @for (r of roleOptions; track r.value) {
              <mat-option [value]="r.value">{{ r.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (errorMessage()) {
          <div class="error-message" role="alert">{{ errorMessage() }}</div>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button (click)="cancel()" [disabled]="submitting()">Cancel</button>
        <button
          mat-flat-button
          color="primary"
          [disabled]="!canSend() || submitting()"
          (click)="showConfirmation.set(true)"
        >
          Send Invitation
        </button>
      </mat-dialog-actions>
    }

    @if (showConfirmation()) {
      <mat-dialog-content>
        <mat-card appearance="outlined" class="confirmation-card">
          <mat-card-content>
            <p>
              Inviting <strong>{{ email }}</strong> as <strong>{{ role }}</strong>.
              They will receive an email with a 24-hour invitation link. This will be logged.
            </p>
          </mat-card-content>
        </mat-card>

        @if (errorMessage()) {
          <div class="error-message" role="alert">{{ errorMessage() }}</div>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button (click)="showConfirmation.set(false)" [disabled]="submitting()">
          Edit
        </button>
        <button
          mat-flat-button
          color="primary"
          [disabled]="submitting()"
          (click)="confirmAndSend()"
        >
          @if (submitting()) {
            <mat-spinner diameter="20" class="btn-spinner"></mat-spinner>
            Sending...
          } @else {
            Confirm & Send
          }
        </button>
      </mat-dialog-actions>
    }
  `,
  styles: `
    .full-width { width: 100%; }
    .confirmation-card { background: #F5F6FA; margin-bottom: 8px; }
    .error-message { color: #991B1B; background: #FEF2F2; padding: 8px 12px; border-radius: 4px; margin-top: 8px; font-size: 14px; }
    .btn-spinner { display: inline-block; margin-right: 6px; vertical-align: middle; }
    mat-dialog-content { min-width: 420px; padding-top: 8px; }
  `,
})
export class InviteDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<InviteDialogComponent, SendInvitationResponse>);
  private readonly invitationService = inject(InvitationService);

  readonly roleOptions: RoleOption[] = [
    { value: 'Coordinator', label: 'Coordinator' },
    { value: 'SocialWorker', label: 'Social Worker' },
    { value: 'CaseWorker', label: 'Case Worker' },
    { value: 'Accountant', label: 'Accountant' },
  ];

  readonly showConfirmation = signal(false);
  readonly submitting = signal(false);
  readonly errorMessage = signal('');

  email = '';
  role = '';

  canSend(): boolean {
    return this.email.trim().length > 0
      && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.email)
      && this.role.length > 0;
  }

  async confirmAndSend(): Promise<void> {
    if (!this.canSend() || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set('');

    try {
      const request: SendInvitationRequest = {
        email: this.email.trim(),
        role: this.role,
      };

      const response = await this.invitationService.sendInvitation(request);
      this.dialogRef.close(response);
    } catch (err: unknown) {
      const body = (err as { error?: { detail?: string } })?.error;
      this.errorMessage.set(
        body?.detail ?? 'Failed to send invitation. Please try again.',
      );
    } finally {
      this.submitting.set(false);
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
