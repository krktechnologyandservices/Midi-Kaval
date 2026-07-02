import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CreateOrganisationRequest {
  name: string;
  targetDirectorEmail: string;
}

export interface CreateOrganisationResponse {
  organisationId: string;
  name: string;
  status: string;
}

export interface VendorOrganisationSummary {
  id: string;
  name: string;
  isActive: boolean;
  directorCount: number;
  hasPendingRecovery: boolean;
  createdAtUtc: string;
}

export interface VendorOrganisationDetail extends VendorOrganisationSummary {
  lastKnownDirectorName: string | null;
  lastKnownDirectorActiveAt: string | null;
}

export interface ReissueActivationRequest {
  targetDirectorEmail: string;
}

export interface ReissueActivationResponse {
  status: string;
  targetDirectorEmail: string;
}

export interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

@Injectable({ providedIn: 'root' })
export class VendorApiService {
  constructor(private readonly http: HttpClient) {}

  createOrganisation(name: string, targetDirectorEmail: string): Promise<CreateOrganisationResponse> {
    const body: CreateOrganisationRequest = { name, targetDirectorEmail };
    return firstValueFrom(
      this.http.post<ApiEnvelope<CreateOrganisationResponse>>(
        `${environment.apiBaseUrl}/api/v1/vendor/organisations`,
        body,
      ),
    ).then(e => e.data);
  }

  getOrganisations(): Promise<VendorOrganisationSummary[]> {
    return firstValueFrom(
      this.http.get<ApiEnvelope<VendorOrganisationSummary[]>>(
        `${environment.apiBaseUrl}/api/v1/vendor/organisations`,
      ),
    ).then(e => e.data);
  }

  getOrganisationDetail(id: string): Promise<VendorOrganisationDetail> {
    return firstValueFrom(
      this.http.get<ApiEnvelope<VendorOrganisationDetail>>(
        `${environment.apiBaseUrl}/api/v1/vendor/organisations/${id}`,
      ),
    ).then(e => e.data);
  }

  reissueActivation(organisationId: string, targetDirectorEmail: string): Promise<ReissueActivationResponse> {
    const body: ReissueActivationRequest = { targetDirectorEmail };
    return firstValueFrom(
      this.http.post<ApiEnvelope<ReissueActivationResponse>>(
        `${environment.apiBaseUrl}/api/v1/vendor/organisations/${organisationId}/reissue-activation`,
        body,
      ),
    ).then(e => e.data);
  }

  getBackupCodeRemainingCount(): Promise<{ remaining: number }> {
    return firstValueFrom(
      this.http.get<{ remaining: number }>(
        `${environment.apiBaseUrl}/api/v1/auth/backup-codes/remaining`,
      ),
    );
  }

  regenerateBackupCodes(): Promise<{ codes: string[] }> {
    return firstValueFrom(
      this.http.post<{ codes: string[] }>(
        `${environment.apiBaseUrl}/api/v1/auth/backup-codes/regenerate`,
        {},
      ),
    );
  }

  changePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Promise<{ message: string }> {
    return firstValueFrom(
      this.http.post<{ message: string }>(
        `${environment.apiBaseUrl}/api/v1/auth/change-password`,
        { currentPassword, newPassword, confirmNewPassword },
      ),
    );
  }
}
