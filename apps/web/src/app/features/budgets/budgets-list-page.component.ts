import { Component, WritableSignal, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { AppRole } from '@midi-kaval/shared-types';
import { BudgetsApiService } from './services/budgets-api.service';
import {
  BudgetListDto,
  BUDGET_APPROVAL_STATUS_LABELS,
  BUDGET_STATUS_COLORS,
  formatAmount,
  formatFinancialYear,
} from './budget.models';
import { BudgetCreateDialogComponent } from './budget-create-dialog.component';
import { BudgetEditDialogComponent } from './budget-edit-dialog.component';
import { BudgetApproveDialogComponent } from './budget-approve-dialog.component';
import { BudgetReportDialogComponent } from './budget-report-dialog.component';

@Component({
  selector: 'app-budgets-list-page',
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatPaginatorModule,
    MatTooltipModule,
  ],
  template: `
    <div class="budgets-page">
      <header class="page-header">
        <div>
          <h1>Budgets</h1>
          <p class="subtitle">Manage project budgets and track expenditure</p>
        </div>
        @if (canCreate) {
          <button mat-flat-button color="primary" (click)="create()">
            <mat-icon>add</mat-icon>
            Create Budget
          </button>
        }
        @if (isDirector) {
          <button mat-stroked-button color="primary" (click)="exportReport()">
            <mat-icon>description</mat-icon>
            Export Report
          </button>
        }
      </header>

      @if (errorMessage()) {
        <div class="error-banner" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage() }}</span>
          <button mat-stroked-button (click)="loadBudgets()">Retry</button>
        </div>
      }

      <mat-card>
        <mat-card-content>
          @if (loading()) {
            <div class="skeleton-list" aria-label="Loading budgets">
              @for (row of [1, 2, 3, 4, 5]; track row) {
                <div class="skeleton-row">
                  <div class="skeleton skeleton-text skeleton-text--short"></div>
                  <div class="skeleton skeleton-text"></div>
                  <div class="skeleton skeleton-text skeleton-text--medium"></div>
                  <div class="skeleton skeleton-text skeleton-text--short"></div>
                  <div class="skeleton skeleton-text"></div>
                </div>
              }
            </div>
          } @else if (items().length === 0) {
            <div class="empty-state">
              <mat-icon>account_balance</mat-icon>
              <p>No budgets yet</p>
              @if (canCreate) {
                <button mat-stroked-button color="primary" (click)="create()">
                  <mat-icon>add</mat-icon>
                  Create first budget
                </button>
              }
            </div>
          } @else {
            <div class="table-container">
              <table mat-table [dataSource]="items()">
                <ng-container matColumnDef="source">
                  <th mat-header-cell *matHeaderCellDef>Source</th>
                  <td mat-cell *matCellDef="let item">{{ item.source }}</td>
                </ng-container>

                <ng-container matColumnDef="financialYear">
                  <th mat-header-cell *matHeaderCellDef>Financial Year</th>
                  <td mat-cell *matCellDef="let item">
                    {{ formatFinancialYear(item.financialYearStart, item.financialYearEnd) }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let item">
                    <span class="status-badge" [style.--status-color]="statusColor(item.approvalStatus)">
                      {{ statusLabel(item.approvalStatus) }}
                    </span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="allocated">
                  <th mat-header-cell *matHeaderCellDef>Allocated</th>
                  <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.totalAllocated) }}</td>
                </ng-container>

                <ng-container matColumnDef="utilized">
                  <th mat-header-cell *matHeaderCellDef>Utilized</th>
                  <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.totalUtilized) }}</td>
                </ng-container>

                <ng-container matColumnDef="balance">
                  <th mat-header-cell *matHeaderCellDef>Balance</th>
                  <td mat-cell *matCellDef="let item" class="num">{{ formatAmount(item.totalAllocated - item.totalUtilized) }}</td>
                </ng-container>

                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef>Actions</th>
                  <td mat-cell *matCellDef="let item">
                    <button mat-icon-button [attr.aria-label]="'View budget ' + item.source" (click)="view(item)" matTooltip="View">
                      <mat-icon>visibility</mat-icon>
                    </button>
                    @if (canEdit(item)) {
                      <button mat-icon-button [attr.aria-label]="'Edit budget ' + item.source" (click)="edit(item)" matTooltip="Edit">
                        <mat-icon>edit</mat-icon>
                      </button>
                    }
                    @if (canPropose(item)) {
                      <button mat-icon-button [attr.aria-label]="'Propose budget ' + item.source" (click)="propose(item)" matTooltip="Propose">
                        <mat-icon>send</mat-icon>
                      </button>
                    }
                    @if (canApprove(item)) {
                      <button mat-icon-button [attr.aria-label]="'Approve budget ' + item.source" (click)="approve(item)" matTooltip="Approve">
                        <mat-icon>check_circle</mat-icon>
                      </button>
                      <button mat-icon-button [attr.aria-label]="'Return budget ' + item.source" (click)="returnBudget(item)" matTooltip="Return">
                        <mat-icon>undo</mat-icon>
                      </button>
                    }
                    @if (canExecute(item)) {
                      <button mat-icon-button [attr.aria-label]="'Execute budget ' + item.source" (click)="execute(item)" matTooltip="Execute">
                        <mat-icon>play_arrow</mat-icon>
                      </button>
                    }
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns" [style.cursor]="'pointer'" (click)="view(row)"></tr>
              </table>
            </div>

            <mat-paginator
              [length]="totalCount()"
              [pageSize]="pageSize()"
              [pageIndex]="currentPage() - 1"
              [pageSizeOptions]="[10, 25, 50, 100]"
              [disabled]="loading()"
              (page)="onPageChange($event)"
              showFirstLastButtons
              aria-label="Budget list pagination"
            />
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .budgets-page { max-width: 1200px; margin: 0 auto; padding: 1.5rem; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1.5rem; }
    .page-header h1 { margin: 0; font-size: 1.5rem; font-weight: 500; }
    .subtitle { color: var(--mat-sys-on-surface-variant, #475467); margin: 0.25rem 0 0; font-size: 0.875rem; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .status-badge {
      display: inline-block;
      padding: 0.125rem 0.5rem;
      border-radius: 1rem;
      font-size: 0.75rem;
      font-weight: 500;
      background: color-mix(in srgb, var(--status-color, #9E9E9E) 15%, transparent);
      color: var(--status-color, #9E9E9E);
    }
    .error-banner {
      display: flex; align-items: center; gap: 0.75rem;
      padding: 0.75rem 1rem;
      background: #FEF2F2; color: #991B1B;
      border-radius: 0.5rem; margin-bottom: 1rem;
    }
    .error-banner mat-icon { flex-shrink: 0; }
    .error-banner span { flex: 1; }
    .skeleton-list { display: flex; flex-direction: column; gap: 0.75rem; padding: 0.5rem 0; }
    .skeleton-row { display: flex; gap: 1rem; }
    .skeleton { background: #E5E7EB; border-radius: 0.25rem; height: 1rem; }
    .skeleton-text--short { width: 20%; }
    .skeleton-text--medium { width: 40%; }
    .skeleton-text--long { width: 60%; }
    .empty-state {
      display: flex; flex-direction: column; align-items: center; gap: 0.75rem;
      padding: 3rem 1rem; color: var(--mat-sys-on-surface-variant, #475467);
    }
    .empty-state mat-icon { font-size: 3rem; width: 3rem; height: 3rem; opacity: 0.4; }
    .table-container { overflow-x: auto; }
    table { width: 100%; }
    .mat-mdc-row:hover { background: #F9FAFB; }
  `,
})
export class BudgetsListPageComponent {
  private readonly api = inject(BudgetsApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly items: WritableSignal<BudgetListDto[]> = signal([]);
  readonly loading: WritableSignal<boolean> = signal(false);
  readonly errorMessage: WritableSignal<string | null> = signal(null);
  readonly totalCount: WritableSignal<number> = signal(0);
  readonly currentPage: WritableSignal<number> = signal(1);
  readonly pageSize: WritableSignal<number> = signal(25);

  readonly displayedColumns = ['source', 'financialYear', 'status', 'allocated', 'utilized', 'balance', 'actions'];

  constructor() {
    this.loadBudgets();
  }

  formatFinancialYear = formatFinancialYear;
  formatAmount = formatAmount;
  statusLabel(status: string): string {
    return BUDGET_APPROVAL_STATUS_LABELS[status] ?? status;
  }
  statusColor(status: string): string {
    return BUDGET_STATUS_COLORS[status] ?? '#9E9E9E';
  }

  private get role(): AppRole | undefined {
    const r = this.auth.currentUser()?.role;
    return r as AppRole | undefined;
  }

  canEdit(item: BudgetListDto): boolean {
    return (item.approvalStatus === 'Draft' || item.approvalStatus === 'Returned')
      && (this.role === AppRole.Director || this.role === AppRole.Accountant);
  }

  canPropose(item: BudgetListDto): boolean {
    return item.approvalStatus === 'Draft'
      && (this.role === AppRole.Accountant || this.role === AppRole.Director);
  }

  canApprove(item: BudgetListDto): boolean {
    return item.approvalStatus === 'Proposed' && this.role === AppRole.Director;
  }

  canExecute(item: BudgetListDto): boolean {
    return item.approvalStatus === 'Approved'
      && (this.role === AppRole.Accountant || this.role === AppRole.Director);
  }

  get canCreate(): boolean {
    return this.role === AppRole.Accountant || this.role === AppRole.Director;
  }

  get isDirector(): boolean {
    return this.role === AppRole.Director;
  }

  async loadBudgets(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const result = await this.api.list(this.currentPage(), this.pageSize());
      this.items.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  view(item: BudgetListDto): void {
    this.router.navigate(['/budgets', item.id]);
  }

  async create(): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(BudgetCreateDialogComponent, { width: '600px' }).afterClosed(),
    );
    if (result) {
      await this.loadBudgets();
    }
  }

