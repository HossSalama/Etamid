import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DealsService, Product, SellerReview, SellerStore } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * A seller's public store page: banner, name, category, rating, successful
 * deals and visible reviews.
 */
@Component({
  selector: 'app-store-page',
  standalone: true,
  imports: [RouterLink, DatePipe, AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen store-page">
      <app-header
        [title]="store() ? (store()!.storeName ?? store()!.sellerName) : lang.t('product_store_title')"
        [subtitle]="store()?.category ?? ''"
        backUrl="/shop"
      />

      <main class="app-page-shell" style="max-width: 920px;">
        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('store_loading') }}</p>
          </div>
        } @else if (notFound()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p class="profile-message profile-error" role="alert">{{ lang.t('store_not_found') }}</p>
            <a routerLink="/shop" class="btn btn-outline-coral" style="margin-top: 1rem;">{{ lang.t('back') }}</a>
          </div>
        } @else if (store(); as s) {
          <!-- Store Hero Card -->
          <section class="app-card store-hero">
            @if (s.bannerUrl) {
              <img class="store-banner" [src]="s.bannerUrl" [alt]="s.storeName ?? s.sellerName" />
            }
            <div class="store-hero-body">
              <h1 class="store-name-title">{{ s.storeName ?? s.sellerName }}</h1>
              @if (s.storeName) {
                <p class="store-seller">👤 {{ s.sellerName }}</p>
              }
              <div class="store-stats">
                <span class="badge badge-warning">★ {{ s.ratingAvg.toFixed(1) }} ({{ s.ratingCount }})</span>
                <span class="badge badge-success">✓ {{ s.successfulDeals }} {{ lang.t('store_successful_deals') }}</span>
              </div>
            </div>
          </section>

          <!-- Store Products -->
          <section class="store-products-section">
            <h2 class="section-title">{{ lang.t('product_store_title') }}</h2>
            @if (!products().length && !productsLoading()) {
              <div class="app-card" style="padding: 2rem; text-align: center;">
                <p>{{ lang.t('product_no_products') }}</p>
              </div>
            }
            <div class="store-products-grid">
              @for (product of products(); track product.id) {
                <article class="store-product-card">
                  @if (product.imageUrl) {
                    <img class="store-product-thumb" [src]="product.imageUrl" [alt]="product.name" />
                  } @else {
                    <div class="store-product-thumb-empty">{{ product.name.charAt(0) }}</div>
                  }
                  <div class="store-product-body">
                    <strong class="product-title">{{ product.name }}</strong>
                    @if (product.description) {
                      <p class="store-product-desc">{{ product.description }}</p>
                    }
                    <div class="product-footer-row">
                      <p class="store-product-price">{{ product.price }} {{ lang.t('product_currency') }}</p>
                      <span [class]="product.quantity > 0 ? 'badge badge-success' : 'badge badge-danger'">
                        {{ product.quantity > 0 ? lang.t('product_available') + ' · ' + product.quantity : lang.t('product_out_of_stock') }}
                      </span>
                    </div>
                  </div>
                </article>
              }
            </div>
            @if (productsHasMore()) {
              <div style="text-align: center; margin-top: 1rem;">
                <button type="button" class="btn btn-outline-coral" (click)="loadMoreProducts()">
                  {{ lang.t('store_load_more') }}
                </button>
              </div>
            }
          </section>

          <!-- Store Reviews -->
          <section class="store-reviews-section">
            <h2 class="section-title">{{ lang.t('store_reviews') }}</h2>
            @if (!reviews().length) {
              <div class="app-card" style="padding: 2rem; text-align: center;">
                <p>{{ lang.t('store_no_reviews') }}</p>
              </div>
            }
            <div class="reviews-list">
              @for (review of reviews(); track review.id) {
                <article class="app-card store-review">
                  <div class="store-review-head">
                    <strong class="reviewer-name">{{ review.raterName }}</strong>
                    <span class="badge badge-warning">★ {{ review.stars }}</span>
                  </div>
                  <p class="store-review-deal">{{ review.dealTitle }}</p>
                  @if (review.comment) {
                    <p class="review-comment">{{ review.comment }}</p>
                  }
                  <small class="review-date">🕒 {{ review.createdAt | date }}</small>
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
          </section>
        }
      </main>

      <app-bottom-nav activeNav="shop" />
    </div>
  `,
  styles: [`
    .store-hero {
      overflow: hidden;
      padding: 0;
    }
    .store-banner {
      width: 100%;
      max-height: 240px;
      object-fit: cover;
      display: block;
    }
    .store-hero-body {
      padding: 1.5rem;
    }
    .store-name-title {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 850;
      color: #111827;
    }
    .store-seller {
      color: #6b7280;
      margin: 0.35rem 0 0.75rem;
      font-size: 0.9rem;
    }
    .store-stats {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .section-title {
      font-size: 1.25rem;
      font-weight: 850;
      color: #111827;
      margin: 0.5rem 0 0.75rem;
    }
    .store-products-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 1rem;
    }
    .store-product-card {
      border: 1px solid #e5e7eb;
      border-radius: 16px;
      overflow: hidden;
      background: #fff;
      display: flex;
      flex-direction: column;
      box-shadow: 0 4px 16px rgba(17, 24, 39, 0.04);
    }
    .store-product-thumb, .store-product-thumb-empty {
      width: 100%;
      height: 150px;
      object-fit: cover;
    }
    .store-product-thumb-empty {
      display: flex;
      align-items: center;
      justify-content: center;
      background: #fff1f2;
      color: #eb5757;
      font-size: 2rem;
      font-weight: 800;
    }
    .store-product-body {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      padding: 1rem;
      flex: 1;
    }
    .product-title {
      font-size: 0.95rem;
      color: #111827;
      font-weight: 800;
    }
    .store-product-desc {
      color: #6b7280;
      font-size: 0.82rem;
      margin: 0;
      line-height: 1.45;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .product-footer-row {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 0.5rem;
      margin-top: auto;
      padding-top: 0.5rem;
    }
    .store-product-price {
      font-weight: 850;
      font-size: 1.1rem;
      color: #111827;
      margin: 0;
    }
    .reviews-list {
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
    }
    .store-review {
      padding: 1.25rem;
    }
    .store-review-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
    }
    .reviewer-name {
      font-size: 0.95rem;
      color: #111827;
    }
    .store-review-deal {
      font-weight: 700;
      color: #374151;
      font-size: 0.88rem;
      margin: 0.35rem 0 0.2rem;
    }
    .review-comment {
      color: #4b5563;
      font-size: 0.88rem;
      line-height: 1.5;
      margin: 0;
    }
    .review-date {
      color: #9ca3af;
      font-size: 0.76rem;
      display: block;
      margin-top: 0.4rem;
    }
  `],
})
export class StorePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  readonly store = signal<SellerStore | null>(null);
  readonly reviews = signal<SellerReview[]>([]);
  readonly products = signal<Product[]>([]);
  readonly productsLoading = signal(true);
  readonly isLoading = signal(true);
  readonly notFound = signal(false);
  readonly hasMore = signal(false);
  readonly productsHasMore = signal(false);
  private page = 1;
  private productsPage = 1;
  private sellerId = 0;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    const id = Number(this.route.snapshot.paramMap.get('sellerId'));
    if (!Number.isFinite(id) || id <= 0) {
      this.isLoading.set(false);
      this.notFound.set(true);
      return;
    }
    this.sellerId = id;
    this.dealsApi.getSellerStore(id).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) {
          this.notFound.set(true);
          return;
        }
        this.store.set(response.data);
      },
      error: () => {
        this.isLoading.set(false);
        this.notFound.set(true);
      },
    });

    this.loadProducts();
    this.load();
  }

  loadProducts(): void {
    this.dealsApi.getSellerProducts(this.sellerId, this.productsPage).subscribe({
      next: (res) => {
        this.productsLoading.set(false);
        if (!res.success || !res.data) return;
        this.products.update((current) => [...current, ...res.data!]);
        const total = res.meta?.total ?? this.products().length;
        this.productsHasMore.set(this.products().length < total);
      },
      error: () => this.productsLoading.set(false),
    });
  }

  loadMoreProducts(): void {
    this.productsPage += 1;
    this.loadProducts();
  }

  loadMore(): void {
    this.page += 1;
    this.load();
  }

  stars(rating: number): string {
    const rounded = Math.round(rating);
    return '★'.repeat(Math.max(0, Math.min(5, rounded))) + '☆'.repeat(Math.max(0, 5 - rounded));
  }

  private load(): void {
    this.dealsApi.getSellerReviews(this.sellerId, this.page).subscribe({
      next: (response) => {
        if (!response.success || !response.data) return;
        this.reviews.update((current) => [...current, ...response.data!]);
        const total = response.meta?.total ?? this.reviews().length;
        this.hasMore.set(this.reviews().length < total);
      },
      error: () => {},
    });
  }
}
