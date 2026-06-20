import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-dashboard-page',
  imports: [MatCardModule],
  template: `
    <mat-card>
      <mat-card-header>
        <mat-card-title>Dashboard</mat-card-title>
        <mat-card-subtitle>Placeholder — Epic 8</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <p>Dashboard widgets will be implemented in Epic 8.</p>
      </mat-card-content>
    </mat-card>
  `,
})
export class DashboardPageComponent {}

