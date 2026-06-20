import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionService } from './auth-session.service';

export const supervisorGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  if (auth.isSupervisorRole()) {
    return true;
  }

  if (auth.isMobileOnlyRole()) {
    return router.createUrlTree(['/mobile-only']);
  }

  return router.createUrlTree(['/login']);
};
