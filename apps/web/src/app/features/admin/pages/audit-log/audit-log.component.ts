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
import { MatSelectModule } from '@angular/material/select';
import { AuditApiService } from '../../services/audit.service';
import { AuditEventDto, AuditLogFilter } from '../../models/audit.models';
import { EVENT_TYPE_OPTIONS, formatEventType } from '../../models/audit.utils';

@Component({
  selector: 'app-audit-log',
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
    MatSelectModule,
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
              <mat-select [(ngModel)]="filterEventType">
                <mat-option value="">All types</mat-option>
                @for (type of eventTypeOptions; track type) {
                  <mat-option [value]="type">{{ formatEventType(type) }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Actor user ID</mat-label>
              <input matInput [(ngModel)]="filterActorUserId" placeholder="Paste actor user ID" />
            </mat-form-field>

            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Target user ID</mat-label>
              <input matInput [(ngModel)]="filterSubjectUserId" placeholder="Paste target user ID" />
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
          @if (errorMessage()) {
            <!-- Error state shown via banner above — no empty state here -->
          } @else if (loading()) {
            <div class="skeleton-list" aria-label="Loading audit events">
              @for (row of [1, 2, 3, 4, 5]; track row) {
                <div class="skeleton-row">
                  <div class="skeleton skeleton-text skeleton-text--short"></div>
                  <div class="skeleton skeleton-text"></div>
                  <div class="skeleton skeleton-text skeleton-text--medium"></div>
                  <div class="skeleton skeleton-text skeleton-text--short"></div>
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
              <table mat-table [dataSource]="items()" class="audit-table" multiTemplateDataRows>
                <ng-container matColumnDef="timestamp">
                  <th mat-header-cell *matHeaderCellDef>Timestamp</th>
                  <td mat-cell *matCellDef="let row">{{ row.createdAtUtc | date:'medium' }}</td>
                </ng-container>

                <ng-container matColumnDef="eventType">
                  <th mat-header-cell *matHeaderCellDef>Event Type</th>
                  <td mat-cell *matCellDef="let row" [title]="row.eventType">
                    {{ formatEventType(row.eventType ?? '') }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="actor">
                  <th mat-header-cell *matHeaderCellDef>Actor</th>
                  <td mat-cell *matCellDef="let row">
                    {{ row.actorEmail ?? 'System' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="subject">
                  <th mat-header-cell *matHeaderCellDef>Target</th>
                  <td mat-cell *matCellDef="let row">
                    {{ row.targetUserSnapshot?.email ?? row.subjectEmail ?? '—' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="ipAddress">
                  <th mat-header-cell *matHeaderCellDef>IP Address</th>
                  <td mat-cell *matCellDef="let row">
                    {{ row.actorIpAddress ?? '—' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="expand">
                  <th mat-header-cell *matHeaderCellDef aria-label="Details"></th>
                  <td mat-cell *matCellDef="let row">
                    <button mat-icon-button (click)="toggleRow(row)" [attr.aria-label]="isExpanded(row) ? 'Collapse details' : 'Expand details'">
                      <mat-icon>{{ isExpanded(row) ? 'expand_less' : 'expand_more' }}</mat-icon>
                    </button>
                  </td>
                </ng-container>

                <ng-container matColumnDef="expandedDetail">
                  <td mat-cell *matCellDef="let row" [attr.colspan]="displayedColumns.length">
                    @if (isExpanded(row)) {
                      <div class="row-detail">
                        <div class="detail-grid">
                          <div class="detail-item">
                            <span class="detail-label">Event type (raw)</span>
                            <span class="detail-value code">{{ row.eventType }}</span>
                          </div>
                          <div class="detail-item">
                            <span class="detail-label">Actor name</span>
                            <span class="detail-value">{{ row.actorName ?? 'System' }}</span>
                          </div>
                          <div class="detail-item">
                            <span class="detail-label">Actor email</span>
                            <span class="detail-value">{{ row.actorEmail ?? 'System' }}</span>
                          </div>
                          @if (row.targetUserSnapshot) {
                            <div class="detail-item">
                              <span class="detail-label">Target snapshot</span>
                              <span class="detail-value">Email: {{ row.targetUserSnapshot.email }}, Name: {{ row.targetUserSnapshot.name }}, Role: {{ row.targetUserSnapshot.role }}</span>
                            </div>
                          }
                          <div class="detail-item">
                            <span class="detail-label">IP address</span>
                            <span class="detail-value code">{{ row.actorIpAddress ?? '—' }}</span>
                          </div>
                          @if (row.metadata) {
                            <div class="detail-item full-width">
                              <span class="detail-label">Metadata</span>
                              <pre class="metadata-json">{{ row.metadata | json }}</pre>
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns;"
                    class="audit-row"
                    [class.expanded]="isExpanded(row)"
                    (click)="toggleRow(row)"></tr>
                <tr mat-row *matRowDef="let row; columns: ['expandedDetail']" class="detail-row"></tr>
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
  styleUrls: ['./audit-log.component.scss'],
})
export class AuditLogComponent {
  private readonly api = inject(AuditApiService);

  readonly items: WritableSignal<AuditEventDto[]> = signal([]);
  readonly loading: WritableSignal<boolean> = signal(false);
  readonly errorMessage: WritableSignal<string | null> = signal(null);
  readonly totalCount: WritableSignal<number> = signal(0);
  readonly currentPage: WritableSignal<number> = signal(1);
  readonly pageSize: WritableSignal<number> = signal(50);

  filterEventType = '';
  filterActorUserId = '';
  filterSubjectUserId = '';
  filterFrom: Date | null = null;
  filterTo: Date | null = null;

  readonly eventTypeOptions = EVENT_TYPE_OPTIONS;
  readonly displayedColumns = ['timestamp', 'eventType', 'actor', 'subject', 'ipAddress', 'expand'];

  private expandedRows = new Set<string>();

  constructor() {
    this.loadEvents();
  }

  isExpanded(row: AuditEventDto): boolean {
    return this.expandedRows.has(row.id);
  }

  toggleRow(row: AuditEventDto): void {
    if (this.expandedRows.has(row.id)) {
      this.expandedRows.delete(row.id);
    } else {
      this.expandedRows.add(row.id);
    }
    // Force signal update by replacing the array reference
    this.items.set([...this.items()]);
  }

  private async loadEvents(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const filter: AuditLogFilter = {
        eventType: this.filterEventType || undefined,
        actorUserId: this.filterActorUserId.trim() || undefined,
        subjectUserId: this.filterSubjectUserId.trim() || undefined,
        from: this.filterFrom?.toISOString(),
        to: this.filterTo?.toISOString(),
        page: this.currentPage(),
        pageSize: this.pageSize(),
      };

      const result = await this.api.list(filter);
      this.items.set(result.items);
      this.totalCount.set(result.meta.totalCount ?? 0);
      this.expandedRows.clear();
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
    this.filterEventType = '';
    this.filterActorUserId = '';
    this.filterSubjectUserId = '';
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
}
