import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationDto } from '../cases/models/case.models';

@Injectable({ providedIn: 'root' })
export class NotificationDeeplinkService {
  private readonly router = inject(Router);

  navigate(notification: NotificationDto): void {
    const resourceType = notification.resourceType;
    const caseId = notification.caseId;
    const resourceId = notification.resourceId;

    if (resourceType === 'TravelClaim' && resourceId) {
      void this.router.navigate(['/admin/travel-claims', resourceId]);
      return;
    }

    if (resourceType === 'ProjectBudget' && resourceId) {
      void this.router.navigate(['/budgets', resourceId]);
      return;
    }

    if (resourceType === 'CourtSitting' && caseId) {
      void this.router.navigate(['/cases', caseId]);
      return;
    }

    if (resourceType === 'Intervention' && caseId) {
      void this.router.navigate(['/cases', caseId]);
      return;
    }

    if (caseId) {
      void this.router.navigate(['/cases', caseId]);
      return;
    }

    console.warn(
      'NotificationDeeplinkService: no matching route for notification',
      { resourceType, caseId, resourceId },
    );
  }
}
