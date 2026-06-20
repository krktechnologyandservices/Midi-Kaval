import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {
  buildDuplicateCheckRequest,
  CaseDetailDto,
  CaseNoteDto,
  CaseNoteListResultDto,
  CreateCaseNoteRequest,
  CreateInterventionRequest,
  CaseDto,
  CaseSearchResultDto,
  CheckCaseDuplicateRequest,
  CheckCaseDuplicateResultDto,
  CreateCaseRequest,
  CaseGpsDto,
  FieldWorkerUserDto,
  InterventionDto,
  InterventionListResultDto,
  RevealCasePiiResponse,
  UpdateInterventionRequest,
  CourtSittingDto,
  CourtSittingListResultDto,
  CreateCourtSittingRequest,
  UpdateCourtSittingRequest,
  VerifyCaseGpsRequest,
} from './case.models';

export {buildDuplicateCheckRequest};

export class CaseApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async checkDuplicate(
    request: CheckCaseDuplicateRequest,
  ): Promise<CheckCaseDuplicateResultDto> {
    try {
      const envelope = await this.auth.postApi<CheckCaseDuplicateResultDto>(
        '/api/v1/cases/check-duplicate',
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createCase(request: CreateCaseRequest): Promise<CaseDto> {
    try {
      const envelope = await this.auth.postApi<CaseDto>(
        '/api/v1/cases',
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async mergeCase(caseId: string, request: CreateCaseRequest): Promise<CaseDto> {
    try {
      const envelope = await this.auth.postApi<CaseDto>(
        `/api/v1/cases/${caseId}/merge`,
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getCaseDetail(caseId: string): Promise<CaseDetailDto> {
    try {
      const envelope = await this.auth.getApi<CaseDetailDto>(
        `/api/v1/cases/${caseId}`,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listAssignedCases(
    page = 1,
    pageSize = 25,
  ): Promise<CaseSearchResultDto> {
    try {
      const envelope = await this.auth.getApi<CaseSearchResultDto>(
        `/api/v1/cases/assigned?page=${page}&pageSize=${pageSize}`,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async verifyCaseGps(
    caseId: string,
    request: VerifyCaseGpsRequest,
  ): Promise<CaseGpsDto> {
    try {
      const envelope = await this.auth.postApi<CaseGpsDto>(
        `/api/v1/cases/${caseId}/gps/verify`,
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async revealCasePii(caseId: string): Promise<RevealCasePiiResponse> {
    try {
      const envelope = await this.auth.postApi<RevealCasePiiResponse>(
        `/api/v1/cases/${caseId}/reveal-pii`,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listCaseNotes(caseId: string): Promise<CaseNoteDto[]> {
    try {
      const envelope = await this.auth.getApi<CaseNoteListResultDto>(
        `/api/v1/cases/${caseId}/notes`,
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createCaseNote(
    caseId: string,
    request: CreateCaseNoteRequest,
  ): Promise<CaseNoteDto> {
    try {
      const envelope = await this.auth.postApi<CaseNoteDto>(
        `/api/v1/cases/${caseId}/notes`,
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listInterventions(caseId: string): Promise<InterventionDto[]> {
    try {
      const envelope = await this.auth.getApi<InterventionListResultDto>(
        `/api/v1/cases/${caseId}/interventions`,
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
      const envelope = await this.auth.postApi<InterventionDto>(
        `/api/v1/cases/${caseId}/interventions`,
        request,
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
      const envelope = await this.auth.patchApi<InterventionDto>(
        `/api/v1/cases/${caseId}/interventions/${interventionId}`,
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listCourtSittings(caseId: string): Promise<CourtSittingDto[]> {
    try {
      const envelope = await this.auth.getApi<CourtSittingListResultDto>(
        `/api/v1/cases/${caseId}/court-sittings`,
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
      const envelope = await this.auth.postApi<CourtSittingDto>(
        `/api/v1/cases/${caseId}/court-sittings`,
        request,
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
      const envelope = await this.auth.patchApi<CourtSittingDto>(
        `/api/v1/cases/${caseId}/court-sittings/${sittingId}`,
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listFieldWorkers(): Promise<FieldWorkerUserDto[]> {
    try {
      const envelope = await this.auth.getApi<FieldWorkerUserDto[]>(
        '/api/v1/users/field-workers',
      );
      return envelope.data ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
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

  private wrapError(error: unknown): CaseApiError {
    if (error instanceof ApiClientError) {
      if (error.status === 0) {
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

export const caseApiService = new CaseApiService();
