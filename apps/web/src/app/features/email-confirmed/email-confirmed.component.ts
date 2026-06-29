import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface ProblemDetails {
  detail?: string;
  type?: string;
  extensions?: Record<string, unknown>;
  [key: string]: unknown;
}

interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

interface ConfirmEmailResponse {
  message: string;
}

@Component({
  selector: 'app-email-confirmed',
  imports: [],
  templateUrl: './email-confirmed.component.html',
  styleUrl: './email-confirmed.component.scss',
})
export class EmailConfirmedComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  readonly step = signal<'loading' | 'success' | 'expired' | 'already-confirmed' | 'error'>('loading');
  readonly message = signal('');
  readonly errorMessage = signal('');

  constructor() {
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';
    const sig = this.route.snapshot.queryParamMap.get('sig') ?? '';

    if (!token || !sig) {
      this.errorMessage.set('Invalid confirmation link.');
      this.step.set('error');
      return;
    }

    this.confirmEmail(token, sig);
  }

  private async confirmEmail(token: string, sig: string): Promise<void> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<ConfirmEmailResponse>>(
          `${environment.apiBaseUrl}/api/v1/auth/confirm-email`,
          { token, signature: sig },
        ),
      );
      this.message.set(envelope.data.message);
      this.step.set('success');
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const problem = error.error as ProblemDetails | null;
        const errorCode = problem?.extensions?.['code'] as string | undefined;

        if (errorCode === 'CONFIRMATION_EXPIRED') {
          this.step.set('expired');
          this.errorMessage.set(problem?.detail ?? 'This confirmation link has expired. Contact your Director to request a new invitation.');
        } else if (errorCode === 'CONFIRMATION_ALREADY_USED') {
          this.step.set('already-confirmed');
          this.errorMessage.set(problem?.detail ?? 'This confirmation link has already been used. You can log in with your credentials.');
        } else if (error.status === 422 && problem?.detail) {
          this.errorMessage.set(problem.detail);
          this.step.set('error');
        } else if (problem?.detail) {
          this.errorMessage.set(problem.detail);
          this.step.set('error');
        } else {
          this.errorMessage.set('This confirmation link has expired. Contact your Director to request a new invitation.');
          this.step.set('expired');
        }
      } else {
        this.errorMessage.set('Something went wrong. Please try again.');
        this.step.set('error');
      }
    }
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
