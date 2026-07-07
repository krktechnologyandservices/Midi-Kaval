import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { AppRole } from '@midi-kaval/shared-types';
import { BudgetsApiService } from './services/budgets-api.service';
import { BudgetDetailDto, BudgetUtilizationListDto, formatAmount } from './budget.models';
import { BudgetRecordUtilizationDialogComponent } from './budget-record-utilization-dialog.component';

function toLocalDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

@Component({
  selector: 'app-budget-utilizations-page',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatTooltipModule,
  ],
  template: `
    <div class="utilizations-page">
      <a class="back-link" [routerLink]="['/budgets', budgetId]">
        <mat-icon>arrow_back</mat-icon>
        Back to budget
      </a>

      <div class="page-header">
        <h1>Utilization Ledger</h1>
        @if (canRecord()) {
          <button mat-flat-button color="primary" (click)="recordUtilization()">
            <mat-icon>receipt_long</mat-icon>
            Record Utilization
          </button>
        }
      </div>

      <mat-card class="filters-card">
        <mat-card-content>
          <div class="filters-row">
            <mat-form-field appearance="outline" class="filter-field">
              <mat-label>From date</mat-label>
              <input matInput [matDatepicker]="fromPicker" [(ngModel)]="fromDate" (ngModelChange)="onFilterChange()" />
              <mat-datepicker-toggle matSuffix [for]="fromPicker" />
              <mat-datepicker #fromPicker />
            </mat-form-field>
            <mat-form-field appearance="outline" class="filter-field">
              <mat-label>To date</mat-label>
              <input matInput [matDatepicker]="toPicker" [(ngModel)]="toDate" (ngModelChange)="onFilterChange()" />
              <mat-datepicker-toggle matSuffix [for]="toPicker" />
              <mat-datepicker #toPicker />
            </mat-form-field>
            @if (fromDate || toDate) {
              <button mat-button (click)="clearFilters()">Clear filters</button>
            }
          </div>
        </mat-card-content>
      </mat-card>

      @if (errorMessage) {
        <div class="error-banner" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage }}</span>
        </div>
      }

      <mat-card>
        <mat-card-content>
          @if (loading) {
            <div class="skeleton-list">
              @for (row of [1, 2, 3, 4]; track row) {
                <div class="skeleton skeleton-text"></div>
              }
            </div>
          } @else if (items.length === 0) {
            <div class="empty-state">
              <mat-icon>receipt_long</mat-icon>
              <p>No utilization entries {{ fromDate || toDate ? 'in this date range' : 'recorded yet' }}.</p>
            </div>
          } @else {
            <div class="table-container">
              <table mat-table [dataSource]="items">
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
                <ng-container matColumnDef="receipt">
                  <th mat-header-cell *matHeaderCellDef>Receipt</th>
                  <td mat-cell *matCellDef="let item">
                    @if (item.attachments?.length > 0) {
                      <mat-icon class="receipt-icon" matTooltip="Receipt attached">description</mat-icon>
                    } @else {
                      —
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="created">
                  <th mat-header-cell *matHeaderCellDef>Created At</th>
                  <td mat-cell *matCellDef="let item">{{ item.createdAtUtc | date:'medium' }}</td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let item">
                    @if (canRecord()) {
                      <button mat-icon-button (click)="editUtilization(item)" aria-label="Edit">
                        <mat-icon>edit</mat-icon>
                      </button>
                      <button mat-icon-button color="warn" (click)="deleteUtilization(item)" aria-label="Delete">
                        <mat-icon>delete</mat-icon>
                      </button>
                    }
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr mat-row *matRowDef="let row; columns: columns"></tr>
              </table>
            </div>

            <mat-paginator
              [length]="totalCount"
              [pageSize]="pageSize"
              [pageIndex]="page - 1"
              [pageSizeOptions]="[20, 50, 100]"
              (page)="onPageChange($event)"
            />
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .utilizations-page { max-width: 1200px; margin: 0 auto; padding: 1.5rem; }
    .back-link { display: inline-flex; align-items: center; gap: 0.25rem; color: var(--mat-sys-primary, #0D6E6E); text-decoration: none; font-size: 0.875rem; margin-bottom: 1rem; }
    .back-link:hover { text-decoration: underline; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .page-header h1 { margin: 0; font-size: 1.5rem; font-weight: 500; }
    .filters-card { margin-bottom: 1rem; }
    .filters-row { display: flex; gap: 1rem; align-items: center; flex-wrap: wrap; }
    .filter-field { width: 200px; }
    mat-card { margin-bottom: 1rem; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .table-container { overflow-x: auto; }
    table { width: 100%; }
    .receipt-icon { color: var(--mat-sys-primary, #0D6E6E); font-size: 1.125rem; width: 1.125rem; height: 1.125rem; vertical-align: middle; }
    .error-banner {
      display: flex; align-items: center; gap: 0.75rem;
      padding: 0.75rem 1rem; background: #FEF2F2; color: #991B1B;
      border-radius: 0.5rem; margin-bottom: 1rem;
    }
    .empty-state { display: flex; flex-direction: column; align-items: center; gap: 0.75rem; padding: 3rem 1rem; text-align: center; color: var(--mat-sys-on-surface-variant, #475467); }
    .empty-state mat-icon { font-size: 3rem; width: 3rem; height: 3rem; opacity: 0.4; }
    .skeleton-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .skeleton { background: #E5E7EB; border-radius: 0.25rem; height: 1rem; }
  `,
})
export class BudgetUtilizationsPageComponent implements OnInit {
  private readonly api = inject(BudgetsApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  readonly columns = ['date', 'head', 'amount', 'description', 'case', 'receipt', 'created', 'actions'];
  formatAmount = formatAmount;

  budgetId = '';
  budget: BudgetDetailDto | null = null;
  items: BudgetUtilizationListDto[] = [];
  loading = true;
  errorMessage = '';

  page = 1;
  pageSize = 20;
  totalCount = 0;

  fromDate: Date | null = null;
  toDate: Date | null = null;

  private get role(): AppRole | undefined {
    return this.auth.currentUser()?.role as AppRole | undefined;
  }

  canRecord(): boolean {
    return this.role === AppRole.Accountant || this.role === AppRole.Director;
  }

  ngOnInit(): void {
    this.budgetId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.budgetId) return;
    void this.loadBudget();
    void this.load();
  }

