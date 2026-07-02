import { Component, DestroyRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
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
  selector: 'app-confirm-reset-2fa-dialog',
  template: `
    <h2 mat-dialog-title>Reset Two-Factor Authentication</h2>
    <mat-dialog-content>
      <p>This will clear <strong>{{ data.userName }}</strong>'s two-factor authentication enrollment. They will need to re-enroll on their next login.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close cdkFocusInitial>Cancel</button>
      <button mat-raised-button color="warn" [mat-dialog-close]="true">Reset 2FA</button>
    </mat-dialog-actions>
  `,
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
})
export class ConfirmReset2faDialogComponent {
  readonly data = inject(MAT_DIALOG_DATA) as { userName: string; userId: string };
}

@Component({
  selector: 'app-bypass-code-dialog',
  template: `
    <h2 mat-dialog-title>Temporary Bypass Code</h2>
    <mat-dialog-content>
      <div class="code-display">{{ data.bypassCode }}</div>

      <div class="warning-banner">
        <mat-icon>warning</mat-icon>
        <span>This code expires in <strong>30 minutes</strong> and can only be used once. Share it securely with the user.</span>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-raised-button (click)="copyCode()" class="copy-btn">
        <mat-icon>{{ copied() ? 'check' : 'content_copy' }}</mat-icon>
        {{ copied() ? 'Copied!' : 'Copy Code' }}
      </button>
      <button mat-button mat-dialog-close color="primary">Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .code-display {
      font-family: 'SF Mono', 'Fira Code', monospace;
      font-size: 32px;
      letter-spacing: 0.15em;
      text-align: center;
      padding: 16px;
      background: #F5F6FA;
      border-radius: 8px;
      margin-bottom: 16px;
    }
    .warning-banner {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      background: #FFF3E0;
      padding: 12px;
      border-radius: 8px;
      color: #E65100;
      font-size: 14px;
    }
    .warning-banner mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
      flex-shrink: 0;
    }
    .copy-btn {
      display: flex;
      align-items: center;
      gap: 4px;
    }
  `,
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
})
export class BypassCodeDialogComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogRef = inject(MatDialogRef<BypassCodeDialogComponent>);
  readonly data = inject(MAT_DIALOG_DATA) as { bypassCode: string; expiresInSeconds: number };
  readonly copied = signal(false);
  private copyTimerId: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.copyTimerId) clearTimeout(this.copyTimerId);
    });
  }

  async copyCode(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.data.bypassCode);
      this.copied.set(true);
      if (this.copyTimerId) clearTimeout(this.copyTimerId);
      this.copyTimerId = setTimeout(() => {
        this.copied.set(false);
        this.copyTimerId = null;
      }, 2000);
    } catch {
      console.warn('Clipboard write failed. The page may not be served over HTTPS.');
    }
  }
}

