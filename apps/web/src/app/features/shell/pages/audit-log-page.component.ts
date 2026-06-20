import { Component, WritableSignal, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { AuditApiService } from '../services/audit-api.service';
import { AuditEventDto, AuditLogFilter } from '../audit.models';

function formatEventType(eventType: string): string {
  const labels: Record<string, string> = {
    'auth.login.success': 'Login success',
    'auth.login.failed': 'Login failed',
    'auth.otp.failed': 'OTP failed',
    'auth.logout': 'Logout',
    'auth.refresh.success': 'Token refreshed',
    'auth.session.invalidated': 'Session invalidated',
    'auth.password_reset.requested': 'Password reset requested',
    'auth.password_reset.completed': 'Password reset completed',
    'case.created': 'Case created',
    'case.stage.changed': 'Case stage changed',
    'case.merged': 'Case merged',
    'case.transferred': 'Case transferred',
    'case.gps.verified': 'GPS verified',
    'case.pii.revealed': 'PII revealed',
    'case.note.created': 'Case note created',
    'visit.scheduled': 'Visit scheduled',
    'visit.completed': 'Visit completed',
    'visit.rescheduled': 'Visit rescheduled',
    'visit.started': 'Visit started',
    'visit.note.merged': 'Visit note merged',
    'case.intervention.created': 'Intervention created',
    'case.intervention.updated': 'Intervention updated',
    'court.sitting.created': 'Court sitting created',
    'court.sitting.updated': 'Court sitting updated',
    'court.sitting.reminder_sent': 'Court reminder sent',
    'court.sitting.miss_escalated': 'Court miss escalated',
    'travel.claim.created': 'Travel claim created',
    'travel.claim.updated': 'Travel claim updated',
    'travel.claim.submitted': 'Travel claim submitted',
    'travel.claim.approved': 'Travel claim approved',
    'travel.claim.returned': 'Travel claim returned',
    'attachment.presign.issued': 'Upload URL issued',
    'attachment.confirmed': 'Attachment confirmed',
    'legend.created': 'Legend created',
    'legend.updated': 'Legend updated',
    'legend.deactivated': 'Legend deactivated',
    'legend.reactivated': 'Legend reactivated',
    'staff.created': 'Staff created',
    'staff.updated': 'Staff updated',
    'staff.deactivated': 'Staff deactivated',
    'staff.reactivated': 'Staff reactivated',
    'staff.force-reset': 'Staff force reset',
  };
  return labels[eventType] ?? eventType;
}

@Component({
  selector: 'app-audit-log-page',
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
  ],
  template: `
    <div class="audit-log-page">
      <header class="page-header">
        <h1>Audit Log</h1>
        <p class="subtitle">Review data-changing events across the organisation</p>
      </header>

      <mat-card class="filter-card">
        <mat-card-content>
          <div class="filter-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Event type</mat-label>
              <input matInput [ngModel]="filterEventType()" (ngModelChange)="filterEventType.set($event)" placeholder="e.g. staff." />
            </mat-form-field>

            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>From date</mat-label>
              <input
                matInput
                [matDatepicker]="fromPicker"
                [(ngModel)]="filterFrom"
                placeholder="YYYY-MM-DD"
              />
              <mat-datepicker-toggle matSuffix [for]="fromPicker" />
              <mat-datepicker #fromPicker />
            </mat-form-field>

            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>To date</mat-label>
              <input
                matInput
                [matDatepicker]="toPicker"
                [(ngModel)]="filterTo"
                placeholder="YYYY-MM-DD"
              />
              <mat-datepicker-toggle matSuffix [for]="toPicker" />
              <mat-datepicker #toPicker />
            </mat-form-field>

            <div class="filter-actions">
              <button mat-flat-button color="primary" (click)="applyFilters()" [disabled]="loading()">
                <mat-icon>search</mat-icon>
                Search
              </button>
              <button mat-stroked-button (click)="clearFilters()" [disabled]="loading()">
                <mat-icon>clear</mat-icon>
                Clear
              </button>
            </div>
          </div>
        </mat-card-content>
      </mat-card>

      @if (errorMessage()) {
        <div class="error-banner" role="alert">
          <mat-icon>error</mat-icon>
          <span>{{ errorMessage() }}</span>
          <button mat-icon-button (click)="errorMessage.set(null)" aria-label="Dismiss error">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      }

      <mat-card>
        <mat-card-content>
          @if (loading()) {
            <div class="skeleton-list" aria-label="Loading audit events">
              @for (row of [1, 2, 3, 4, 5]; track row) {
                <div class="skeleton-row">
                  <div class="skeleton skeleton-text skeleton-text--short"></div>
                  <div class="skeleton skeleton-text"></div>
                  <div class="skeleton skeleton-text skeleton-text--medium"></div>
                  <div class="skeleton skeleton-text skeleton-text--short"></div>
                  <div class="skeleton skeleton-text skeleton-text--long"></div>
                </div>
              }
            </div>
          } @else if (items().length === 0) {
            <div class="empty-state">
              <mat-icon>receipt_long</mat-icon>
              <p>No audit events match your filter</p>
            </div>
          } @else {
            <div class="table-container">
              <table mat-table [dataSource]="items()" class="audit-table">
                <ng-container matColumnDef="timestamp">
                  <th mat-header-cell *matHeaderCellDef>Timestamp</th>
                  <td mat-cell *matCellDef="let row">{{ row.createdAtUtc | date:'medium' }}</td>
                </ng-container>

                <ng-container matColumnDef="eventType">
                  <th mat-header-cell *matHeaderCellDef>Event Type</th>
                  <td mat-cell *matCellDef="let row" [title]="row.eventType">
                    {{ formatEventType(row.eventType) }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="actor">
                  <th mat-header-cell *matHeaderCellDef>Actor</th>
                  <td mat-cell *matCellDef="let row">
                    {{ row.actorEmail ?? 'System' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="subject">
                  <th mat-header-cell *matHeaderCellDef>Subject</th>
                  <td mat-cell *matCellDef="let row">
                    {{ row.subjectEmail ?? '—' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="details">
                  <th mat-header-cell *matHeaderCellDef>Details</th>
                  <td mat-cell *matCellDef="let row">
                    {{ formatDetails(row.metadata) }}
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
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
              aria-label="Audit log pagination"
            />
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styleUrls: ['./audit-log-page.component.scss'],
})
export class AuditLogPageComponent {
  private readonly api = inject(AuditApiService);

  readonly items: WritableSignal<AuditEventDto[]> = signal([]);
  readonly loading: WritableSignal<boolean> = signal(false);
  readonly errorMessage: WritableSignal<string | null> = signal(null);
  readonly totalCount: WritableSignal<number> = signal(0);
  readonly currentPage: WritableSignal<number> = signal(1);
  readonly pageSize: WritableSignal<number> = signal(25);

  filterEventType = signal('');
  filterFrom: Date | null = null;
  filterTo: Date | null = null;

  readonly displayedColumns = ['timestamp', 'eventType', 'actor', 'subject', 'details'];

  constructor() {
    this.loadEvents();
  }

  private async loadEvents(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const filter: AuditLogFilter = {
        eventType: this.filterEventType().trim() || undefined,
        from: this.filterFrom?.toISOString(),
        to: this.filterTo?.toISOString(),
        page: this.currentPage(),
        pageSize: this.pageSize(),
      };

      const result = await this.api.list(filter);
      this.items.set(result.items);
      this.totalCount.set(result.meta.totalCount ?? 0);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
      this.items.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadEvents();
  }

  clearFilters(): void {
    this.filterEventType.set('');
    this.filterFrom = null;
    this.filterTo = null;
    this.currentPage.set(1);
    this.loadEvents();
  }

  onPageChange(event: PageEvent): void {
    this.currentPage.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadEvents();
  }

  formatEventType(eventType: string): string {
    return formatEventType(eventType);
  }

  formatDetails(metadata: Record<string, unknown> | null): string {
    if (!metadata) return '—';
    const text = JSON.stringify(metadata);
    return text.length > 100 ? text.substring(0, 100) + '…' : text;
  }
}
