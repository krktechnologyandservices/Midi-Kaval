import { Injectable, OnDestroy, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class OnlineStateService implements OnDestroy {
  readonly isOnline = signal(true);
  readonly lastOnlineChange = signal<Date>(new Date());

  private readonly onOnline: () => void;
  private readonly onOffline: () => void;

  constructor() {
    this.isOnline.set(navigator.onLine);

    this.onOnline = () => {
      this.isOnline.set(true);
      this.lastOnlineChange.set(new Date());
    };
    this.onOffline = () => {
      this.isOnline.set(false);
      this.lastOnlineChange.set(new Date());
    };

    window.addEventListener('online', this.onOnline);
    window.addEventListener('offline', this.onOffline);
  }

  ngOnDestroy(): void {
    window.removeEventListener('online', this.onOnline);
    window.removeEventListener('offline', this.onOffline);
  }
}
