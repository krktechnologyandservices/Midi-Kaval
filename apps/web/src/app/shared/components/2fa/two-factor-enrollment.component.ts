import { booleanAttribute, Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpErrorResponse } from '@angular/common/http';
import { TwoFactorService } from '../../services/two-factor.service';
import { BackupCodeService } from '../../services/backup-code.service';
import { BackupCodesDisplayComponent } from './backup-codes-display.component';
import { AuthSessionService } from '../../../core/auth/auth-session.service';

type EnrollmentStep = 'initiate' | 'qr-display' | 'verify' | 'backup-codes' | 'success';

@Component({
  selector: 'app-two-factor-enrollment',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    BackupCodesDisplayComponent,
  ],
  templateUrl: './two-factor-enrollment.component.html',
  styleUrls: ['./two-factor-enrollment.component.scss'],
})
export class TwoFactorEnrollmentComponent {
  private readonly twoFactorService = inject(TwoFactorService);
  private readonly backupCodeService = inject(BackupCodeService);
  readonly authSession = inject(AuthSessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialogRef = inject(MatDialogRef, { optional: true });

  readonly pageMode = input(false, { transform: booleanAttribute });

  readonly currentStep = signal<EnrollmentStep>('initiate');
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly provisioningUri = signal<string | null>(null);
  readonly secretBase32 = signal<string | null>(null);
  readonly qrCodeDataUrl = signal<string | null>(null);
  readonly verificationCode = signal('');
  readonly backupCodes = signal<string[]>([]);
  readonly successAutoClosed = signal(false);

  private readonly routePageMode = signal(false);

  constructor() {
    if (this.route.snapshot.data['pageMode'] === true) {
      this.routePageMode.set(true);
    }
  }

  async initiateEnrollment(): Promise<void> {
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      const result = await this.twoFactorService.enroll();
      this.provisioningUri.set(result.provisioningUri);
      this.secretBase32.set(result.secretBase32);
      await this.generateQrCode(result.provisioningUri);
      this.currentStep.set('qr-display');
    } catch (error) {
      this.errorMessage.set(
        error instanceof HttpErrorResponse
          ? error.error?.detail ?? 'Failed to initiate enrollment.'
          : 'Failed to initiate enrollment.',
      );
      this.currentStep.set('initiate');
    } finally {
      this.submitting.set(false);
    }
  }

  async verifyEnrollment(): Promise<void> {
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      await this.twoFactorService.verifyEnroll(this.verificationCode());
      const codes = await this.backupCodeService.generate();
      this.backupCodes.set(codes.codes);
      this.currentStep.set('backup-codes');
    } catch (error) {
      this.errorMessage.set(
        error instanceof HttpErrorResponse
          ? error.error?.detail ?? 'Invalid code. Please try again.'
          : 'Invalid code. Please try again.',
      );
    } finally {
      this.submitting.set(false);
    }
  }

  onCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.verificationCode.set(input.value);
    if (input.value.length === 6) {
      void this.verifyEnrollment();
    }
  }

  onBackupCodesSaved(): void {
    this.currentStep.set('success');
    if (this.isPageMode()) {
      return;
    }
    this.successAutoClosed.set(true);
    setTimeout(() => this.dialogRef?.close(true), 1500);
  }

  goToDashboard(): void {
    this.authSession.clear2faSetupRequired();
    this.authSession.navigateAfterLogin();
  }

  goToVerify(): void {
    this.currentStep.set('verify');
  }

  copySecret(): void {
    const secret = this.secretBase32();
    if (secret) {
      void navigator.clipboard.writeText(secret);
    }
  }

  private async generateQrCode(uri: string): Promise<void> {
    try {
      const { default: qrcodeGenerator } = await import(
        'qrcode-generator'
      ) as unknown as {
        default: (
          typeNumber: number,
          errorCorrectionLevel: string,
        ) => {
          addData: (data: string) => void;
          make: () => void;
          createDataURL: (cellSize: number, margin: number) => string;
        };
      };
      const qr = qrcodeGenerator(0, 'M');
      qr.addData(uri);
      qr.make();
      const dataUrl = qr.createDataURL(4, 4);
      this.qrCodeDataUrl.set(dataUrl);
    } catch {
      this.qrCodeDataUrl.set(null);
    }
  }

  isPageMode(): boolean {
    return this.pageMode() || this.routePageMode();
  }
}
