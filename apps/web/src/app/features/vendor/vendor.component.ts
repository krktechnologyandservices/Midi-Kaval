import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { VendorApiService } from './vendor-api.service';

@Component({
  selector: 'app-vendor',
  imports: [ReactiveFormsModule],
  templateUrl: './vendor.component.html',
  styleUrl: './vendor.component.scss',
})
export class VendorComponent {
  private readonly fb = inject(FormBuilder);
  private readonly vendorApi = inject(VendorApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly showForm = signal(true);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(256)]],
    targetDirectorEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    try {
      const result = await this.vendorApi.createOrganisation(
        this.form.value.name!,
        this.form.value.targetDirectorEmail!,
      );

      this.successMessage.set(`Activation link sent to ${this.form.value.targetDirectorEmail}`);
      this.showForm.set(false);
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

  async logout(): Promise<void> {
    await this.auth.logout();
  }
}
