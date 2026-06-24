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
}
