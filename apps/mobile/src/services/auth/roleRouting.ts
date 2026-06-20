import {AppRole} from '@midi-kaval/shared-types';

export type AuthDestination = 'auth' | 'tabs' | 'web-only';

export function resolveAuthDestination(
  isAuthenticated: boolean,
  role: string | null | undefined,
): AuthDestination {
  if (!isAuthenticated) {
    return 'auth';
  }

  if (role === AppRole.Director || role === AppRole.Coordinator) {
    return 'web-only';
  }

  if (role === AppRole.SocialWorker || role === AppRole.CaseWorker) {
    return 'tabs';
  }

  return 'auth';
}

export function isFieldRole(role: string | null | undefined): boolean {
  return role === AppRole.SocialWorker || role === AppRole.CaseWorker;
}

export function isSupervisorRole(role: string | null | undefined): boolean {
  return role === AppRole.Director || role === AppRole.Coordinator;
}
