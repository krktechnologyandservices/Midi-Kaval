import { Routes } from '@angular/router';
import { authGuard, guestGuard, otpGuard } from './core/auth/auth.guard';
import { directorGuard } from './core/auth/director.guard';
import { supervisorGuard } from './core/auth/supervisor.guard';
import { vendorGuard } from './core/auth/vendor.guard';

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
    path: 'activate',
    loadComponent: () =>
      import('./features/activation/activation.component').then(
        (m) => m.ActivationComponent,
      ),
  },
  {
    path: '',
    canActivate: [authGuard, supervisorGuard],
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
        loadComponent: () =>
          import('./features/shell/pages/crisis-queue-page.component').then(
            (m) => m.CrisisQueuePageComponent,
          ),
      },
      {
        path: 'crisis-queue/travel-claims/:id',
        loadComponent: () =>
          import('./features/travel/travel-claim-review.component').then(
            (m) => m.TravelClaimReviewComponent,
          ),
        data: { readOnly: true },
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/shell/pages/dashboard-page.component').then(
            (m) => m.DashboardPageComponent,
          ),
      },
      {
        path: 'cases',
        loadComponent: () =>
          import('./features/cases/registry/case-registry.component').then(
            (m) => m.CaseRegistryComponent,
          ),
      },
      {
        path: 'cases/new',
        loadComponent: () =>
          import('./features/cases/create/case-create.component').then(
            (m) => m.CaseCreateComponent,
          ),
      },
      {
        path: 'cases/:id',
        loadComponent: () =>
          import('./features/cases/detail/case-detail-placeholder.component').then(
            (m) => m.CaseDetailPlaceholderComponent,
          ),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/shell/pages/reports-page.component').then(
            (m) => m.ReportsPageComponent,
          ),
      },
      {
        path: 'legends',
        loadComponent: () =>
          import('./features/shell/pages/legends-page.component').then(
            (m) => m.LegendsPageComponent,
          ),
      },
      {
        path: 'budgets',
        loadComponent: () =>
          import('./features/budgets/budgets-list-page.component').then(
            (m) => m.BudgetsListPageComponent,
          ),
      },
      {
        path: 'budgets/:id',
        loadComponent: () =>
          import('./features/budgets/budget-detail-page.component').then(
            (m) => m.BudgetDetailPageComponent,
          ),
      },
      {
        path: 'admin',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/shell/pages/admin-page.component').then(
            (m) => m.AdminPageComponent,
          ),
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
        path: 'admin/audit',
        canActivate: [directorGuard],
        loadComponent: () =>
          import('./features/shell/pages/audit-log-page.component').then(
            (m) => m.AuditLogPageComponent,
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
    canActivate: [authGuard, vendorGuard],
    loadComponent: () =>
      import('./features/vendor/vendor.component').then((m) => m.VendorComponent),
  },
  {
    path: '**',
    redirectTo: 'login',
  },
];
