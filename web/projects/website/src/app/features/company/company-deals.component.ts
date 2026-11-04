import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DealsService, CompanyDealSummary } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * Company deals list: the deals the user created or is the counterparty of.
 * Requires login, like the shop page; guests are sent to the login page.
 */
@Component({
  selector: 'app-company-deals',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="company-page">
      <header class="company-header-card">
        <a class="company-back" routerLink="/home">→ {{ lang.t('back') }}</a>
        <h1>{{ lang.t('company_title') }}</h1>
        <a class="company-new" routerLink="/company-deals/new">{{ lang.t('company_new') }}</a>
      </header>
      <div class="company-content">
        <div class="company-filters">
          @for (f of filters; track f) {
            <button type="button" class="company-filter" [class.active]="statusFilter() === f" (click)="filter(f)">
              {{ f === '' ? lang.t('company_title') : lang.t('company_status_' + f) }}
            </button>
          }
        </div>
        @if (isLoading()) {
          <p>{{ lang.t('company_loading') }}</p>
        } @else if (!deals().length) {
          <p>{{ lang.t('company_empty') }}</p>
        }
        <div class="company-grid">
          @for (deal of deals(); track deal.id) {
            <a class="company-card" [routerLink]="['/company-deals', deal.id]">
              <strong>{{ deal.title }}</strong>
              <span>{{ deal.counterpartyName }}</span>
              <span class="company-total">{{ deal.totalAmount }} {{ deal.currency }}</span>
              <span class="company-status">{{ lang.t('company_status_' + deal.status) }}</span>
              <span class="company-date">{{ deal.startDate }}</span>
            </a>
          }
        </div>
        @if (hasMore()) {
          <button type="button" class="company-load-more" (click)="loadMore()">
            {{ lang.t('store_load_more') }}
          </button>
        }
      </div>
    </main>
  `,
  styles: [`
    /* Full-bleed screen like the shop page: the header spans the whole
       width and only the inner content is constrained. Button styles are
       local: .btn/.btn-black live only inside the auth/home components. */
    .company-page { width: 100%; min-height: 100vh; padding: 0 0 3rem; }
    .company-header-card { background: #eb5757; border-radius: 0 0 28px 28px; padding: clamp(1.25rem, 3vw, 2.5rem) clamp(1rem, 5vw, 4.5rem) clamp(1.5rem, 3vw, 2.25rem); color: #fff; }
    .company-header-card h1 { margin: 0.5rem 0 0; color: #fff; font-size: clamp(1.8rem, 3vw, 2.6rem); }
    .company-back { color: #fff; text-decoration: none; font-weight: 700; }
    .company-new { display: inline-block; margin-top: 1rem; background: #fff; color: #eb5757; font-weight: 800; padding: 0.6rem 1.25rem; border-radius: 999px; text-decoration: none; }
    .company-content { width: min(1280px, calc(100% - 2rem)); margin: 1.5rem auto 0; }
    .company-filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; }
    .company-filter { border: 1px solid #e5e7eb; background: #fff; border-radius: 999px; padding: 0.4rem 1rem; cursor: pointer; }
    .company-filter.active { background: #eb5757; border-color: #eb5757; color: #fff; }
    .company-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 1rem; margin: 0.75rem 0 1rem; }
    .company-card { display: flex; flex-direction: column; gap: 0.25rem; border: 1px solid #e5e7eb; border-radius: 18px; background: #fff; padding: 1rem 1.25rem; text-decoration: none; color: inherit; box-shadow: 0 8px 24px rgba(17, 24, 39, 0.04); }
    .company-total { font-weight: 800; font-size: 1.1rem; }
    .company-status { opacity: 0.75; font-size: 0.85rem; }
    .company-date { opacity: 0.6; font-size: 0.85rem; }
    .company-load-more { border: 1px solid #e5e7eb; background: #fff; border-radius: 999px; padding: 0.6rem 1.5rem; cursor: pointer; font-weight: 700; }
  `],
})
export class CompanyDealsComponent implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly lang = inject(LanguageService);

  protected readonly deals = signal<CompanyDealSummary[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly statusFilter = signal('');
  protected readonly hasMore = signal(false);
  protected readonly filters = ['', 'draft', 'pending', 'active', 'completed', 'rejected', 'cancelled'];
  private page = 1;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.load();
  }

  protected filter(status: string): void {
    this.statusFilter.set(status);
    this.page = 1;
    this.load();
  }

  protected loadMore(): void {
    this.page += 1;
    this.load(true);
  }

  private load(append = false): void {
    this.isLoading.set(true);
    const status = this.statusFilter() || undefined;
    this.dealsService.getCompanyDeals(status, this.page).subscribe({
      next: (res) => {
        const items = res.data ?? [];
        this.deals.set(append ? [...this.deals(), ...items] : items);
        this.hasMore.set((res.meta?.total ?? 0) > this.deals().length);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
