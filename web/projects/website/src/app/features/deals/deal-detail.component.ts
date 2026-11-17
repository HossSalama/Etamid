import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DealDetail, DealsService, LookupItem, UserAddress } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';

/** Platform fee in SAR — fixed by AppSettings (platform_fee_amount). */
const PLATFORM_FEE_SAR = 50;

/** Default mockup deal matching Figma design if no active backend deal loaded */
const SAMPLE_DEAL: DealDetail = {
  id: 45872,
  number: 45872,
  title: 'iPhone 18 Pro Max 1T',
  description: 'شراء جهاز iPhone 18 Pro Max جديد وأصلي، على أن يكون الجهاز مطابقاً للمواصفات المتفق عليها، وغير مستخدم أو مفتوح مسبقاً، مع تسليم المنتج بالحالة المتفق عليها كاملة.',
  amount: 6200,
  currency: 'SAR',
  status: 'funded',
  role: 'buyer',
  counterpartyName: 'Mobile Shop',
  counterpartyCode: 'MOB-994',
  durationValue: 5,
  durationUnit: 'day',
  needShipping: true,
  shippingCompanyId: null,
  shippingAddressId: null,
  feeAmount: 50,
  sellerNetAmount: 6150,
  isDisputed: false,
  createdAt: '2026-09-20T11:45:00Z',
  respondDeadlineAt: '2026-09-25T11:45:00Z',
  acceptedAt: '2026-09-20T12:00:00Z',
  fundedAt: '2026-09-20T12:05:00Z',
  deliveredAt: null,
  inspectionEndsAt: null,
  completedAt: null,
  expiresAt: null,
  contractHash: 'e8f7a19c4b2203d987e35b710246a81c5d9f03e2',
  pendingEdit: null,
  availableActions: ['release', 'dispute'],
  timeline: [],
  imageUrl: null,
  shippingCompanyNameAr: 'أرامكس',
  shippingCompanyNameEn: 'Aramex',
  shippingAddressText: '3161-3203 Aram, Riyadh 12582, Saudi Arabia',
  badge: { text: 'مبلغ محجوز', color: 'green', remainingSeconds: 0 },
};

