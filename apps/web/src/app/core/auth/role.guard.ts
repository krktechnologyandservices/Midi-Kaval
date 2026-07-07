import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from './auth-session.service';

// Blocks a route for roles the backend Authorize policy would 403 anyway (see
// apps/api/Infrastructure/Auth/Policies.cs), so the nav-item filtering in
// supervisor-shell.component.ts is backed by an actual guard rather than relying on
// child pages to handle a 403 response gracefully.
export function roleGuard(allowedRoles: readonly string[], fallbackPath: string): CanActivateFn {
  return () => {
    const auth = inject(AuthSessionService);
    const router = inject(Router);

    const role = auth.currentUser()?.role;
    if (role && allowedRoles.includes(role)) {
      return true;
    }

    return router.createUrlTree([fallbackPath]);
  };
}
