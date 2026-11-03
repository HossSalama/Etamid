import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DealsService, Product, SellerReview, SellerStore } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * A seller's public store page: banner, name, category, rating, successful
 * deals and visible reviews — never the seller's deals. Requires login, like
 * the rest of the app; guests are sent to the login page.
 */
@Component({
  selector: 'app-store-page',
  standalone: true,
  imports: [RouterLink, DatePipe],
  template: `
    <main class="store-page">
      @if (isLoading()) {
        <p>{{ lang.t('store_loading') }}</p>
      } @else if (notFound()) {
        <p class="profile-message profile-error" role="alert">{{ lang.t('store_not_found') }}</p>
        <a routerLink="/home">{{ lang.t('back') }}</a>
      } @else if (store(); as s) {
        <section class="profile-card store-hero">
          @if (s.bannerUrl) {
            <img class="store-banner" [src]="s.bannerUrl" [alt]="s.storeName ?? s.sellerName" />
          }
          <h1>{{ s.storeName ?? s.sellerName }}</h1>
          @if (s.storeName) {
            <p class="store-seller">{{ s.sellerName }}</p>
          }
          @if (s.category) {
            <p class="store-category">{{ s.category }}</p>
          }
          <div class="store-stats">
            <span>{{ stars(s.ratingAvg) }} {{ s.ratingAvg.toFixed(1) }} ({{ s.ratingCount }})</span>
            <span>{{ s.successfulDeals }} {{ lang.t('store_successful_deals') }}</span>
          </div>
        </section>

        <section class="store-products">
          <h2>{{ lang.t('product_store_title') }}</h2>
          @if (!products().length && !productsLoading()) {
            <p>{{ lang.t('product_no_products') }}</p>
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
                  <strong>{{ product.name }}</strong>
                  @if (product.description) {
                    <p class="store-product-desc">{{ product.description }}</p>
                  }
                  <p class="store-product-price">{{ product.price }} {{ lang.t('product_currency') }}</p>
                  <span [class]="product.quantity > 0 ? 'store-stock-ok' : 'store-stock-out'">
                    {{ product.quantity > 0 ? lang.t('product_available') + ' · ' + product.quantity : lang.t('product_out_of_stock') }}
                  </span>
                </div>
              </article>
            }
          </div>
          @if (productsHasMore()) {
            <button type="button" class="btn btn-outline-coral" (click)="loadMoreProducts()">
              {{ lang.t('store_load_more') }}
            </button>
          }
        </section>

        <section class="store-reviews">
          <h2>{{ lang.t('store_reviews') }}</h2>
          @if (!reviews().length) {
            <p>{{ lang.t('store_no_reviews') }}</p>
          }
          @for (review of reviews(); track review.id) {
            <article class="store-review">
              <div class="store-review-head">
                <strong>{{ review.raterName }}</strong>
                <span>{{ stars(review.stars) }}</span>
              </div>
              <p class="store-review-deal">{{ review.dealTitle }}</p>
              @if (review.comment) {
                <p>{{ review.comment }}</p>
              }
              <small>{{ review.createdAt | date }}</small>
            </article>
          }
          @if (hasMore()) {
            <button type="button" class="btn btn-outline-coral" (click)="loadMore()">
              {{ lang.t('store_load_more') }}
            </button>
          }
        </section>
      }
    </main>
  `,
  styles: [`
    .store-page { max-width: 820px; margin: 0 auto; padding: 1.5rem 1rem 3rem; }
    .store-hero { margin-bottom: 2rem; }
    .store-banner { width: 100%; max-height: 260px; object-fit: cover; border-radius: 16px; }
    .store-seller, .store-category { opacity: 0.75; margin: 0.25rem 0; }
    .store-stats { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.75rem; font-weight: 700; }
    .store-stats span { background: #f3f4f6; padding: 0.35rem 0.8rem; border-radius: 999px; font-size: 0.85rem; }
    .store-products { margin-bottom: 2rem; }
    .store-products-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 0.75rem; margin: 0.75rem 0 1rem; }
    .store-product-card { border: 1px solid #e5e7eb; border-radius: 14px; overflow: hidden; background: #fff; }
    .store-product-thumb, .store-product-thumb-empty { width: 100%; height: 140px; object-fit: cover; }
    .store-product-thumb-empty { display: flex; align-items: center; justify-content: center; background: #fff1f2; color: #eb5757; font-size: 2rem; font-weight: 800; }
    .store-product-body { display: flex; flex-direction: column; gap: 0.25rem; padding: 0.75rem 1rem 1rem; }
    .store-product-desc { opacity: 0.75; font-size: 0.85rem; margin: 0; overflow-wrap: anywhere; }
    .store-product-price { font-weight: 800; margin: 0.25rem 0 0; }
    .store-stock-ok, .store-stock-out { width: fit-content; font-size: 0.8rem; font-weight: 700; padding: 0.1rem 0.5rem; border-radius: 999px; }
    .store-stock-ok { background: #eef6ee; color: #047857; }
    .store-stock-out { background: #fef2f2; color: #b91c1c; }
    .store-review { border: 1px solid #e5e7eb; border-radius: 12px; padding: 0.9rem 1rem; margin-bottom: 0.75rem; }
    .store-review-head { display: flex; justify-content: space-between; gap: 1rem; }
    .store-review-deal { font-weight: 600; margin: 0.3rem 0; }
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
    this.loadReviews();
    this.loadProducts();
  }

  stars(value: number): string {
    const full = Math.max(0, Math.min(5, Math.round(value)));
    return '★'.repeat(full) + '☆'.repeat(5 - full);
  }

  loadMore(): void {
    this.page += 1;
    this.loadReviews();
  }

  loadMoreProducts(): void {
    this.productsPage += 1;
    this.loadProducts();
  }

  private loadProducts(): void {
    this.dealsApi.getSellerProducts(this.sellerId, this.productsPage).subscribe({
      next: (response) => {
        this.productsLoading.set(false);
        if (!response.success || !response.data) return;
        this.products.update((current) => [...current, ...response.data!]);
        const total = response.meta?.total ?? this.products().length;
        this.productsHasMore.set(this.products().length < total);
      },
      error: () => this.productsLoading.set(false),
    });
  }

  private loadReviews(): void {
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