  async edit(item: BudgetListDto): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(BudgetEditDialogComponent, {
        data: { budgetId: item.id },
        width: '600px',
      }).afterClosed(),
    );
    if (result) {
      await this.loadBudgets();
    }
  }

  async propose(item: BudgetListDto): Promise<void> {
    this.errorMessage.set(null);
    try {
      await this.api.propose(item.id);
      await this.loadBudgets();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async approve(item: BudgetListDto): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(BudgetApproveDialogComponent, {
        data: { mode: 'approve', budgetSource: item.source },
        width: '420px',
      }).afterClosed(),
    );
    if (!result) return;
    this.errorMessage.set(null);
    try {
      await this.api.approve(item.id, result.comment);
      await this.loadBudgets();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async returnBudget(item: BudgetListDto): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(BudgetApproveDialogComponent, {
        data: { mode: 'return', budgetSource: item.source },
        width: '420px',
      }).afterClosed(),
    );
    if (!result) return;
    this.errorMessage.set(null);
    try {
      await this.api.returnBudget(item.id, result.comment);
      await this.loadBudgets();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async execute(item: BudgetListDto): Promise<void> {
    this.errorMessage.set(null);
    try {
      await this.api.execute(item.id);
      await this.loadBudgets();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  onPageChange(event: PageEvent): void {
    this.currentPage.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadBudgets();
  }

  async exportReport(): Promise<void> {
    await firstValueFrom(
      this.dialog.open(BudgetReportDialogComponent, { width: '420px' }).afterClosed(),
    );
  }
}
