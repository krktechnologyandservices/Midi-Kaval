import { Routes } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { authGuard, guestGuard, otpGuard } from './core/auth/auth.guard';
import { directorGuard } from './core/auth/director.guard';
import { roleGuard } from './core/auth/role.guard';
import { supervisorGuard } from './core/auth/supervisor.guard';
import { twoFactorSetupGuard } from './core/auth/two-factor-setup.guard';
import { vendorGuard } from './core/auth/vendor.guard';

const coordinatorOrAboveGuard = roleGuard([AppRole.Director, AppRole.Coordinator], '/budgets');
const budgetViewerGuard = roleGuard(
  [AppRole.Director, AppRole.Coordinator, AppRole.Accountant],
  '/crisis-queue',
);

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'login/otp',
    canActivate: [otpGuard],
    loadComponent: () =>
      import('./features/auth/otp/otp.component').then((m) => m.OtpComponent),
  },
  {
    path: 'login/totp',
    loadComponent: () =>
      import('./features/auth/totp-login/totp-login.component').then(
        (m) => m.TotpLoginComponent,
      ),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent,
      ),
  },
  {
    path: 'session-expired',
    loadComponent: () =>
      import('./features/auth/session-expired/session-expired.component').then(
        (m) => m.SessionExpiredComponent,
      ),
  },
  {
    path: 'mobile-only',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/auth/mobile-only/mobile-only.component').then(
        (m) => m.MobileOnlyComponent,
      ),
  },
  {
    path: 'settings/2fa',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/components/2fa/two-factor-enrollment.component').then(
        (m) => m.TwoFactorEnrollmentComponent,
      ),
    data: { pageMode: true },
  },
  {
    path: 'activate',
    loadComponent: () =>
      import('./features/activation/activation.component').then(
        (m) => m.ActivationComponent,
      ),
  },
  {
    path: 'invite',
    loadComponent: () =>
      import('./features/invitation-accept/invitation-accept.component').then(
        (m) => m.InvitationAcceptComponent,
      ),
  },
  {
    path: 'confirm-email',
    loadComponent: () =>
      import('./features/email-confirmed/email-confirmed.component').then(
        (m) => m.EmailConfirmedComponent,
      ),
  },
  {
    path: '',
    canActivate: [authGuard, twoFactorSetupGuard, supervisorGuard],
    loadComponent: () =>
      import('./features/shell/supervisor-shell.component').then(
        (m) => m.SupervisorShellComponent,
      ),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'crisis-queue' },
      {
        path: 'home',
        loadComponent: () =>
          import('./features/home/supervisor-home.component').then(
            (m) => m.SupervisorHomeComponent,
          ),
      },
      {
        path: 'crisis-queue',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/shell/pages/crisis-queue-page.component').then(
            (m) => m.CrisisQueuePageComponent,
          ),
      },
      {
        path: 'crisis-queue/travel-claims/:id',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/travel/travel-claim-review.component').then(
            (m) => m.TravelClaimReviewComponent,
          ),
        data: { readOnly: true },
      },
      {
        path: 'dashboard',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/shell/pages/dashboard-page.component').then(
            (m) => m.DashboardPageComponent,
          ),
      },
      {
        path: 'cases',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/cases/registry/case-registry.component').then(
            (m) => m.CaseRegistryComponent,
          ),
      },
      {
        path: 'cases/new',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/cases/create/case-create.component').then(
            (m) => m.CaseCreateComponent,
          ),
      },
      {
        path: 'cases/:id',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/cases/detail/case-detail-placeholder.component').then(
            (m) => m.CaseDetailPlaceholderComponent,
          ),
      },
      {
        path: 'reports',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/shell/pages/reports-page.component').then(
            (m) => m.ReportsPageComponent,
          ),
      },
      {
        path: 'legends',
        canActivate: [coordinatorOrAboveGuard],
        loadComponent: () =>
          import('./features/shell/pages/legends-page.component').then(
            (m) => m.LegendsPageComponent,
          ),
      },
      {
        path: 'budgets',
        canActivate: [budgetViewerGuard],
        loadComponent: () =>
          import('./features/budgets/budgets-list-page.component').then(
            (m) => m.BudgetsListPageComponent,
          ),
      },
      {
        path: 'budgets/:id/utilizations',
        canActivate: [budgetViewerGuard],
        loadComponent: () =>
          import('./features/budgets/budget-utilizations-page.component').then(
            (m) => m.BudgetUtilizationsPageComponent,
          ),
      },
      {
        path: 'budgets/:id',
        canActivate: [budgetViewerGuard],
        loadComponent: () =>
          import('./features/budgets/budget-detail-page.component').then(
            (m) => m.BudgetDetailPageComponent,
          ),
      },
      {
        path: 'admin',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/admin/admin.component').then(
            (m) => m.AdminShellComponent,
          ),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'team' },
          {
            path: 'team',
            loadComponent: () =>
              import('./features/admin/pages/team-roster/team-roster.component').then(
                (m) => m.TeamRosterComponent,
              ),
          },
          {
            path: 'invitations',
            loadComponent: () =>
              import('./features/admin/pages/invitations/invitations.component').then(
                (m) => m.InvitationsComponent,
              ),
          },
          {
            path: 'settings',
            loadComponent: () =>
              import('./features/admin/pages/settings/organisation-settings.component').then(
                (m) => m.OrganisationSettingsComponent,
              ),
          },
          {
            path: 'audit',
            loadComponent: () =>
              import('./features/admin/pages/audit-log/audit-log.component').then(
                (m) => m.AuditLogComponent,
              ),
          },
        ],
      },
      {
        path: 'admin/staff',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/shell/pages/staff-page.component').then(
            (m) => m.StaffPageComponent,
          ),
      },
      {
        path: 'admin/import',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/shell/pages/import-page.component').then(
            (m) => m.ImportPageComponent,
          ),
      },
      {
        path: 'admin/travel-claims',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/travel/travel-claims-pending-list.component').then(
            (m) => m.TravelClaimsPendingListComponent,
          ),
      },
      {
        path: 'admin/travel-claims/:id',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/travel/travel-claim-review.component').then(
            (m) => m.TravelClaimReviewComponent,
          ),
        data: { readOnly: false },
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/notifications/notification-list-page.component').then(
            (m) => m.NotificationListPageComponent,
          ),
      },
    ],
  },
  {
    path: 'vendor',
    canActivate: [authGuard, twoFactorSetupGuard, vendorGuard],
    loadComponent: () =>
      import('./features/vendor/vendor.component').then((m) => m.VendorComponent),
  },
  {
    path: 'vendor/settings',
    canActivate: [authGuard, twoFactorSetupGuard, vendorGuard],
    loadComponent: () =>
      import('./features/vendor/settings/vendor-settings.component').then(
        (m) => m.VendorSettingsComponent,
      ),
  },
  {
    path: '**',
    redirectTo: 'login',
  },
];
