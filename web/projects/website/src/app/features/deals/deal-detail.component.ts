import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DealDetail, DealsService, LookupItem, UserAddress } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * Deal detail page (/deals/:id): the deal's info, fees before funding,
 * timeline, and the cycle actions. The accept action runs the two-step
 * flow from the iOS designs: terms checklist first, then the delivery
 * address (shipping deals only). There is deliberately NO payment-method
 * step here: no real gateway is connected yet (Fake provider only), so a
 * selector would be a dead control — payment happens at funding.
 */

/** Platform fee in SAR — fixed by AppSettings (platform_fee_amount). */
const PLATFORM_FEE_SAR = 50;

const KNOWN_STATUSES = [
  'pending_acceptance',
  'awaiting_funding',
  'funded',
  'in_delivery',
  'inspection',
  'disputed',
  'released',
  'rejected',
  'cancelled',
  'expired',
  'refund_pending',
  'refunded',
];

@Component({
  selector: 'app-deal-detail',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="app-screen deal-detail-screen">
      <header class="detail-header">
        <a routerLink="/home" class="detail-back">‹ {{ lang.t('deal_detail_back') }}</a>
        <h1>{{ lang.t('deal_detail_title') }}</h1>
      </header>

      @if (isLoading()) {
        <p>{{ lang.t('wallet_loading') }}</p>
      } @else if (deal(); as d) {
        <main class="detail-body">
          <!-- Product + counterparty -->
          <div class="detail-card">
            @if (d.imageUrl) {
              <img class="detail-image" [src]="d.imageUrl" [alt]="d.title" />
            }
            <h2>{{ d.title }}</h2>
            <p class="detail-number">{{ lang.t('deal_detail_number') }}: #{{ d.number }}</p>
            <p class="detail-counterparty">{{ d.counterpartyName }} · {{ d.counterpartyCode }}</p>
            <p class="detail-status">
              {{ statusText() }}
              @if (d.badge) {
                <span class="deal-badge deal-badge-{{ d.badge.color }}">{{ d.badge.text }}</span>
              }
            </p>
          </div>

          <!-- Money + facts, fees visible before funding -->
          <div class="detail-card">
            <h3>{{ lang.t('deal_detail_info') }}</h3>
            <div class="detail-row">
              <span>{{ lang.t('deal_detail_created') }}</span>
              <span>{{ fmtDate(d.createdAt) }}</span>
            </div>
            <div class="detail-row">
              <span>{{ lang.t('deal_detail_amount') }}</span>
              <span>{{ money(d.amount) }} {{ lang.t('home_sar') }}</span>
            </div>
            <div class="detail-row">
              <span>{{ lang.t('deal_detail_fee') }}</span>
              <span>{{ money(fee()) }} {{ lang.t('home_sar') }}</span>
            </div>
            @if (d.role === 'seller') {
              <div class="detail-row">
                <span>{{ lang.t('deal_detail_seller_receives') }}</span>
                <span>{{ money(sellerNet()) }} {{ lang.t('home_sar') }}</span>
              </div>
            } @else {
              <div class="detail-row">
                <span>{{ lang.t('deal_detail_buyer_pays') }}</span>
                <span>{{ money(d.amount) }} {{ lang.t('home_sar') }}</span>
              </div>
            }
            <div class="detail-row">
              <span>{{ lang.t('deal_detail_duration') }}</span>
              <span>{{ d.durationValue }} {{ lang.t(d.durationUnit === 'hour' ? 'deal_detail_unit_hour' : 'deal_detail_unit_day') }}</span>
            </div>
            @if (d.needShipping) {
              <div class="detail-row">
                <span>{{ lang.t('deal_detail_shipping_company') }}</span>
                <span>{{ lang.isAr() ? (d.shippingCompanyNameAr ?? '') : (d.shippingCompanyNameEn ?? '') }}</span>
              </div>
              @if (d.shippingAddressText) {
                <div class="detail-row">
                  <span>{{ lang.t('deal_detail_address') }}</span>
                  <span>{{ d.shippingAddressText }}</span>
                </div>
              }
            }
            @if (d.status === 'pending_acceptance') {
              <div class="detail-row">
                <span>{{ lang.t('deal_detail_respond_by') }}</span>
                <span>{{ fmtDateTime(d.respondDeadlineAt) }}</span>
              </div>
            }
            @if (d.status === 'awaiting_funding' && d.expiresAt) {
              <div class="detail-row">
                <span>{{ lang.t('deal_detail_fund_by') }}</span>
                <span>{{ fmtDateTime(d.expiresAt) }}</span>
              </div>
            }
            @if (d.status === 'inspection' && d.inspectionEndsAt) {
              <div class="detail-row">
                <span>{{ lang.t('deal_detail_inspect_by') }}</span>
                <span>{{ fmtDateTime(d.inspectionEndsAt) }}</span>
              </div>
            }
            @if (d.description) {
              <h3>{{ lang.t('deal_detail_description') }}</h3>
              <p class="detail-desc">{{ d.description }}</p>
            }
          </div>

          <!-- Timeline -->
          @if (d.timeline.length) {
            <div class="detail-card">
              <h3>{{ lang.t('deal_detail_timeline') }}</h3>
              @for (item of d.timeline; track item.at) {
                <div class="detail-row">
                  <span>{{ item.from ? item.from + ' → ' + item.to : item.to }}</span>
                  <span>{{ fmtDateTime(item.at) }}</span>
                </div>
                @if (item.note) {
                  <p class="detail-note">{{ item.note }}</p>
                }
              }
            </div>
          }

          <!-- Terms note -->
          <div class="detail-card">
            <h3>{{ lang.t('deal_detail_before_complete') }}</h3>
            <p>{{ lang.t('deal_detail_terms_note') }}</p>
            <a routerLink="/terms" class="detail-terms-link">{{ lang.t('deal_detail_terms_link') }}</a>
          </div>

          <!-- Cycle actions -->
          <div class="detail-actions">
            @if (d.availableActions.includes('accept') && acceptStep() === null) {
              <button type="button" class="btn btn-black" (click)="startAccept()" [disabled]="isProcessing()">
                {{ lang.t('home_deal_accept') }}
              </button>
            }
            @if (d.availableActions.includes('fund') && !showFakePay()) {
              <button type="button" class="btn btn-black" (click)="fund()" [disabled]="isProcessing()">
                {{ isProcessing() ? lang.t('home_deal_funding') : lang.t('home_deal_fund') }}
              </button>
            }
            @if (d.availableActions.includes('deliver')) {
              <button type="button" class="btn btn-black" (click)="deliver()" [disabled]="isProcessing()">
                {{ isProcessing() ? lang.t('home_deal_delivering') : lang.t('home_deal_deliver') }}
              </button>
            }
            @if (d.availableActions.includes('release')) {
              <button type="button" class="btn btn-black" (click)="release()" [disabled]="isProcessing()">
                {{ lang.t('home_deal_release') }}
              </button>
            }
            @if (d.availableActions.includes('reject') && !rejectOpen()) {
              <button type="button" class="btn btn-outline-coral" (click)="startReject()" [disabled]="isProcessing()">
                {{ lang.t('deal_detail_reject') }}
              </button>
            }
            @if (d.availableActions.includes('cancel')) {
              <button type="button" class="btn btn-outline-coral" (click)="cancel()" [disabled]="isProcessing()">
                {{ lang.t('home_deal_cancel') }}
              </button>
            }
            @if (d.availableActions.includes('dispute')) {
              <a class="btn btn-outline-coral" [routerLink]="['/deals', d.id, 'dispute']">
                {{ lang.t('home_deal_dispute') }}
              </a>
            }
            @if (d.availableActions.includes('rate') && !rateOpen()) {
              <button type="button" class="btn btn-outline-coral" (click)="rateOpen.set(true)">
                {{ lang.t('home_deal_rate') }}
              </button>
            }
          </div>

          <!-- Accept flow: step 1 — terms checklist -->
          @if (acceptStep() === 'terms') {
            <div class="detail-card">
              <h3>{{ lang.t('deal_accept_terms_title') }}</h3>
              <ul class="terms-list">
                <li>{{ lang.t('deal_terms_1') }}</li>
                <li>{{ lang.t('deal_terms_2') }}</li>
                <li>{{ lang.t('deal_terms_3') }}</li>
                <li>{{ lang.t('deal_terms_4') }}</li>
                <li>{{ lang.t('deal_terms_5') }}</li>
                <li>{{ lang.t('deal_terms_6') }}</li>
              </ul>
              <label class="terms-agree">
                <input
                  type="checkbox"
                  [checked]="termsAccepted()"
                  (change)="termsAccepted.set($any($event.target).checked)"
                />
                <span>{{ lang.t('deal_accept_agree') }}</span>
              </label>
              <div class="detail-actions">
                <button
                  type="button"
                  class="btn btn-black"
                  (click)="continueFromTerms()"
                  [disabled]="!termsAccepted() || isProcessing()"
                >
                  {{ lang.t('deal_accept_continue') }}
                </button>
                <button type="button" class="btn btn-outline-coral" (click)="acceptStep.set(null)">
                  {{ lang.t('home_deal_accept_cancel') }}
                </button>
              </div>
            </div>
          }

          <!-- Accept flow: step 2 — delivery address (shipping deals only) -->
          @if (acceptStep() === 'address') {
            <div class="detail-card">
              <h3>{{ lang.t('deal_accept_address_title') }}</h3>
              @if (isLoadingAcceptAddresses()) {
                <p>{{ lang.t('wallet_loading') }}</p>
              } @else if (!acceptAddresses().length) {
                <p>{{ lang.t('home_deal_accept_no_address') }}</p>
                <a routerLink="/addresses" class="btn btn-black">{{ lang.t('deal_accept_add_address') }}</a>
              } @else {
                <label>
                  <span>{{ lang.t('home_deal_accept_address') }}</span>
                  <select
                    [value]="selectedAcceptAddressId() ?? ''"
                    (change)="selectedAcceptAddressId.set($any($event.target).value ? +$any($event.target).value : null)"
                  >
                    <option value="" disabled>{{ lang.t('home_deal_accept_address_placeholder') }}</option>
                    @for (address of acceptAddresses(); track address.id) {
                      <option [value]="address.id">{{ addressLabel(address) }}</option>
                    }
                  </select>
                </label>
                <div class="detail-actions">
                  <button
                    type="button"
                    class="btn btn-black"
                    (click)="submitAccept()"
                    [disabled]="!selectedAcceptAddressId() || isProcessing()"
                  >
                    {{ isProcessing() ? lang.t('home_deal_accepting') : lang.t('deal_accept_confirm') }}
                  </button>
                  <button type="button" class="btn btn-outline-coral" (click)="acceptStep.set('terms')">
                    {{ lang.t('deal_accept_back') }}
                  </button>
                </div>
              }
            </div>
          }

          <!-- Reject panel -->
          @if (rejectOpen()) {
            <div class="detail-card">
              <h3>{{ lang.t('deal_detail_reject') }}</h3>
              <label>
                <span>{{ lang.t('home_deal_reject_reason') }}</span>
                <select
                  [value]="selectedRejectReasonId() ?? ''"
                  (change)="selectedRejectReasonId.set($any($event.target).value ? +$any($event.target).value : null)"
                >
                  <option value="" disabled>{{ lang.t('home_deal_reject_reason') }}</option>
                  @for (reason of rejectReasons(); track reason.id) {
                    <option [value]="reason.id">{{ reason.name }}</option>
                  }
                </select>
              </label>
              <label>
                <span>{{ lang.t('home_deal_reject_note') }}</span>
                <input
                  type="text"
                  [value]="rejectNote()"
                  (input)="rejectNote.set($any($event.target).value)"
                />
              </label>
              <div class="detail-actions">
                <button
                  type="button"
                  class="btn btn-black"
                  (click)="submitReject()"
                  [disabled]="!selectedRejectReasonId() || isProcessing()"
                >
                  {{ lang.t('deal_detail_reject') }}
                </button>
                <button type="button" class="btn btn-outline-coral" (click)="rejectOpen.set(false)">
                  {{ lang.t('home_deal_accept_cancel') }}
                </button>
              </div>
            </div>
          }

          <!-- Fake payment confirm (development only, same as the dashboard) -->
          @if (showFakePay()) {
            <div class="detail-card">
              <p>{{ lang.t('home_deal_fake_payment_notice') }}</p>
              <div class="detail-actions">
                <button type="button" class="btn btn-black" (click)="completeFakePay()" [disabled]="isProcessing()">
                  {{ lang.t('home_deal_fake_payment') }}
                </button>
                <button type="button" class="btn btn-outline-coral" (click)="showFakePay.set(false)">
                  {{ lang.t('home_deal_accept_cancel') }}
                </button>
              </div>
            </div>
          }

          <!-- Rating panel -->
          @if (rateOpen()) {
            <div class="detail-card">
              <h3>{{ lang.t('home_deal_rate') }}</h3>
              <label>
                <span>{{ lang.t('home_deal_rate_stars') }}</span>
                <select
                  [value]="rateStars()"
                  (change)="rateStars.set(+$any($event.target).value)"
                >
                  @for (star of [1, 2, 3, 4, 5]; track star) {
                    <option [value]="star">{{ star }}</option>
                  }
                </select>
              </label>
              <label>
                <span>{{ lang.t('home_deal_rate_comment') }}</span>
                <input
                  type="text"
                  [value]="rateComment()"
                  (input)="rateComment.set($any($event.target).value)"
                />
              </label>
              <div class="detail-actions">
                <button type="button" class="btn btn-black" (click)="submitRating()" [disabled]="isProcessing()">
                  {{ lang.t('deal_detail_rate_submit') }}
                </button>
                <button type="button" class="btn btn-outline-coral" (click)="rateOpen.set(false)">
                  {{ lang.t('home_deal_accept_cancel') }}
                </button>
              </div>
            </div>
          }

          @if (actionError()) {
            <p class="detail-error" role="alert">{{ lang.t(actionError()!) }}</p>
          }
        </main>
      } @else {
        <p>{{ lang.t(loadError() ?? 'deal_detail_not_found') }}</p>
        <a routerLink="/home" class="btn btn-black">{{ lang.t('deal_detail_back') }}</a>
      }
    </div>
  `,
  styles: [`
    .deal-detail-screen {
      padding: 1rem;
      max-width: 720px;
      margin: 0 auto;
    }

    .detail-header {
      display: flex;
      align-items: baseline;
      gap: 0.75rem;
      margin-bottom: 1rem;

      h1 {
        font-size: 1.25rem;
        font-weight: 800;
        color: #111827;
        margin: 0;
      }
    }

    .detail-back {
      color: #e11d48;
      text-decoration: none;
      font-weight: 700;
      white-space: nowrap;
    }

    .detail-body {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .detail-card {
      background: #fff;
      border: 1px solid #f1f5f9;
      border-radius: 1rem;
      padding: 1rem;

      h2 {
        font-size: 1.1rem;
        font-weight: 800;
        color: #111827;
        margin: 0.5rem 0 0.25rem;
      }

      h3 {
        font-size: 1rem;
        font-weight: 800;
        color: #111827;
        margin: 0 0 0.75rem;
      }

      p {
        font-size: 0.9rem;
        color: #374151;
        margin: 0.25rem 0;
      }

      label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        font-size: 0.85rem;
        font-weight: 700;
        color: #374151;
        margin-bottom: 0.75rem;

        input, select {
          font: inherit;
          font-weight: 400;
          padding: 0.6rem 0.75rem;
          border: 1px solid #e5e7eb;
          border-radius: 0.6rem;
          background: #fff;
          color: #111827;
        }
      }
    }

    .detail-image {
      width: 100%;
      max-height: 280px;
      object-fit: cover;
      border-radius: 0.75rem;
    }

    .detail-number, .detail-counterparty {
      color: #6b7280;
      font-size: 0.85rem;
    }

    .detail-status {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
      font-weight: 700;
    }

    .deal-badge {
      font-size: 0.75rem;
      font-weight: 800;
      padding: 0.2rem 0.6rem;
      border-radius: 999px;
    }

    .deal-badge-green { background: #dcfce7; color: #166534; }
    .deal-badge-yellow { background: #fef9c3; color: #854d0e; }
    .deal-badge-red { background: #fee2e2; color: #991b1b; }
    .deal-badge-grey { background: #f1f5f9; color: #475569; }

    .detail-row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid #f8fafc;
      font-size: 0.9rem;

      span:first-child {
        color: #6b7280;
      }

      span:last-child {
        font-weight: 700;
        color: #111827;
        text-align: end;
      }
    }

    .detail-desc {
      white-space: pre-wrap;
    }

    .detail-note {
      font-size: 0.8rem;
      color: #6b7280;
    }

    .detail-terms-link {
      color: #e11d48;
      font-weight: 700;
    }

    .terms-list {
      margin: 0 0 1rem;
      padding-inline-start: 1.25rem;

      li {
        font-size: 0.9rem;
        color: #374151;
        margin-bottom: 0.5rem;
      }
    }

    .terms-agree {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.9rem;
      font-weight: 700;
      color: #111827;
      margin-bottom: 1rem;
    }

    .detail-actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }

    .detail-error {
      color: #b91c1c;
      font-weight: 700;
    }
  `],
})
export class DealDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsApi = inject(DealsService);
  readonly authService = inject(AuthService);
  readonly lang = inject(LanguageService);

  readonly deal = signal<DealDetail | null>(null);
  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly isProcessing = signal(false);

  readonly acceptStep = signal<'terms' | 'address' | null>(null);
  readonly termsAccepted = signal(false);
  readonly acceptAddresses = signal<UserAddress[]>([]);
  readonly selectedAcceptAddressId = signal<number | null>(null);
  readonly isLoadingAcceptAddresses = signal(false);

  readonly rejectOpen = signal(false);
  readonly rejectReasons = signal<LookupItem[]>([]);
  readonly selectedRejectReasonId = signal<number | null>(null);
  readonly rejectNote = signal('');

  readonly showFakePay = signal(false);
  readonly rateOpen = signal(false);
  readonly rateStars = signal(5);
  readonly rateComment = signal('');

  readonly fee = computed(() => this.deal()?.feeAmount ?? PLATFORM_FEE_SAR);
  readonly sellerNet = computed(() => {
    const d = this.deal();
    if (!d) return 0;
    return d.sellerNetAmount ?? d.amount - this.fee();
  });

  readonly statusText = computed(() => {
    const d = this.deal();
    if (!d) return '';
    const key = KNOWN_STATUSES.includes(d.status) ? d.status : 'unknown';
    return this.lang.t(`home_deal_status_${key}`);
  });

  constructor() {
    if (!this.authService.isAuthenticated()) {
      this.isLoading.set(false);
      this.loadError.set('deal_detail_login_required');
      return;
    }
    this.load();
  }

  load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.isLoading.set(false);
      this.loadError.set('deal_detail_not_found');
      return;
    }
    this.isLoading.set(true);
    this.loadError.set(null);
    this.dealsApi.getDealDetail(id).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) {
          this.loadError.set('deal_detail_not_found');
          return;
        }
        this.deal.set(response.data);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set('deal_detail_load_error');
      },
    });
  }

  money(value: number): string {
    return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  fmtDate(value: string): string {
    return new Date(value).toLocaleDateString(this.lang.isAr() ? 'ar-SA' : 'en-US');
  }

  fmtDateTime(value: string | null): string {
    if (!value) return '';
    return new Date(value).toLocaleString(this.lang.isAr() ? 'ar-SA' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    });
  }

  addressLabel(address: UserAddress): string {
    const tag = address.label?.trim();
    const location = [address.city, address.district].filter(Boolean).join('، ');
    return [tag, location || address.fullText].filter(Boolean).join(' — ');
  }

  startAccept(): void {
    const d = this.deal();
    if (d) {
      void this.router.navigate(['/deals', d.id, 'accept']);
    }
  }

  continueFromTerms(): void {
    const d = this.deal();
    if (!d || !this.termsAccepted()) return;
    if (!d.needShipping) {
      this.submitAccept();
      return;
    }
    this.acceptStep.set('address');
    this.isLoadingAcceptAddresses.set(true);
    this.dealsApi.getMyAddresses().subscribe({
      next: (response) => {
        this.isLoadingAcceptAddresses.set(false);
        if (!response.success || !response.data) return;
        this.acceptAddresses.set(response.data);
        const preferred = response.data.find((a) => a.isDefault) ?? response.data[0];
        this.selectedAcceptAddressId.set(preferred ? preferred.id : null);
      },
      error: () => {
        this.isLoadingAcceptAddresses.set(false);
      },
    });
  }

  submitAccept(): void {
    const d = this.deal();
    if (!d || this.isProcessing()) return;
    if (d.needShipping && !this.selectedAcceptAddressId()) {
      this.actionError.set('home_deal_accept_no_address');
      return;
    }
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.acceptDeal(d.id, d.needShipping ? this.selectedAcceptAddressId() : null).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.acceptStep.set(null);
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  startReject(): void {
    this.rejectOpen.set(true);
    this.actionError.set(null);
    if (!this.rejectReasons().length) {
      this.dealsApi.getLookups('reject-reasons').subscribe({
        next: (response) => this.rejectReasons.set(response.success ? response.data ?? [] : []),
        error: () => {},
      });
    }
  }

  submitReject(): void {
    const d = this.deal();
    const reasonId = this.selectedRejectReasonId();
    if (!d || !reasonId || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.rejectDeal(d.id, reasonId, this.rejectNote().trim() || undefined).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.rejectOpen.set(false);
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  cancel(): void {
    const d = this.deal();
    if (!d || this.isProcessing() || !globalThis.confirm(this.lang.t('home_deal_cancel_confirm'))) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.cancelDeal(d.id).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  fund(): void {
    const d = this.deal();
    if (!d || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.fundDeal(d.id).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success || !response.data) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        if (response.data.provider?.toLowerCase() === 'fake') {
          this.showFakePay.set(true);
          return;
        }
        globalThis.location.assign(response.data.redirectUrl);
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  completeFakePay(): void {
    const d = this.deal();
    if (!d || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.completeFakePayment(d.id).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.showFakePay.set(false);
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  deliver(): void {
    const d = this.deal();
    if (!d || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.deliverDeal(d.id).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  release(): void {
    const d = this.deal();
    if (!d || this.isProcessing() || !globalThis.confirm(this.lang.t('home_deal_release_confirm'))) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.releaseEscrow(d.id).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }

  submitRating(): void {
    const d = this.deal();
    if (!d || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.rateDeal(d.id, this.rateStars(), this.rateComment().trim() || undefined).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.actionError.set('home_deal_action_error');
          return;
        }
        this.rateOpen.set(false);
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }
}
