import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { CrisisQueueApiService } from '../../travel/services/crisis-queue-api.service';
import { CrisisQueueItemDto } from '../../travel/travel.models';

@Component({
  selector: 'app-crisis-queue-page',
  imports: [RouterLink, MatButtonModule],
  templateUrl: './crisis-queue-page.component.html',
  styleUrl: './crisis-queue-page.component.scss',
})
export class CrisisQueuePageComponent implements OnInit, OnDestroy {
  private readonly crisisQueueApi = inject(CrisisQueueApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly router = inject(Router);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly items = signal<CrisisQueueItemDto[]>([]);
  readonly loading = signal(true);
  private loadingGuard = false;
  readonly refreshing = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly subtitle = computed(() => {
    const count = this.items().length;
    if (count > 0) {
      return `${count} ${count === 1 ? 'item' : 'items'} need attention`;
    }
    return 'No urgent items';
  });

  async ngOnInit(): Promise<void> {
    try {
      await this.load();
    } catch {
      // ngOnInit errors are handled inside load() — this catch prevents
      // unhandled Promise rejections from the async lifecycle hook
    }
    this.refreshTimer = setInterval(() => this.autoRefresh(), 30000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer !== null) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  async load(): Promise<void> {
    if (this.loadingGuard) {
      return;
    }
    this.loadingGuard = true;
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      this.items.set(await this.crisisQueueApi.list());
    } catch (error) {
      this.errorMessage.set(this.crisisQueueApi.extractErrorMessage(error));
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
      this.items.set(await this.crisisQueueApi.list());
      this.errorMessage.set(null);
    } catch {
      // Silently ignore auto-refresh errors — keep the last successful data
    } finally {
      this.refreshing.set(false);
    }
  }

  rowClass(item: CrisisQueueItemDto): string {
    const severity = item.severity ?? '';
    if (severity === 'critical' || severity === 'warning' || severity === 'info' || severity === 'neutral') {
      return 'crisis-row-' + severity;
    }
    return 'crisis-row-neutral';
  }

  navigateRow(item: CrisisQueueItemDto): void {
    if (item.rowType === 'travel_claim_pending') {
      if (!item.travelClaimId) {
        console.warn('CrisisQueuePageComponent: travel_claim_pending row has no travelClaimId', item);
        return;
      }

      const role = this.auth.currentUser()?.role;
      if (role === AppRole.Director) {
        void this.router.navigate(['/admin/travel-claims', item.travelClaimId]);
        return;
      }

      void this.router.navigate(['/crisis-queue/travel-claims', item.travelClaimId]);
      return;
    }

    if (!item.caseId) {
      console.warn('CrisisQueuePageComponent: non-claim row has no caseId', item);
      return;
    }

    void this.router.navigate(['/cases', item.caseId]);
  }
}
