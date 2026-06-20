import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from './auth-session.service';

export const directorGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  if (auth.currentUser()?.role === AppRole.Director) {
    return true;
  }

  return router.createUrlTree(['/crisis-queue']);
};
