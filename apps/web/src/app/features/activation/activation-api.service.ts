import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ValidateLinkResponse {
  email: string;
  organisationName: string;
}

export interface ActivateOrganisationRequest {
  token: string;
  signature: string;
  fullName: string;
  password: string;
}

export interface ActivateOrganisationResponse {
  userId: string;
  organisationId: string;
  organisationName: string;
  message: string;
}

export interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

@Injectable({ providedIn: 'root' })
export class ActivationApiService {
  constructor(private readonly http: HttpClient) {}

  validateLink(token: string, signature: string): Promise<ValidateLinkResponse> {
    return firstValueFrom(
      this.http.get<ApiEnvelope<ValidateLinkResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/activate`,
        { params: { token, sig: signature } },
      ),
    ).then((e) => e.data);
  }

  activateOrganisation(
    token: string,
    signature: string,
    fullName: string,
    password: string,
  ): Promise<ActivateOrganisationResponse> {
    const body: ActivateOrganisationRequest = { token, signature, fullName, password };
    return firstValueFrom(
      this.http.post<ApiEnvelope<ActivateOrganisationResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/activate`,
        body,
      ),
    ).then((e) => e.data);
  }
}
