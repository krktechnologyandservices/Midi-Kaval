import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  detail?: string;
  type?: string;
  extensions?: Record<string, unknown>;
  [key: string]: unknown;
}

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    RouterLink,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthSessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void {
    const resetSuccess = this.route.snapshot.queryParamMap.get('resetSuccess');
    if (resetSuccess) {
      this.successMessage.set(resetSuccess);
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
      await this.auth.login(this.form.getRawValue());
      // Navigation is handled by AuthSessionService.login()
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        const problem = error.error as ProblemDetails | null;
        if (problem?.extensions?.['code'] === 'ACCOUNT_NOT_CONFIRMED') {
          this.errorMessage.set('Please check your email to confirm your account before logging in.');
          return;
        }
      }
      this.errorMessage.set(this.auth.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
