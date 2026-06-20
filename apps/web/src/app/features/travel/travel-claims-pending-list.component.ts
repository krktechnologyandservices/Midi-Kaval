import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TravelClaimApiService } from './services/travel-claim-api.service';
import { TravelClaimDto } from './travel.models';

@Component({
  selector: 'app-travel-claims-pending-list',
  imports: [DatePipe, RouterLink, MatButtonModule, MatProgressSpinnerModule],
  templateUrl: './travel-claims-pending-list.component.html',
  styleUrl: './travel-claims-pending-list.component.scss',
})
export class TravelClaimsPendingListComponent implements OnInit {
  private readonly travelApi = inject(TravelClaimApiService);

  readonly items = signal<TravelClaimDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      this.items.set(await this.travelApi.listPending());
    } catch (error) {
      this.errorMessage.set(this.travelApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  receiptCount(claim: TravelClaimDto): number {
    return claim.attachments?.length ?? 0;
  }
}
