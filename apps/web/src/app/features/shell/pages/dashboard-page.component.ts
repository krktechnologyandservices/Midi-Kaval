import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { DashboardApiService } from '../services/dashboard-api.service';
import { DashboardResultDto } from '../shell.models';
import { OnlineStateService } from '../../../core/services/online-state.service';
import { OfflineCacheService } from '../../../core/services/offline-cache.service';
import { StaleBannerComponent } from '../../../shared/stale-banner/stale-banner.component';

const SKELETON_WIDGET_COUNT = 10;
const CACHE_KEY = 'dashboard';

@Component({
  selector: 'app-dashboard-page',
  imports: [CommonModule, RouterLink, MatButtonModule, StaleBannerComponent],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(DashboardApiService);
  private readonly onlineState = inject(OnlineStateService);
  private readonly offlineCache = inject(OfflineCacheService);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly data = signal<DashboardResultDto | null>(null);
  readonly loading = signal(true);
  private loadingGuard = false;
  readonly refreshing = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly staleTimestamp = signal<Date | null>(null);

  readonly showingStale = computed(() => this.staleTimestamp() !== null);

  readonly skeletonCount = SKELETON_WIDGET_COUNT;

  readonly subtitle = computed(() => {
    const d = this.data();
    if (!d) {
      return 'Loading dashboard metrics…';
    }
    return 'Organisation status at a glance';
  });

  async ngOnInit(): Promise<void> {
    try {
      await this.load();
    } catch {
      // Handled inside load()
    }
    this.refreshTimer = setInterval(() => this.autoRefresh(), 60000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer !== null) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  async load(): Promise<void> {
    if (this.loadingGuard || this.refreshing()) {
      return;
    }
    this.loadingGuard = true;
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.api.get();
      this.data.set(data);
      this.staleTimestamp.set(null);
      this.offlineCache.set(CACHE_KEY, data);
    } catch (error) {
      this.tryServeFromCache(error);
    } finally {
      this.loading.set(false);
      this.loadingGuard = false;
    }
  }

  private async autoRefresh(): Promise<void> {
    if (this.loading() || this.refreshing()) {
      return;
    }
    this.refreshing.set(true);
    try {
      const data = await this.api.get();
      this.data.set(data);
      this.errorMessage.set(null);
      this.staleTimestamp.set(null);
      this.offlineCache.set(CACHE_KEY, data);
    } catch (error) {
      // Auto-refresh errors: try to serve stale cache if not already showing stale data
      if (!this.showingStale()) {
        this.tryServeFromCache(error);
      }
    } finally {
      this.refreshing.set(false);
    }
  }

  private tryServeFromCache(error?: unknown): void {
    const cached = this.offlineCache.get<DashboardResultDto>(CACHE_KEY);
    if (cached) {
      this.data.set(cached.data);
      this.staleTimestamp.set(new Date(cached.timestamp));
      this.errorMessage.set(null);
    } else {
      this.errorMessage.set(
        error
          ? this.api.extractErrorMessage(error)
          : 'Unable to load data — check your connection',
      );
    }
  }

  /** Max value from an array of numbers; returns 0 for empty/null input */
  private maxCount(values: (number | undefined)[] | undefined): number {
    if (!values || values.length === 0) {
      return 0;
    }
    return Math.max(...values.filter((v): v is number => v != null), 0);
  }

  /** Proportional width for distribution bars: value / max * 100 */
  pct(value: number, max: number): number {
    if (max <= 0 || value < 0 || Number.isNaN(value)) {
      return 0;
    }
    return (value / max) * 100;
  }

  /** Max count across CasesByStage items */
  maxStageCount(): number {
    return this.maxCount(this.data()?.casesByStage?.map((x) => x.count));
  }

  /** Max count across CasesByOffenceClassification items */
  maxOffenceCount(): number {
    return this.maxCount(this.data()?.casesByOffenceClassification?.map((x) => x.count));
  }

  /** Max count across CasesByDomicile items */
  maxDomicileCount(): number {
    return this.maxCount(this.data()?.casesByDomicile?.map((x) => x.count));
  }

  /** Max count across CasesByStaff items */
  maxStaffCount(): number {
    return this.maxCount(this.data()?.casesByStaff?.map((x) => x.caseCount));
  }

  /** Max count across intake trend for bar height */
  maxIntakeCount(): number {
    return this.maxCount(this.data()?.intakeTrend?.map((x) => x.count));
  }

  /** Bar height percentage for intake trend */
  trendHeight(value: number, max: number): number {
    if (max <= 0 || value < 0 || Number.isNaN(value)) {
      return 0;
    }
    // Minimum 4% so zero-count months render as a visible hairline
    return Math.max(4, (value / max) * 100);
  }

  /** Format amount as INR currency string */
  formatAmount(amount: number | null | undefined): string {
    if (amount == null || Number.isNaN(amount)) {
      return '\u2014';
    }
    try {
      return amount.toLocaleString('en-IN', { maximumFractionDigits: 0 });
    } catch {
      return String(amount);
    }
  }

  /** Format oldest pending days as human-readable string */
  oldestDays(days: number | null | undefined): string {
    if (days == null || Number.isNaN(days)) {
      return '\u2014';
    }
    if (days <= 0) {
      return 'Today';
    }
    return `${days} ${days === 1 ? 'day' : 'days'} pending`;
  }

  /** Month abbreviation from YYYY-MM */
  monthLabel(month: string): string {
    if (!month || month.length < 7) {
      return month;
    }
    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];
    const monthNum = parseInt(month.slice(5, 7), 10);
    if (monthNum >= 1 && monthNum <= 12) {
      return months[monthNum - 1];
    }
    return month.slice(5, 7);
  }

  /** Whether all intake trend points have zero count (or array is empty) */
  allIntakeZero(): boolean {
    const trend = this.data()?.intakeTrend;
    return !trend || trend.length === 0 || trend.every((p) => p.count == null || p.count === 0);
  }
}
