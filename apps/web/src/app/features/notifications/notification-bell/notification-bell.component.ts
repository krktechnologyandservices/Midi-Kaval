import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { filter, interval, startWith, switchMap } from 'rxjs';
import { CaseApiService } from '../../cases/services/case-api.service';

@Component({
  selector: 'app-notification-bell',
  imports: [RouterModule, MatIconModule, MatBadgeModule],
  template: `
    <a
      class="notification-bell"
      routerLink="/notifications"
      [matBadge]="unreadCount"
      matBadgeOverlap="false"
      matBadgeSize="small"
      matBadgeColor="warn"
      [matBadgeHidden]="unreadCount === 0"
      aria-label="Notifications">
      <mat-icon>notifications</mat-icon>
    </a>
  `,
  styles: [`
    .notification-bell {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 8px;
      color: #475467;
      text-decoration: none;
    }
    .notification-bell:hover {
      color: #0d6e6e;
    }
    .notification-bell mat-icon {
      font-size: 24px;
      width: 24px;
      height: 24px;
    }
  `],
})
export class NotificationBellComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  unreadCount = 0;

  ngOnInit(): void {
    const navigationEnd$ = this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    );

    interval(60_000)
      .pipe(
        startWith(0),
        switchMap(() => navigationEnd$.pipe(startWith(null))),
        switchMap(() => this.caseApi.getUnreadCount()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: count => (this.unreadCount = count),
        error: () => {
          // Silently retry on next poll cycle — keep showing last known count
        },
      });
  }
}
