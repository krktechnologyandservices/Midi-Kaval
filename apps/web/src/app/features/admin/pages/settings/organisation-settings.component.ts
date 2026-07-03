import { Component, WritableSignal, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminUserService } from '../../services/admin-user.service';

@Component({
  selector: 'app-organisation-settings',
  imports: [
    MatCardModule,
    MatSlideToggleModule,
    MatIconModule,
    MatTooltipModule,
    MatDividerModule,
    MatSnackBarModule,
  ],
  template: `
    <div class="settings-page">
      <header class="page-header">
        <h1>Organisation Settings</h1>
        <p class="subtitle">Manage two-factor authentication policies for your organisation.</p>
      </header>

      <mat-card class="settings-card">
        <mat-card-content>
          <div class="toggle-row">
            <div class="toggle-content">
              <div class="toggle-label-wrapper">
                <span class="toggle-label">Require two-factor authentication for all staff</span>
                <mat-icon
                  class="info-icon"
                  matTooltip="When enabled, users without two-factor authentication will be prompted to set it up on their next login."
                  aria-label="More information"
                >info_outline</mat-icon>
              </div>
              <p class="toggle-desc">When enabled, users without two-factor authentication will be prompted to set it up on their next login. They won't be able to access protected features until enrollment is complete.</p>
            </div>
            <mat-slide-toggle
              [checked]="require2fa()"
              [disabled]="loadingRequire2fa()"
              (change)="onRequire2faChange($event.checked)"
            />
          </div>

          <mat-divider class="toggle-divider" />

          <div class="toggle-row">
            <div class="toggle-content">
              <div class="toggle-label-wrapper">
                <span class="toggle-label">Allow Coordinators to reset 2FA for field workers</span>
                <mat-icon
                  class="info-icon"
                  matTooltip="Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."
                  aria-label="More information"
                >info_outline</mat-icon>
              </div>
              <p class="toggle-desc">Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes.</p>
            </div>
            <mat-slide-toggle
              [checked]="delegation()"
              [disabled]="loadingDelegation()"
              (change)="onDelegationChange($event.checked)"
            />
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .settings-page { max-width: 720px; }
    .page-header { margin-bottom: 24px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 500; }
    .subtitle { margin: 4px 0 0; font-size: 14px; color: #6B7280; }
    .settings-card { border-radius: 8px; }
    .toggle-row { display: flex; align-items: flex-start; gap: 16px; padding: 8px 0; }
    .toggle-content { flex: 1; }
    .toggle-label-wrapper { display: flex; align-items: center; gap: 6px; margin-bottom: 4px; }
    .toggle-label { font-size: 14px; font-weight: 500; color: #212121; }
    .toggle-desc { margin: 0; font-size: 13px; color: #6B7280; line-height: 1.4; }
    .info-icon { font-size: 18px; width: 18px; height: 18px; color: #9CA3AF; cursor: help; }
    .toggle-divider { margin: 12px 0; }
  `,
})
export class OrganisationSettingsComponent {
  private readonly adminUserService = inject(AdminUserService);
  private readonly snackBar = inject(MatSnackBar);

  readonly require2fa: WritableSignal<boolean> = signal(false);
  readonly loadingRequire2fa: WritableSignal<boolean> = signal(false);
  readonly delegation: WritableSignal<boolean> = signal(false);
  readonly loadingDelegation: WritableSignal<boolean> = signal(false);

  constructor() {
    this.loadSettings();
  }

  private async loadSettings(): Promise<void> {
    this.loadingRequire2fa.set(true);
    this.loadingDelegation.set(true);
    try {
      const [requireResult, delegationResult] = await Promise.all([
        this.adminUserService.getRequire2faStatus(),
        this.adminUserService.getDelegationStatus(),
      ]);
      this.require2fa.set(requireResult.require2fa);
      this.delegation.set(delegationResult.enabled);
    } catch {
      this.snackBar.open('Failed to load organisation settings.', 'Close', { duration: 4000 });
    } finally {
      this.loadingRequire2fa.set(false);
      this.loadingDelegation.set(false);
    }
  }

  async onRequire2faChange(newValue: boolean): Promise<void> {
    const previousValue = this.require2fa();
    this.require2fa.set(newValue);
    this.loadingRequire2fa.set(true);
    try {
      await this.adminUserService.setRequire2fa(newValue);
      this.snackBar.open('Two-factor authentication requirement updated.', 'Close', { duration: 4000 });
    } catch {
      this.require2fa.set(previousValue);
      this.snackBar.open('Failed to update setting. Please try again.', 'Close', { duration: 4000 });
    } finally {
      this.loadingRequire2fa.set(false);
    }
  }

  async onDelegationChange(newValue: boolean): Promise<void> {
    const previousValue = this.delegation();
    this.delegation.set(newValue);
    this.loadingDelegation.set(true);
    try {
      await this.adminUserService.setDelegation(newValue);
      this.snackBar.open('Delegation setting updated.', 'Close', { duration: 4000 });
    } catch {
      this.delegation.set(previousValue);
      this.snackBar.open('Failed to update delegation setting. Please try again.', 'Close', { duration: 4000 });
    } finally {
      this.loadingDelegation.set(false);
    }
  }
}
