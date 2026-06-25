import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { VendorApiService, VendorOrganisationSummary, VendorOrganisationDetail } from './vendor-api.service';

type ViewState = 'create' | 'list' | 'detail';

@Component({
  selector: 'app-vendor',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './vendor.component.html',
  styleUrl: './vendor.component.scss',
})
export class VendorComponent {
  private readonly fb = inject(FormBuilder);
  private readonly vendorApi = inject(VendorApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly loadingList = signal(false);
  readonly loadingDetail = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly viewState = signal<ViewState>('create');
  readonly organisations = signal<VendorOrganisationSummary[]>([]);
  readonly selectedOrganisation = signal<VendorOrganisationDetail | null>(null);
  readonly reissueSuccess = signal<string | null>(null);
  readonly reissueError = signal<string | null>(null);
  readonly reissueSubmitting = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(256)]],
    targetDirectorEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });

  readonly reissueForm = this.fb.nonNullable.group({
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
      await this.vendorApi.createOrganisation(
        this.form.value.name!,
        this.form.value.targetDirectorEmail!,
      );

      this.successMessage.set(`Activation link sent to ${this.form.value.targetDirectorEmail}`);
      this.viewState.set('list');
      await this.loadOrganisations();
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

  async loadOrganisations(): Promise<void> {
    this.loadingList.set(true);
    try {
      const orgs = await this.vendorApi.getOrganisations();
      this.organisations.set(orgs);
    } catch {
      this.errorMessage.set('Failed to load organisations.');
    } finally {
      this.loadingList.set(false);
    }
  }

  async showDetail(orgId: string): Promise<void> {
    this.loadingDetail.set(true);
    this.reissueSuccess.set(null);
    this.reissueError.set(null);
    this.reissueForm.reset();

    try {
      const detail = await this.vendorApi.getOrganisationDetail(orgId);
      this.selectedOrganisation.set(detail);
      this.viewState.set('detail');
    } catch {
      this.errorMessage.set('Failed to load organisation details.');
    } finally {
      this.loadingDetail.set(false);
    }
  }

  async showList(): Promise<void> {
    this.viewState.set('list');
    this.selectedOrganisation.set(null);
    this.reissueSuccess.set(null);
    this.reissueError.set(null);
    await this.loadOrganisations();
  }

  showCreateForm(): void {
    this.viewState.set('create');
    this.successMessage.set(null);
    this.errorMessage.set(null);
    this.form.reset();
  }

  async reissueActivation(): Promise<void> {
    this.reissueError.set(null);
    this.reissueSuccess.set(null);

    if (this.reissueForm.invalid) {
      this.reissueForm.markAllAsTouched();
      return;
    }

    const org = this.selectedOrganisation();
    if (!org) return;

    this.reissueSubmitting.set(true);

    try {
      const result = await this.vendorApi.reissueActivation(
        org.id,
        this.reissueForm.value.targetDirectorEmail!,
      );

      this.reissueSuccess.set(`Activation link sent to ${result.targetDirectorEmail}`);
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 429) {
          this.reissueError.set('Too many requests. Please try again later.');
        } else if (error.status === 409) {
          this.reissueError.set('This organisation already has active Directors.');
        } else if (error.error?.detail) {
          this.reissueError.set(error.error.detail);
        } else {
          this.reissueError.set('Something went wrong. Please try again.');
        }
      } else {
        this.reissueError.set('Something went wrong. Please try again.');
      }
    } finally {
      this.reissueSubmitting.set(false);
    }
  }

  async logout(): Promise<void> {
    await this.auth.logout();
  }
}
