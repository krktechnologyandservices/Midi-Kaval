import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminUserListResult } from '../models/admin.models';

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
}
