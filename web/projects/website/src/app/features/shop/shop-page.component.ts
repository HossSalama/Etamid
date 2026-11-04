import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DealsService, ShopProduct } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * The shop feed: every seller's products, newest first, for any logged-in
 * user. Each card links to the seller's store page. Requires login, like the
 * store page; guests are sent to the login page.
 */
@Component({
  selector: 'app-shop-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="shop-page">
      <header class="shop-header-card">
        <a class="shop-back" routerLink="/home">→ {{ lang.t('back') }}</a>
        <h1>{{ lang.t('shop_title') }}</h1>
      </header>
      <div class="shop-content">
        @if (isLoading()) {
          <p>{{ lang.t('shop_loading') }}</p>
        } @else if (!products().length) {
          <p>{{ lang.t('shop_empty') }}</p>
        }
        <div class="shop-grid">
          @for (product of products(); track product.id) {
            <article class="shop-card">
              @if (product.imageUrl) {
                <img class="shop-thumb" [src]="product.imageUrl" [alt]="product.name" />
              } @else {
                <div class="shop-thumb-empty">{{ product.name.charAt(0) }}</div>
              }
              <div class="shop-body">
                <strong>{{ product.name }}</strong>
                @if (product.description) {
                  <p class="shop-desc">{{ product.description }}</p>
                }
                <p class="shop-price">{{ product.price }} {{ lang.t('product_currency') }}</p>
                <span [class]="product.quantity > 0 ? 'store-stock-ok' : 'store-stock-out'">
                  {{ product.quantity > 0 ? lang.t('product_available') + ' · ' + product.quantity : lang.t('product_out_of_stock') }}
                </span>
                <a class="shop-seller" [routerLink]="['/store', product.sellerId]">
                  {{ product.storeName ?? product.sellerName }}
                </a>
                <button type="button" class="shop-order" (click)="order(product)">
                  {{ lang.t('shop_order') }}
                </button>
              </div>
            </article>
          }
        </div>
        @if (hasMore()) {
          <button type="button" class="shop-load-more" (click)="loadMore()">
            {{ lang.t('store_load_more') }}
          </button>
        }
      </div>
    </main>
  `,
  styles: [`
    /* Full-bleed screen like the home screen: the header spans the whole
       width and only the inner content is constrained, so nothing floats
       as a narrow block in the middle of the page. */
    .shop-page { width: 100%; min-height: 100vh; padding: 0 0 3rem; }
    /* Same header treatment as the home header card: full width, padding
       grows with the viewport instead of an inner container. */
    .shop-header-card { background: #eb5757; border-radius: 0 0 28px 28px; padding: clamp(1.25rem, 3vw, 2.5rem) clamp(1rem, 5vw, 4.5rem) clamp(1.5rem, 3vw, 2.25rem); color: #fff; }
    .shop-header-card h1 { margin: 0.5rem 0 0; color: #fff; font-size: clamp(1.8rem, 3vw, 2.6rem); }
    .shop-back { color: #fff; text-decoration: none; font-weight: 700; }
    .shop-content { width: min(1280px, calc(100% - 2rem)); margin: 1.5rem auto 0; }
    .shop-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 1rem; margin: 0.75rem 0 1rem; }
    .shop-card { border: 1px solid #e5e7eb; border-radius: 18px; overflow: hidden; background: #fff; box-shadow: 0 8px 24px rgba(17, 24, 39, 0.04); }
    .shop-thumb, .shop-thumb-empty { width: 100%; height: 180px; object-fit: cover; }
    .shop-thumb-empty { display: flex; align-items: center; justify-content: center; background: #fff1f2; color: #eb5757; font-size: 2rem; font-weight: 800; }
    .shop-body { display: flex; flex-direction: column; gap: 0.25rem; padding: 1rem 1.25rem 1.25rem; }
    .shop-desc { opacity: 0.75; font-size: 0.85rem; margin: 0; overflow-wrap: anywhere; }
    .shop-price { font-weight: 800; font-size: 1.1rem; margin: 0.25rem 0 0; }
    .store-stock-ok, .store-stock-out { width: fit-content; font-size: 0.8rem; font-weight: 700; padding: 0.1rem 0.5rem; border-radius: 999px; }
    .store-stock-ok { background: #eef6ee; color: #047857; }
    .store-stock-out { background: #fef2f2; color: #b91c1c; }
    .shop-seller { margin-top: 0.25rem; font-weight: 700; color: #eb5757; text-decoration: none; }
    /* Button styles are local: .btn/.btn-black live only inside the auth
       and home components' scoped styles, so reusing those class names
       here rendered an unstyled browser button. */
    .shop-order { width: 100%; margin-top: 0.5rem; background: #111827; color: #fff; border: none; border-radius: 12px; padding: 0.7rem 1rem; font-weight: 700; cursor: pointer; }
    .shop-order:hover { background: #000; }
    .shop-load-more { background: #fff; border: 1.5px solid #eb5757; color: #eb5757; border-radius: 12px; padding: 0.7rem 1.25rem; font-weight: 700; cursor: pointer; }
    .shop-load-more:hover { background: #fef2f2; }
    @media (min-width: 768px) {
      .shop-header-card { border-radius: 0 0 36px 36px; }
    }
  `],
})
export class ShopPageComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  readonly products = signal<ShopProduct[]>([]);
  readonly isLoading = signal(true);
  readonly hasMore = signal(false);
  private page = 1;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.load();
  }

  loadMore(): void {
    this.page += 1;
    this.load();
  }

  /**
   * Opens the new-deal form with this product prefilled: the seller is looked
   * up by code, and the item name, description and price are already filled.
   * The buyer still completes duration and shipping before creating the deal.
   * The product photo does not carry over (a deal photo must be the buyer's
   * own upload), and the stock is not changed by creating a deal.
   * A company seller's product opens the company-deal wizard instead, with
   * the same prefill (one installment for the price; the buyer picks the
   * due date and start date there).
   */
  order(product: ShopProduct): void {
    const target = product.isCompany ? '/company-deals/new' : '/deals/new';
    void this.router.navigate([target], {
      state: {
        fromProduct: {
          sellerCode: product.sellerCode,
          name: product.name,
          description: product.description,
          price: product.price,
        },
      },
    });
  }

  private load(): void {
    this.dealsApi.getShopProducts(this.page).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) return;
        this.products.update((current) => [...current, ...response.data!]);
        const total = response.meta?.total ?? this.products().length;
        this.hasMore.set(this.products().length < total);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
