import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-admin-page',
  imports: [RouterLink, MatButtonModule, MatCardModule],
  template: `
    <mat-card>
      <mat-card-header>
        <mat-card-title>Admin</mat-card-title>
        <mat-card-subtitle>Director tools</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <nav class="admin-nav" aria-label="Admin sections">
          <a mat-stroked-button routerLink="/admin/travel-claims">Travel claims approval</a>
        </nav>
        <p class="hint">User management and audit log will be implemented in Epic 9.</p>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .admin-nav {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .hint {
      color: var(--mat-sys-on-surface-variant, #475467);
      font-size: 0.875rem;
    }
  `,
})
export class AdminPageComponent {}
