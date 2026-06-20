import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { ApiEnvelope } from '../../cases/models/case.models';
import {
  ApproveTravelClaimRequest,
  ReturnTravelClaimRequest,
  TravelClaimDto,
  TravelClaimListResultDto,
} from '../travel.models';

@Injectable({ providedIn: 'root' })
export class TravelClaimApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthSessionService);

  async listPending(): Promise<TravelClaimDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<TravelClaimListResultDto>>(
          `${environment.apiBaseUrl}/api/v1/director/travel-claims/pending`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getForDirectorReview(claimId: string): Promise<TravelClaimDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<TravelClaimDto>>(
          `${environment.apiBaseUrl}/api/v1/director/travel-claims/${claimId}`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getForSupervisorReview(claimId: string): Promise<TravelClaimDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<TravelClaimDto>>(
          `${environment.apiBaseUrl}/api/v1/supervisor/travel-claims/${claimId}`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async approve(claimId: string, request: ApproveTravelClaimRequest): Promise<TravelClaimDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<TravelClaimDto>>(
          `${environment.apiBaseUrl}/api/v1/travel-claims/${claimId}/approve`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async returnClaim(claimId: string, request: ReturnTravelClaimRequest): Promise<TravelClaimDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<TravelClaimDto>>(
          `${environment.apiBaseUrl}/api/v1/travel-claims/${claimId}/return`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof TravelClaimApiError) {
      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  private wrapError(error: unknown): TravelClaimApiError {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return new TravelClaimApiError('network', 0, error);
      }

      return new TravelClaimApiError('http', error.status, error);
    }

    return new TravelClaimApiError('unknown', 0, error);
  }
}

export class TravelClaimApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly status: number,
    readonly sourceError: unknown,
  ) {
    super('Travel claim API error');
    this.name = 'TravelClaimApiError';
  }
}
