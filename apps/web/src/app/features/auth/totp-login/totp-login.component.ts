import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthSessionService } from '../../../core/auth/auth-session.service';

@Component({
  selector: 'app-totp-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    RouterLink,
  ],
  template: `
    <div class="auth-container">
      <mat-card class="auth-card">
        <mat-card-header>
          <mat-card-title>Two-Factor Authentication</mat-card-title>
          <mat-card-subtitle>Enter the code from your authenticator app</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          @if (errorMessage()) {
            <div class="error-message">{{ errorMessage() }}</div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Authentication Code</mat-label>
              <input
                matInput
                type="text"
                inputmode="numeric"
                autocomplete="one-time-code"
                maxlength="6"
                placeholder="000000"
                formControlName="code"
                (input)="onCodeInput()"
              />
              @if (form.controls.code.hasError('required')) {
                <mat-error>Code is required</mat-error>
              }
              @if (form.controls.code.hasError('pattern')) {
                <mat-error>Enter a valid 6-digit code</mat-error>
              }
            </mat-form-field>

            <button
              mat-raised-button
              color="primary"
              class="full-width"
              type="submit"
              [disabled]="submitting()"
            >
              {{ submitting() ? 'Verifying...' : 'Verify' }}
            </button>
          </form>

          <div class="help-link">
            <a routerLink="/login" (click)="onLostAccess()">Lost access to your authenticator app?</a>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .auth-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; background: #f5f6fa; padding: 16px; }
    .auth-card { max-width: 400px; width: 100%; padding: 24px; }
    mat-card-header { margin-bottom: 20px; }
    mat-card-subtitle { margin-top: 4px; }
    .full-width { width: 100%; }
    .error-message { background: #FEF2F2; border: 1px solid #FCA5A5; color: #991B1B; padding: 10px 14px; border-radius: 6px; margin-bottom: 16px; font-size: 14px; }
    .help-link { text-align: center; margin-top: 20px; font-size: 14px; }
    .help-link a { color: #4F46E5; text-decoration: none; }
    .help-link a:hover { text-decoration: underline; }
  `,
})
export class TotpLoginComponent implements OnInit {
  private readonly auth = inject(AuthSessionService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  ngOnInit(): void {
    if (!this.auth.requiresTotp()) {
      void this.router.navigate(['/login']);
    }
  }

  onCodeInput(): void {
    const val = this.form.controls.code.value;
    if (val.length > 6) {
      this.form.controls.code.setValue(val.slice(0, 6), { emitEvent: false });
    }
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    try {
      await this.auth.verifyTotpLogin(this.form.controls.code.value);
      this.auth.navigateAfterLogin();
    } catch (error) {
      this.errorMessage.set(this.auth.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  onLostAccess(): void {
    this.auth.clearSession();
  }
}
