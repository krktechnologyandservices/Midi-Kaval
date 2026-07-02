import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionService } from './auth-session.service';

export const twoFactorSetupGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  if (!auth.requires2faSetup()) {
    return true;
  }

  if (router.url.startsWith('/settings/2fa') || router.url.startsWith('/vendor/settings')) {
    return true;
  }

  return router.createUrlTree([auth.setupUrl() ?? '/settings/2fa']);
};
