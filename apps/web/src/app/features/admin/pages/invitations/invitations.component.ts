import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { InvitationService } from '../../services/invitation.service';
import { InvitationSummary } from '../../models/admin.models';
import { InviteDialogComponent } from '../../components/invite-dialog/invite-dialog.component';

@Component({
  selector: 'app-invitations',
  imports: [
    MatTableModule, MatSortModule, MatPaginatorModule, MatButtonModule,
    MatChipsModule, MatIconModule, MatProgressSpinnerModule, DatePipe,
  ],
  template: `
    <div class="page-header">
      <h1>Invitations</h1>
      <button mat-flat-button color="primary" (click)="openInviteDialog()">
        Invite User
      </button>
    </div>

    @if (loading()) {
      <div class="loading-overlay">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    }

    @if (!loading() && !hasItems()) {
      <div class="empty-state">
        <mat-icon>mail_outline</mat-icon>
        <p>No invitations sent yet.</p>
      </div>
    }

    @if (hasItems()) {
      <div class="table-container">
        <table
          mat-table
          [dataSource]="invitations()"
          matSort
          (matSortChange)="onSortChange($event)"
          [matSortActive]="sortBy()"
          [matSortDirection]="sortDesc() ? 'desc' : 'asc'"
          class="invitations-table"
        >
          <ng-container matColumnDef="targetEmail">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Email</th>
            <td mat-cell *matCellDef="let i">{{ i.targetEmail }}</td>
          </ng-container>

          <ng-container matColumnDef="role">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Role</th>
            <td mat-cell *matCellDef="let i">
              <span class="role-pill">{{ i.role }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
            <td mat-cell *matCellDef="let i">
              <mat-chip [class]="'status-badge status-' + i.status" [attr.aria-label]="'Status: ' + statusLabel(i)">
                {{ statusLabel(i) }}
              </mat-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="createdAtUtc">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Sent Date</th>
            <td mat-cell *matCellDef="let i">{{ i.createdAtUtc | date:'mediumDate' }}</td>
          </ng-container>

          <ng-container matColumnDef="expiresAtUtc">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Expires</th>
            <td mat-cell *matCellDef="let i">{{ expiryDisplay(i) }}</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>Actions</th>
            <td mat-cell *matCellDef="let i">
              @if (i.status === 'pending' || i.status === 'expired') {
                <button
                  mat-stroked-button
                  size="small"
                  (click)="resend(i)"
                  [disabled]="resendingId() === i.id"
                >
                  {{ resendingId() === i.id ? 'Resending...' : 'Resend' }}
                </button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
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
  `,
  styles: `
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 500; }
    .loading-overlay { display: flex; justify-content: center; padding: 48px; }
    .empty-state { text-align: center; padding: 48px; color: #6B7280; }
    .empty-state mat-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 12px; }
    .table-container { position: relative; }
    .invitations-table { width: 100%; }
    .role-pill { display: inline-block; padding: 2px 10px; border-radius: 4px; font-size: 12px; background: #E8EDF5; color: #1B2A4A; }
    ::ng-deep .status-badge { font-size: 12px; padding: 0 8px; border-radius: 4px; min-height: 24px; }
    ::ng-deep .status-pending { background: #FFFBEB !important; color: #B45309 !important; }
    ::ng-deep .status-confirmed { background: #ECFDF5 !important; color: #0F6E4A !important; }
    ::ng-deep .status-expired { background: #F3F4F6 !important; color: #6B7280 !important; }
  `,
})
export class InvitationsComponent implements OnInit {
  private readonly invitationService = inject(InvitationService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly invitations = signal<InvitationSummary[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly resendingId = signal<string | null>(null);

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly sortBy = signal('createdAtUtc');
  readonly sortDesc = signal(true);

  readonly displayedColumns = ['targetEmail', 'role', 'status', 'createdAtUtc', 'expiresAtUtc', 'actions'];

  async ngOnInit(): Promise<void> {
    await this.loadInvitations();
  }

  hasItems(): boolean {
    return this.invitations().length > 0;
  }

  statusLabel(i: InvitationSummary): string {
    switch (i.status) {
      case 'pending': return 'Pending';
      case 'confirmed': return 'Confirmed';
      case 'expired': return 'Expired';
      default: return i.status;
    }
  }

  expiryDisplay(i: InvitationSummary): string {
    if (i.status === 'expired') {
      const days = Math.floor(
        (Date.now() - new Date(i.expiresAtUtc).getTime()) / (1000 * 60 * 60 * 24),
      );
      return `Expired ${days} days ago`;
    }
    if (i.status === 'confirmed') return 'Confirmed';
    const remaining = new Date(i.expiresAtUtc).getTime() - Date.now();
    if (remaining <= 0) return 'Expiring soon';
    const hours = Math.floor(remaining / (1000 * 60 * 60));
    const mins = Math.floor((remaining % (1000 * 60 * 60)) / (1000 * 60));
    return `${hours}h ${mins}m remaining`;
  }

  async onPageChange(event: { pageIndex: number; pageSize: number }): Promise<void> {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    await this.loadInvitations();
  }

  async onSortChange(sort: Sort): Promise<void> {
    this.sortBy.set(sort.active);
    this.sortDesc.set(sort.direction === 'desc');
    this.page.set(1);
    await this.loadInvitations();
  }

  async loadInvitations(): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.invitationService.getInvitations(
        this.page(), this.pageSize(),
      );
      this.invitations.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch {
      this.snackBar.open('Failed to load invitations.', 'Dismiss', { duration: 5000 });
    } finally {
      this.loading.set(false);
    }
  }

  openInviteDialog(): void {
    const ref = this.dialog.open(InviteDialogComponent, {
      width: '480px',
      disableClose: true,
    });

    ref.afterClosed().subscribe(result => {
      if (result) {
        this.snackBar.open(`Invitation sent to ${result.targetEmail}.`, 'Dismiss', { duration: 5000 });
        this.page.set(1);
        this.loadInvitations();
      }
    });
  }

  async resend(invitation: InvitationSummary): Promise<void> {
    this.resendingId.set(invitation.id);
    try {
      const result = await this.invitationService.resendInvitation(invitation.id);
      this.snackBar.open(`New invitation sent to ${result.targetEmail}.`, 'Dismiss', { duration: 5000 });
      await this.loadInvitations();
    } catch {
      this.snackBar.open('Failed to resend invitation.', 'Dismiss', { duration: 5000 });
    } finally {
      this.resendingId.set(null);
    }
  }
}
