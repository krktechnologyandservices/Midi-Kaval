import { Component, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthSessionService } from '../../../core/auth/auth-session.service';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value;
  const confirm = control.get('confirmPassword')?.value;
  if (!password || !confirm) {
    return null;
  }

  return password === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-reset-password',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    RouterLink,
  ],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss',
})
export class ResetPasswordComponent implements OnInit {
  private readonly auth = inject(AuthSessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  private resetToken = '';

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    this.resetToken = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.resetToken) {
      this.errorMessage.set('Invalid or expired reset token.');
    }
  }

  hasToken(): boolean {
    return !!this.resetToken;
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);

    if (!this.resetToken) {
      this.errorMessage.set('Invalid or expired reset token.');
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    try {
      const result = await this.auth.resetPassword(
        this.resetToken,
        this.form.controls.newPassword.value,
      );
      await this.router.navigate(['/login'], {
        queryParams: { resetSuccess: result.message },
      });
    } catch (error) {
      this.errorMessage.set(this.auth.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  passwordMismatch(): boolean {
    return this.form.hasError('passwordMismatch')
      && this.form.controls.confirmPassword.touched;
  }
}
