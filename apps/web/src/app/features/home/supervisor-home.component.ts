import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthSessionService } from '../../core/auth/auth-session.service';

@Component({
  selector: 'app-supervisor-home',
  imports: [MatToolbarModule, MatCardModule, MatButtonModule, RouterLink],
  templateUrl: './supervisor-home.component.html',
  styleUrl: './supervisor-home.component.scss',
})
export class SupervisorHomeComponent {
  readonly auth = inject(AuthSessionService);

  logout(): void {
    void this.auth.logout();
  }
}
