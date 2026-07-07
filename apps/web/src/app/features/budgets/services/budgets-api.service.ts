import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiEnvelope } from '../../cases/models/case.models';
import {
  BudgetDetailDto,
  BudgetListDto,
  BudgetUtilizationListDto,
  BudgetUtilizationSummaryDto,
  CreateBudgetRequest,
  CreateBudgetUtilizationRequest,
  PaginatedResult,
  UpdateBudgetRequest,
  UpdateBudgetUtilizationRequest,
} from '../budget.models';

@Injectable({ providedIn: 'root' })
export class BudgetsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/budgets`;

  async list(page = 1, pageSize = 20): Promise<PaginatedResult<BudgetListDto>> {
    const p = Math.max(1, page);
    const s = Math.max(1, pageSize);
    try {
      const params = new HttpParams().set('page', p).set('pageSize', s);
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<PaginatedResult<BudgetListDto>>>(this.baseUrl, { params }),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getById(id: string): Promise<BudgetDetailDto> {
    if (!id?.trim()) throw new Error('id is required');
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<BudgetDetailDto>>(`${this.baseUrl}/${id}`),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async create(request: CreateBudgetRequest): Promise<BudgetDetailDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<BudgetDetailDto>>(this.baseUrl, request),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async update(id: string, request: UpdateBudgetRequest): Promise<BudgetDetailDto> {
    if (!id?.trim()) throw new Error('id is required');
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<BudgetDetailDto>>(`${this.baseUrl}/${id}`, request),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async propose(id: string): Promise<void> {
    if (!id?.trim()) throw new Error('id is required');
    try {
      await firstValueFrom(this.http.post(`${this.baseUrl}/${id}/propose`, {}));
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async approve(id: string, decisionComment?: string): Promise<void> {
    if (!id?.trim()) throw new Error('id is required');
    try {
      await firstValueFrom(
        this.http.post(`${this.baseUrl}/${id}/approve`, { decisionComment }),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async returnBudget(id: string, decisionComment: string): Promise<void> {
    if (!id?.trim()) throw new Error('id is required');
    try {
      await firstValueFrom(
        this.http.post(`${this.baseUrl}/${id}/return`, { decisionComment }),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async execute(id: string): Promise<void> {
    if (!id?.trim()) throw new Error('id is required');
    try {
      await firstValueFrom(this.http.post(`${this.baseUrl}/${id}/execute`, {}));
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listUtilizations(
    budgetId: string,
    page = 1,
    pageSize = 20,
    fromDate?: string,
    toDate?: string,
  ): Promise<PaginatedResult<BudgetUtilizationListDto>> {
    if (!budgetId?.trim()) throw new Error('budgetId is required');
    const p = Math.max(1, page);
    const s = Math.max(1, pageSize);
    try {
      let params = new HttpParams().set('page', p).set('pageSize', s);
      if (fromDate) params = params.set('fromDate', fromDate);
      if (toDate) params = params.set('toDate', toDate);
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<PaginatedResult<BudgetUtilizationListDto>>>(
          `${this.baseUrl}/${budgetId}/utilizations`,
          { params },
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async updateUtilization(
    budgetId: string,
    id: string,
    request: UpdateBudgetUtilizationRequest,
  ): Promise<BudgetUtilizationListDto> {
    if (!budgetId?.trim()) throw new Error('budgetId is required');
    if (!id?.trim()) throw new Error('id is required');
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<BudgetUtilizationListDto>>(
          `${this.baseUrl}/${budgetId}/utilizations/${id}`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async deleteUtilization(budgetId: string, id: string, force = false): Promise<void> {
    if (!budgetId?.trim()) throw new Error('budgetId is required');
    if (!id?.trim()) throw new Error('id is required');
    try {
      const params = new HttpParams().set('force', force);
      await firstValueFrom(
        this.http.delete(`${this.baseUrl}/${budgetId}/utilizations/${id}`, { params }),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async createUtilization(
    budgetId: string,
    request: CreateBudgetUtilizationRequest,
  ): Promise<BudgetUtilizationListDto> {
    if (!budgetId?.trim()) throw new Error('budgetId is required');
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<BudgetUtilizationListDto>>(
          `${this.baseUrl}/${budgetId}/utilizations`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getUtilizationSummary(budgetId: string): Promise<BudgetUtilizationSummaryDto> {
    if (!budgetId?.trim()) throw new Error('budgetId is required');
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<BudgetUtilizationSummaryDto>>(
          `${this.baseUrl}/${budgetId}/utilizations/summary`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async exportReport(frequency: string, year: number): Promise<Blob> {
    const params = new HttpParams().set('frequency', frequency).set('year', year);
    try {
      return await firstValueFrom(
        this.http.get(`${this.baseUrl}/report/export`, {
          params,
          responseType: 'blob',
        }),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
        const text = await error.error.text();
        const parsed = JSON.parse(text);
        throw new BudgetsApiError(parsed.error ?? text);
      }
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof BudgetsApiError) {
      return this.extractErrorMessage(error.sourceError);
    }
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return 'Network error — check your connection';
      }
      if (error.error?.detail) {
        return error.error.detail;
      }
      if (error.error?.title) {
        return error.error.title;
      }
      if (error.error?.error) {
        return error.error.error;
      }
      return `Request failed (${error.status})`;
    }
    if (error instanceof Error) {
      return error.message;
    }
    return 'An unknown error occurred';
  }

  getHttpStatus(error: unknown): number | null {
    let source: unknown = error;
    if (source instanceof BudgetsApiError) {
      source = source.sourceError;
    }
    if (source instanceof HttpErrorResponse) {
      return source.status;
    }
    return null;
  }

  private wrapError(error: unknown): BudgetsApiError {
    if (error instanceof HttpErrorResponse) {
      return new BudgetsApiError(error);
    }
    return new BudgetsApiError(error);
  }
}

export class BudgetsApiError extends Error {
  constructor(readonly sourceError: unknown) {
    super('Budgets API error');
    this.name = 'BudgetsApiError';
  }
}
