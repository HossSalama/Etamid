import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'home',
  },
  {
    path: 'auth/welcome',
    loadComponent: () =>
      import('./features/auth/welcome/welcome.component').then(
        (m) => m.WelcomeComponent
      ),
  },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then(
        (m) => m.LoginComponent
      ),
  },
  {
    path: 'auth/forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent
      ),
  },
  {
    path: 'auth/register-individual',
    loadComponent: () =>
      import('./features/auth/register-individual/register-individual.component').then(
        (m) => m.RegisterIndividualComponent
      ),
  },
  {
    path: 'auth/register-company',
    loadComponent: () =>
      import('./features/auth/register-company/register-company.component').then(
        (m) => m.RegisterCompanyComponent
      ),
  },
  {
    path: 'auth/verify-otp',
    loadComponent: () =>
      import('./features/auth/verify-otp/verify-otp.component').then(
        (m) => m.VerifyOtpComponent
      ),
  },
  {
    path: 'home',
    loadComponent: () =>
      import('./features/dashboard/home.component').then(
        (m) => m.HomeComponent
      ),
  },
  {
    path: 'deals/new',
    loadComponent: () =>
      import('./features/deals/new-deal.component').then(
        (m) => m.NewDealComponent
      ),
  },
  {
    path: '**',
    redirectTo: 'home',
  },
];
