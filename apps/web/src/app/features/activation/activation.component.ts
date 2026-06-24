import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivationApiService } from './activation-api.service';

export function passwordPolicyValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value ?? '';
    const errors: Record<string, boolean> = {};
    if (value.length < 8) errors['minlength'] = true;
    if (!/[A-Z]/.test(value)) errors['uppercase'] = true;
    if (!/[a-z]/.test(value)) errors['lowercase'] = true;
    if (!/[0-9]/.test(value)) errors['digit'] = true;
    return Object.keys(errors).length > 0 ? errors : null;
  };
}

export function passwordStrength(password: string): { label: string; percent: number; class: string } {
  let score = 0;
  if (password.length >= 8) score++;
  if (password.length >= 12) score++;
  if (/[A-Z]/.test(password)) score++;
  if (/[a-z]/.test(password)) score++;
  if (/[0-9]/.test(password)) score++;
  if (/[^A-Za-z0-9]/.test(password)) score++;

  if (score <= 2) return { label: 'Weak', percent: 33, class: 'weak' };
  if (score <= 4) return { label: 'Medium', percent: 66, class: 'medium' };
  return { label: 'Strong', percent: 100, class: 'strong' };
}

@Component({
  selector: 'app-activation',
  imports: [ReactiveFormsModule],
  templateUrl: './activation.component.html',
  styleUrl: './activation.component.scss',
})
export class ActivationComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ActivationApiService);

  readonly token: string;
  readonly signature: string;

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly step = signal<'validate' | 'info' | 'form' | 'success' | 'error'>('validate');
  readonly email = signal('');
  readonly organisationName = signal('');
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal('');

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(256)]],
    password: ['', [Validators.required, passwordPolicyValidator()]],
  });

  constructor() {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.signature = this.route.snapshot.queryParamMap.get('sig') ?? '';

    if (!this.token || !this.signature) {
      this.errorMessage.set('Invalid activation link.');
      this.loading.set(false);
      this.step.set('error');
      return;
    }

    this.validateLink();
  }

  get passwordStrength() {
    const value = this.form.controls.password.value ?? '';
    return passwordStrength(value);
  }

  get passwordErrors(): string[] {
    const errors = this.form.controls.password.errors;
    if (!errors) return [];
    const msgs: string[] = [];
    if (errors['minlength']) msgs.push('At least 8 characters');
    if (errors['uppercase']) msgs.push('One uppercase letter');
    if (errors['lowercase']) msgs.push('One lowercase letter');
    if (errors['digit']) msgs.push('One digit');
    return msgs;
  }

  private async validateLink(): Promise<void> {
    try {
      const result = await this.api.validateLink(this.token, this.signature);
      this.email.set(result.email);
      this.organisationName.set(result.organisationName);
      this.step.set('info');
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        if (error.error?.detail) {
          this.errorMessage.set(error.error.detail);
        } else {
          this.errorMessage.set('This link has expired or already been used. Please contact the Vendor to request a new activation link.');
        }
      } else {
        this.errorMessage.set('Something went wrong. Please try again.');
      }
      this.step.set('error');
    } finally {
      this.loading.set(false);
    }
  }

  proceedToForm(): void {
    this.step.set('form');
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    try {
      const result = await this.api.activateOrganisation(
        this.token,
        this.signature,
        this.form.value.fullName!,
        this.form.value.password!,
      );

      this.successMessage.set(result.message);
      this.step.set('success');
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 429) {
          this.errorMessage.set('Too many requests. Please try again later.');
        } else if (error.error?.detail) {
          this.errorMessage.set(error.error.detail);
        } else {
          this.errorMessage.set('Something went wrong. Please try again.');
        }
      } else {
        this.errorMessage.set('Something went wrong. Please try again.');
      }
    } finally {
      this.submitting.set(false);
    }
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
