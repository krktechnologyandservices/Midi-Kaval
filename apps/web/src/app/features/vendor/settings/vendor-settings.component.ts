import { Component, Inject, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { TwoFactorEnrollmentComponent } from '../../../shared/components/2fa/two-factor-enrollment.component';
import { TwoFactorService } from '../../../shared/services/two-factor.service';
import { VendorApiService } from '../vendor-api.service';
import { ChangePasswordDialogComponent } from './change-password-dialog.component';

@Component({
  selector: 'app-vendor-settings',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    TwoFactorEnrollmentComponent,
  ],
  templateUrl: './vendor-settings.component.html',
  styleUrl: './vendor-settings.component.scss',
})
export class VendorSettingsComponent {
  private readonly twoFactorService = inject(TwoFactorService);
  private readonly vendorApi = inject(VendorApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly enrolled = signal(false);
  readonly enrolledAt = signal<string | null>(null);
  readonly backupCodesRemaining = signal(8);
  readonly loading = signal(true);
  readonly lowCodesWarningDismissed = signal(
    sessionStorage.getItem('midi_kaval_backup_warning_dismissed') === 'true',
  );
  readonly infoBannerDismissed = signal(false);
  readonly isEnrolling = signal(false);

  async ngOnInit(): Promise<void> {
    await this.loadStatus();
  }

  private async loadStatus(): Promise<void> {
    this.loading.set(true);
    try {
      const status = await this.twoFactorService.status();
      this.enrolled.set(status.enrolled);
      this.enrolledAt.set(status.enrolledAt ?? null);

      if (status.enrolled) {
        const { remaining } = await this.vendorApi.getBackupCodeRemainingCount();
        this.backupCodesRemaining.set(remaining);
      }
    } catch {
      // Silently fail — component shows fallback state
    } finally {
      this.loading.set(false);
    }
  }

  dismissLowCodesWarning(): void {
    this.lowCodesWarningDismissed.set(true);
    sessionStorage.setItem('midi_kaval_backup_warning_dismissed', 'true');
  }

  dismissInfoBanner(): void {
    this.infoBannerDismissed.set(true);
  }

  openReEnrollDialog(): void {
    this.dialog.open(TwoFactorEnrollmentComponent, {
      width: '520px',
      maxWidth: '95vw',
      disableClose: true,
    });
  }

  async regenerateBackupCodes(): Promise<void> {
    try {
      const { codes } = await this.vendorApi.regenerateBackupCodes();
      const { remaining } = await this.vendorApi.getBackupCodeRemainingCount();
      this.backupCodesRemaining.set(remaining);

      this.dialog.open(BackupCodesRegeneratedDialogComponent, {
        width: '480px',
        data: { codes },
        disableClose: false,
      });
    } catch {
      this.snackBar.open('Failed to regenerate backup codes. Please try again.', 'Close', {
        duration: 5000,
      });
    }
  }

  openChangePasswordDialog(): void {
    const dialogRef = this.dialog.open(ChangePasswordDialogComponent, {
      width: '400px',
      maxWidth: '95vw',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.snackBar.open('Password updated successfully.', 'Close', { duration: 3000 });
      }
    });
  }
}

@Component({
  selector: 'app-backup-codes-regenerated',
  template: `
    <h2 mat-dialog-title>New Backup Codes</h2>
    <mat-dialog-content>
      <p>These codes are displayed once and cannot be recovered. Save them securely.</p>
      <div class="codes-grid">
        @for (code of data.codes; track $index) {
          <code class="backup-code">{{ code }}</code>
        }
      </div>
      <button mat-stroked-button (click)="download()">
        <mat-icon>download</mat-icon> Download as .txt
      </button>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .codes-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 8px;
        margin: 16px 0;
      }
      .backup-code {
        font-family: 'Consolas', 'Courier New', monospace;
        font-size: 14px;
        padding: 8px;
        background: rgba(0, 0, 0, 0.04);
        border-radius: 4px;
        user-select: all;
      }
    `,
  ],
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
})
export class BackupCodesRegeneratedDialogComponent {
  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: { codes: string[] },
    private readonly snackBar: MatSnackBar,
  ) {}

  download(): void {
    const header = `Kaval Online — Backup Codes\nGenerated: ${new Date().toISOString()}\n\n`;
    const blob = new Blob([header + this.data.codes.join('\n')], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'kaval-backup-codes.txt';
    a.click();
    URL.revokeObjectURL(url);
  }
}