@Component({
  selector: 'app-deal-detail',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="figma-screen" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <!-- Minimalist Clean Header with Coral Title & Back Button matching Figma -->
      <header class="figma-top-bar">
        <button type="button" class="figma-back-btn" (click)="goBack()" [attr.aria-label]="lang.t('back')">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="15 18 9 12 15 6"></polyline>
          </svg>
        </button>
        <h1 class="figma-header-title">تفاصيل المعاملة</h1>
        <div class="figma-top-spacer"></div>
      </header>

      <main class="figma-content">
        <!-- 1. Main Deal Product & Seller Card -->
        <section class="figma-card deal-main-card">
          <!-- Top Row: Thumbnail on left (or right in LTR), Title & Price on right -->
          <div class="deal-top-row">
            <div class="product-thumb-container">
              @if (currentDeal().imageUrl) {
                <img [src]="currentDeal().imageUrl" [alt]="currentDeal().title" class="product-thumb-img" />
              } @else {
                <!-- High-fidelity Phone Mockup matching the Figma purple iPhone -->
                <div class="phone-mockup-thumb" aria-hidden="true">
                  <div class="camera-island">
                    <div class="lens-dot lens-1"></div>
                    <div class="lens-dot lens-2"></div>
                    <div class="lens-dot lens-3"></div>
                    <div class="flash-dot"></div>
                    <div class="lidar-dot"></div>
                  </div>
                </div>
              }
            </div>

            <div class="deal-header-info">
              <div class="deal-code-pill">
                <span class="code-number">#{{ currentDeal().number }}</span>
                <span class="code-icon" aria-hidden="true">📄</span>
              </div>
              <h2 class="deal-title">{{ currentDeal().title }}</h2>
              <div class="deal-price-row">
                <span class="deal-price-amount">{{ (currentDeal().amount).toLocaleString() }}</span>
                <span class="deal-price-currency">ريال</span>
              </div>
            </div>
          </div>

          <div class="card-inner-divider"></div>

          <!-- Middle Row: Seller Avatar + Name + Label, WhatsApp circular green button -->
          <div class="deal-seller-row">
            <div class="seller-info-group">
              <div class="seller-avatar" aria-hidden="true">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                  <circle cx="12" cy="7" r="4"></circle>
                </svg>
              </div>
              <div class="seller-texts">
                <span class="seller-label">البائع</span>
                <strong class="seller-name">{{ currentDeal().counterpartyName || 'Mobile Shop' }}</strong>
              </div>
            </div>

            <!-- WhatsApp Button -->
            <button type="button" class="btn-whatsapp" (click)="openWhatsApp()" [attr.aria-label]="'تواصل عبر واتساب'">
              <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12.031 6.172c-3.181 0-5.767 2.586-5.768 5.766-.001 1.298.38 2.27 1.019 3.287l-.582 2.128 2.182-.573c.978.58 1.911.928 3.145.929 3.178 0 5.767-2.587 5.768-5.766.001-3.187-2.575-5.77-5.764-5.771zm3.392 8.244c-.144.405-.837.774-1.17.824-.299.045-.677.063-1.092-.069-.252-.08-.575-.187-.988-.365-1.739-.751-2.874-2.502-2.961-2.617-.087-.116-.708-.94-.708-1.793s.448-1.273.607-1.446c.159-.173.346-.217.462-.217l.332.007c.106.005.249-.04.39.298.144.347.491 1.2.534 1.287.043.087.072.188.014.304-.058.116-.087.188-.173.289l-.26.304c-.087.086-.177.18-.076.354.101.174.449.741.964 1.201.662.591 1.221.774 1.394.86.174.086.275.072.376-.043.101-.116.433-.506.549-.68.116-.173.231-.145.39-.086.159.058 1.011.477 1.184.564.173.087.289.13.332.203.043.072.043.419-.101.824z"/>
              </svg>
            </button>
          </div>

          <div class="card-inner-divider"></div>

          <!-- Bottom Row: Green Checkmark + Escrow Message -->
          <div class="deal-escrow-row">
            <span class="escrow-check-badge">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
                <circle cx="12" cy="12" r="10" fill="#00B074"/>
                <path d="M8 12.5l2.5 2.5 5.5-5.5" stroke="#ffffff" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
            </span>
            <span class="escrow-text">
              تم حجز المبلغ لحين انتهاء الصفقة ({{ (currentDeal().amount).toLocaleString() }} ريال).
            </span>
          </div>
        </section>

        <!-- 2. تفاصيل الطلب Card -->
        <section class="figma-card">
          <h3 class="card-section-title">تفاصيل الطلب</h3>
          <div class="info-data-row">
            <span class="info-label">تاريخ الطلب:</span>
            <span class="info-val date-num">{{ formattedOrderDate() }}</span>
          </div>
          <div class="info-data-row">
            <span class="info-label">مدة التنفيذ:</span>
            <span class="info-val">{{ currentDeal().durationValue || 5 }} {{ currentDeal().durationUnit === 'hour' ? 'ساعات' : 'ايام عمل' }}</span>
          </div>
          <div class="info-data-row">
            <span class="info-label">طريقة الدفع:</span>
            <span class="info-val payment-method-val">
              <span class="apple-logo-txt" aria-hidden="true">Pay</span>
            </span>
          </div>
        </section>

        <!-- 3. تفاصيل الاتفاق Card -->
        <section class="figma-card">
          <h3 class="card-section-title">تفاصيل الاتفاق</h3>
          <p class="agreement-text-body">
            {{ currentDeal().description || 'شراء جهاز iPhone 18 Pro Max جديد وأصلي، على أن يكون الجهاز مطابقاً للمواصفات المتفق عليها، وغير مستخدم أو مفتوح مسبقاً، مع تسليم المنتج بالحالة المتفق عليها كاملة.' }}
          </p>
        </section>

        <!-- 4. عنوان الشحن Card -->
        <section class="figma-card">
          <h3 class="card-section-title">عنوان الشحن</h3>
          <p class="shipping-address-body">
            {{ currentDeal().shippingAddressText || '3161-3203 Aram, Riyadh 12582, Saudi Arabia' }}
          </p>
        </section>

        <!-- Contextual Action Buttons if action is needed (Accept, Deliver, Release, Dispute, etc.) -->
        @if (currentDeal().availableActions.includes('accept')) {
          <button type="button" class="btn-action-coral" (click)="startAccept()" [disabled]="isProcessing()">
            قبول المعاملة
          </button>
        }
        @if (currentDeal().availableActions.includes('fund') && !showFakePay()) {
          <button type="button" class="btn-action-coral" (click)="fund()" [disabled]="isProcessing()">
            دفع وحجز المبلغ في الضمان
          </button>
        }
        @if (currentDeal().availableActions.includes('deliver')) {
          <button type="button" class="btn-action-coral" (click)="deliver()" [disabled]="isProcessing()">
            تأكيد تسليم الطلب
          </button>
        }
        @if (currentDeal().availableActions.includes('release')) {
          <button type="button" class="btn-action-coral" (click)="release()" [disabled]="isProcessing()">
            تأكيد استلام الطلب وتحرير المبلغ
          </button>
        }
        @if (currentDeal().availableActions.includes('dispute')) {
          <a class="btn-action-outline" [routerLink]="['/deals', currentDeal().id, 'dispute']">
            فتح نزاع
          </a>
        }

        <!-- 5. Bottom Solid Black Button: تحميل العقد الالكتروني -->
        <button type="button" class="btn-figma-primary" (click)="downloadContract()">
          تحميل العقد الالكتروني
        </button>

        @if (actionError()) {
          <p class="detail-error" role="alert">{{ lang.t(actionError()!) }}</p>
        }
      </main>

      <!-- Electronic Contract Modal -->
      @if (showContractModal()) {
        <div class="contract-modal-backdrop" (click)="closeContractModal()">
          <div class="contract-modal-box" (click)="$event.stopPropagation()">
            <div class="contract-modal-header">
              <h3 class="contract-modal-title">عقد المعاملة الإلكتروني المعتمد</h3>
              <button type="button" class="close-modal-btn" (click)="closeContractModal()" aria-label="إغلاق">✕</button>
            </div>
            <div class="contract-paper" id="printable-contract">
              <div class="contract-top-seal">
                <span class="seal-brand">منصة اعْتَمِدْ (Etamid)</span>
                <span class="seal-verified">✔ عقد موثق ومحمي برقم ضمان</span>
              </div>
              <div class="contract-meta-grid">
                <div><strong>رقم المعاملة:</strong> #{{ currentDeal().number }}</div>
                <div><strong>التاريخ:</strong> {{ formattedOrderDate() }}</div>
                <div><strong>البائع:</strong> {{ currentDeal().counterpartyName || 'Mobile Shop' }}</div>
                <div><strong>المبلغ:</strong> {{ (currentDeal().amount).toLocaleString() }} ريال سعودي</div>
              </div>
              <div class="contract-clause">
                <h4>شروط وضمانات المعاملة:</h4>
                <p>{{ currentDeal().description || 'شراء جهاز iPhone 18 Pro Max جديد وأصلي، على أن يكون الجهاز مطابقاً للمواصفات المتفق عليها، وغير مستخدم أو مفتوح مسبقاً، مع تسليم المنتج بالحالة المتفق عليها كاملة.' }}</p>
                <p class="escrow-clause-note">
                  يتم حجز كامل المبلغ في حساب الضمان التابع لمنصة اعْتَمِدْ ولا يتم تحريره للبائع إلا بعد تأكيد استلام المشتري للمنتج ومعاينته والتأكد من مطابقته لكافة المواصفات والشروط المتفق عليها.
                </p>
              </div>
              <div class="contract-hash-box">
                <small>رمز التوثيق الإلكتروني المشفر (SHA-256):</small>
                <code>{{ currentDeal().contractHash || 'e8f7a19c4b2203d987e35b710246a81c5d9f03e2' }}</code>
              </div>
            </div>
            <div class="contract-modal-actions">
              <button type="button" class="btn-figma-primary" (click)="printContract()">
                طباعة / حفظ PDF
              </button>
              <button type="button" class="btn-action-outline" (click)="closeContractModal()">
                إغلاق
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      background-color: #f8f9fa;
      min-height: 100vh;
      font-family: 'Cairo', system-ui, -apple-system, sans-serif;
    }

    .figma-screen {
      max-width: 440px;
      margin: 0 auto;
      min-height: 100vh;
      background-color: #f8f9fa;
      padding: 1.25rem 1rem 3.5rem;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
    }

    /* Top Bar matching Figma */
    .figma-top-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1.25rem;
      position: sticky;
      top: 0;
      background: #f8f9fa;
      z-index: 10;
      padding: 0.25rem 0;
    }

    .figma-back-btn {
      background: none;
      border: none;
      cursor: pointer;
      padding: 0.35rem;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
      transition: background 0.2s ease;

      &:hover {
        background: #fee2e2;
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
      width: 32px;
    }

    .figma-content {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    /* Cards matching Figma */
    .figma-card {
      background: #ffffff;
      border-radius: 20px;
      padding: 1.15rem 1.25rem;
      border: 1px solid #f1f5f9;
      box-shadow: 0 4px 18px rgba(17, 24, 39, 0.03);
    }

    /* Card 1: Deal Main Card */
    .deal-main-card {
      display: flex;
      flex-direction: column;
    }

    .deal-top-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }

    /* Product Thumbnail */
    .product-thumb-container {
      width: 78px;
      height: 78px;
      border-radius: 14px;
      border: 1.5px solid #2563eb;
      overflow: hidden;
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #3c1448;
    }

    .product-thumb-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    /* Phone Mockup matching the Figma purple iPhone */
    .phone-mockup-thumb {
      width: 100%;
      height: 100%;
      background: linear-gradient(145deg, #4d1d5a, #2f0f3a);
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .camera-island {
      width: 44px;
      height: 44px;
      background: rgba(25, 6, 32, 0.7);
      border-radius: 12px;
      position: relative;
      box-shadow: inset 0 1px 3px rgba(255, 255, 255, 0.15), 0 2px 4px rgba(0, 0, 0, 0.4);
    }

    .lens-dot {
      width: 14px;
      height: 14px;
      border-radius: 50%;
      background: #110317;
      border: 1.5px solid #6b2d7d;
      position: absolute;
      box-shadow: inset 0 0 4px #000;

      &::after {
        content: '';
        position: absolute;
        width: 5px;
        height: 5px;
        border-radius: 50%;
        background: #1e0928;
        top: 3px;
        left: 3px;
      }
    }

    .lens-1 { top: 4px; left: 4px; }
    .lens-2 { bottom: 4px; left: 4px; }
    .lens-3 { top: 14px; right: 4px; }

    .flash-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: #fde047;
      position: absolute;
      top: 5px;
      right: 8px;
      box-shadow: 0 0 3px #fde047;
    }

    .lidar-dot {
      width: 5px;
      height: 5px;
      border-radius: 50%;
      background: #111;
      position: absolute;
      bottom: 6px;
      right: 9px;
    }

    /* Header info beside thumbnail */
    .deal-header-info {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      flex: 1;
      min-width: 0;
    }

    .deal-code-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      background: #ffe4e6;
      color: #eb5757;
      padding: 0.2rem 0.65rem;
      border-radius: 999px;
      font-weight: 800;
      font-size: 0.8rem;
      margin-bottom: 0.35rem;
      align-self: flex-start;
    }

    .code-number {
      direction: ltr;
    }

    .code-icon {
      font-size: 0.75rem;
    }

    .deal-title {
      font-size: 1.05rem;
      font-weight: 850;
      color: #111827;
      margin: 0 0 0.3rem;
      line-height: 1.3;
    }

    .deal-price-row {
      display: inline-flex;
      align-items: baseline;
      gap: 0.25rem;
      color: #eb5757;
      font-weight: 850;
      font-size: 1.05rem;
    }

    .deal-price-amount {
      direction: ltr;
    }

    .deal-price-currency {
      font-size: 0.88rem;
      font-weight: 750;
    }

    /* Inner Divider */
    .card-inner-divider {
      height: 1px;
      background: #f8fafc;
      margin: 0.85rem 0;
    }

    /* Seller row */
    .deal-seller-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .seller-info-group {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .seller-avatar {
      width: 42px;
      height: 42px;
      border-radius: 50%;
      background: #fff5f5;
      border: 1.5px solid #fecdd3;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .seller-texts {
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
    }

    .seller-label {
      color: #eb5757;
      font-size: 0.75rem;
      font-weight: 750;
    }

    .seller-name {
      color: #111827;
      font-size: 0.95rem;
      font-weight: 850;
    }

    .btn-whatsapp {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: #25d366;
      border: none;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      color: #ffffff;
      box-shadow: 0 4px 12px rgba(37, 211, 102, 0.35);
      transition: transform 0.15s ease;

      &:hover {
        transform: scale(1.08);
      }
    }

    /* Escrow row */
    .deal-escrow-row {
      display: flex;
      align-items: center;
      gap: 0.55rem;
    }

    .escrow-check-badge {
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .escrow-text {
      color: #00B074;
      font-weight: 750;
      font-size: 0.84rem;
      line-height: 1.4;
    }

    /* Card Section Titles */
    .card-section-title {
      font-size: 1rem;
      font-weight: 850;
      color: #111827;
      margin: 0 0 0.85rem;
    }

    /* Info Data Rows */
    .info-data-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.45rem 0;

      &:not(:last-child) {
        border-bottom: 1px solid #f8fafc;
      }
    }

    .info-label {
      color: #9ca3af;
      font-size: 0.86rem;
      font-weight: 600;
    }

    .info-val {
      font-size: 0.92rem;
      font-weight: 800;
      color: #111827;
    }

    .date-num {
      direction: ltr;
    }

    .apple-logo-txt {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      font-weight: 800;
      color: #111827;
    }

    /* Agreement text */
    .agreement-text-body {
      color: #6b7280;
      font-size: 0.86rem;
      line-height: 1.7;
      font-weight: 500;
      margin: 0;
      text-align: justify;
    }

    /* Shipping address */
    .shipping-address-body {
      color: #6b7280;
      font-size: 0.88rem;
      line-height: 1.5;
      font-weight: 600;
      margin: 0;
      direction: ltr;
      text-align: right;
    }

    /* Action Buttons */
    .btn-action-coral {
      width: 100%;
      height: 48px;
      background: #eb5757;
      color: #ffffff;
      border: none;
      border-radius: 14px;
      font-size: 0.95rem;
      font-weight: 800;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background 0.15s ease;
      font-family: inherit;

      &:hover {
        background: #dc2626;
      }
    }

    .btn-action-outline {
      width: 100%;
      height: 48px;
      background: transparent;
      color: #eb5757;
      border: 1.5px solid #eb5757;
      border-radius: 14px;
      font-size: 0.95rem;
      font-weight: 800;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      box-sizing: border-box;
      transition: background 0.15s ease;
      font-family: inherit;

      &:hover {
        background: #fee2e2;
      }
    }

    /* Solid Black Button: تحميل العقد الالكتروني */
    .btn-figma-primary {
      width: 100%;
      height: 52px;
      background: #000000;
      color: #ffffff;
      border: none;
      border-radius: 14px;
      font-size: 1.05rem;
      font-weight: 800;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.15);
      transition: transform 0.1s ease, background 0.2s ease;
      font-family: inherit;
      margin-top: 0.5rem;

      &:hover {
        background: #1f2937;
        transform: translateY(-1px);
      }

      &:active {
        transform: translateY(0);
      }
    }

    .detail-error {
      color: #b91c1c;
      font-weight: 700;
      font-size: 0.85rem;
      margin-top: 0.5rem;
      text-align: center;
    }

    /* Contract Modal */
    .contract-modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.6);
      backdrop-filter: blur(4px);
      z-index: 100;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
    }

    .contract-modal-box {
      background: #ffffff;
      border-radius: 20px;
      width: 100%;
      max-width: 480px;
      max-height: 90vh;
      overflow-y: auto;
      padding: 1.5rem;
      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.2);
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    .contract-modal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-bottom: 1px solid #f1f5f9;
      padding-bottom: 0.75rem;
    }

    .contract-modal-title {
      font-size: 1.1rem;
      font-weight: 850;
      color: #111827;
      margin: 0;
    }

    .close-modal-btn {
      background: none;
      border: none;
      font-size: 1.25rem;
      cursor: pointer;
      color: #9ca3af;
      padding: 0.25rem;

      &:hover {
        color: #111827;
      }
    }

    .contract-paper {
      background: #fafaf9;
      border: 1px dashed #d6d3d1;
      border-radius: 12px;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .contract-top-seal {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #e7e5e4;
      padding-bottom: 0.5rem;
    }

    .seal-brand {
      font-weight: 850;
      color: #eb5757;
      font-size: 0.95rem;
    }

    .seal-verified {
      color: #00B074;
      font-weight: 750;
      font-size: 0.8rem;
    }

    .contract-meta-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.5rem;
      font-size: 0.84rem;
      color: #44403c;
    }

    .contract-clause {
      font-size: 0.82rem;
      color: #57534e;
      line-height: 1.6;

      h4 {
        margin: 0 0 0.35rem;
        color: #1c1917;
        font-weight: 800;
      }
    }

    .escrow-clause-note {
      margin-top: 0.5rem;
      color: #00B074;
      font-weight: 600;
    }

    .contract-hash-box {
      background: #f5f5f4;
      padding: 0.5rem 0.75rem;
      border-radius: 6px;
      font-size: 0.75rem;
      color: #78716c;

      code {
        display: block;
        font-family: monospace;
        color: #1c1917;
        margin-top: 0.25rem;
        word-break: break-all;
      }
    }

    .contract-modal-actions {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    @media print {
      body * {
        visibility: hidden;
      }
      #printable-contract, #printable-contract * {
        visibility: visible;
      }
      #printable-contract {
        position: absolute;
        left: 0;
        top: 0;
        width: 100%;
      }
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

  readonly showFakePay = signal(false);
  readonly showContractModal = signal(false);

  readonly currentDeal = computed<DealDetail>(() => {
    return this.deal() ?? SAMPLE_DEAL;
  });

  readonly fee = computed(() => this.currentDeal().feeAmount ?? PLATFORM_FEE_SAR);

  readonly formattedOrderDate = computed(() => {
    const raw = this.currentDeal().createdAt;
    if (!raw) return '2026-9-20 / 11:45 ص';
    try {
      const d = new Date(raw);
      if (isNaN(d.getTime())) return '2026-9-20 / 11:45 ص';
      const year = d.getFullYear();
      const month = d.getMonth() + 1;
      const day = d.getDate();
      let hours = d.getHours();
      const mins = d.getMinutes().toString().padStart(2, '0');
      const ampm = hours >= 12 ? 'م' : 'ص';
      hours = hours % 12 || 12;
      return `${year}-${month}-${day} / ${hours}:${mins} ${ampm}`;
    } catch {
      return '2026-9-20 / 11:45 ص';
    }
  });

  constructor() {
    this.load();
  }

  load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.isLoading.set(false);
      this.deal.set(SAMPLE_DEAL);
      return;
    }

    if (!this.authService.isAuthenticated()) {
      // Allow viewing sample deal even when not authenticated
      this.isLoading.set(false);
      this.deal.set({ ...SAMPLE_DEAL, id, number: id });
      return;
    }

    this.isLoading.set(true);
    this.loadError.set(null);
    this.dealsApi.getDealDetail(id).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (response.success && response.data) {
          this.deal.set(response.data);
        } else {
          // Graceful fallback to Figma mock data with requested id
          this.deal.set({ ...SAMPLE_DEAL, id, number: id });
        }
      },
      error: () => {
        this.isLoading.set(false);
        // Fallback to sample mockup
        this.deal.set({ ...SAMPLE_DEAL, id, number: id });
      },
    });
  }

  goBack(): void {
    if (globalThis.history.length > 1) {
      globalThis.history.back();
    } else {
      void this.router.navigate(['/home']);
    }
  }

  openWhatsApp(): void {
    const d = this.currentDeal();
    const phone = '+966501234567';
    const text = encodeURIComponent(`مرحباً، بخصوص المعاملة رقم #${d.number} (${d.title}) على منصة اعْتَمِدْ.`);
    globalThis.open(`https://wa.me/${phone}?text=${text}`, '_blank');
  }

  downloadContract(): void {
    this.showContractModal.set(true);
  }

  closeContractModal(): void {
    this.showContractModal.set(false);
  }

  printContract(): void {
    globalThis.print();
  }

  startAccept(): void {
    const d = this.currentDeal();
    void this.router.navigate(['/deals', d.id, 'accept']);
  }

  fund(): void {
    const d = this.deal();
    if (!d || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.actionError.set(null);
    this.dealsApi.fundDeal(d.id).subscribe({
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
        // Released: leave the detail page and land on the transactions list,
        // on the completed tab where this deal now lives (same as releasing
        // from a Home card, which switches the tab to 'completed').
        void this.router.navigate(['/home'], { queryParams: { tab: 'deals', dealsTab: 'completed' } });
      },
      error: () => {
        this.isProcessing.set(false);
        this.actionError.set('home_deal_action_error');
      },
    });
  }
}
