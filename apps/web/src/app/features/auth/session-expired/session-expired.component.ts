import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-session-expired',
  imports: [MatCardModule, MatButtonModule, RouterLink],
  templateUrl: './session-expired.component.html',
  styleUrl: './session-expired.component.scss',
})
export class SessionExpiredComponent {}
