import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DealDetail, DealsService, UserAddress } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';

@Component({
  selector: 'app-accept-deal',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="accept-deal-screen" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <!-- Minimalist Clean White Header with Coral Title matching Figma -->
      <header class="figma-top-bar">
        <button type="button" class="figma-back-btn" (click)="goBack()" [attr.aria-label]="lang.t('back')">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="15 18 9 12 15 6"></polyline>
          </svg>
        </button>
        <h1 class="figma-header-title">{{ lang.isAr() ? 'قبول المعاملة' : 'Accept Deal' }}</h1>
        <div class="figma-top-spacer"></div>
      </header>

      <main class="accept-content">
        <!-- 1. Map & Delivery Address Card -->
        <section class="map-address-card">
          <!-- Stylized Map Graphic Preview with Red Pin -->
          <div class="map-graphic-box">
            <svg class="map-svg" viewBox="0 0 400 180" preserveAspectRatio="xMidYMid slice" fill="none">
              <!-- Background / Land -->
              <rect width="400" height="180" fill="#f4f3f0" />
              <!-- Green areas / parks -->
              <path d="M-10 140 Q 60 110 100 170 L -10 190 Z" fill="#d8ecd5" />
              <path d="M280 -10 Q 340 30 380 0 L 410 -10 Z" fill="#d8ecd5" />
              <!-- Water feature / subtle river -->
              <path d="M-20 40 Q 80 80 180 50 T 420 70" stroke="#c4e1f6" stroke-width="14" fill="none" opacity="0.6" />
              <!-- City street grid -->
              <path d="M30 -10 V190 M90 -10 V190 M150 -10 V190 M210 -10 V190 M270 -10 V190 M330 -10 V190 M390 -10 V190" stroke="#ffffff" stroke-width="4.5" />
              <path d="M-10 30 H410 M-10 85 H410 M-10 135 H410" stroke="#ffffff" stroke-width="4.5" />
              <!-- Secondary lanes -->
              <path d="M60 -10 V190 M180 -10 V190 M300 -10 V190" stroke="#fcfcfc" stroke-width="2.5" />
              <path d="M-10 60 H410 M-10 110 H410 M-10 160 H410" stroke="#fcfcfc" stroke-width="2.5" />
              <!-- Main yellow road / arterial street -->
              <path d="M-10 100 Q 120 70 240 110 T 420 90" stroke="#fed7aa" stroke-width="6" fill="none" />
              <path d="M130 -10 L 130 190" stroke="#fed7aa" stroke-width="5" />
              <!-- Subtle points of interest dots -->
              <circle cx="85" cy="50" r="4" fill="#60a5fa" opacity="0.6" />
              <circle cx="295" cy="95" r="4" fill="#f59e0b" opacity="0.6" />
              <circle cx="190" cy="140" r="4" fill="#a855f7" opacity="0.6" />
              <!-- Center Red Location Pin -->
              <g transform="translate(190, 60)">
                <!-- Pin shadow -->
                <ellipse cx="10" cy="30" rx="6" ry="2.5" fill="#000000" opacity="0.2" />
                <!-- Pin body -->
                <path d="M10 2 C 5.58 2 2 5.58 2 10 C 2 16 10 28 10 28 C 10 28 18 16 18 10 C 18 5.58 14.42 2 10 2 Z" fill="#eb5757" />
                <circle cx="10" cy="9.5" r="3" fill="#ffffff" />
              </g>
            </svg>
          </div>

          <!-- Address Details row matching Figma -->
          <div class="address-details-box">
            <div class="address-main-info">
              <div class="address-text-col">
                <p class="address-primary-text">{{ currentAddressText() }}</p>
              </div>
              <div class="pin-icon-wrapper" aria-hidden="true">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"></path>
                  <circle cx="12" cy="10" r="3"></circle>
                </svg>
              </div>
            </div>

            <button type="button" class="btn-change-address" (click)="toggleAddressSelector()">
              {{ lang.isAr() ? 'تغيير' : 'Change' }}
            </button>

            @if (showAddressList()) {
              <div class="address-picker-dropdown">
                <div class="picker-header">
                  <span>{{ lang.isAr() ? 'اختر عنوان التوصيل' : 'Select Delivery Address' }}</span>
                  <a routerLink="/addresses" class="manage-addr-link">{{ lang.isAr() ? '+ إدارة العناوين' : '+ Manage' }}</a>
                </div>
                @for (addr of addresses(); track addr.id) {
                  <button type="button" class="addr-select-item" [class.selected]="addr.id === selectedAddressId()" (click)="selectAddress(addr)">
                    <strong>{{ addr.label || addr.city }}</strong>
                    <small>{{ addr.fullText }}</small>
                  </button>
                }
              </div>
            }
          </div>
        </section>

        <!-- 2. Payment Method Section ("طريقة الدفع") -->
        <section class="payment-methods-section">
          <h2 class="payment-section-title">{{ lang.isAr() ? 'طريقة الدفع' : 'Payment Method' }}</h2>

          <div class="payment-options-grid">
            <!-- 1. Apple Pay Button (Full width) -->
            <button
              type="button"
              class="payment-option-card apple-pay-card"
              [class.active-payment]="selectedMethod() === 'apple_pay'"
              (click)="selectedMethod.set('apple_pay')"
            >
              <!-- Official Apple Pay SVG Mark -->
              <svg width="60" height="26" viewBox="0 0 60 26" fill="none">
                <!-- Apple Icon -->
                <path d="M12.98 12.82c-.01-2.07 1.69-3.07 1.77-3.12-0.97-1.4-2.48-1.59-3.01-1.61-1.28-.13-2.51.75-3.16.75-.66 0-1.66-.73-2.73-.71-1.4.02-2.7 0.82-3.42 2.07-1.46 2.53-.37 6.27 1.04 8.31.69.99 1.51 2.1 2.59 2.06 1.04-.04 1.43-.67 2.68-.67 1.25 0 1.61.67 2.69.65 1.11-.02 1.81-1 2.49-1.99.79-1.14 1.11-2.25 1.13-2.31-.02-.01-2.06-.8-2.07-3.43z" fill="#000" />
                <path d="M11.23 6.6c.57-.69.96-1.66.85-2.6-.82.03-1.82.55-2.4 1.23-.52.59-.97 1.57-.85 2.49.92.07 1.83-.43 2.4-1.12z" fill="#000" />
                <!-- Pay Text -->
                <path d="M22.5 8.2h3.9c2.4 0 3.9 1.5 3.9 3.8 0 2.4-1.5 3.9-4 3.9h-2.1v4.8h-1.7V8.2zm3.8 6.1c1.4 0 2.2-.8 2.2-2.3 0-1.4-.8-2.3-2.2-2.3h-2.1v4.6h2.1z" fill="#000" />
                <path d="M36.7 13.9c0-1.3-.9-2.1-2.3-2.1-1.3 0-2.3.8-2.4 2.1h4.7zm-4.7 1.3c.1 1.4 1.1 2.2 2.5 2.2 1 0 1.8-.4 2.2-1.2h1.6c-.6 1.6-2 2.5-3.8 2.5-2.4 0-4.1-1.7-4.1-4.2s1.7-4.2 4-4.2c2.4 0 3.9 1.7 3.9 4.1v.8h-6.3z" fill="#000" />
                <path d="M41.8 17.5l-3.3-8.8h1.8l2.4 6.7 2.3-6.7h1.8l-4 9.9c-.6 1.4-1.3 1.9-2.6 1.9-.3 0-.6 0-.8-.1v-1.4c.2 0 .4.1.6.1.7 0 1.2-.3 1.8-1.6z" fill="#000" />
              </svg>
            </button>

            <!-- 2. Two Columns: Mada & Wallet -->
            <div class="two-payment-row">
              <!-- Mada Button -->
              <button
                type="button"
                class="payment-option-card"
                [class.active-payment]="selectedMethod() === 'mada'"
                (click)="selectedMethod.set('mada')"
              >
                <!-- Official Mada Logo SVG -->
                <svg width="68" height="24" viewBox="0 0 68 24" fill="none">
                  <!-- Green & Blue Bars -->
                  <rect x="0" y="2" width="28" height="9" rx="4.5" fill="#007a3d" />
                  <rect x="0" y="13" width="28" height="9" rx="4.5" fill="#78be20" />
                  <!-- Mada Text in Arabic / English -->
                  <text x="34" y="11" font-family="'Cairo', sans-serif" font-size="10" font-weight="850" fill="#0f172a">مدى</text>
                  <text x="34" y="21" font-family="'Cairo', sans-serif" font-size="9" font-weight="800" fill="#0f172a">mada</text>
                </svg>
              </button>

              <!-- Wallet Button (Etamid Wallet) -->
              <button
                type="button"
                class="payment-option-card wallet-option-card"
                [class.active-payment]="selectedMethod() === 'wallet'"
                (click)="selectedMethod.set('wallet')"
              >
                <!-- Teal/Cyan Wallet SVG matching Figma Image 3 -->
                <svg width="34" height="28" viewBox="0 0 34 28" fill="none">
                  <rect x="2" y="3" width="28" height="22" rx="6" fill="#00c48c" />
                  <rect x="18" y="9" width="14" height="10" rx="3.5" fill="#f59e0b" />
                  <circle cx="23" cy="14" r="2" fill="#ffffff" />
                </svg>
              </button>
            </div>
          </div>
        </section>

        <!-- 3. Bottom Black Action Button: "أعتمد" -->
        <div class="bottom-action-container">
          <button
            type="button"
            class="btn-figma-confirm"
            [disabled]="isProcessing()"
            (click)="confirmAccept()"
          >
            {{ isProcessing() ? (lang.isAr() ? 'جاري الاعتماد...' : 'Processing...') : (lang.isAr() ? 'أعتمد' : 'Confirm') }}
          </button>
        </div>

        @if (actionError()) {
          <div class="error-toast" role="alert">
            ⚠️ {{ lang.t(actionError()!) }}
          </div>
        }
      </main>
    </div>
  `,
  styles: [`
    .accept-deal-screen {
      width: 100%;
      max-width: 440px;
      margin: 0 auto;
      min-height: 100vh;
      background-color: #f8f9fa;
      padding-bottom: 3rem;
      position: relative;
      font-family: 'Cairo', -apple-system, BlinkMacSystemFont, sans-serif;
    }

    /* Top Bar matching Figma: clean white, coral title & back button */
    .figma-top-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1rem 1.25rem;
      background: #ffffff;
      border-bottom: 1px solid #f1f5f9;
      position: sticky;
      top: 0;
      z-index: 50;
    }

    .figma-back-btn {
      background: none;
      border: none;
      cursor: pointer;
      padding: 0.35rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      color: #eb5757;
      transition: transform 0.15s ease;

      &:active {
        transform: scale(0.92);
      }
    }

    

    .figma-header-title {
      font-size: 1.25rem;
      font-weight: 850;
      color: #eb5757;
      margin: 0;
      text-align: center;
      flex: 1;
    }

    .figma-top-spacer {
      width: 28px;
    }

    /* Content Area */
    .accept-content {
      width: 100%;
      padding: 1.25rem 1rem;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }

    /* 1. Map & Address Card */
    .map-address-card {
      background: #ffffff;
      border-radius: 22px;
      overflow: hidden;
      border: 1px solid #e5e7eb;
      box-shadow: 0 6px 20px rgba(17, 24, 39, 0.04);
    }

    .map-graphic-box {
      width: 100%;
      height: 160px;
      position: relative;
      background: #f1f5f9;
      overflow: hidden;
    }

    .map-svg {
      width: 100%;
      height: 100%;
      display: block;
    }

    .address-details-box {
      padding: 1.15rem 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      position: relative;
    }

    .address-main-info {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.75rem;
    }

    .address-text-col {
      flex: 1;
    }

    .address-primary-text {
      color: #111827;
      font-size: 0.85rem;
      font-weight: 700;
      line-height: 1.5;
      margin: 0;
      direction: ltr;
      text-align: start;
    }

    .pin-icon-wrapper {
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding-top: 0.15rem;
    }

    .btn-change-address {
      align-self: flex-start;
      background: none;
      border: none;
      color: #eb5757;
      font-size: 0.88rem;
      font-weight: 800;
      font-family: inherit;
      cursor: pointer;
      padding: 0;
      margin-top: 0.25rem;
      transition: opacity 0.2s;

      &:hover {
        opacity: 0.8;
      }
    }

    /* Address Picker Dropdown */
    .address-picker-dropdown {
      margin-top: 0.75rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .picker-header {
      display: flex;
      justify-content: space-between;
      font-size: 0.8rem;
      font-weight: 700;
      color: #6b7280;
    }

    .manage-addr-link {
      color: #eb5757;
      text-decoration: none;
    }

    .addr-select-item {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.15rem;
      padding: 0.5rem 0.75rem;
      border: 1px solid #e5e7eb;
      border-radius: 10px;
      background: #fafafa;
      cursor: pointer;
      text-align: start;
      font-family: inherit;

      &.selected {
        border-color: #eb5757;
        background: #fff5f5;
      }
    }

    /* 2. Payment Methods Section */
    .payment-methods-section {
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
    }

    .payment-section-title {
      font-size: 1.05rem;
      font-weight: 850;
      color: #111827;
      margin: 0;
    }

    .payment-options-grid {
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
    }

    .payment-option-card {
      background: #ffffff;
      border: 1.5px solid #e5e7eb;
      border-radius: 14px;
      height: 52px;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: border-color 0.2s, box-shadow 0.2s, transform 0.15s ease;

      &:hover {
        border-color: #cbd5e1;
      }

      &.active-payment {
        border-color: #eb5757;
        box-shadow: 0 0 0 2px rgba(235, 87, 87, 0.2);
      }

      &:active {
        transform: scale(0.99);
      }
    }

    .apple-pay-card {
      width: 100%;
    }

    .two-payment-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.85rem;
    }

    /* 3. Bottom Confirm Button */
    .bottom-action-container {
      margin-top: 1rem;
    }

    .btn-figma-confirm {
      width: 100%;
      height: 54px;
      background: #000000;
      color: #ffffff;
      border: none;
      border-radius: 14px;
      font-size: 1.15rem;
      font-weight: 850;
      font-family: inherit;
      cursor: pointer;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.2);
      transition: background 0.2s, transform 0.15s ease;

      &:hover:not(:disabled) {
        background: #1f2937;
      }

      &:active:not(:disabled) {
        transform: translateY(1px);
      }

      &:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
    }

    .error-toast {
      background: #fef2f2;
      color: #b91c1c;
      padding: 0.75rem 1rem;
      border-radius: 12px;
      font-size: 0.85rem;
      font-weight: 700;
      text-align: center;
    }
  `],
})
export class AcceptDealComponent implements OnInit {
  private readonly dealsApi = inject(DealsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly lang = inject(LanguageService);

  protected dealId = 0;
  protected readonly deal = signal<DealDetail | null>(null);
  protected readonly addresses = signal<UserAddress[]>([]);
  protected readonly selectedAddressId = signal<number | null>(null);
  protected readonly showAddressList = signal(false);
  protected readonly selectedMethod = signal<'apple_pay' | 'mada' | 'wallet'>('apple_pay');
  protected readonly isProcessing = signal(false);
  protected readonly actionError = signal<string | null>(null);

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }

    this.dealId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadDeal();
    this.loadAddresses();
  }

  protected goBack(): void {
    if (globalThis.history && globalThis.history.length > 1) {
      globalThis.history.back();
    } else {
      void this.router.navigate(['/home']);
    }
  }

  protected toggleAddressSelector(): void {
    this.showAddressList.update((v) => !v);
  }

  protected selectAddress(addr: UserAddress): void {
    this.selectedAddressId.set(addr.id);
    this.showAddressList.set(false);
  }

  protected currentAddressText(): string {
    const selected = this.addresses().find((a) => a.id === this.selectedAddressId());
    if (selected) return selected.fullText || `${selected.city}, ${selected.district || ''}`;
    return '3161-3203 Aram, Riyadh 12582, 3161-3203 Aram, Riyadh 12582, Saudi Arabia';
  }

  private loadDeal(): void {
    this.dealsApi.getDealDetail(this.dealId).subscribe({
      next: (res: any) => {
        if (res.data) this.deal.set(res.data);
      },
      error: () => {},
    });
  }

  private loadAddresses(): void {
    this.dealsApi.getMyAddresses().subscribe({
      next: (res) => {
        const list = res.data ?? [];
        this.addresses.set(list);
        const def = list.find((a) => a.isDefault) ?? list[0];
        if (def) this.selectedAddressId.set(def.id);
      },
      error: () => {},
    });
  }

  protected confirmAccept(): void {
    if (this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);

    const d = this.deal();
    const addressId = d?.needShipping ? this.selectedAddressId() : (this.selectedAddressId() ?? null);

    this.dealsApi.acceptDeal(this.dealId, addressId).subscribe({
      next: (res) => {
        this.isProcessing.set(false);
        if (!res.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        void this.router.navigate(['/deals', this.dealId]);
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }
}
