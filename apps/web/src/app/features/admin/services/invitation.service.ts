import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  InvitationListResult,
  SendInvitationRequest,
  SendInvitationResponse,
  ResendInvitationResponse,
} from '../models/admin.models';

interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

@Injectable({ providedIn: 'root' })
export class InvitationService {
  constructor(private readonly http: HttpClient) {}

  sendInvitation(request: SendInvitationRequest): Promise<SendInvitationResponse> {
    return firstValueFrom(
      this.http.post<ApiEnvelope<SendInvitationResponse>>(
        `${environment.apiBaseUrl}/api/v1/admin/invitations`,
        request,
      ),
    ).then(e => e.data);
  }

  getInvitations(page = 1, pageSize = 25): Promise<InvitationListResult> {
    return firstValueFrom(
      this.http.get<ApiEnvelope<InvitationListResult>>(
        `${environment.apiBaseUrl}/api/v1/admin/invitations?page=${page}&pageSize=${pageSize}`,
      ),
    ).then(e => e.data);
  }

  resendInvitation(id: string): Promise<ResendInvitationResponse> {
    return firstValueFrom(
      this.http.post<ApiEnvelope<ResendInvitationResponse>>(
        `${environment.apiBaseUrl}/api/v1/admin/invitations/${id}/resend`,
        {},
      ),
    ).then(e => e.data);
  }
}
