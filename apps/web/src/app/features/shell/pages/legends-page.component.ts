import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-legends-page',
  imports: [MatCardModule],
  template: `
    <mat-card>
      <mat-card-header>
        <mat-card-title>Legends</mat-card-title>
        <mat-card-subtitle>Placeholder — Epic 9</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <p>Legends administration will be implemented in Epic 9.</p>
      </mat-card-content>
    </mat-card>
  `,
})
export class LegendsPageComponent {}

