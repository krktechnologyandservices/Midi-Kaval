import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-stale-banner',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    @if (staleTimestamp) {
      <div class="stale-banner" role="status" aria-live="polite">
        <mat-icon class="banner-icon" aria-hidden="true">info</mat-icon>
        <span class="banner-text">
          You're offline — showing data from {{ staleTimestamp | date:'MMM d, yyyy, h:mm a' }}. Some features may be unavailable.
        </span>
      </div>
    }
  `,
  styles: [`
    .stale-banner {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.625rem 1rem;
      margin-bottom: 1rem;
      border-radius: 0.5rem;
      background: #eef4ff;
      border: 1px solid #c7d7fe;
      color: #175cd3;
      font-size: 0.875rem;
      line-height: 1.25rem;
    }
    .banner-icon {
      font-size: 1.25rem;
      width: 1.25rem;
      height: 1.25rem;
      flex-shrink: 0;
    }
    .banner-text {
      flex: 1;
    }
  `],
})
export class StaleBannerComponent {
  @Input() staleTimestamp: Date | null = null;
}
