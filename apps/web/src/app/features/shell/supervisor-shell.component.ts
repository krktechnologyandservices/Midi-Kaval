import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from '../../core/auth/auth-session.service';

type NavItem = { label: string; path: string; icon: string; roles: readonly string[] };

// Mirrors the backend Authorize policies for each area (see apps/api/Infrastructure/Auth/Policies.cs
// and the [Authorize(Policy = ...)] attributes on the corresponding controllers) — a nav item must
// only be shown to roles that can actually load the page behind it.
const COORDINATOR_OR_ABOVE = [AppRole.Director, AppRole.Coordinator];
const BUDGET_VIEWERS = [AppRole.Director, AppRole.Coordinator, AppRole.Accountant];

@Component({
  selector: 'app-supervisor-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatIconModule],
  templateUrl: './supervisor-shell.component.html',
  styleUrl: './supervisor-shell.component.scss',
})
export class SupervisorShellComponent {
  private readonly auth = inject(AuthSessionService);

  readonly user = this.auth.currentUser;

  private readonly allNavItems: NavItem[] = [
    { label: 'Crisis queue', path: '/crisis-queue', icon: 'emergency', roles: COORDINATOR_OR_ABOVE },
    { label: 'Dashboard', path: '/dashboard', icon: 'dashboard', roles: COORDINATOR_OR_ABOVE },
    { label: 'Cases', path: '/cases', icon: 'folder_shared', roles: COORDINATOR_OR_ABOVE },
    { label: 'Reports', path: '/reports', icon: 'bar_chart', roles: COORDINATOR_OR_ABOVE },
    { label: 'Budgets', path: '/budgets', icon: 'account_balance_wallet', roles: BUDGET_VIEWERS },
    { label: 'Legends', path: '/legends', icon: 'list_alt', roles: COORDINATOR_OR_ABOVE },
    { label: 'Admin', path: '/admin', icon: 'admin_panel_settings', roles: [AppRole.Director] },
  ];

  readonly navItems = computed(() => {
    const role = this.user()?.role;
    if (!role) {
      return [];
    }
    return this.allNavItems.filter((item) => item.roles.includes(role));
  });
}

