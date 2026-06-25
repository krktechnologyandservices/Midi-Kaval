import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';

@Component({
  selector: 'app-admin-shell',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive,
    MatSidenavModule, MatToolbarModule, MatListModule,
    MatIconModule, MatButtonModule,
  ],
  template: `
    <mat-sidenav-container class="admin-sidenav-container">
      <mat-sidenav
        mode="side"
        [opened]="true"
        class="admin-sidenav"
      >
        <div class="sidenav-header">
          <span class="sidenav-title">Admin</span>
        </div>
        <mat-nav-list>
          <a
            mat-list-item
            routerLink="/admin/team"
            routerLinkActive="active-link"
            [routerLinkActiveOptions]="{ exact: true }"
          >
            <mat-icon matListItemIcon>group</mat-icon>
            <span matListItemTitle>Team Roster</span>
          </a>
          <a
            mat-list-item
            routerLink="/admin/invitations"
            routerLinkActive="active-link"
          >
            <mat-icon matListItemIcon>mail_outline</mat-icon>
            <span matListItemTitle>Invitations</span>
          </a>
          <a
            mat-list-item
            routerLink="/admin/audit"
            routerLinkActive="active-link"
          >
            <mat-icon matListItemIcon>receipt_long</mat-icon>
            <span matListItemTitle>Audit Log</span>
          </a>
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content class="admin-content">
        <div class="content-wrapper">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .admin-sidenav-container { height: calc(100vh - 64px); }
    .admin-sidenav { width: 240px; background: #1B2A4A; border: none; }
    .sidenav-header { padding: 20px 16px; color: #FFFFFF; font-size: 18px; font-weight: 600; letter-spacing: 0.02em; }
    .sidenav-title { color: #FFFFFF; }
    ::ng-deep .admin-sidenav .mat-mdc-nav-list .mdc-list-item { color: #C8D0DD; }
    ::ng-deep .admin-sidenav .mat-mdc-nav-list .active-link { color: #FFFFFF; border-left: 3px solid #2E7D8F; background: rgba(255,255,255,0.08); }
    ::ng-deep .admin-sidenav .mat-mdc-nav-list .active-link .mat-mdc-list-item-icon { color: #2E7D8F; }
    .admin-content { background: #F5F6FA; }
    .content-wrapper { padding: 24px; max-width: 1200px; }
  `,
})
export class AdminShellComponent {}
