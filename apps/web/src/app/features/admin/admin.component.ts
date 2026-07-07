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
            routerLink="/admin/settings"
            routerLinkActive="active-link"
          >
            <mat-icon matListItemIcon>settings</mat-icon>
            <span matListItemTitle>Settings</span>
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

    .admin-sidenav {
      width: 260px;
      border: none;
      background: linear-gradient(180deg, #1F2E52 0%, #131E38 100%);
      box-shadow: 2px 0 16px rgba(0, 0, 0, 0.18);
    }

    .sidenav-header {
      padding: 24px 20px;
      color: #FFFFFF;
      font-size: 19px;
      font-weight: 700;
      letter-spacing: 0.03em;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      margin-bottom: 12px;
    }

    .sidenav-title { color: #FFFFFF; }

    ::ng-deep .admin-sidenav .mat-mdc-nav-list {
      padding: 8px 12px;
      // Angular Material's MDC list reads text/icon color from these tokens directly on
      // the nested primary-text/icon elements — overriding .mdc-list-item's own "color"
      // doesn't reach them, since those children set their own color explicitly.
      --mdc-list-list-item-label-text-color: #A9B4CC;
      --mdc-list-list-item-leading-icon-color: #7C8BAE;
      --mdc-list-list-item-hover-label-text-color: #FFFFFF;
      --mdc-list-list-item-hover-leading-icon-color: #4ECDC4;
      --mdc-list-list-item-hover-state-layer-color: #FFFFFF;
      --mdc-list-list-item-hover-state-layer-opacity: 0.07;
    }

    ::ng-deep .admin-sidenav .mat-mdc-nav-list .mdc-list-item {
      border-radius: 10px;
      margin-bottom: 4px;
      transition: background-color 0.18s ease;
    }

    ::ng-deep .admin-sidenav .mat-mdc-nav-list .mdc-list-item__primary-text,
    ::ng-deep .admin-sidenav .mat-mdc-nav-list .mat-mdc-list-item-title,
    ::ng-deep .admin-sidenav .mat-mdc-nav-list .mat-icon {
      transition: color 0.18s ease;
    }

    ::ng-deep .admin-sidenav .mat-mdc-nav-list .active-link {
      font-weight: 600;
      background: linear-gradient(90deg, rgba(78, 205, 196, 0.20) 0%, rgba(78, 205, 196, 0.03) 100%);
      border-left: 3px solid #4ECDC4;
      --mdc-list-list-item-label-text-color: #FFFFFF;
      --mdc-list-list-item-leading-icon-color: #4ECDC4;
    }

    .admin-content { background: #F4F6FB; }
    .content-wrapper { padding: 28px 32px; max-width: 1200px; }
  `,
})
export class AdminShellComponent {}
