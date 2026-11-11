import { Routes } from '@angular/router';
import { adminAuthGuard } from './core/guards/admin-auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'login/2fa',
    loadComponent: () =>
      import('./features/auth/verify-2fa/verify-2fa.component').then((m) => m.Verify2faComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./features/shell/admin-shell.component').then((m) => m.AdminShellComponent),
    canActivate: [adminAuthGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'withdrawals',
        loadComponent: () =>
          import('./features/withdrawals/withdrawals.component').then((m) => m.WithdrawalsComponent),
      },
      {
        path: 'penalties',
        loadComponent: () =>
          import('./features/penalties/penalties.component').then((m) => m.PenaltiesComponent),
      },
      {
        path: 'disputes',
        loadComponent: () =>
          import('./features/disputes/disputes.component').then((m) => m.DisputesComponent),
      },
      {
        path: 'disputes/:id',
        loadComponent: () =>
          import('./features/disputes/dispute-detail.component').then(
            (m) => m.DisputeDetailComponent,
          ),
      },
      {
        path: 'deals',
        loadComponent: () =>
          import('./features/deals/deals.component').then((m) => m.DealsComponent),
      },
      {
        path: 'admins',
        loadComponent: () =>
          import('./features/admins/admins.component').then((m) => m.AdminsComponent),
      },
      {
        path: 'admins/:id',
        loadComponent: () =>
          import('./features/admins/admin-detail.component').then((m) => m.AdminDetailComponent),
      },
      {
        path: 'roles',
        loadComponent: () =>
          import('./features/roles/roles.component').then((m) => m.RolesComponent),
      },
      {
        path: 'roles/:id',
        loadComponent: () =>
          import('./features/roles/role-detail.component').then((m) => m.RoleDetailComponent),
      },
      {
        path: 'permissions',
        redirectTo: 'dashboard',
      },
      {
        path: 'audit-log',
        loadComponent: () =>
          import('./features/audit-log/audit-log.component').then((m) => m.AuditLogComponent),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/profile/profile.component').then((m) => m.ProfileComponent),
      },
      {
        path: '**',
        redirectTo: 'dashboard',
      },
    ],
  },
  {
    path: '**',
    redirectTo: 'login',
  },
];
