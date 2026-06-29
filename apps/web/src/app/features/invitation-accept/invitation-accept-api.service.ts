import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ValidateInvitationLinkResponse {
  email: string;
  organisationName: string;
  role: string;
  isValid: boolean;
}

export interface AcceptInvitationRequest {
  token: string;
  signature: string;
  fullName: string;
  password: string;
}

export interface AcceptInvitationResponse {
  email: string;
  organisationName: string;
  message: string;
}

export interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

@Injectable({ providedIn: 'root' })
export class InvitationAcceptApiService {
  constructor(private readonly http: HttpClient) {}

  validateInvitationLink(token: string, sig: string): Promise<ValidateInvitationLinkResponse> {
    return firstValueFrom(
      this.http.get<ApiEnvelope<ValidateInvitationLinkResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/accept-invitation`,
        { params: { token, sig } },
      ),
    ).then((e) => e.data);
  }

  acceptInvitation(
    token: string,
    signature: string,
    fullName: string,
    password: string,
  ): Promise<AcceptInvitationResponse> {
    const body: AcceptInvitationRequest = { token, signature, fullName, password };
    return firstValueFrom(
      this.http.post<ApiEnvelope<AcceptInvitationResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/accept-invitation`,
        body,
      ),
    ).then((e) => e.data);
  }
}
