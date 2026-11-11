import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DealsService, CompanyDealSummary } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * Company deals list: the deals the user created or is the counterparty of.
 */
@Component({
  selector: 'app-company-deals',
  standalone: true,
  imports: [RouterLink, AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen company-page">
      <app-header [title]="lang.t('company_title')" backUrl="/home">
        <div header-actions>
          <a
            routerLink="/company-deals/new"
            class="btn btn-pill company-new-btn"
          >
            + {{ lang.t('company_new') }}
          </a>
        </div>
      </app-header>

      <main class="app-page-shell">
        <div class="company-filters">
          @for (f of filters; track f) {
            <button
              type="button"
              class="company-filter-chip"
              [class.active]="statusFilter() === f"
              (click)="filter(f)"
            >
              {{ f === '' ? (lang.isAr() ? 'الكل' : 'All') : lang.t('company_status_' + f) }}
            </button>
          }
        </div>

        @if (isLoading()) {
          <div class="app-card" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('company_loading') }}</p>
          </div>
        } @else if (!deals().length) {
          <div class="app-card empty-deals-box" style="padding: 3rem 1.5rem; text-align: center;">
            <span style="font-size: 2.5rem; display: block; margin-bottom: 0.5rem;" aria-hidden="true">🏢</span>
            <h4 style="font-size: 1.1rem; color: #111827; margin-bottom: 0.35rem;">{{ lang.t('company_empty') }}</h4>
            <a routerLink="/company-deals/new" class="btn btn-black" style="margin-top: 1rem;">
              + {{ lang.t('company_new') }}
            </a>
          </div>
        } @else {
          <div class="company-grid">
            @for (deal of deals(); track deal.id) {
              <a class="app-card company-card" [routerLink]="['/company-deals', deal.id]">
                <div class="company-card-header">
                  <div class="company-icon" aria-hidden="true">🏢</div>
                  <div class="company-card-title-group">
                    <strong class="company-deal-title">{{ deal.title }}</strong>
                    <span class="company-counterparty">{{ deal.counterpartyName }}</span>
                  </div>
                  <span class="badge" [class]="getStatusBadgeClass(deal.status)">
                    {{ lang.t('company_status_' + deal.status) }}
                  </span>
                </div>

                <div class="company-card-footer">
                  <div class="company-amount-block">
                    <span class="company-total">{{ deal.totalAmount.toLocaleString() }}</span>
                    <span class="company-currency">{{ deal.currency }}</span>
                  </div>
                  @if (deal.startDate) {
                    <span class="company-date">📅 {{ deal.startDate }}</span>
                  }
                </div>
              </a>
            }
          </div>

          @if (hasMore()) {
            <div style="text-align: center; margin-top: 1rem;">
              <button type="button" class="btn btn-outline-coral" (click)="loadMore()">
                {{ lang.t('store_load_more') }}
              </button>
            </div>
          }
        }
      </main>

      <app-bottom-nav activeNav="company" />
    </div>
  `,
  styles: [`
    .company-new-btn {
      background: #ffffff;
      color: #eb5757 !important;
      font-weight: 850;
      min-height: 40px;
      padding: 0.45rem 1rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
      &:hover {
        background: #fdf2f2;
      }
    }
    .company-filters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .company-filter-chip {
      border: 1px solid #e5e7eb;
      background: #ffffff;
      color: #4b5563;
      border-radius: 999px;
      padding: 0.45rem 1rem;
      cursor: pointer;
      font-family: inherit;
      font-size: 0.85rem;
      font-weight: 700;
      transition: all 0.2s ease;
      user-select: none;
      &:hover {
        background: #f9fafb;
        border-color: #d1d5db;
      }
      &.active {
        background: #eb5757;
        border-color: #eb5757;
        color: #ffffff;
        box-shadow: 0 4px 12px rgba(235, 87, 87, 0.25);
      }
    }
    .company-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 1.25rem;
    }
    .company-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      text-decoration: none;
      color: inherit;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
      cursor: pointer;
      &:hover {
        transform: translateY(-2px);
        box-shadow: 0 12px 30px rgba(17, 24, 39, 0.08);
      }
    }
    .company-card-header {
      display: flex;
      align-items: flex-start;
      gap: 0.75rem;
    }
    .company-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: #fef2f2;
      color: #eb5757;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.3rem;
      flex-shrink: 0;
    }
    .company-card-title-group {
      flex: 1;
      min-width: 0;
    }
    .company-deal-title {
      display: block;
      font-size: 1.05rem;
      color: #111827;
      font-weight: 800;
      line-height: 1.35;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .company-counterparty {
      display: block;
      color: #6b7280;
      font-size: 0.82rem;
      margin-top: 0.15rem;
    }
    .company-card-footer {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 0.5rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;
    }
    .company-amount-block {
      display: flex;
      align-items: baseline;
      gap: 0.25rem;
    }
    .company-total {
      font-weight: 850;
      font-size: 1.25rem;
      color: #111827;
    }
    .company-currency {
      color: #6b7280;
      font-size: 0.82rem;
      font-weight: 700;
    }
    .company-date {
      color: #9ca3af;
      font-size: 0.8rem;
    }
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

  protected getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'active':
      case 'completed':
        return 'badge-success';
      case 'pending':
        return 'badge-warning';
      case 'rejected':
      case 'cancelled':
        return 'badge-danger';
      default:
        return 'badge-neutral';
    }
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
