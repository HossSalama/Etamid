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
    path: 'about',
    loadComponent: () =>
      import('./features/marketing/about/about.component').then(
        (m) => m.AboutComponent
      ),
  },
  {
    path: 'pricing',
    loadComponent: () =>
      import('./features/marketing/pricing/pricing.component').then(
        (m) => m.PricingComponent
      ),
  },
  {
    path: 'faq',
    loadComponent: () =>
      import('./features/marketing/faq/faq.component').then(
        (m) => m.FaqComponent
      ),
  },
  {
    path: 'contact',
    loadComponent: () =>
      import('./features/marketing/contact/contact.component').then(
        (m) => m.ContactComponent
      ),
  },
  {
    path: 'features',
    loadComponent: () =>
      import('./features/marketing/features/features.component').then(
        (m) => m.FeaturesPageComponent
      ),
  },
  {
    path: 'how-it-works',
    loadComponent: () =>
      import('./features/marketing/how-it-works/how-it-works.component').then(
        (m) => m.HowItWorksPageComponent
      ),
  },
  {
    path: 'terms',
    loadComponent: () =>
      import('./features/marketing/terms/terms.component').then(
        (m) => m.TermsComponent
      ),
  },
  {
    path: 'privacy',
    loadComponent: () =>
      import('./features/marketing/privacy/privacy.component').then(
        (m) => m.PrivacyComponent
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
    path: 'deals/:id/dispute',
    loadComponent: () =>
      import('./features/disputes/dispute-page.component').then(
        (m) => m.DisputePageComponent
      ),
  },
  {
    path: 'addresses',
    loadComponent: () =>
      import('./features/profile/my-addresses.component').then(
        (m) => m.MyAddressesComponent
      ),
  },
  {
    path: 'store/:sellerId',
    loadComponent: () =>
      import('./features/store/store-page.component').then(
        (m) => m.StorePageComponent
      ),
  },
  {
    path: 'shop',
    loadComponent: () =>
      import('./features/shop/shop-page.component').then(
        (m) => m.ShopPageComponent
      ),
  },
  {
    path: 'company-deals',
    loadComponent: () =>
      import('./features/company/company-deals.component').then(
        (m) => m.CompanyDealsComponent
      ),
  },
  {
    path: 'company-deals/new',
    loadComponent: () =>
      import('./features/company/company-deal-wizard.component').then(
        (m) => m.CompanyDealWizardComponent
      ),
  },
  {
    path: 'company-deals/:id',
    loadComponent: () =>
      import('./features/company/company-deal-detail.component').then(
        (m) => m.CompanyDealDetailComponent
      ),
  },
  {
    path: 'company-deals/:id/edit',
    loadComponent: () =>
      import('./features/company/company-deal-wizard.component').then(
        (m) => m.CompanyDealWizardComponent
      ),
  },
  {
    path: '**',
    redirectTo: 'home',
  },
];
