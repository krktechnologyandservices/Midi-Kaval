import { Component, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthSessionService } from './core/auth/auth-session.service';
import { NotificationBellComponent } from './features/notifications/notification-bell/notification-bell.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule, MatButtonModule, MatIconModule, MatTooltipModule, NotificationBellComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly router = inject(Router);
  readonly auth = inject(AuthSessionService);

  readonly title = 'Kaval';
  readonly showToolbar = signal(false);

  readonly userInitials = computed(() => {
    const email = this.auth.currentUser()?.email;
    return email ? email.charAt(0).toUpperCase() : '?';
  });

  constructor() {
    effect(() => {
      this.auth.isAuthenticated();
      this.updateToolbar(this.router.url);
    });

    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.updateToolbar((event as NavigationEnd).urlAfterRedirects);
      });
  }

  logout(): void {
    void this.auth.logout();
  }

  private updateToolbar(url: string): void {
    const publicRoutes = ['/login', '/login/otp', '/session-expired'];
    const isPublic = publicRoutes.some((route) => url.startsWith(route));
    this.showToolbar.set(this.auth.isAuthenticated() && !isPublic);
  }
}
