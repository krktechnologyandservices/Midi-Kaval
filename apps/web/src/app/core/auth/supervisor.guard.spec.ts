import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthSessionService } from './auth-session.service';
import { supervisorGuard } from './supervisor.guard';

describe('supervisorGuard', () => {
  let flags: { authenticated: boolean; supervisor: boolean; mobileOnly: boolean };
  let auth: AuthSessionService;
  let router: Router;

  beforeEach(() => {
    flags = { authenticated: false, supervisor: false, mobileOnly: false };
    auth = {
      isAuthenticated: () => flags.authenticated,
      isSupervisorRole: () => flags.supervisor,
      isMobileOnlyRole: () => flags.mobileOnly,
    } as unknown as AuthSessionService;

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthSessionService, useValue: auth },
        {
          provide: Router,
          useValue: {
            createUrlTree: (commands: string[]) => ({ commands } as unknown as UrlTree),
          },
        },
      ],
    });

    router = TestBed.inject(Router);
  });

  it('allows Director role', () => {
    flags = { authenticated: true, supervisor: true, mobileOnly: false };

    const result = TestBed.runInInjectionContext(() => supervisorGuard({} as never, {} as never));
    expect(result).toBeTrue();
  });

  it('redirects SocialWorker to mobile-only', () => {
    flags = { authenticated: true, supervisor: false, mobileOnly: true };

    const result = TestBed.runInInjectionContext(() => supervisorGuard({} as never, {} as never));
    expect(result).toEqual(router.createUrlTree(['/mobile-only']));
  });

  it('redirects unauthenticated users to login', () => {
    flags = { authenticated: false, supervisor: false, mobileOnly: false };

    const result = TestBed.runInInjectionContext(() => supervisorGuard({} as never, {} as never));
    expect(result).toEqual(router.createUrlTree(['/login']));
  });
});
