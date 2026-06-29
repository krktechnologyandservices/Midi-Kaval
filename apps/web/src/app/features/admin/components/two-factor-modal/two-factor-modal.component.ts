import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../environments/environment';

interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

type ModalStep = 'initiate' | 'qr-display' | 'verify' | 'success' | 'error';

@Component({
  selector: 'app-two-factor-modal',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>Set Up Two-Factor Authentication</h2>
    <mat-dialog-content>
      @switch (currentStep()) {
        @case ('initiate') {
          <p>Two-factor authentication adds an extra layer of security to your account. You need to set it up before performing management actions.</p>
          <p>You'll need an authenticator app (e.g., Google Authenticator, Microsoft Authenticator, Authy).</p>
          <button mat-raised-button color="primary" (click)="initiateEnrollment()" [disabled]="submitting()">
            {{ submitting() ? 'Setting up...' : 'Set Up Two-Factor Authentication' }}
          </button>
        }
        @case ('qr-display') {
          <p>Scan the QR code below with your authenticator app, or enter the secret manually.</p>
          <div class="qr-container">
            @if (qrCodeDataUrl()) {
              <img [src]="qrCodeDataUrl()" alt="TOTP QR Code" class="qr-code" />
            }
            <div class="secret-fallback">
              <p>Can't scan the QR code? Enter this secret manually:</p>
              <code>{{ secretBase32() }}</code>
            </div>
          </div>
          <button mat-stroked-button color="primary" (click)="currentStep.set('verify')">
            I've scanned the code — Continue
          </button>
        }
        @case ('verify') {
          <p>Enter the 6-digit code from your authenticator app to verify setup.</p>
          @if (errorMessage()) {
            <div class="error-message">{{ errorMessage() }}</div>
          }
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Authentication Code</mat-label>
            <input
              matInput
              type="text"
              inputmode="numeric"
              maxlength="6"
              placeholder="000000"
              [(ngModel)]="verificationCode"
            />
          </mat-form-field>
          <button mat-raised-button color="primary" (click)="verifyEnrollment()" [disabled]="submitting() || verificationCode().length !== 6">
            {{ submitting() ? 'Verifying...' : 'Verify & Activate' }}
          </button>
        }
        @case ('success') {
          <div class="success-state">
            <mat-icon class="success-icon">check_circle</mat-icon>
            <p>Two-factor authentication is now active on your account.</p>
          </div>
        }
        @case ('error') {
          <div class="error-state">
            <mat-icon class="error-icon">error</mat-icon>
            <p>{{ errorMessage() }}</p>
            <button mat-stroked-button color="primary" (click)="currentStep.set('initiate'); errorMessage.set(null)">
              Try Again
            </button>
          </div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="submitting()">Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    .qr-container { display: flex; flex-direction: column; align-items: center; gap: 16px; margin: 16px 0; }
    .qr-code { width: 200px; height: 200px; border: 1px solid #E2E5EB; border-radius: 8px; }
    .secret-fallback { text-align: center; font-size: 14px; }
    .secret-fallback code { display: block; margin-top: 8px; padding: 8px 12px; background: #F5F6FA; border-radius: 4px; font-family: monospace; font-size: 13px; word-break: break-all; }
    .error-message { background: #FEF2F2; border: 1px solid #FCA5A5; color: #991B1B; padding: 10px 14px; border-radius: 6px; margin-bottom: 16px; font-size: 14px; }
    .success-state, .error-state { text-align: center; padding: 16px 0; }
    .success-icon { font-size: 48px; width: 48px; height: 48px; color: #16A34A; }
    .error-icon { font-size: 48px; width: 48px; height: 48px; color: #DC2626; }
    p { margin: 12px 0; font-size: 14px; line-height: 1.5; }
    button { margin-top: 8px; }
  `,
})
export class TwoFactorModalComponent {
  private readonly http = inject(HttpClient);
  private readonly dialogRef = inject(MatDialogRef<TwoFactorModalComponent>);

  readonly currentStep = signal<ModalStep>('initiate');
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly provisioningUri = signal<string | null>(null);
  readonly secretBase32 = signal<string | null>(null);
  readonly qrCodeDataUrl = signal<string | null>(null);
  readonly verificationCode = signal('');

  async initiateEnrollment(): Promise<void> {
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      const result = await firstValueFrom(
        this.http.post<{ provisioningUri: string; secretBase32: string }>(
          `${environment.apiBaseUrl}/api/v1/auth/enroll-2fa`,
          {},
        ),
      );

      this.provisioningUri.set(result.provisioningUri);
      this.secretBase32.set(result.secretBase32);
      this.generateQrCode(result.provisioningUri);
      this.currentStep.set('qr-display');
    } catch (error) {
      this.errorMessage.set(
        error instanceof HttpErrorResponse ? error.error?.detail ?? 'Failed to initiate enrollment.' : 'Failed to initiate enrollment.',
      );
      this.currentStep.set('error');
    } finally {
      this.submitting.set(false);
    }
  }

  async verifyEnrollment(): Promise<void> {
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      await firstValueFrom(
        this.http.post<{ success: boolean }>(
          `${environment.apiBaseUrl}/api/v1/auth/verify-enroll-2fa`,
          { code: this.verificationCode() },
        ),
      );

      this.currentStep.set('success');
      setTimeout(() => this.dialogRef.close(true), 1500);
    } catch (error) {
      this.errorMessage.set(
        error instanceof HttpErrorResponse ? error.error?.detail ?? 'Invalid code. Please try again.' : 'Invalid code. Please try again.',
      );
    } finally {
      this.submitting.set(false);
    }
  }

  private async generateQrCode(uri: string): Promise<void> {
    try {
      const { default: qrcodeGenerator } = await import(
        'qrcode-generator'
      ) as { default: (typeNumber: number, errorCorrectionLevel: string) => { addData: (data: string) => void; make: () => void; createDataURL: (cellSize: number, margin: number) => string } };
      const qr = qrcodeGenerator(0, 'M');
      qr.addData(uri);
      qr.make();
      const dataUrl = qr.createDataURL(4, 4);
      this.qrCodeDataUrl.set(dataUrl);
    } catch {
      // QR generation failed silently — fall back to showing the provisioning URI text
      this.qrCodeDataUrl.set(null);
    }
  }
}