  private async loadBudget(): Promise<void> {
    try {
      this.budget = await this.api.getById(this.budgetId);
    } catch {
      // Budget header context is optional here — the list still loads independently.
    }
  }

  async load(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';
    try {
      const result = await this.api.listUtilizations(
        this.budgetId,
        this.page,
        this.pageSize,
        this.fromDate ? toLocalDateString(this.fromDate) : undefined,
        this.toDate ? toLocalDateString(this.toDate) : undefined,
      );
      this.items = result.items;
      this.totalCount = result.totalCount;
    } catch (error) {
      this.errorMessage = this.api.extractErrorMessage(error);
    } finally {
      this.loading = false;
    }
  }

  onFilterChange(): void {
    this.page = 1;
    void this.load();
  }

  clearFilters(): void {
    this.fromDate = null;
    this.toDate = null;
    this.page = 1;
    void this.load();
  }

  onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    void this.load();
  }

  async recordUtilization(): Promise<void> {
    if (!this.budget) return;
    const result = await firstValueFrom(
      this.dialog.open(BudgetRecordUtilizationDialogComponent, {
        data: { budgetId: this.budgetId, lineItems: this.budget.lineItems },
        width: '480px',
      }).afterClosed(),
    );
    if (!result) return;
    await Promise.all([this.loadBudget(), this.load()]);
  }

  async editUtilization(item: BudgetUtilizationListDto): Promise<void> {
    if (!this.budget) return;
    const result = await firstValueFrom(
      this.dialog.open(BudgetRecordUtilizationDialogComponent, {
        data: { budgetId: this.budgetId, lineItems: this.budget.lineItems, existing: item },
        width: '480px',
      }).afterClosed(),
    );
    if (!result) return;
    await Promise.all([this.loadBudget(), this.load()]);
  }

  async deleteUtilization(item: BudgetUtilizationListDto): Promise<void> {
    if (!confirm(`Remove this utilization entry (${this.formatAmount(item.amountUtilized)} on ${item.utilizationDate})? It will be hidden from reports but kept in the record; contact an admin to restore it.`)) {
      return;
    }
    try {
      // force=true takes the soft-delete path on the API (keeps the row, sets DeletedAtUtc) instead of
      // permanently removing the financial record — the UI should never trigger the irreversible path.
      await this.api.deleteUtilization(this.budgetId, item.id, true);
      await Promise.all([this.loadBudget(), this.load()]);
    } catch (error) {
      this.errorMessage = this.api.extractErrorMessage(error);
    }
  }
}
