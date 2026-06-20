import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthSessionService } from '../../../core/auth/auth-session.service';

@Component({
  selector: 'app-otp',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    RouterLink,
  ],
  templateUrl: './otp.component.html',
  styleUrl: './otp.component.scss',
})
export class OtpComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthSessionService);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly expiredMessage = signal<string | null>(null);
  readonly secondsRemaining = signal(0);

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  private timerId: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    const challenge = this.auth.otpChallenge();
    if (!challenge) {
      return;
    }

    this.secondsRemaining.set(challenge.expiresInSeconds);
    this.timerId = setInterval(() => {
      const next = this.secondsRemaining() - 1;
      if (next <= 0) {
        this.secondsRemaining.set(0);
        this.expiredMessage.set('Code expired — request new code');
        this.clearTimer();
        return;
      }

      this.secondsRemaining.set(next);
    }, 1000);
  }

  ngOnDestroy(): void {
    this.clearTimer();
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    if (this.expiredMessage()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    try {
      await this.auth.verifyOtp(this.form.controls.code.value);
      this.auth.navigateAfterLogin();
    } catch (error) {
      this.errorMessage.set(this.auth.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private clearTimer(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }
}
