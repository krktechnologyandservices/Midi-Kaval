import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-mobile-only',
  imports: [MatCardModule],
  templateUrl: './mobile-only.component.html',
  styleUrl: './mobile-only.component.scss',
})
export class MobileOnlyComponent {}
