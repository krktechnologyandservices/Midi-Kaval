import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import {
  ApiEnvelope,
  CaseDto,
  CaseSearchParams,
  CaseSearchPresetDto,
  CaseSearchResultDto,
  CaseSummaryDto,
  CaseDetailDto,
  CaseNoteDto,
  CaseNoteListResultDto,
  CreateCaseNoteRequest,
  CreateInterventionRequest,
  InterventionDto,
  UpdateInterventionRequest,
  CourtSittingDto,
  CourtSittingListResultDto,
  CreateCourtSittingRequest,
  UpdateCourtSittingRequest,
  NotificationDto,
  NotificationListResultDto,
  UnreadCountDto,
  CheckCaseDuplicateRequest,
  CheckCaseDuplicateResultDto,
  CreateCaseRequest,
  CreateCaseSearchPresetRequest,
  FieldWorkerUserDto,
  Stage2DataDto,
  Stage3SupportDto,
  Stage4PlacementDto,
  Stage5ReintegrationDto,
  Stage6TerminationExclusionDto,
  TransferCaseRequest,
  TransitionCaseStageRequest,
  UpsertStage2DataRequest,
  UpsertStage3SupportsRequest,
  UpsertStage4PlacementRequest,
  UpsertStage5ReintegrationRequest,
  UpsertStage6TerminationExclusionRequest,
  VisitListItemDto,
  VisitPlaceDto,
  AddVisitPlaceRequest,
  GeocodingResultDto,
  ScheduleVisitRequest,
  CancelVisitRequest,
  RescheduleVisitRequest,
} from '../models/case.models';

