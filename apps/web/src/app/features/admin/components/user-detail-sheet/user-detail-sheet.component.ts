import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminUserSummary, getUserStatus } from '../../models/admin.models';
import { AdminUserService } from '../../services/admin-user.service';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-user-detail-sheet',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatSnackBarModule, MatTooltipModule,
    StatusBadgeComponent,
  ],
  styleUrls: ['./user-detail-sheet.component.scss'],
  template: `
    @if (user(); as u) {
      <div class="sheet-backdrop" (click)="onClose()"></div>
      <div class="sheet-panel" role="dialog" aria-label="User Details" tabindex="0" (keydown.escape)="onClose()">
        <div class="sheet-header">
          <h2>{{ u.firstName }} {{ u.lastName }}</h2>
          <button mat-icon-button aria-label="Close" (click)="onClose()"><mat-icon>close</mat-icon></button>
        </div>

        <div class="sheet-body">
          @if (getUserStatus(u) === 'deleted') {
            <div class="deleted-banner">
              <mat-icon>info</mat-icon>
              <span>User has been permanently deleted.</span>
            </div>
          }

          <section class="profile-section">
            <h3>Profile</h3>
            <div class="info-row"><span class="label">Email</span><span class="value">{{ u.email }}</span></div>
            <div class="info-row"><span class="label">Role</span><span class="value role-pill">{{ u.role }}</span></div>
          </section>

          <section class="status-section">
            <h3>Status</h3>
            <div class="status-badge-row">
              <app-status-badge [status]="getUserStatus(u)" />
              @if (u.isSuspended && suspendedAt()) {
                <span class="status-date">Suspended since {{ suspendedAt() }}</span>
              }
              @if (!u.isSuspended && getUserStatus(u) !== 'deleted') {
                <span class="status-date">Active</span>
              }
            </div>
          </section>

          <section class="activity-section">
            <h3>Activity</h3>
            <div class="info-row"><span class="label">Member since</span><span class="value">{{ u.createdAtUtc | date:'mediumDate' }}</span></div>
          </section>

            @if (getUserStatus(u) !== 'deleted') {
            <section class="actions-section">
              <h3>Actions</h3>
              @if (u.isSuspended) {
                <div class="action-row">
                  <span class="action-label">Reactivate user</span>
                  <button
                    mat-stroked-button
                    color="primary"
                    [disabled]="isProcessing() || isSelf(u)"
                    [attr.aria-label]="isSelf(u) ? 'You cannot reactivate your own account' : 'Reactivate this user'"
                    (click)="confirmReactivate()"
                  >
                    {{ isProcessing() ? 'Processing...' : 'Reactivate' }}
                  </button>
                  @if (isSelf(u)) {
                    <mat-icon class="info-icon" aria-label="Cannot reactivate yourself">info</mat-icon>
                  }
                </div>
              } @else {
                <div class="action-row">
                  <span class="action-label">Suspend user</span>
                  <button
                    mat-stroked-button
                    color="warn"
                    [disabled]="isProcessing() || isSelf(u) || isLastDirector()"
                    [matTooltip]="isLastDirector() ? 'At least one Director must remain active. Promote another user to Director first.' : ''"
                    [attr.aria-label]="getSuspendLabel(u)"
                    (click)="confirmSuspend()"
                  >
                    {{ isProcessing() ? 'Processing...' : 'Suspend' }}
                  </button>
                  @if (isSelf(u)) {
                    <mat-icon class="info-icon" aria-label="Cannot suspend yourself">info</mat-icon>
                  }
                </div>
              }

              @if (u.role === 'Director' && !isSelf(u)) {
                <div class="action-row">
                  <span class="action-label">Reset two-factor authentication</span>
                  <button
                    mat-stroked-button
                    color="primary"
                    [disabled]="isProcessing()"
                    [matTooltip]="'Reset this Director\\'s two-factor authentication. They will need to re-enroll.'"
                    (click)="confirmResetTwoFactor()"
                  >
                    {{ isProcessing() ? 'Processing...' : 'Reset 2FA' }}
                  </button>
                </div>
              }
            </section>
          }

          @if (getUserStatus(u) !== 'deleted' && !isSelf(u) && !isLastDirector()) {
            <section class="danger-zone">
              <h3>Danger Zone</h3>
              <div class="action-row">
                <span class="action-label">Permanently delete this user's account and anonymise their data</span>
                <button
                  mat-raised-button
                  color="warn"
                  [disabled]="isProcessing()"
                  (click)="confirmDelete()"
                >
                  {{ isProcessing() ? 'Processing...' : 'Permanently Delete' }}
                </button>
              </div>
            </section>
          }
        </div>
      </div>
    }
  `,
  styles: `
    :host { display: contents; }
    .sheet-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.3); z-index: 999; }
    .sheet-panel {
      position: fixed; top: 0; right: 0; bottom: 0; width: 400px; max-width: 90vw;
      background: #fff; z-index: 1000; display: flex; flex-direction: column;
      box-shadow: -4px 0 24px rgba(0,0,0,0.12); overflow-y: auto;
    }
    .sheet-header {
      display: flex; justify-content: space-between; align-items: center;
      padding: 20px 24px; border-bottom: 1px solid #E2E5EB;
    }
    .sheet-header h2 { margin: 0; font-size: 20px; font-weight: 500; }
    .sheet-body { padding: 24px; flex: 1; }
    section { margin-bottom: 24px; }
    section h3 { font-size: 14px; font-weight: 600; color: #697586; text-transform: uppercase; letter-spacing: 0.05em; margin: 0 0 12px; }
    .info-row { display: flex; justify-content: space-between; align-items: center; padding: 8px 0; }
    .info-row .label { color: #697586; font-size: 14px; }
    .info-row .value { font-size: 14px; font-weight: 500; }
    .role-pill { display: inline-block; padding: 2px 10px; border-radius: 4px; font-size: 12px; background: #E8EDF5; color: #1B2A4A; }
    .status-badge-row { display: flex; align-items: center; gap: 12px; }
    .status-date { font-size: 13px; color: #697586; }
    .action-row { display: flex; justify-content: space-between; align-items: center; padding: 12px 0; }
    .action-label { font-size: 14px; }
    .info-icon { color: #697586; font-size: 18px; width: 18px; height: 18px; }
    .deleted-banner {
      display: flex; align-items: center; gap: 8px; padding: 10px 14px;
      background: #FFFBEB; border: 1px solid #FDE68A; border-radius: 6px;
      color: #92400E; font-size: 14px; margin-bottom: 16px;
    }
    .deleted-banner mat-icon { font-size: 20px; width: 20px; height: 20px; color: #D97706; }
    .danger-zone {
      border: 1px solid #FCA5A5; border-radius: 6px; padding: 16px; background: #FFF5F5;
    }
    .danger-zone h3 { color: #991B1B; }
  `,
})
export class UserDetailSheetComponent {
  private readonly adminUserService = inject(AdminUserService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly user = signal<AdminUserSummary | null>(null);
  readonly suspendedAt = signal<string | null>(null);
  readonly isLastDirector = signal(false);
  readonly isProcessing = signal(false);
  readonly closed = output<void>();
  readonly suspended = output<string>();
  readonly reactivated = output<string>();
  readonly deleted = output<string>();

  private currentUserId: string | null = null;

  protected readonly getUserStatus = getUserStatus;

  setData(data: {
    user: AdminUserSummary;
    currentUserId: string | null;
    isLastDirector: boolean;
    suspendedAt?: string | null;
  }): void {
    this.user.set(data.user);
    this.currentUserId = data.currentUserId;
    this.isLastDirector.set(data.isLastDirector);
    this.suspendedAt.set(data.suspendedAt ?? data.user.suspendedAtUtc
      ? new Date(data.user.suspendedAtUtc!).toLocaleDateString()
      : null);
  }

  onClose(): void {
    this.closed.emit();
  }

  isSelf(u: AdminUserSummary): boolean {
    return this.currentUserId === u.id;
  }

  getSuspendLabel(u: AdminUserSummary): string {
    if (this.isSelf(u)) return 'You cannot suspend your own account';
    if (this.isLastDirector()) return 'At least one Director must remain active';
    return 'Suspend this user';
  }

  async confirmSuspend(): Promise<void> {
    const u = this.user();
    if (!u) return;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Suspend this user?',
        message: 'They will lose access immediately. This action will be logged. You can reactivate at any time.',
        confirmText: 'Suspend',
        confirmColor: 'warn' as const,
      },
    });

    const sub = dialogRef.afterClosed().subscribe(async (confirmed: boolean) => {
      if (!confirmed) return;
      this.isProcessing.set(true);
      try {
        const result = await this.adminUserService.suspendUser(u.id);
        this.suspendedAt.set(new Date(result.actionedAtUtc).toLocaleDateString());
        this.user.update(val => val ? { ...val, isSuspended: true } : null);
        this.snackBar.open(`${u.firstName} ${u.lastName} has been suspended.`, 'Dismiss', { duration: 4000 });
        this.suspended.emit(u.id);
      } catch {
        this.snackBar.open('Failed to suspend user. Please try again.', 'Dismiss', { duration: 4000 });
      } finally {
        this.isProcessing.set(false);
      }
    });

    this.destroyRef.onDestroy(() => sub.unsubscribe());
  }

  async confirmReactivate(): Promise<void> {
    const u = this.user();
    if (!u) return;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Reactivate this user?',
        message: 'They will regain access immediately.',
        confirmText: 'Reactivate',
        confirmColor: 'primary' as const,
      },
    });

    const sub = dialogRef.afterClosed().subscribe(async (confirmed: boolean) => {
      if (!confirmed) return;
      this.isProcessing.set(true);
      try {
        const result = await this.adminUserService.reactivateUser(u.id);
        this.suspendedAt.set(null);
        this.user.update(val => val ? { ...val, isSuspended: false } : null);
        this.snackBar.open(`${u.firstName} ${u.lastName} has been reactivated.`, 'Dismiss', { duration: 4000 });
        this.reactivated.emit(u.id);
      } catch {
        this.snackBar.open('Failed to reactivate user. Please try again.', 'Dismiss', { duration: 4000 });
      } finally {
        this.isProcessing.set(false);
      }
    });

    this.destroyRef.onDestroy(() => sub.unsubscribe());
  }

  async confirmDelete(): Promise<void> {
    const u = this.user();
    if (!u) return;

    const dialogRef = this.dialog.open(ConfirmDeleteDialogComponent, {
      data: { userEmail: u.email },
      disableClose: true,
    });

    const sub = dialogRef.afterClosed().subscribe(async (confirmed: boolean) => {
      if (!confirmed) return;
      this.isProcessing.set(true);
      try {
        await this.adminUserService.deleteUser(u.id, u.email);
        this.snackBar.open(`${u.firstName} ${u.lastName} has been permanently deleted.`, 'Dismiss', { duration: 4000 });
        this.onClose();
        this.deleted.emit(u.id);
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : 'Failed to delete user. Please try again.';
        this.snackBar.open(msg, 'Dismiss', { duration: 4000 });
      } finally {
        this.isProcessing.set(false);
      }
    });

    this.destroyRef.onDestroy(() => sub.unsubscribe());
  }

  async confirmResetTwoFactor(): Promise<void> {
    const u = this.user();
    if (!u) return;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Reset Two-Factor Authentication?',
        message: `${u.firstName} ${u.lastName} will need to re-enroll two-factor authentication before performing management actions. This action will be logged.`,
        confirmText: 'Reset 2FA',
        confirmColor: 'primary' as const,
      },
    });

    const sub = dialogRef.afterClosed().subscribe(async (confirmed: boolean) => {
      if (!confirmed) return;
      this.isProcessing.set(true);
      try {
        await this.adminUserService.resetTwoFactor(u.id);
        this.user.update(val => val ? { ...val, totpEnrolledAt: null } : null);
        this.snackBar.open('Two-factor authentication has been reset for this user.', 'Dismiss', { duration: 4000 });
      } catch {
        this.snackBar.open('Failed to reset two-factor authentication. Please try again.', 'Dismiss', { duration: 4000 });
      } finally {
        this.isProcessing.set(false);
      }
    });

    this.destroyRef.onDestroy(() => sub.unsubscribe());
  }
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button [color]="data.confirmColor" [mat-dialog-close]="true">
        {{ data.confirmText }}
      </button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDialogComponent {
  readonly data: { title: string; message: string; confirmText: string; confirmColor: 'primary' | 'warn' };

  constructor() {
    this.data = inject(MAT_DIALOG_DATA);
  }
}

@Component({
  selector: 'app-confirm-delete-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Permanently Delete User?</h2>
    <mat-dialog-content>
      <p>This will permanently remove this user's access and anonymise their data. This action is irreversible.</p>
      <p><strong>Type the user's email to confirm:</strong></p>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>User email</mat-label>
        <input
          matInput
          [ngModel]="typedEmail()"
          (ngModelChange)="typedEmail.set($event)"
          (input)="onEmailInput()"
          placeholder="user@example.com"
          [required]="true"
        />
        @if (typedEmail() && !isEmailMatch()) {
          <mat-error>Email does not match</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-raised-button
        color="warn"
        [disabled]="!isEmailMatch()"
        [mat-dialog-close]="true"
      >
        Confirm Delete
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    mat-dialog-content p { margin-bottom: 12px; }
  `,
})
export class ConfirmDeleteDialogComponent {
  readonly data: { userEmail: string } = inject(MAT_DIALOG_DATA);
  readonly typedEmail = signal('');

  isEmailMatch(): boolean {
    return this.typedEmail().toLowerCase() === this.data.userEmail.toLowerCase();
  }

  onEmailInput(): void {
    // Signal is updated via ngModel binding; isEmailMatch re-evaluates
  }
}