@Component({
  selector: 'app-team-roster',
  imports: [
    CommonModule, FormsModule,
    MatTableModule, MatSortModule, MatPaginatorModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatChipsModule, MatMenuModule, MatTooltipModule,
    MatDialogModule, MatDividerModule, MatSnackBarModule,
    MatProgressSpinnerModule,
    StatusBadgeComponent,
    UserDetailSheetComponent,
    ConfirmReset2faDialogComponent,
    BypassCodeDialogComponent,
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

          <ng-container matColumnDef="2fa">
            <th mat-header-cell *matHeaderCellDef style="text-align:center;width:60px">2FA</th>
            <td mat-cell *matCellDef="let u" style="text-align:center;width:60px">
              @if (getUserStatus(u) === 'deleted' || getUserStatus(u) === 'suspended') {
                <span style="color:#ccc">—</span>
              } @else {
                <div
                  class="fa-cell"
                  [matTooltip]="getEnrollmentTooltip(u)"
                  [matMenuTriggerFor]="faMenu"
                  (click)="$event.stopPropagation()"
                >
                  @if (u.totpEnrolledAt) {
                    <mat-icon class="fa-icon fa-enrolled">check_circle</mat-icon>
                  } @else {
                    <mat-icon class="fa-icon fa-not-enrolled">cancel</mat-icon>
                  }
                </div>
                <mat-menu #faMenu="matMenu">
                  @if (u.totpEnrolledAt) {
                    <button
                      mat-menu-item
                      class="danger-item"
                      [disabled]="isLastDirectorUser(u)"
                      [matTooltip]="isLastDirectorUser(u) ? 'At least one Director must remain active.' : ''"
                      (click)="confirmReset2fa(u)"
                    >
                      <mat-icon class="danger-icon">security</mat-icon>
                      <span>Reset 2FA</span>
                    </button>
                    <mat-divider />
                    <button mat-menu-item (click)="generateBypassCode(u)">
                      <mat-icon>vpn_key</mat-icon>
                      <span>Generate Bypass Code</span>
                    </button>
                  } @else {
                    <button
                      mat-menu-item
                      class="danger-item"
                      [disabled]="isLastDirectorUser(u)"
                      [matTooltip]="isLastDirectorUser(u) ? 'At least one Director must remain active.' : ''"
                      (click)="confirmReset2fa(u)"
                    >
                      <mat-icon class="danger-icon">security</mat-icon>
                      <span>Reset 2FA</span>
                    </button>
                    <mat-divider />
                    <button mat-menu-item (click)="sendReminder(u)">
                      <mat-icon>notifications</mat-icon>
                      <span>Send Reminder</span>
                    </button>
                  }
                </mat-menu>
              }
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
    .fa-cell { display: inline-flex; align-items: center; justify-content: center; width: 40px; height: 40px; border-radius: 8px; cursor: pointer; }
    .fa-cell:hover { background: #f0f0f0; }
    .fa-icon { font-size: 20px; width: 20px; height: 20px; }
    .fa-enrolled { color: #2E7D32; }
    .fa-not-enrolled { color: #C62828; }
    .danger-item { color: #C62828; }
    .danger-icon { color: #C62828; }
  `,
})
export class TeamRosterComponent implements OnInit {
  private readonly adminUserService = inject(AdminUserService);
  private readonly authSessionService = inject(AuthSessionService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
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

  readonly displayedColumns = ['name', 'email', 'role', '2fa', 'status', 'createdAtUtc', 'actions'];

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

  getEnrollmentTooltip(user: AdminUserSummary): string {
    if (!user) return '';
    if (getUserStatus(user) === 'deleted' || getUserStatus(user) === 'suspended') return '';
    if (!user.totpEnrolledAt) return 'Not enrolled';
    const d = new Date(user.totpEnrolledAt);
    return isNaN(d.getTime()) ? 'Enrolled' : `Enrolled on ${d.toLocaleDateString()}`;
  }

  confirmReset2fa(user: AdminUserSummary): void {
    const dialogRef = this.dialog.open(ConfirmReset2faDialogComponent, {
      data: { userName: `${user.firstName} ${user.lastName}`, userId: user.id },
      width: '440px',
      disableClose: false,
    });

    dialogRef.afterClosed().subscribe(result => {
      if (!result) return;

      this.adminUserService.resetTwoFactor(user.id).then(() => {
        this.snackBar.open(
          `Two-factor authentication has been reset for ${user.firstName} ${user.lastName}.`,
          'Close',
          { duration: 4000 },
        );
        this.loadUsers();
      }).catch(err => {
        if (err instanceof HttpErrorResponse && err.status === 422) {
          this.snackBar.open(
            'Cannot reset 2FA for the last active Director.',
            'Close',
            { duration: 4000 },
          );
        } else {
          this.snackBar.open(
            'Failed to reset 2FA. Please try again.',
            'Close',
            { duration: 4000 },
          );
        }
      });
    });
  }

  async generateBypassCode(user: AdminUserSummary): Promise<void> {
    try {
      const result = await this.adminUserService.generateBypassCode(user.id);
      if (!result?.bypassCode) {
        this.snackBar.open('Failed to generate bypass code. Please try again.', 'Close', { duration: 4000 });
        return;
      }
      this.dialog.open(BypassCodeDialogComponent, {
        data: { bypassCode: result.bypassCode, expiresInSeconds: result.expiresInSeconds },
        width: '480px',
        disableClose: false,
      });
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 429) {
        this.snackBar.open(
          'Bypass code limit reached. You can generate 2 codes per hour. Try again later.',
          'Close',
          { duration: 5000 },
        );
      } else {
        this.snackBar.open(
          'Failed to generate bypass code. Please try again.',
          'Close',
          { duration: 4000 },
        );
      }
    }
  }

  suspendUser(user: AdminUserSummary): void {
    this.openDetailSheet(user);
  }

  reactivateUser(user: AdminUserSummary): void {
    this.openDetailSheet(user);
  }

  sendReminder(user: AdminUserSummary): void {
    this.adminUserService.sendReminder(user.id).then(() => {
      this.snackBar.open(`Reminder sent to ${user.email ?? 'the user'}.`, 'Close', { duration: 4000 });
    }).catch(err => {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        this.snackBar.open('User not found.', 'Close', { duration: 4000 });
      } else {
        this.snackBar.open('Failed to send reminder. Please try again.', 'Close', { duration: 4000 });
      }
    });
  }
}


