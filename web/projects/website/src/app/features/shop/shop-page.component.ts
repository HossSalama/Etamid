import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DealsService, ShopProduct } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * The shop feed: every seller's products, newest first, for any logged-in
 * user. Each card links to the seller's store page.
 */
@Component({
  selector: 'app-shop-page',
  standalone: true,
  imports: [RouterLink, AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen shop-page">
      <app-header [title]="lang.t('shop_title')" backUrl="/home" />

      <main class="app-page-shell">
        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('shop_loading') }}</p>
          </div>
        } @else if (!products().length) {
          <div class="app-card text-center" style="padding: 3rem 1.5rem; text-align: center;">
            <span style="font-size: 2.5rem; display: block; margin-bottom: 0.5rem;" aria-hidden="true">🛍️</span>
            <h4 style="font-size: 1.1rem; color: #111827; margin-bottom: 0.35rem;">{{ lang.t('shop_empty') }}</h4>
          </div>
        } @else {
          <div class="shop-grid">
            @for (product of products(); track product.id) {
              <article class="shop-card">
                @if (product.imageUrl) {
                  <img class="shop-thumb" [src]="product.imageUrl" [alt]="product.name" />
                } @else {
                  <div class="shop-thumb-empty">{{ product.name.charAt(0) }}</div>
                }
                <div class="shop-body">
                  <strong class="shop-product-title">{{ product.name }}</strong>
                  @if (product.description) {
                    <p class="shop-desc">{{ product.description }}</p>
                  }
                  <div class="shop-meta-row">
                    <p class="shop-price">{{ product.price }} {{ lang.t('product_currency') }}</p>
                    <span [class]="product.quantity > 0 ? 'badge badge-success' : 'badge badge-danger'">
                      {{ product.quantity > 0 ? lang.t('product_available') + ' · ' + product.quantity : lang.t('product_out_of_stock') }}
                    </span>
                  </div>
                  <a class="shop-seller" [routerLink]="['/store', product.sellerId]">
                    🏪 {{ product.storeName ?? product.sellerName }}
                  </a>
                  <button type="button" class="btn btn-black shop-order-btn" (click)="order(product)">
                    {{ lang.t('shop_order') }}
                  </button>
                </div>
              </article>
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

      <app-bottom-nav activeNav="shop" />
    </div>
  `,
  styles: [`
    .shop-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 1.25rem;
    }
    .shop-card {
      border: 1px solid #e5e7eb;
      border-radius: 18px;
      overflow: hidden;
      background: #fff;
      box-shadow: 0 8px 24px rgba(17, 24, 39, 0.04);
      display: flex;
      flex-direction: column;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
      &:hover {
        transform: translateY(-2px);
        box-shadow: 0 12px 30px rgba(17, 24, 39, 0.08);
      }
    }
    .shop-thumb, .shop-thumb-empty {
      width: 100%;
      height: 180px;
      object-fit: cover;
    }
    .shop-thumb-empty {
      display: flex;
      align-items: center;
      justify-content: center;
      background: #fff1f2;
      color: #eb5757;
      font-size: 2.2rem;
      font-weight: 800;
    }
    .shop-body {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
      padding: 1.25rem;
      flex: 1;
    }
    .shop-product-title {
      font-size: 1.05rem;
      font-weight: 800;
      color: #111827;
      line-height: 1.35;
    }
    .shop-desc {
      color: #6b7280;
      font-size: 0.85rem;
      margin: 0;
      line-height: 1.5;
      overflow-wrap: anywhere;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .shop-meta-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      margin-top: 0.25rem;
    }
    .shop-price {
      font-weight: 850;
      font-size: 1.2rem;
      color: #111827;
      margin: 0;
    }
    .shop-seller {
      margin-top: 0.25rem;
      font-weight: 700;
      color: #eb5757;
      font-size: 0.88rem;
      text-decoration: none;
      &:hover {
        text-decoration: underline;
      }
    }
    .shop-order-btn {
      width: 100%;
      margin-top: 0.5rem;
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
