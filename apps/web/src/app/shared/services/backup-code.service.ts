import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class BackupCodeService {
  private readonly http = inject(HttpClient);

  generate(): Promise<{ codes: string[] }> {
    return firstValueFrom(
      this.http.post<{ codes: string[] }>(
        `${environment.apiBaseUrl}/api/v1/auth/generate-backup-codes`,
        {},
      ),
    );
  }
}
