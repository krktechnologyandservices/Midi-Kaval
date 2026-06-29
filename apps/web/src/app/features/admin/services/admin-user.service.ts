import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AdminUserListResult, DeleteUserResponse, SuspendUserRequest, UserActionResponse } from '../models/admin.models';

interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

@Injectable({ providedIn: 'root' })
export class AdminUserService {
  constructor(private readonly http: HttpClient) {}

  getUsers(
    page = 1,
    pageSize = 25,
    search?: string,
    roles?: string,
    status?: string,
    sortBy?: string,
    sortDesc?: boolean,
  ): Promise<AdminUserListResult> {
    const params = new URLSearchParams();
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    if (search) params.set('search', search);
    if (roles) params.set('roles', roles);
    if (status) params.set('status', status);
    if (sortBy) params.set('sortBy', sortBy);
    if (sortDesc !== undefined) params.set('sortDesc', String(sortDesc));

    return firstValueFrom(
      this.http.get<ApiEnvelope<AdminUserListResult>>(
        `${environment.apiBaseUrl}/api/v1/admin/users?${params.toString()}`,
      ),
    ).then(e => e.data);
  }

  suspendUser(id: string, reason?: string): Promise<UserActionResponse> {
    const body: SuspendUserRequest = { reason };
    return firstValueFrom(
      this.http.post<ApiEnvelope<UserActionResponse>>(
        `${environment.apiBaseUrl}/api/v1/admin/users/${id}/suspend`,
        body,
      ),
    ).then(e => e.data);
  }

  reactivateUser(id: string): Promise<UserActionResponse> {
    return firstValueFrom(
      this.http.post<ApiEnvelope<UserActionResponse>>(
        `${environment.apiBaseUrl}/api/v1/admin/users/${id}/reactivate`,
        {},
      ),
    ).then(e => e.data);
  }

  deleteUser(id: string, confirmationEmail: string): Promise<DeleteUserResponse> {
    return firstValueFrom(
      this.http.delete<ApiEnvelope<DeleteUserResponse>>(
        `${environment.apiBaseUrl}/api/v1/admin/users/${id}`,
        { body: { confirmationEmail } },
      ),
    ).then(e => e.data);
  }

  isLastDirector(userId: string): Promise<boolean> {
    return firstValueFrom(
      this.http.get<{ isLastDirector: boolean }>(
        `${environment.apiBaseUrl}/api/v1/admin/users/${userId}/is-last-director`,
      ),
    ).then(r => r.isLastDirector);
  }

  resetTwoFactor(userId: string): Promise<{ id: string; message: string }> {
    return firstValueFrom(
      this.http.post<ApiEnvelope<{ id: string; message: string }>>(
        `${environment.apiBaseUrl}/api/v1/admin/users/${userId}/reset-2fa`,
        {},
      ),
    ).then(e => e.data);
  }
}
