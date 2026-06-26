import { Component, DestroyRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthSessionService } from '../../../../core/auth/auth-session.service';
import { AdminUserService } from '../../services/admin-user.service';
import { AdminUserSummary, getUserStatus } from '../../models/admin.models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { UserDetailSheetComponent } from '../../components/user-detail-sheet/user-detail-sheet.component';

interface RoleOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-team-roster',
  imports: [
    CommonModule, FormsModule,
    MatTableModule, MatSortModule, MatPaginatorModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatChipsModule, MatMenuModule, MatTooltipModule,
    MatProgressSpinnerModule,
    StatusBadgeComponent,
    UserDetailSheetComponent,
  ],
  template: `
    <div class="roster-header">
      <h1>Team Roster</h1>
    </div>

    <div class="filters-row">
      <mat-form-field appearance="outline" class="search-field">
        <mat-label>Search name or email</mat-label>
        <input
          matInput
          [ngModel]="searchTerm()"
          (ngModelChange)="onSearchChange($event)"
          placeholder="Type to search..."
        />
        @if (searchTerm()) {
          <button matSuffix mat-icon-button aria-label="Clear search" (click)="clearSearch()">
            <mat-icon>close</mat-icon>
          </button>
        }
        <mat-icon matPrefix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>Role</mat-label>
        <mat-select
          multiple
          [ngModel]="selectedRoles()"
          (ngModelChange)="onRoleChange($event)"
        >
          @for (r of roleOptions; track r.value) {
            <mat-option [value]="r.value">{{ r.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>Status</mat-label>
        <mat-select
          [ngModel]="selectedStatus()"
          (ngModelChange)="onStatusChange($event)"
        >
          <mat-option value="">All</mat-option>
          <mat-option value="active">Active</mat-option>
          <mat-option value="suspended">Suspended</mat-option>
          <mat-option value="deleted">Deleted</mat-option>
        </mat-select>
      </mat-form-field>
    </div>

    @if (loading()) {
      <div class="loading-overlay">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    }

    @if (error()) {
      <div class="error-state">
        <mat-icon>error_outline</mat-icon>
        <p>Something went wrong loading users.</p>
        <button mat-stroked-button (click)="loadUsers()">Retry</button>
      </div>
    }

    @if (!loading() && !error() && !hasItems()) {
      <div class="empty-state">
        <mat-icon>people_outline</mat-icon>
        <p>No matching users. Try adjusting your filters.</p>
      </div>
    }

    @if (!error() && hasItems()) {
      <div class="table-container">
        <table
          mat-table
          [dataSource]="users()"
          matSort
          (matSortChange)="onSortChange($event)"
          [matSortActive]="sortBy()"
          [matSortDirection]="sortDesc() ? 'desc' : 'asc'"
          class="roster-table"
        >
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
            <td mat-cell *matCellDef="let u">{{ u.firstName }} {{ u.lastName }}</td>
          </ng-container>

          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Email</th>
            <td mat-cell *matCellDef="let u">{{ u.email }}</td>
          </ng-container>

          <ng-container matColumnDef="role">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Role</th>
            <td mat-cell *matCellDef="let u">
              <span class="role-pill">{{ u.role }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
            <td mat-cell *matCellDef="let u">
              <app-status-badge [status]="getUserStatus(u)" />
            </td>
          </ng-container>

          <ng-container matColumnDef="createdAtUtc">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Created Date</th>
            <td mat-cell *matCellDef="let u">{{ u.createdAtUtc | date:'mediumDate' }}</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let u">
              <button mat-icon-button [matMenuTriggerFor]="menu" aria-label="User actions">
                <mat-icon>more_vert</mat-icon>
              </button>
              <mat-menu #menu="matMenu">
                <button mat-menu-item (click)="openDetailSheet(u)">
                  <mat-icon>person</mat-icon>
                  <span>View Details</span>
                </button>
                @if (!u.isSuspended && getUserStatus(u) !== 'deleted') {
                  <button
                    mat-menu-item
                    (click)="suspendUser(u)"
                    [disabled]="isCurrentUser(u) || isLastDirectorUser(u)"
                    [matTooltip]="isCurrentUser(u) ? 'You cannot suspend your own account.' : isLastDirectorUser(u) ? 'At least one Director must remain active. Promote another user to Director first.' : ''"
                  >
                    <mat-icon>block</mat-icon>
                    <span>Suspend</span>
                  </button>
                }
                @if (u.isSuspended && getUserStatus(u) !== 'deleted') {
                  <button mat-menu-item (click)="reactivateUser(u)" [disabled]="isCurrentUser(u)">
                    <mat-icon>check_circle</mat-icon>
                    <span>Reactivate</span>
                  </button>
                }
              </mat-menu>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr
            mat-row
            *matRowDef="let row; columns: displayedColumns; trackBy: trackByUserId"
            [class.clickable-row]="true"
            (click)="openDetailSheet(row)"
            role="button"
            tabindex="0"
            (keydown.enter)="openDetailSheet(row)"
          ></tr>
        </table>

        <mat-paginator
          [length]="totalCount()"
          [pageSize]="pageSize()"
          [pageIndex]="page() - 1"
          (page)="onPageChange($event)"
          [pageSizeOptions]="[10, 25, 50]"
          showFirstLastButtons
        >
        </mat-paginator>
      </div>
    }

    @if (selectedUser(); as u) {
      <app-user-detail-sheet
        (closed)="onDetailSheetClosed()"
        (suspended)="onUserSuspended($event)"
        (reactivated)="onUserReactivated($event)"
        (deleted)="onUserDeleted($event)"
      />
    }
  `,
  styles: `
    .roster-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .roster-header h1 { margin: 0; font-size: 24px; font-weight: 500; }
    .filters-row { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }
    .search-field { flex: 1; min-width: 240px; }
    .filter-field { width: 180px; }
    .loading-overlay { display: flex; justify-content: center; padding: 48px; }
    .empty-state { text-align: center; padding: 48px; color: #6B7280; }
    .empty-state mat-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 12px; }
    .error-state { text-align: center; padding: 48px; color: #991B1B; }
    .error-state mat-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 12px; }
    .error-state button { margin-top: 8px; }
    .table-container { position: relative; overflow-x: auto; }
    .roster-table { width: 100%; }
    .roster-table th { background: #F5F6FA; color: #697586; font-size: 12px; text-transform: uppercase; letter-spacing: 0.05em; }
    .roster-table td { border-bottom: 1px solid #E2E5EB; }
    .roster-table tr:hover td { background: #F5F6FA; cursor: pointer; }
    .role-pill { display: inline-block; padding: 2px 10px; border-radius: 4px; font-size: 12px; background: #E8EDF5; color: #1B2A4A; }
  `,
})
export class TeamRosterComponent implements OnInit {
  private readonly adminUserService = inject(AdminUserService);
  private readonly authSessionService = inject(AuthSessionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchSubject = new Subject<string>();
  private currentRequest = 0;

  readonly selectedUser = signal<AdminUserSummary | null>(null);
  readonly detailSheet = viewChild(UserDetailSheetComponent);

  readonly users = signal<AdminUserSummary[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly error = signal(false);

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly searchTerm = signal('');
  readonly selectedRoles = signal<string[]>([]);
  readonly selectedStatus = signal('');
  readonly sortBy = signal('createdAtUtc');
  readonly sortDesc = signal(true);

  readonly displayedColumns = ['name', 'email', 'role', 'status', 'createdAtUtc', 'actions'];

  readonly roleOptions: RoleOption[] = [
    { value: 'Director', label: 'Director' },
    { value: 'Coordinator', label: 'Coordinator' },
    { value: 'SocialWorker', label: 'Social Worker' },
    { value: 'CaseWorker', label: 'Case Worker' },
    { value: 'Accountant', label: 'Accountant' },
    { value: 'Vendor', label: 'Vendor' },
  ];

  protected readonly getUserStatus = getUserStatus;

  constructor() {
    const sub = this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
    ).subscribe(() => {
      this.page.set(1);
      this.loadUsers();
    });
    this.destroyRef.onDestroy(() => sub.unsubscribe());
  }

  async ngOnInit(): Promise<void> {
    await this.loadUsers();
  }

  hasItems(): boolean {
    return this.users().length > 0;
  }

  trackByUserId(_: number, u: AdminUserSummary): string {
    return u.id;
  }

  onSearchChange(value: string): void {
    const trimmed = value.trim();
    this.searchTerm.set(value);
    this.searchSubject.next(trimmed);
  }

  clearSearch(): void {
    this.searchTerm.set('');
    this.page.set(1);
    this.loadUsers();
  }

  onRoleChange(value: string[]): void {
    this.selectedRoles.set(value);
    this.page.set(1);
    this.loadUsers();
  }

  onStatusChange(value: string): void {
    this.selectedStatus.set(value);
    this.page.set(1);
    this.loadUsers();
  }

  onSortChange(sort: Sort): void {
    if (!sort.direction) return;
    this.sortBy.set(sort.active);
    this.sortDesc.set(sort.direction === 'desc');
    this.page.set(1);
    this.loadUsers();
  }

  async onPageChange(event: PageEvent): Promise<void> {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    await this.loadUsers();
  }

  async loadUsers(): Promise<void> {
    const requestId = ++this.currentRequest;
    this.error.set(false);
    this.loading.set(true);
    try {
      const result = await this.adminUserService.getUsers(
        this.page(),
        this.pageSize(),
        this.searchTerm() || undefined,
        this.selectedRoles().length > 0 ? this.selectedRoles().join(',') : undefined,
        this.selectedStatus() || undefined,
        this.sortBy(),
        this.sortDesc(),
      );
      if (requestId !== this.currentRequest) return;
      this.users.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch {
      if (requestId !== this.currentRequest) return;
      this.users.set([]);
      this.totalCount.set(0);
      this.error.set(true);
    } finally {
      if (requestId === this.currentRequest) {
        this.loading.set(false);
      }
    }
  }

  openDetailSheet(user: AdminUserSummary): void {
    this.selectedUser.set(user);
    const currentUser = this.authSessionService.currentUser();

    const isLastDirector = user.role === 'Director' &&
      !this.users().some(u =>
        u.id !== user.id &&
        u.role === 'Director' &&
        getUserStatus(u) === 'active'
      );

    Promise.resolve().then(() => {
      const sheet = this.detailSheet();
      if (sheet) {
        sheet.setData({
          user,
          currentUserId: currentUser?.id ?? null,
          isLastDirector,
        });
      }
    });

    // Fire-and-forget server check for authoritative isLastDirector value
    this.adminUserService.isLastDirector(user.id).then(serverIsLastDirector => {
      const sheet = this.detailSheet();
      if (sheet) {
        sheet.setData({
          user,
          currentUserId: currentUser?.id ?? null,
          isLastDirector: serverIsLastDirector,
        });
      }
    }).catch(() => {
      // Server check failed — client-side computation was the initial value, keep it
    });
  }

  onDetailSheetClosed(): void {
    this.selectedUser.set(null);
  }

  onUserSuspended(_userId: string): void {
    this.loadUsers();
  }

  onUserReactivated(_userId: string): void {
    this.loadUsers();
  }

  onUserDeleted(_userId: string): void {
    this.loadUsers();
  }

  isCurrentUser(user: AdminUserSummary): boolean {
    return this.authSessionService.currentUser()?.id === user.id;
  }

  isLastDirectorUser(user: AdminUserSummary): boolean {
    // If we only see one page of results, the client-side computation is unreliable —
    // return false (conservative: don't block based on incomplete data) and let the
    // server authoritative check handle it.
    if (this.users().length < this.totalCount()) {
      return false;
    }
    return user.role === 'Director' &&
      !this.users().some(u =>
        u.id !== user.id &&
        u.role === 'Director' &&
        getUserStatus(u) === 'active'
      );
  }

  suspendUser(user: AdminUserSummary): void {
    this.openDetailSheet(user);
  }

  reactivateUser(user: AdminUserSummary): void {
    this.openDetailSheet(user);
  }
}
