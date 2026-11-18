import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DealsService, Wallet, WalletTransaction, Withdrawal } from '../../core/services/deals.service';
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
        @if (isLoading()) {
          <p class="wallet-loading">{{ lang.t('wallet_loading') }}</p>
        } @else if (wallet() && !wallet()!.activated) {
          <!-- Locked wallet: activation card only, no balances visible -->
          <section class="activation-card">
            <h2>{{ lang.t('wallet_activate_title') }}</h2>
            <p>{{ lang.t('wallet_activate_desc') }}</p>
            <button type="button" class="activate-btn" (click)="activateWallet()" [disabled]="isActivatingWallet()">
              {{ lang.t('wallet_activate_btn') }}
            </button>
          </section>
        } @else if (wallet()?.activated) {
          <!-- Top Balance & Bank Account Card -->
          <section class="balance-card-grid">
            <!-- Text Balance Column -->
            <div class="balance-text-block">
              <span class="balance-title-coral">{{ lang.isAr() ? 'رصيد المحفظة' : 'Wallet Balance' }}</span>
              <div class="balance-number-large">
                {{ displayBalance().toLocaleString('en-US') }}
              </div>
              <span class="balance-sub-label">{{ lang.isAr() ? 'ريال سعودي' : 'Saudi Riyal (SAR)' }}</span>
              <span class="held-row">{{ lang.t('wallet_held') }}: {{ displayHeld().toLocaleString('en-US') }} {{ lang.isAr() ? 'ر.س' : 'SAR' }}</span>
            </div>

            <!-- Real bank account card (no credit card exists in the system) -->
            @if (wallet()!.hasBankAccount && wallet()!.bankAccountMasked) {
              <div class="credit-card-item" aria-label="{{ lang.t('wallet_bank_account') }}">
                <div class="card-inner">
                  <div class="card-header-line">
                    <span class="card-type-label">{{ lang.t('wallet_bank_account') }}</span>
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
                  <div class="card-number-line" dir="ltr">
                    {{ wallet()!.bankAccountMasked }}
                  </div>
                  @if (auth.currentUser()?.name) {
                    <div class="card-holder-line">
                      {{ auth.currentUser()?.name }}
                    </div>
                  }
                </div>
              </div>
            } @else {
              <div class="no-bank-card">
                <p>{{ lang.t('wallet_no_bank') }}</p>
                <button type="button" class="add-bank-btn" (click)="goToBankAccount()">
                  {{ lang.t('wallet_add_bank') }}
                </button>
              </div>
            }
          </section>

          <!-- Transaction History Section ("سجل التحويلات") -->
          <section class="history-section">
            <h2 class="history-heading">{{ lang.t('wallet_history') }}</h2>

            <div class="history-card">
              @if (!transactions().length) {
                <p class="empty-text">{{ lang.t('wallet_history_empty') }}</p>
              }
              @for (tx of transactions(); track tx.id) {
                <div class="history-item-row">
                  <div class="history-item-start">
                    <!-- Rounded square icon (Red for out, Green for in) -->
                    <div class="tx-badge" [class.badge-paid]="!isCredit(tx)" [class.badge-refund]="isCredit(tx)">
                      @if (!isCredit(tx)) {
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                          <rect x="2" y="5" width="20" height="14" rx="2"></rect>
                          <line x1="2" y1="10" x2="22" y2="10"></line>
                          <path d="M12 14v4M10 16l2 2 2-2"></path>
                        </svg>
                      } @else {
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                          <rect x="2" y="5" width="20" height="14" rx="2"></rect>
                          <line x1="2" y1="10" x2="22" y2="10"></line>
                          <path d="M12 18v-4M10 16l2-2 2 2"></path>
                        </svg>
                      }
                    </div>

                    <div class="tx-details">
                      <span class="tx-title">{{ lang.t('wallet_tx_' + tx.type) }}</span>
                      <span class="tx-date">{{ formatDate(tx.createdAt) }}</span>
                    </div>
                  </div>

                  <div class="history-item-amount" [class.amount-negative]="!isCredit(tx)" [class.amount-positive]="isCredit(tx)">
                    {{ isCredit(tx) ? '+SR ' : '-SR ' }}{{ Math.abs(tx.amount).toLocaleString() }}
                  </div>
                </div>
              }
              @if (transactions().length < transactionsTotal()) {
                <button type="button" class="load-more-btn" (click)="loadMoreTransactions()">
                  {{ lang.t('store_load_more') }}
                </button>
              }
            </div>
          </section>

          <!-- Withdrawal Requests -->
          <section class="history-section">
            <h2 class="history-heading">{{ lang.t('wallet_withdrawals') }}</h2>

            <div class="history-card">
              @if (!withdrawals().length) {
                <p class="empty-text">{{ lang.t('wallet_withdrawals_empty') }}</p>
              }
              @for (w of withdrawals(); track w.id) {
                <div class="history-item-row">
                  <div class="history-item-start">
                    <div class="tx-details">
                      <span class="tx-title">{{ w.amount.toLocaleString() }} {{ lang.isAr() ? 'ر.س' : 'SAR' }} · <span dir="ltr">{{ w.ibanSnapshotMasked }}</span></span>
                      <span class="tx-date">{{ formatDate(w.createdAt) }}</span>
                    </div>
                  </div>
                  <div class="history-item-amount">
                    {{ lang.t('wallet_status_' + w.status) }}
                  </div>
                </div>
                @if (w.bankReference) {
                  <p class="tx-note">{{ lang.t('wallet_bank_reference') }}: <span dir="ltr">{{ w.bankReference }}</span></p>
                }
                @if (w.rejectionReason) {
                  <p class="tx-note">{{ w.rejectionReason }}</p>
                }
              }
              @if (withdrawals().length < withdrawalsTotal()) {
                <button type="button" class="load-more-btn" (click)="loadMoreWithdrawals()">
                  {{ lang.t('store_load_more') }}
                </button>
              }
            </div>
          </section>
        }
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

    .wallet-loading {
      text-align: center;
      color: #64748b;
      font-weight: 700;
      padding: 2rem 0;
    }

    /* Activation card (locked wallet: no balances visible) */
    .activation-card {
      background: #ffffff;
      border-radius: 18px;
      border: 1px solid #f1f5f9;
      box-shadow: 0 4px 18px rgba(17, 24, 39, 0.03);
      padding: 1.5rem 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      text-align: center;

      h2 {
        font-size: 1.1rem;
        font-weight: 850;
        color: #111827;
        margin: 0;
      }

      p {
        font-size: 0.9rem;
        color: #64748b;
        margin: 0;
      }
    }

    .activate-btn,
    .add-bank-btn {
      background: #111827;
      color: #ffffff;
      border: none;
      border-radius: 12px;
      padding: 0.75rem 1.25rem;
      font-size: 0.95rem;
      font-weight: 800;
      font-family: inherit;
      cursor: pointer;

      &:disabled {
        opacity: 0.5;
        cursor: default;
      }
    }

    /* Top Balance + Bank Card Grid */
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

    .held-row {
      color: #64748b;
      font-size: 0.82rem;
      font-weight: 700;
      margin-top: 0.35rem;
    }

    /* Bank Account Card Graphic */
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

    .no-bank-card {
      background: #ffffff;
      border-radius: 18px;
      border: 1px dashed #cbd5e1;
      padding: 1.25rem 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      align-items: flex-start;

      p {
        font-size: 0.9rem;
        font-weight: 700;
        color: #64748b;
        margin: 0;
      }
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

    .empty-text {
      font-size: 0.9rem;
      color: #9ca3af;
      font-weight: 700;
      text-align: center;
      padding: 1rem 0;
      margin: 0;
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

    .tx-note {
      font-size: 0.8rem;
      color: #64748b;
      font-weight: 600;
      margin: 0;
      padding-bottom: 0.85rem;
    }

    .load-more-btn {
      background: none;
      border: 1px solid #eb5757;
      color: #eb5757;
      border-radius: 12px;
      padding: 0.6rem 1rem;
      font-size: 0.9rem;
      font-weight: 800;
      font-family: inherit;
      cursor: pointer;
      margin: 0.5rem 0;
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

  protected readonly Math = Math;

  protected readonly wallet = signal<Wallet | null>(null);
  protected readonly transactions = signal<WalletTransaction[]>([]);
  protected readonly withdrawals = signal<Withdrawal[]>([]);
  protected readonly transactionsTotal = signal(0);
  protected readonly withdrawalsTotal = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly isActivatingWallet = signal(false);

  private txPage = 1;
  private withdrawalPage = 1;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.loadData();
  }

  protected goBack(): void {
    if (globalThis.history && globalThis.history.length > 1) {
      globalThis.history.back();
    } else {
      void this.router.navigate(['/home']);
    }
  }

  protected goToBankAccount(): void {
    void this.router.navigate(['/home']);
  }

  private loadData(): void {
    this.isLoading.set(true);
    this.dealsApi.getWallet().subscribe({
      next: (res) => {
        if (res.data) {
          this.wallet.set(res.data);
          if (res.data.activated) {
            this.loadTransactions();
            this.loadWithdrawals();
          }
        }
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  private loadTransactions(): void {
    this.dealsApi.getWalletTransactions(this.txPage).subscribe({
      next: (res) => {
        if (!res.success || !res.data) return;
        this.transactionsTotal.set(res.meta?.total ?? 0);
        this.transactions.update((list) => [...list, ...res.data]);
      },
      error: () => {},
    });
  }

  protected loadMoreTransactions(): void {
    this.txPage += 1;
    this.loadTransactions();
  }

  private loadWithdrawals(): void {
    this.dealsApi.getMyWithdrawals(this.withdrawalPage).subscribe({
      next: (res) => {
        if (!res.success || !res.data) return;
        this.withdrawalsTotal.set(res.meta?.total ?? 0);
        this.withdrawals.update((list) => [...list, ...res.data]);
      },
      error: () => {},
    });
  }

  protected loadMoreWithdrawals(): void {
    this.withdrawalPage += 1;
    this.loadWithdrawals();
  }

  protected activateWallet(): void {
    if (this.isActivatingWallet()) return;
    this.isActivatingWallet.set(true);
    this.dealsApi.activateWallet().subscribe({
      next: (res) => {
        this.isActivatingWallet.set(false);
        if (!res.success || !res.data) return;
        this.wallet.set(res.data);
        if (res.data.activated) {
          this.loadTransactions();
          this.loadWithdrawals();
        }
      },
      error: () => this.isActivatingWallet.set(false),
    });
  }

  protected displayBalance(): number {
    return this.wallet()?.availableBalance ?? 0;
  }

  protected displayHeld(): number {
    return this.wallet()?.escrowHeld ?? 0;
  }

  protected isCredit(tx: WalletTransaction): boolean {
    return tx.direction === 'credit';
  }

  protected formatDate(iso: string): string {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '';
    return `${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()}`;
  }
}
