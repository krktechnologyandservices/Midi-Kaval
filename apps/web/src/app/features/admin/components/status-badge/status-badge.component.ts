import { Component, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';

@Component({
  selector: 'app-status-badge',
  imports: [MatChipsModule],
  template: `
    <mat-chip
      [class.badge-active]="status() === 'active'"
      [class.badge-suspended]="status() === 'suspended'"
      [attr.aria-label]="'Status: ' + (status() === 'active' ? 'Active' : 'Suspended')"
    >
      {{ status() === 'active' ? 'Active' : 'Suspended' }}
    </mat-chip>
  `,
  styles: `
    ::ng-deep .badge-active { background: #ECFDF5 !important; color: #0F6E4A !important; border-radius: 4px !important; }
    ::ng-deep .badge-suspended { background: #FEF2F2 !important; color: #991B1B !important; border-radius: 4px !important; }
  `,
})
export class StatusBadgeComponent {
  readonly status = input.required<'active' | 'suspended'>();
}
