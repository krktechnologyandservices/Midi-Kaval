import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class TwoFactorService {
  private readonly http = inject(HttpClient);

  enroll(): Promise<{ provisioningUri: string; secretBase32: string }> {
    return firstValueFrom(
      this.http.post<{ provisioningUri: string; secretBase32: string }>(
        `${environment.apiBaseUrl}/api/v1/auth/enroll-2fa`,
        {},
      ),
    );
  }

  verifyEnroll(code: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(
        `${environment.apiBaseUrl}/api/v1/auth/verify-enroll-2fa`,
        { code },
      ),
    );
  }

  status(): Promise<{ enrolled: boolean; enrolledAt: string | null }> {
    return firstValueFrom(
      this.http.get<{ enrolled: boolean; enrolledAt: string | null }>(
        `${environment.apiBaseUrl}/api/v1/auth/2fa-status`,
      ),
    );
  }
}
