import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { NotificationBellComponent } from '../notifications/notification-bell/notification-bell.component';

type NavItem = { label: string; path: string; requiresDirector?: boolean };

@Component({
  selector: 'app-supervisor-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBellComponent],
  templateUrl: './supervisor-shell.component.html',
  styleUrl: './supervisor-shell.component.scss',
})
export class SupervisorShellComponent {
  private readonly auth = inject(AuthSessionService);

  readonly user = this.auth.currentUser;

  private readonly allNavItems: NavItem[] = [
    { label: 'Crisis queue', path: '/crisis-queue' },
    { label: 'Dashboard', path: '/dashboard' },
    { label: 'Cases', path: '/cases' },
    { label: 'Reports', path: '/reports' },
    { label: 'Budgets', path: '/budgets' },
    { label: 'Legends', path: '/legends' },
    { label: 'Admin', path: '/admin', requiresDirector: true },
  ];

  readonly navItems = computed(() => {
    const role = this.user()?.role;
    return this.allNavItems.filter((item) => {
      if (!item.requiresDirector) {
        return true;
      }

      return role === AppRole.Director;
    });
  });

}

