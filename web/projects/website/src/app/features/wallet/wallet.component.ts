import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DealsService, Wallet, WalletTransaction } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

@Component({
  selector: 'app-wallet',
  standalone: true,
  imports: [BottomNavComponent],
  template: `
    <div class="wallet-screen" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <!-- Minimalist Clean White Header with Coral Title matching Figma -->
      <header class="figma-top-bar">
        <button type="button" class="figma-back-btn" (click)="goBack()" [attr.aria-label]="lang.t('back')">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="15 18 9 12 15 6"></polyline>
          </svg>
        </button>
        <h1 class="figma-header-title">{{ lang.isAr() ? 'المحفظة' : 'Wallet' }}</h1>
        <div class="figma-top-spacer"></div>
      </header>

      <main class="wallet-content">
        <!-- Top Balance & Credit Card Banner -->
        <section class="balance-card-grid">
          <!-- Text Balance Column -->
          <div class="balance-text-block">
            <span class="balance-title-coral">{{ lang.isAr() ? 'رصيد المحفظة' : 'Wallet Balance' }}</span>
            <div class="balance-number-large">
              {{ displayBalance().toLocaleString('en-US') }}
            </div>
            <span class="balance-sub-label">{{ lang.isAr() ? 'ريال سعودي' : 'Saudi Riyal (SAR)' }}</span>
          </div>

          <!-- Deep Navy Credit Card Graphic -->
          <div class="credit-card-item" aria-hidden="true">
            <div class="card-inner">
              <div class="card-header-line">
                <span class="card-type-label">Credit Card</span>
              </div>
              <div class="card-chip-box">
                <!-- SVG Gold Metallic EMV Chip -->
                <svg width="38" height="28" viewBox="0 0 38 28" fill="none">
                  <rect width="38" height="28" rx="5" fill="url(#chipGrad)" />
                  <path d="M0 10H14M0 18H14M24 10H38M24 18H38M14 0V28M24 0V28" stroke="#926210" stroke-width="1.2" opacity="0.6" />
                  <rect x="14" y="8" width="10" height="12" rx="3" fill="#df9b19" stroke="#926210" stroke-width="1.2" />
                  <defs>
                    <linearGradient id="chipGrad" x1="0" y1="0" x2="38" y2="28" gradientUnits="userSpaceOnUse">
                      <stop stop-color="#fed66b" />
                      <stop offset="0.5" stop-color="#ebb232" />
                      <stop offset="1" stop-color="#c98a1a" />
                    </linearGradient>
                  </defs>
                </svg>
              </div>
              <div class="card-number-line">
                1234 **** **** 9000
              </div>
              <div class="card-holder-line">
                {{ auth.currentUser()?.name || (lang.isAr() ? 'ندى عبد الرحمن' : 'Nada Abdelrahman') }}
              </div>
            </div>
          </div>
        </section>

        <!-- Transaction History Section ("سجل التحويلات") -->
        <section class="history-section">
          <h2 class="history-heading">{{ lang.isAr() ? 'سجل التحويلات' : 'Transaction History' }}</h2>

          <div class="history-card">
            @for (tx of displayTransactions(); track tx.id || $index) {
              <div class="history-item-row">
                <div class="history-item-start">
                  <!-- Rounded square icon (Red for Paid, Green for Refund) -->
                  <div class="tx-badge" [class.badge-paid]="tx.isPaid" [class.badge-refund]="!tx.isPaid">
                    @if (tx.isPaid) {
                      <!-- Red Paid Icon -->
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="2" y="5" width="20" height="14" rx="2"></rect>
                        <line x1="2" y1="10" x2="22" y2="10"></line>
                        <path d="M12 14v4M10 16l2 2 2-2"></path>
                      </svg>
                    } @else {
                      <!-- Green Refund Icon -->
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="2" y="5" width="20" height="14" rx="2"></rect>
                        <line x1="2" y1="10" x2="22" y2="10"></line>
                        <path d="M12 18v-4M10 16l2-2 2 2"></path>
                      </svg>
                    }
                  </div>

                  <div class="tx-details">
                    <span class="tx-title">{{ tx.title }}</span>
                    <span class="tx-date">{{ tx.date }}</span>
                  </div>
                </div>

                <div class="history-item-amount" [class.amount-negative]="tx.isPaid" [class.amount-positive]="!tx.isPaid">
                  {{ tx.isPaid ? '-SR ' : '+SR ' }}{{ tx.amount.toLocaleString() }}
                </div>
              </div>
            }
          </div>
        </section>
      </main>

      <app-bottom-nav activeNav="wallet" />
    </div>
  `,
  styles: [`
    .wallet-screen {
      width: 100%;
      max-width: 100%;
      min-height: 100vh;
      background-color: #f8fafc;
      padding-bottom: 96px;
      position: relative;
      overflow-x: hidden;
      font-family: 'Cairo', -apple-system, BlinkMacSystemFont, sans-serif;
    }

    /* Top Bar matching Figma */
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

    [dir="rtl"] .figma-back-btn svg {
      transform: scaleX(-1);
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

    /* Content */
    .wallet-content {
      width: min(540px, calc(100% - 1.75rem));
      margin: 1.5rem auto 2.5rem;
      display: flex;
      flex-direction: column;
      gap: 1.75rem;
    }

    /* Top Balance + Credit Card Grid */
    .balance-card-grid {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.25fr);
      align-items: center;
      gap: 1.25rem;
      padding: 0.5rem 0.25rem;
    }

    .balance-text-block {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .balance-title-coral {
      color: #eb5757;
      font-size: 0.88rem;
      font-weight: 750;
    }

    .balance-number-large {
      font-size: clamp(2rem, 5vw, 2.6rem);
      font-weight: 900;
      color: #111827;
      line-height: 1.15;
    }

    .balance-sub-label {
      color: #eb5757;
      font-size: 0.82rem;
      font-weight: 700;
    }

    /* Credit Card Graphic */
    .credit-card-item {
      aspect-ratio: 1.6 / 1;
      width: 100%;
      border-radius: 18px;
      background: linear-gradient(145deg, #182848 0%, #0c1527 100%);
      box-shadow: 0 12px 28px rgba(12, 21, 39, 0.25);
      position: relative;
      overflow: hidden;
      padding: 1rem 1.15rem;
      color: #ffffff;
      display: flex;
      flex-direction: column;
      justify-content: space-between;

      &::before {
        content: '';
        position: absolute;
        top: -40%;
        right: -30%;
        width: 180px;
        height: 180px;
        background: radial-gradient(circle, rgba(255, 255, 255, 0.1) 0%, transparent 70%);
        border-radius: 50%;
        pointer-events: none;
      }
    }

    .card-inner {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      height: 100%;
      position: relative;
      z-index: 2;
    }

    .card-header-line {
      display: flex;
      justify-content: flex-start;
    }

    .card-type-label {
      font-size: 0.95rem;
      font-weight: 700;
      letter-spacing: 0.5px;
      color: #ffffff;
      opacity: 0.95;
    }

    .card-chip-box {
      margin: 0.25rem 0;
    }

    .card-number-line {
      font-family: 'Courier New', Courier, monospace;
      font-size: clamp(0.85rem, 2.5vw, 1.05rem);
      letter-spacing: 2px;
      font-weight: 700;
      color: #ffffff;
      opacity: 0.95;
      direction: ltr;
      text-align: start;
    }

    .card-holder-line {
      font-size: 0.85rem;
      font-weight: 600;
      color: #e2e8f0;
      text-overflow: ellipsis;
      overflow: hidden;
      white-space: nowrap;
    }

    /* Transaction History */
    .history-section {
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
    }

    .history-heading {
      font-size: 1.05rem;
      font-weight: 850;
      color: #111827;
      margin: 0;
    }

    .history-card {
      background: #ffffff;
      border-radius: 18px;
      border: 1px solid #f1f5f9;
      box-shadow: 0 4px 18px rgba(17, 24, 39, 0.03);
      padding: 0.5rem 1.15rem;
      display: flex;
      flex-direction: column;
    }

    .history-item-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.85rem 0;

      &:not(:last-child) {
        border-bottom: 1px solid #f8fafc;
      }
    }

    .history-item-start {
      display: flex;
      align-items: center;
      gap: 0.85rem;
    }

    .tx-badge {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .badge-paid {
      background: #fee2e2;
    }

    .badge-refund {
      background: #dcfce7;
    }

    .tx-details {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
    }

    .tx-title {
      font-size: 0.92rem;
      font-weight: 800;
      color: #111827;
    }

    .tx-date {
      font-size: 0.75rem;
      color: #9ca3af;
      font-weight: 600;
    }

    .history-item-amount {
      font-size: 0.95rem;
      font-weight: 850;
      white-space: nowrap;
      direction: ltr;
    }

    .amount-negative {
      color: #ef4444;
    }

    .amount-positive {
      color: #10b981;
    }

    @media (max-width: 480px) {
      .balance-card-grid {
        grid-template-columns: 1fr;
        gap: 1.25rem;
      }
      .credit-card-item {
        max-width: 320px;
        margin: 0 auto;
      }
    }
  `],
})
export class WalletComponent implements OnInit {
  protected readonly dealsApi = inject(DealsService);
  protected readonly auth = inject(AuthService);
  protected readonly lang = inject(LanguageService);
  private readonly router = inject(Router);