@Injectable({ providedIn: 'root' })
export class CaseApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthSessionService);

  async checkDuplicate(request: CheckCaseDuplicateRequest): Promise<CheckCaseDuplicateResultDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CheckCaseDuplicateResultDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/check-duplicate`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createCase(request: CreateCaseRequest): Promise<CaseDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CaseDto>>(
          `${environment.apiBaseUrl}/api/v1/cases`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async mergeCase(caseId: string, request: CreateCaseRequest): Promise<CaseDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CaseDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/merge`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async searchCases(params: CaseSearchParams): Promise<{ result: CaseSearchResultDto; totalCount: number }> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<CaseSearchResultDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/search`,
          { params: this.toSearchHttpParams(params) },
        ),
      );
      return {
        result: envelope.data,
        totalCount: envelope.meta.totalCount ?? envelope.data.items?.length ?? 0,
      };
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listSearchPresets(): Promise<CaseSearchPresetDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<CaseSearchPresetDto[]>>(
          `${environment.apiBaseUrl}/api/v1/cases/search-presets`,
        ),
      );
      return envelope.data ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createSearchPreset(request: CreateCaseSearchPresetRequest): Promise<CaseSearchPresetDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CaseSearchPresetDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/search-presets`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async deleteSearchPreset(presetId: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.delete(`${environment.apiBaseUrl}/api/v1/cases/search-presets/${presetId}`),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getCaseDetail(caseId: string): Promise<CaseDetailDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<CaseDetailDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async transferCase(caseId: string, request: TransferCaseRequest): Promise<CaseDetailDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CaseDetailDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/transfer`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async transitionStage(
    caseId: string,
    request: TransitionCaseStageRequest,
  ): Promise<CaseDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.patch<ApiEnvelope<CaseDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getStage2Data(caseId: string): Promise<Stage2DataDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<Stage2DataDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage2-data`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async upsertStage2Data(caseId: string, request: UpsertStage2DataRequest): Promise<Stage2DataDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<Stage2DataDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage2-data`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getStage3Supports(caseId: string): Promise<Stage3SupportDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<Stage3SupportDto[]>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage3-data`,
        ),
      );
      return envelope.data ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async upsertStage3Supports(
    caseId: string,
    request: UpsertStage3SupportsRequest,
  ): Promise<Stage3SupportDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<Stage3SupportDto[]>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage3-data`,
          request,
        ),
      );
      return envelope.data ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getStage4Placement(caseId: string): Promise<Stage4PlacementDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<Stage4PlacementDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage4-data`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async upsertStage4Placement(
    caseId: string,
    request: UpsertStage4PlacementRequest,
  ): Promise<Stage4PlacementDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<Stage4PlacementDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage4-data`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getStage5Reintegration(caseId: string): Promise<Stage5ReintegrationDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<Stage5ReintegrationDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage5-data`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async upsertStage5Reintegration(
    caseId: string,
    request: UpsertStage5ReintegrationRequest,
  ): Promise<Stage5ReintegrationDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<Stage5ReintegrationDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage5-data`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getStage6TerminationExclusion(caseId: string): Promise<Stage6TerminationExclusionDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<Stage6TerminationExclusionDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage6-data`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async upsertStage6TerminationExclusion(
    caseId: string,
    request: UpsertStage6TerminationExclusionRequest,
  ): Promise<Stage6TerminationExclusionDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<Stage6TerminationExclusionDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/stage6-data`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  /** True when the case isn't currently at the stage this data belongs to (backend 404s this way). */
  isStageDataNotFound(error: unknown): boolean {
    return error instanceof CaseApiError && error.status === 404;
  }

  async listFieldWorkers(): Promise<FieldWorkerUserDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<FieldWorkerUserDto[]>>(
          `${environment.apiBaseUrl}/api/v1/users/field-workers`,
        ),
      );
      return envelope.data ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listCaseNotes(caseId: string): Promise<CaseNoteDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<CaseNoteListResultDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/notes`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createCaseNote(caseId: string, request: CreateCaseNoteRequest): Promise<CaseNoteDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CaseNoteDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/notes`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listInterventions(caseId: string): Promise<InterventionDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<{ items?: InterventionDto[] | null }>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/interventions`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createIntervention(
    caseId: string,
    request: CreateInterventionRequest,
  ): Promise<InterventionDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<InterventionDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/interventions`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async updateIntervention(
    caseId: string,
    interventionId: string,
    request: UpdateInterventionRequest,
  ): Promise<InterventionDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.patch<ApiEnvelope<InterventionDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/interventions/${interventionId}`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listCourtSittings(caseId: string): Promise<CourtSittingDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<CourtSittingListResultDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/court-sittings`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createCourtSitting(
    caseId: string,
    request: CreateCourtSittingRequest,
  ): Promise<CourtSittingDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<CourtSittingDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/court-sittings`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async updateCourtSitting(
    caseId: string,
    sittingId: string,
    request: UpdateCourtSittingRequest,
  ): Promise<CourtSittingDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.patch<ApiEnvelope<CourtSittingDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/court-sittings/${sittingId}`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listVisits(caseId: string): Promise<VisitListItemDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<{ items: VisitListItemDto[] }>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/visits`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async scheduleVisit(
    caseId: string,
    request: ScheduleVisitRequest,
  ): Promise<VisitListItemDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<VisitListItemDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/visits`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async cancelVisit(
    caseId: string,
    visitId: string,
    request: CancelVisitRequest,
  ): Promise<VisitListItemDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<VisitListItemDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/visits/${visitId}/cancel`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async rescheduleVisit(
    visitId: string,
    request: RescheduleVisitRequest,
  ): Promise<VisitListItemDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<VisitListItemDto>>(
          `${environment.apiBaseUrl}/api/v1/visits/${visitId}/reschedule`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async addVisitPlace(
    caseId: string,
    visitId: string,
    request: AddVisitPlaceRequest,
  ): Promise<VisitPlaceDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<VisitPlaceDto>>(
          `${environment.apiBaseUrl}/api/v1/cases/${caseId}/visits/${visitId}/places`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async searchGeocodingAddresses(query: string): Promise<GeocodingResultDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<{ items: GeocodingResultDto[] }>>(
          `${environment.apiBaseUrl}/api/v1/geocoding/search`,
          { params: { q: query } },
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listNotifications(): Promise<NotificationDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<NotificationListResultDto>>(
          `${environment.apiBaseUrl}/api/v1/notifications`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async markNotificationRead(notificationId: string): Promise<NotificationDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.patch<ApiEnvelope<NotificationDto>>(
          `${environment.apiBaseUrl}/api/v1/notifications/${notificationId}/read`,
          {},
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getUnreadCount(): Promise<number> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<UnreadCountDto>>(
          `${environment.apiBaseUrl}/api/v1/notifications/unread-count`,
        ),
      );
      return envelope.data.count;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async exportCases(format: 'xlsx' | 'pdf', params: CaseSearchParams): Promise<void> {
    const httpParams: Record<string, string> = {
      format,
      ...this.toSearchHttpParams(params),
    };

    try {
      const response = await firstValueFrom(
        this.http.get(`${environment.apiBaseUrl}/api/v1/cases/search/export`, {
          params: httpParams,
          responseType: 'blob',
          observe: 'response',
        }),
      );

      const blob = response.body;
      if (!blob) {
        throw new Error('Export returned an empty file.');
      }

      const fileName = this.parseContentDispositionFileName(
        response.headers.get('Content-Disposition'),
      ) ?? `cases-export.${format}`;
      this.downloadBlob(blob, fileName);
    } catch (error) {
      throw await this.wrapBlobError(error);
    }
  }

  private parseContentDispositionFileName(header: string | null): string | null {
    if (!header) {
      return null;
    }

    const match = /filename="?([^";]+)"?/i.exec(header);
    return match?.[1] ?? null;
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }

  private async wrapBlobError(error: unknown): Promise<CaseApiError> {
    if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
      const text = await error.error.text();
      try {
        const problem = JSON.parse(text) as { detail?: string; title?: string };
        const synthetic = new HttpErrorResponse({
          error: problem,
          headers: error.headers,
          status: error.status,
          statusText: error.statusText,
          url: error.url ?? undefined,
        });
        return this.wrapError(synthetic);
      } catch {
        return this.wrapError(error);
      }
    }

    return this.wrapError(error);
  }

  private toSearchHttpParams(params: CaseSearchParams): Record<string, string> {
    const httpParams: Record<string, string> = {};
    if (params.q?.trim()) {
      httpParams['q'] = params.q.trim();
    }
    if (params.currentStage) {
      httpParams['currentStage'] = params.currentStage;
    }
    if (params.typeOfOffence?.trim()) {
      httpParams['typeOfOffence'] = params.typeOfOffence.trim();
    }
    if (params.offenceClassification) {
      httpParams['offenceClassification'] = params.offenceClassification;
    }
    if (params.domicile) {
      httpParams['domicile'] = params.domicile;
    }
    if (params.createdByUserId) {
      httpParams['createdByUserId'] = params.createdByUserId;
    }
    if (params.assignedWorkerUserId) {
      httpParams['assignedWorkerUserId'] = params.assignedWorkerUserId;
    }
    if (params.overdue === true) {
      httpParams['overdue'] = 'true';
    }
    if (params.page) {
      httpParams['page'] = String(params.page);
    }
    if (params.pageSize) {
      httpParams['pageSize'] = String(params.pageSize);
    }
    return httpParams;
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof CaseApiError) {
      if (error.kind === 'network') {
        return 'Could not verify Crime/ST — check connection and try again.';
      }

      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  isConflict(error: unknown): boolean {
    return error instanceof CaseApiError && error.status === 409;
  }

  isNetworkError(error: unknown): boolean {
    return error instanceof CaseApiError && error.kind === 'network';
  }

  private wrapError(error: unknown): CaseApiError {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return new CaseApiError('network', 0, error);
      }

      return new CaseApiError('http', error.status, error);
    }

    return new CaseApiError('unknown', 0, error);
  }
}

export class CaseApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly status: number,
    readonly sourceError: unknown,
  ) {
    super('Case API error');
    this.name = 'CaseApiError';
  }
}

export function buildDuplicateCheckRequest(
  crimeNumber: string,
  stNumber: string,
): CheckCaseDuplicateRequest {
  const body: CheckCaseDuplicateRequest = {};
  const crime = crimeNumber.trim();
  const st = stNumber.trim();
  if (crime) {
    body.crimeNumber = crime;
  }
  if (st) {
    body.stNumber = st;
  }
  return body;
}
