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
import QRCode from 'qrcode';

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
      const response = await this.twoFactorService.enroll();
      console.log('Enroll API response:', response);
      const result = (response as any)?.data ?? response;

      if (!result?.provisioningUri || !result?.secretBase32) {
        console.error(
          'Enrollment response is missing provisioningUri/secretBase32:',
          response,
        );
        this.errorMessage.set(
          'Failed to initiate enrollment: the server response was incomplete. ' +
            'Check the browser console for details.',
        );
        this.currentStep.set('initiate');
        return;
      }

      this.provisioningUri.set(result.provisioningUri);
      this.secretBase32.set(result.secretBase32);
      // Generate QR code client-side from the provisioning URI
      try {
        const url = await QRCode.toDataURL(result.provisioningUri, {
          width: 250,
          margin: 2,
          color: { dark: '#000000', light: '#ffffff' },
        });
        this.qrCodeDataUrl.set(url);
        console.log('QR code generated successfully');
      } catch (qrErr) {
        console.warn('QR generation failed, showing manual entry only', qrErr);
      }
      this.currentStep.set('qr-display');
    } catch (error) {
      console.error('Enroll API error:', error);
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

  isPageMode(): boolean {
    return this.pageMode() || this.routePageMode();
  }
}