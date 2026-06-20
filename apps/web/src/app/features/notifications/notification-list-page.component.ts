import { Component, inject, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CaseApiService } from '../cases/services/case-api.service';
import { NotificationDeeplinkService } from './notification-deeplink.service';
import { NotificationDto } from '../cases/models/case.models';

@Component({
  selector: 'app-notification-list-page',
  imports: [RouterModule, DatePipe, MatIconModule],
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Notifications</h1>
      </header>

      @if (loading) {
        <p class="loading">Loading notifications…</p>
      } @else if (error) {
        <div class="error-state">
          <mat-icon>error_outline</mat-icon>
          <p>Couldn't load notifications</p>
          <button class="retry-btn" (click)="loadNotifications()">Retry</button>
        </div>
      } @else if (notifications.length === 0) {
        <div class="empty-state">
          <mat-icon>notifications_none</mat-icon>
          <p>You're up to date</p>
        </div>
      } @else {
        <ul class="notification-list">
          @for (notification of notifications; track notification.id) {
            <li
              class="notification-item"
              [class.unread]="!notification.readAtUtc"
              (click)="onNotificationClick(notification)"
              role="button"
              tabindex="0"
              (keydown.enter)="onNotificationClick(notification)"
            >
              <div class="notification-content">
                <p class="notification-title">{{ notification.title }}</p>
                <p class="notification-body">{{ notification.body }}</p>
                <p class="notification-time">{{ notification.createdAtUtc | date: 'medium' }}</p>
              </div>
              @if (!notification.readAtUtc) {
                <span class="unread-dot" aria-label="Unread"></span>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .page {
      max-width: 720px;
      margin: 0 auto;
      padding: 24px 16px;
    }
    .page-header h1 {
      font-size: 24px;
      font-weight: 600;
      color: #101828;
      margin: 0 0 24px;
    }
    .loading {
      color: #667085;
      text-align: center;
      margin-top: 48px;
    }
    .empty-state {
      text-align: center;
      margin-top: 80px;
      color: #667085;
    }
    .empty-state mat-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      margin-bottom: 16px;
    }
    .empty-state p {
      font-size: 18px;
    }
    .error-state {
      text-align: center;
      margin-top: 80px;
      color: #667085;
    }
    .error-state mat-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      margin-bottom: 16px;
    }
    .error-state p {
      font-size: 18px;
      margin-bottom: 16px;
    }
    .retry-btn {
      background-color: #0d6e6e;
      color: #fff;
      border: none;
      border-radius: 8px;
      padding: 10px 24px;
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
    }
    .retry-btn:hover {
      background-color: #0b5c5c;
    }
    .notification-list {
      list-style: none;
      padding: 0;
      margin: 0;
    }
    .notification-item {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 16px;
      border-bottom: 1px solid #eaecf0;
      cursor: pointer;
      transition: background-color 0.15s;
    }
    .notification-item:hover {
      background-color: #f9fafb;
    }
    .notification-item.unread {
      background-color: #f0f9ff;
    }
    .notification-item.unread:hover {
      background-color: #e0f2fe;
    }
    .notification-content {
      flex: 1;
      min-width: 0;
    }
    .notification-title {
      font-size: 14px;
      font-weight: 600;
      color: #101828;
      margin: 0 0 4px;
    }
    .notification-body {
      font-size: 14px;
      color: #475467;
      margin: 0 0 4px;
      line-height: 1.4;
    }
    .notification-time {
      font-size: 12px;
      color: #98a2b3;
      margin: 0;
    }
    .unread-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background-color: #0d6e6e;
      flex-shrink: 0;
      margin-top: 8px;
    }
  `],
})
export class NotificationListPageComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly deeplink = inject(NotificationDeeplinkService);

  notifications: NotificationDto[] = [];
  loading = true;
  error = false;

  ngOnInit(): void {
    this.loadNotifications();
  }

  protected async loadNotifications(): Promise<void> {
    this.loading = true;
    this.error = false;
    try {
      this.notifications = await this.caseApi.listNotifications();
    } catch {
      this.error = true;
    } finally {
      this.loading = false;
    }
  }

  onNotificationClick(notification: NotificationDto): void {
    if (!notification.readAtUtc && notification.id) {
      this.caseApi.markNotificationRead(notification.id).catch(() => {});
    }
    this.deeplink.navigate(notification);
  }
}