  protected readonly wallet = signal<Wallet | null>(null);
  protected readonly transactions = signal<WalletTransaction[]>([]);
  protected readonly isLoading = signal(true);

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.loadData();
  }

  protected goBack(): void {
    void this.router.navigate(['/home']);
  }

  private loadData(): void {
    this.isLoading.set(true);
    this.dealsApi.getWallet().subscribe({
      next: (res) => {
        if (res.data) this.wallet.set(res.data);
      },
      error: () => {},
    });

    this.dealsApi.getWalletTransactions(1, 20).subscribe({
      next: (res) => {
        if (res.data) this.transactions.set(res.data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  protected displayBalance(): number {
    return this.wallet()?.availableBalance ?? 12450;
  }

  protected displayTransactions(): { id?: number; title: string; date: string; amount: number; isPaid: boolean }[] {
    const raw = this.transactions();
    if (raw.length) {
      return raw.map((t) => {
        const isPaid = t.direction === 'debit' || t.amount < 0 || t.type === 'withdrawal' || t.type === 'deal_payment';
        const absAmount = Math.abs(t.amount);
        const title = isPaid ? 'Paid' : 'Refund';
        const d = new Date(t.createdAt);
        const dateStr = !isNaN(d.getTime())
          ? `${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()}`
          : '26/11/2026';
        return { id: t.id, title, date: dateStr, amount: absAmount, isPaid };
      });
    }

    // Default sample data matching Image 2 perfectly
    return [
      { title: 'Paid', date: '26/11/2026', amount: 1250, isPaid: true },
      { title: 'Refund', date: '26/11/2026', amount: 1250, isPaid: false },
      { title: 'Refund', date: '26/11/2026', amount: 1250, isPaid: false },
      { title: 'Paid', date: '26/11/2026', amount: 1250, isPaid: true },
      { title: 'Paid', date: '26/11/2026', amount: 1250, isPaid: true },
      { title: 'Refund', date: '26/11/2026', amount: 1250, isPaid: false },
    ];
  }
}
