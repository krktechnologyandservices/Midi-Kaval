import { Component, WritableSignal, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { AppRole } from '@midi-kaval/shared-types';
import { BudgetsApiService } from './services/budgets-api.service';
import {
  BudgetDetailDto,
  BudgetHeadSummaryDto,
  BudgetUtilizationListDto,
  BudgetUtilizationSummaryDto,
  BUDGET_APPROVAL_STATUS_LABELS,
  BUDGET_STATUS_COLORS,
  formatAmount,
  formatFinancialYear,
} from './budget.models';
import { BudgetApproveDialogComponent } from './budget-approve-dialog.component';
import { BudgetReportDialogComponent } from './budget-report-dialog.component';

@Component({
  selector: 'app-budget-detail-page',
  imports: [
    CommonModule,
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
  ],
  template: `
    <div class="detail-page">
      <a class="back-link" routerLink="/budgets">
        <mat-icon>arrow_back</mat-icon>
        Back to budgets
      </a>

      @if (loading()) {
        <mat-card>
          <mat-card-content>
            <div class="skeleton-detail">
              <div class="skeleton skeleton-title"></div>
              <div class="skeleton skeleton-text"></div>
              <div class="skeleton skeleton-text skeleton-text--medium"></div>
            </div>
          </mat-card-content>
        </mat-card>
      } @else if (notFound()) {
        <mat-card>
          <mat-card-content>
            <div class="empty-state">
              <mat-icon>search_off</mat-icon>
              <h2>Budget not found</h2>
              <p>The budget you're looking for does not exist or has been removed.</p>
              <a mat-stroked-button color="primary" routerLink="/budgets">
                <mat-icon>arrow_back</mat-icon>
                Back to budgets
              </a>
            </div>
          </mat-card-content>
        </mat-card>
      } @else if (errorMessage()) {
        <div class="error-banner" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage() }}</span>
          <button mat-stroked-button (click)="loadDetail()">Retry</button>
        </div>
      } @else {
        <!-- Header Card -->
        <mat-card class="header-card">
          <mat-card-content>
            <div class="header-grid">
              <div>
                <h1>{{ budget()?.source }}</h1>
                <p class="subtitle">
                  Financial Year {{ formatFinancialYear(budget()!.financialYearStart, budget()!.financialYearEnd) }}
                </p>
                <p class="created-by">Created by {{ budget()?.createdByUserId }}</p>
              </div>
              <div class="header-info">
                <span class="status-badge" [style.--status-color]="statusColor(budget()!.approvalStatus)">
                  {{ statusLabel(budget()!.approvalStatus) }}
                </span>
                @if (budget()?.notes) {
                  <p class="notes">{{ budget()!.notes }}</p>
                }
              </div>
            </div>
          </mat-card-content>
        </mat-card>

        <!-- Action Buttons -->
        @if (showActions) {
          <mat-card class="actions-card">
            <mat-card-content>
              <div class="action-buttons">
                @if (showPropose()) {
                  <button mat-flat-button color="primary" (click)="propose()" [disabled]="actionLoading">
                    <mat-icon>send</mat-icon>
                    Propose
                  </button>
                }
                @if (showApproveReturn()) {
                  <button mat-flat-button color="primary" (click)="approve()" [disabled]="actionLoading">
                    <mat-icon>check_circle</mat-icon>
                    Approve
                  </button>
                  <button mat-flat-button color="warn" (click)="returnBudget()" [disabled]="actionLoading">
                    <mat-icon>undo</mat-icon>
                    Return
                  </button>
                }
                @if (showExecute()) {
                  <button mat-flat-button color="primary" (click)="execute()" [disabled]="actionLoading">
                    <mat-icon>play_arrow</mat-icon>
                    Execute
                  </button>
                }
                @if (isDirector) {
                  <button mat-stroked-button color="primary" (click)="exportReport()" [disabled]="actionLoading">
                    <mat-icon>description</mat-icon>
                    Export Report
                  </button>
                }
              </div>
            </mat-card-content>
          </mat-card>
        }

        <!-- Line Items Table -->
        <mat-card>
          <mat-card-content>
            <h2>Line Items</h2>
            <div class="table-container">
              <table mat-table [dataSource]="budget()?.lineItems ?? []">
                <ng-container matColumnDef="head">
                  <th mat-header-cell *matHeaderCellDef>Budget Head</th>
                  <td mat-cell *matCellDef="let item">{{ item.budgetHead }}</td>
                </ng-container>
                <ng-container matColumnDef="allocated">
                  <th mat-header-cell *matHeaderCellDef>Allocated</th>
                  <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.amountAllocated) }}</td>
                </ng-container>
                <ng-container matColumnDef="utilized">
                  <th mat-header-cell *matHeaderCellDef>Utilized</th>
                  <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.amountUtilized) }}</td>
                </ng-container>
                <ng-container matColumnDef="balance">
                  <th mat-header-cell *matHeaderCellDef>Balance</th>
                  <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.amountAllocated - item.amountUtilized) }}</td>
                </ng-container>
                <ng-container matColumnDef="percentage">
                  <th mat-header-cell *matHeaderCellDef>%</th>
                  <td mat-cell *matCellDef="let item" class="num">
                    {{ item.amountAllocated > 0 ? ((item.amountUtilized / item.amountAllocated * 100) | number:'1.1-1') + '%' : '0%' }}
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="lineItemColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: lineItemColumns"></tr>
              </table>
            </div>
          </mat-card-content>
        </mat-card>

        <!-- Utilization Summary -->
        @if (summary(); as s) {
          <mat-card>
            <mat-card-content>
              <h2>Utilization Summary</h2>
              <div class="summary-totals">
                <div class="summary-stat">
                  <span class="stat-label">Total Allocated</span>
                  <span class="stat-value">{{ formatAmount(s.totalAllocated) }}</span>
                </div>
                <div class="summary-stat">
                  <span class="stat-label">Total Utilized</span>
                  <span class="stat-value utilized">{{ formatAmount(s.totalUtilized) }}</span>
                </div>
                <div class="summary-stat">
                  <span class="stat-label">Total Balance</span>
                  <span class="stat-value" [class.negative]="s.totalBalance < 0">{{ formatAmount(s.totalBalance) }}</span>
                </div>
                <div class="summary-stat">
                  <span class="stat-label">Overall %</span>
                  <span class="stat-value">{{ s.overallUtilizationPercentage }}%</span>
                </div>
              </div>
              <div class="table-container">
                <table mat-table [dataSource]="s.headSummaries">
                  <ng-container matColumnDef="head">
                    <th mat-header-cell *matHeaderCellDef>Budget Head</th>
                    <td mat-cell *matCellDef="let item">{{ item.budgetHead }}</td>
                  </ng-container>
                  <ng-container matColumnDef="allocated">
                    <th mat-header-cell *matHeaderCellDef>Allocated</th>
                    <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.allocated) }}</td>
                  </ng-container>
                  <ng-container matColumnDef="utilized">
                    <th mat-header-cell *matHeaderCellDef>Utilized</th>
                    <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.utilized) }}</td>
                  </ng-container>
                  <ng-container matColumnDef="balance">
                    <th mat-header-cell *matHeaderCellDef>Balance</th>
                    <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.balance) }}</td>
                  </ng-container>
                  <ng-container matColumnDef="percentage">
                    <th mat-header-cell *matHeaderCellDef>%</th>
                    <td mat-cell *matCellDef="let item" class="num">{{ item.utilizationPercentage }}%</td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="summaryColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: summaryColumns"></tr>
                </table>
              </div>
            </mat-card-content>
          </mat-card>
        }

        <!-- Recent Utilization Entries -->
        <mat-card>
          <mat-card-content>
            <h2>Recent Utilization Entries</h2>
            @if (utilizations.loading) {
              <div class="skeleton-list">
                @for (row of [1, 2, 3]; track row) {
                  <div class="skeleton skeleton-text"></div>
                }
              </div>
            } @else if (utilizations.items.length === 0) {
              <div class="empty-state small">
                <p>No utilization entries recorded yet.</p>
              </div>
            } @else {
              <div class="table-container">
                <table mat-table [dataSource]="utilizations.items">
                  <ng-container matColumnDef="date">
                    <th mat-header-cell *matHeaderCellDef>Date</th>
                    <td mat-cell *matCellDef="let item">{{ item.utilizationDate }}</td>
                  </ng-container>
                  <ng-container matColumnDef="head">
                    <th mat-header-cell *matHeaderCellDef>Budget Head</th>
                    <td mat-cell *matCellDef="let item">{{ item.budgetHead }}</td>
                  </ng-container>
                  <ng-container matColumnDef="amount">
                    <th mat-header-cell *matHeaderCellDef>Amount</th>
                    <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.amountUtilized) }}</td>
                  </ng-container>
                  <ng-container matColumnDef="description">
                    <th mat-header-cell *matHeaderCellDef>Description</th>
                    <td mat-cell *matCellDef="let item">{{ item.description }}</td>
                  </ng-container>
                  <ng-container matColumnDef="case">
                    <th mat-header-cell *matHeaderCellDef>Case</th>
                    <td mat-cell *matCellDef="let item">{{ item.caseCrimeNumber ?? '—' }}</td>
                  </ng-container>
                  <ng-container matColumnDef="created">
                    <th mat-header-cell *matHeaderCellDef>Created At</th>
                    <td mat-cell *matCellDef="let item">{{ item.createdAtUtc | date:'medium' }}</td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="utilizationColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: utilizationColumns"></tr>
                </table>
              </div>
            }
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: `
    .detail-page { max-width: 1200px; margin: 0 auto; padding: 1.5rem; }
    .back-link { display: inline-flex; align-items: center; gap: 0.25rem; color: var(--mat-sys-primary, #0D6E6E); text-decoration: none; font-size: 0.875rem; margin-bottom: 1rem; }
    .back-link:hover { text-decoration: underline; }
    .header-grid { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    .header-grid h1 { margin: 0; font-size: 1.5rem; font-weight: 500; }
    .subtitle { margin: 0.25rem 0 0; color: var(--mat-sys-on-surface-variant, #475467); font-size: 0.875rem; }
    .header-info { text-align: right; }
    .created-by { margin: 0.25rem 0 0; font-size: 0.8125rem; color: var(--mat-sys-on-surface-variant, #475467); }
    .notes { margin: 0.5rem 0 0; font-size: 0.875rem; color: var(--mat-sys-on-surface-variant, #475467); max-width: 300px; }
    .status-badge {
      display: inline-block; padding: 0.25rem 0.75rem; border-radius: 1rem;
      font-size: 0.8125rem; font-weight: 500;
      background: color-mix(in srgb, var(--status-color, #9E9E9E) 15%, transparent);
      color: var(--status-color, #9E9E9E);
    }
    .actions-card { margin-top: 1rem; }
    .action-buttons { display: flex; gap: 0.75rem; }
    mat-card { margin-bottom: 1rem; }
    mat-card h2 { margin: 0 0 0.75rem; font-size: 1rem; font-weight: 500; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .table-container { overflow-x: auto; }
    table { width: 100%; }
    .summary-totals { display: flex; gap: 2rem; margin-bottom: 1rem; padding: 1rem; background: #F9FAFB; border-radius: 0.5rem; }
    .summary-stat { display: flex; flex-direction: column; }
    .stat-label { font-size: 0.75rem; color: var(--mat-sys-on-surface-variant, #475467); }
    .stat-value { font-size: 1.125rem; font-weight: 600; font-variant-numeric: tabular-nums; }
    .stat-value.utilized { color: #1976D2; }
    .stat-value.negative { color: #D32F2F; }
    .error-banner {
      display: flex; align-items: center; gap: 0.75rem;
      padding: 0.75rem 1rem; background: #FEF2F2; color: #991B1B;
      border-radius: 0.5rem; margin-bottom: 1rem;
    }
    .error-banner span { flex: 1; }
    .empty-state { display: flex; flex-direction: column; align-items: center; gap: 0.75rem; padding: 3rem 1rem; text-align: center; color: var(--mat-sys-on-surface-variant, #475467); }
    .empty-state mat-icon { font-size: 3rem; width: 3rem; height: 3rem; opacity: 0.4; }
    .empty-state.small { padding: 1.5rem; }
    .skeleton-detail { display: flex; flex-direction: column; gap: 0.75rem; }
    .skeleton-title { width: 40%; height: 1.5rem; background: #E5E7EB; border-radius: 0.25rem; }
    .skeleton-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .skeleton { background: #E5E7EB; border-radius: 0.25rem; height: 1rem; }
    .skeleton-text--medium { width: 60%; }
  `,
})
export class BudgetDetailPageComponent implements OnInit {
  private readonly api = inject(BudgetsApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  readonly budget: WritableSignal<BudgetDetailDto | null> = signal(null);
  readonly summary: WritableSignal<BudgetUtilizationSummaryDto | null> = signal(null);
  readonly loading: WritableSignal<boolean> = signal(true);
  readonly notFound: WritableSignal<boolean> = signal(false);
  readonly errorMessage: WritableSignal<string | null> = signal(null);

  readonly lineItemColumns = ['head', 'allocated', 'utilized', 'balance', 'percentage'];
  readonly summaryColumns = ['head', 'allocated', 'utilized', 'balance', 'percentage'];
  readonly utilizationColumns = ['date', 'head', 'amount', 'description', 'case', 'created'];

  utilizations: { items: BudgetUtilizationListDto[]; loading: boolean } = { items: [], loading: true };

  actionLoading = false;

  private budgetId = '';

  formatFinancialYear = formatFinancialYear;
  formatAmount = formatAmount;
  statusLabel(status: string): string {
    return BUDGET_APPROVAL_STATUS_LABELS[status] ?? status;
  }
  statusColor(status: string): string {
    return BUDGET_STATUS_COLORS[status] ?? '#9E9E9E';
  }

  ngOnInit(): void {
    this.budgetId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.budgetId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.loadDetail();
  }

  async loadDetail(): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.errorMessage.set(null);

    try {
      const [detail, summary, utilResult] = await Promise.all([
        this.api.getById(this.budgetId),
        this.api.getUtilizationSummary(this.budgetId),
        this.api.listUtilizations(this.budgetId, 1, 20),
      ]);
      this.budget.set(detail);
      this.summary.set(summary);
      this.utilizations = { items: utilResult.items, loading: false };
    } catch (error) {
      const httpStatus = this.api.getHttpStatus(error);
      if (httpStatus === 404) {
        this.notFound.set(true);
      } else {
        this.errorMessage.set(this.api.extractErrorMessage(error));
      }
    } finally {
      this.loading.set(false);
    }
  }

  private get role(): AppRole | undefined {
    const r = this.auth.currentUser()?.role;
    return r as AppRole | undefined;
  }

  get showActions(): boolean {
    const b = this.budget();
    if (!b) return false;
    return this.showPropose() || this.showApproveReturn() || this.showExecute();
  }

  showPropose(): boolean {
    return this.budget()?.approvalStatus === 'Draft'
      && (this.role === AppRole.Accountant || this.role === AppRole.Director);
  }

  showApproveReturn(): boolean {
    return this.budget()?.approvalStatus === 'Proposed' && this.role === AppRole.Director;
  }

  showExecute(): boolean {
    return this.budget()?.approvalStatus === 'Approved'
      && (this.role === AppRole.Accountant || this.role === AppRole.Director);
  }

  get isDirector(): boolean {
    return this.role === AppRole.Director;
  }

  async exportReport(): Promise<void> {
    await firstValueFrom(
      this.dialog.open(BudgetReportDialogComponent, { width: '420px' }).afterClosed(),
    );
  }

  async propose(): Promise<void> {
    this.actionLoading = true;
    try {
      await this.api.propose(this.budgetId);
      await this.loadDetail();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.actionLoading = false;
    }
  }

  async approve(): Promise<void> {
    const b = this.budget();
    if (!b) return;
    const result = await firstValueFrom(
      this.dialog.open(BudgetApproveDialogComponent, {
        data: { mode: 'approve', budgetSource: b.source },
        width: '420px',
      }).afterClosed(),
    );
    if (!result) return;
    this.actionLoading = true;
    try {
      await this.api.approve(this.budgetId, result.comment);
      await this.loadDetail();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.actionLoading = false;
    }
  }

  async returnBudget(): Promise<void> {
    const b = this.budget();
    if (!b) return;
    const result = await firstValueFrom(
      this.dialog.open(BudgetApproveDialogComponent, {
        data: { mode: 'return', budgetSource: b.source },
        width: '420px',
      }).afterClosed(),
    );
    if (!result) return;
    this.actionLoading = true;
    try {
      await this.api.returnBudget(this.budgetId, result.comment);
      await this.loadDetail();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.actionLoading = false;
    }
  }

  async execute(): Promise<void> {
    this.actionLoading = true;
    try {
      await this.api.execute(this.budgetId);
      await this.loadDetail();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.actionLoading = false;
    }
  }
}
