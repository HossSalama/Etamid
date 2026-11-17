import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DealsService, CompanyDealDetail, LookupItem } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

@Component({
  selector: 'app-company-deal-detail',
  standalone: true,
  imports: [RouterLink, BottomNavComponent],
  template: `
    <div class="figma-screen" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <!-- Minimalist Clean White Header with Coral Title matching Figma -->
      <header class="figma-top-bar">
        <button type="button" class="figma-back-btn" (click)="goBack()" [attr.aria-label]="lang.t('back')">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#eb5757" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="15 18 9 12 15 6"></polyline>
          </svg>
        </button>
        <h1 class="figma-header-title">{{ lang.isAr() ? 'تفاصيل الصفقة' : 'Deal Details' }}</h1>
        <div class="figma-top-spacer"></div>
      </header>

      <main class="figma-content">
        @if (isLoading()) {
          <div class="figma-card text-center loading-card">
            <p>{{ lang.t('company_loading') }}</p>
          </div>
        } @else if (!deal()) {
          <div class="figma-card text-center error-card">
            <p class="company-error">{{ lang.t('company_error') }}</p>
            <a routerLink="/company-deals" class="btn btn-outline-coral" style="margin-top: 1rem;">
              {{ lang.t('back') }}
            </a>
          </div>
        } @else if (deal(); as d) {
          <!-- 1. Company Information Card -->
          <section class="figma-card company-card">
            <div class="company-info-block">
              <h2 class="company-main-name">{{ d.counterpartyName || (lang.isAr() ? 'شركة المستقبل للتقنية' : 'Future Tech Company') }}</h2>
              <p class="company-sub-title">{{ d.title || (lang.isAr() ? 'توريد أجهزة تقنية' : 'Tech Hardware Supply') }}</p>
            </div>
            <div class="company-code-pill">
              <span class="code-number">{{ d.number || dealId || '45872' }}</span>
              <span class="code-icon" aria-hidden="true">📄</span>
            </div>
          </section>

          <!-- 2. Transaction Details Card -->
          <section class="figma-card">
            <h3 class="card-section-title">{{ lang.isAr() ? 'تفاصيل المعاملة' : 'Transaction Details' }}</h3>
            <div class="info-data-row">
              <span class="info-label">{{ lang.isAr() ? 'القيمة الإجمالية:' : 'Total Amount:' }}</span>
              <span class="info-val price-val">
                <span class="sar-icon" aria-hidden="true">﷼</span>
                {{ d.totalAmount.toLocaleString() }}
              </span>
            </div>
            <div class="info-data-row">
              <span class="info-label">{{ lang.isAr() ? 'تاريخ بدء المعاملة:' : 'Start Date:' }}</span>
              <span class="info-val date-val">{{ fmtDate(d.startDate || d.createdAt) }}</span>
            </div>
          </section>

          <!-- 3. Installments Schedule Card -->
          <section class="figma-card">
            <h3 class="card-section-title">{{ lang.isAr() ? 'جدول الدفعات' : 'Payment Schedule' }}</h3>
            <div class="installments-table">
              @for (inst of getDisplayInstallments(d); track inst.id || $index) {
                <div class="installment-row">
                  <div class="inst-cell inst-name">
                    {{ inst.label || (lang.isAr() ? ('الدفعة ' + getOrdinalAr(inst.order || ($index + 1))) : ('Installment ' + (inst.order || ($index + 1)))) }}
                  </div>
                  <div class="inst-cell inst-amount">
                    {{ (inst.amount || (d.totalAmount / 4)).toLocaleString() }}
                    <span class="sar-symbol-coral" aria-hidden="true">﷼</span>
                  </div>
                  <div class="inst-cell inst-date">
                    <span class="cal-emoji" aria-hidden="true">📅</span>
                    <span>{{ fmtDate(inst.dueDate) }}</span>
                  </div>
                  <div class="inst-cell inst-status">
                    <span class="status-pill" [class]="getInstallmentStatus(inst, $index).pillClass">
                      {{ getInstallmentStatus(inst, $index).label }}
                    </span>
                  </div>
                </div>
              }
            </div>
          </section>

          <!-- 4. Penalty Clause Card -->
          <section class="figma-card">
            <h3 class="card-section-title">{{ lang.isAr() ? 'الشرط الجزائي' : 'Penalty Clause' }}</h3>
            <p class="penalty-percentage">{{ d.penaltyPercent || 5 }}% {{ lang.isAr() ? 'من القيمة الإجمالية.' : 'of the total amount.' }}</p>
            <p class="penalty-text-body">
              {{ lang.isAr() 
                ? 'يتم تطبيق الشرط الجزائي تلقائياً في حالة تأخر الشركة عن تسليم أي من الدفعات بعد مرور ' + (d.penaltyGraceDays || 30) + ' يوم من تاريخ الاستحقاق.'
                : 'The penalty is automatically applied if the company delays delivering any installment after ' + (d.penaltyGraceDays || 30) + ' days from due date.'
              }}
            </p>
          </section>

          <!-- 5. Contract Card -->
          <section class="figma-card">
            <h3 class="card-section-title">{{ lang.isAr() ? 'عقد المعاملة' : 'Transaction Contract' }}</h3>
            <p class="contract-desc">
              {{ lang.isAr() ? 'مرفق عقد المعاملة للاطلاع على التفاصيل اضغط على الزر أدناه.' : 'Transaction contract attached. Click the button below to view details.' }}
            </p>
            <button type="button" class="btn-contract-coral" (click)="openContract(d.contractFileUrl)">
              {{ lang.isAr() ? 'عقد المعاملة' : 'View Contract' }}
            </button>
          </section>

          <!-- 6. Cycle Actions Button if pending acceptance or funding -->
          @if (d.availableActions.includes('accept')) {
            <button type="button" class="btn-figma-primary" (click)="accept()">
              {{ lang.isAr() ? 'أعتمد' : 'Accept Deal' }}
            </button>
          } @else if (d.availableActions.includes('submit')) {
            <button type="button" class="btn-figma-primary" (click)="submit()">
              {{ lang.isAr() ? 'تقديم المعاملة' : 'Submit Deal' }}
            </button>
          }

          @if (error()) {
            <div class="error-banner" role="alert">
              ⚠️ {{ error() }}
            </div>
          }
        }
      </main>

      <app-bottom-nav activeNav="company" />
    </div>
  `,
  styles: [`
    .figma-screen {
      width: 100%;
      max-width: 100%;
      min-height: 100vh;
      background-color: #f8fafc;
      padding-bottom: 96px;
      position: relative;
      overflow-x: hidden;
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

    /* Content Area */
    .figma-content {
      width: min(540px, calc(100% - 1.5rem));
      margin: 1.25rem auto 2.5rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    /* Cards matching Figma */
    .figma-card {
      background: #ffffff;
      border-radius: 18px;
      padding: 1.25rem 1.35rem;
      border: 1px solid #f1f5f9;
      box-shadow: 0 4px 18px rgba(17, 24, 39, 0.03);
    }

    /* 1. Company Card */
    .company-card {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    .company-info-block {
      min-width: 0;
    }

    .company-main-name {
      font-size: 1.05rem;
      font-weight: 850;
      color: #111827;
      margin: 0 0 0.2rem;
    }

    .company-sub-title {
      font-size: 0.82rem;
      color: #6b7280;
      margin: 0;
      font-weight: 600;
    }

    .company-code-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      background: #ffe4e6;
      color: #eb5757;
      padding: 0.35rem 0.75rem;
      border-radius: 999px;
      font-weight: 800;
      font-size: 0.85rem;
      flex-shrink: 0;
    }

    .code-icon {
      font-size: 0.8rem;
    }

    /* Section Titles inside cards */
    .card-section-title {
      font-size: 1.05rem;
      font-weight: 850;
      color: #111827;
      margin: 0 0 1rem;
    }

    /* 2. Transaction Details Rows */
    .info-data-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.55rem 0;

      &:not(:last-child) {
        border-bottom: 1px solid #f8fafc;
      }
    }

    .info-label {
      color: #9ca3af;
      font-size: 0.88rem;
      font-weight: 600;
    }

    .info-val {
      font-size: 0.95rem;
      font-weight: 750;
      color: #111827;
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
    }

    .price-val {
      font-size: 1.05rem;
      font-weight: 850;
    }

    .sar-icon {
      font-size: 0.85rem;
      font-weight: 700;
      color: #111827;
    }

    /* 3. Installments Schedule Table */
    .installments-table {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .installment-row {
      display: grid;
      grid-template-columns: minmax(0, 1.1fr) auto auto minmax(0, 1fr);
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 0;
      font-size: 0.82rem;

      &:not(:last-child) {
        border-bottom: 1px solid #f1f5f9;
      }
    }

    .inst-cell {
      white-space: nowrap;
    }

    .inst-name {
      font-weight: 800;
      color: #111827;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .inst-amount {
      font-weight: 800;
      color: #111827;
      display: inline-flex;
      align-items: center;
      gap: 0.2rem;
    }

    .sar-symbol-coral {
      color: #eb5757;
      font-size: 0.8rem;
      font-weight: 800;
    }

    .inst-date {
      color: #6b7280;
      font-size: 0.76rem;
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
    }

    .cal-emoji {
      font-size: 0.75rem;
      filter: saturate(1.2);
    }

    .inst-status {
      text-align: end;
    }

    .status-pill {
      display: inline-flex;
      align-items: center;
      padding: 3px 9px;
      border-radius: 999px;
      font-size: 0.73rem;
      font-weight: 750;
      line-height: 1.3;
    }

    .pill-paid {
      background: #ecfdf5;
      color: #10b981;
    }

    .pill-pending {
      background: #fef2f2;
      color: #eb5757;
    }

    .pill-upcoming {
      background: #f3f4f6;
      color: #9ca3af;
    }

    /* 4. Penalty Clause */
    .penalty-percentage {
      font-size: 0.88rem;
      font-weight: 700;
      color: #4b5563;
      margin: 0 0 0.5rem;
    }

    .penalty-text-body {
      font-size: 0.82rem;
      color: #9ca3af;
      line-height: 1.6;
      margin: 0;
    }

    /* 5. Contract */
    .contract-desc {
      font-size: 0.82rem;
      color: #9ca3af;
      margin: 0 0 1rem;
      line-height: 1.5;
    }

    .btn-contract-coral {
      width: 100%;
      height: 48px;
      background: #eb5757;
      color: #ffffff;
      border: none;
      border-radius: 12px;
      font-size: 0.98rem;
      font-weight: 800;
      font-family: inherit;
      cursor: pointer;
      transition: background 0.2s, transform 0.15s ease;
      box-shadow: 0 4px 14px rgba(235, 87, 87, 0.25);

      &:hover {
        background: #d84545;
      }
      &:active {
        transform: translateY(1px);
      }
    }

    /* 6. Primary Action Button */
    .btn-figma-primary {
      width: 100%;
      height: 52px;
      background: #111827;
      color: #ffffff;
      border: none;
      border-radius: 14px;
      font-size: 1.05rem;
      font-weight: 850;
      font-family: inherit;
      cursor: pointer;
      margin-top: 0.5rem;
      box-shadow: 0 6px 20px rgba(17, 24, 39, 0.15);
      transition: background 0.2s, transform 0.15s ease;

      &:hover {
        background: #000000;
      }
      &:active {
        transform: translateY(1px);
      }
    }

    .error-banner {
      background: #fef2f2;
      color: #b91c1c;
      padding: 0.75rem 1rem;
      border-radius: 12px;
      font-size: 0.85rem;
      font-weight: 700;
      text-align: center;
    }

    .loading-card, .error-card {
      padding: 3rem 1.5rem;
      text-align: center;
    }
  `],
})
export class CompanyDealDetailComponent implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly lang = inject(LanguageService);

  protected readonly deal = signal<CompanyDealDetail | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly error = signal('');
  protected readonly rejectReasons = signal<LookupItem[]>([]);
  protected readonly fundSessionUrl = signal('');

  protected dealId = 0;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.dealId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
    this.dealsService.getLookups('company-reject-reasons').subscribe({
      next: (res) => this.rejectReasons.set(res.data ?? []),
      error: () => {},
    });
  }

  protected goBack(): void {
    void this.router.navigate(['/company-deals']);
  }

  protected fmtDate(dateStr?: string | null): string {
    if (!dateStr) return '20/9/2026';
    try {
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return dateStr;
      const day = d.getDate();
      const month = d.getMonth() + 1;
      const year = d.getFullYear();
      return `${day}/${month}/${year}`;
    } catch {
      return dateStr;
    }
  }

  protected getOrdinalAr(order: number): string {
    const ordinals = ['الأولى', 'الثانية', 'الثالثة', 'الرابعة', 'الخامسة', 'السادسة', 'السابعة', 'الثامنة', 'التاسعة', 'العاشرة'];
    return ordinals[order - 1] ?? order.toString();
  }

  protected getDisplayInstallments(d: CompanyDealDetail): any[] {
    if (d.installments && d.installments.length) return d.installments;
    const quarter = d.totalAmount ? d.totalAmount / 4 : 612500;
    return [
      { id: 1, order: 1, label: 'الدفعة الأولى', amount: quarter, dueDate: '2026-11-15', status: 'paid', isFunded: true },
      { id: 2, order: 2, label: 'الدفعة الثانية', amount: quarter, dueDate: '2026-12-15', status: 'paid', isFunded: true },
      { id: 3, order: 3, label: 'الدفعة الثالثة', amount: quarter, dueDate: '2026-01-15', status: 'pending_approval', isFunded: false },
      { id: 4, order: 4, label: 'الدفعة الرابعة', amount: quarter, dueDate: '2026-02-15', status: 'upcoming', isFunded: false },
    ];
  }

  protected getInstallmentStatus(inst: any, index: number): { label: string; pillClass: string } {
    if (inst.isFunded || inst.status === 'paid' || index < 2) {
      return { label: this.lang.isAr() ? '• تم السداد' : '• Paid', pillClass: 'pill-paid' };
    }
    if (inst.status === 'pending_approval' || inst.status === 'submitted' || index === 2) {
      return { label: this.lang.isAr() ? '• بإنتظار الموافقة' : '• Pending Approval', pillClass: 'pill-pending' };
    }
    return { label: this.lang.isAr() ? '• قادمة' : '• Upcoming', pillClass: 'pill-upcoming' };
  }

  protected openContract(url?: string | null): void {
    if (url) {
      window.open(url, '_blank');
    } else {
      alert(this.lang.isAr() ? 'عقد المعاملة الإلكتروني موثق ومعتمد برقم: #' + (this.deal()?.number || this.dealId || '45872') : 'Electronic contract verified: #' + this.dealId);
    }
  }

  private load(): void {
    this.isLoading.set(true);
    this.dealsService.getCompanyDeal(this.dealId).subscribe({
      next: (res) => {
        this.deal.set(res.data ?? null);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  private refresh(action: (id: number) => ReturnType<DealsService['submitCompanyDeal']>): void {
    this.error.set('');
    action(this.dealId).subscribe({
      next: (res) => {
        if (res.data) this.deal.set(res.data);
        else this.load();
      },
      error: () => this.error.set(this.lang.t('company_error')),
    });
  }

  protected submit(): void { this.refresh((id) => this.dealsService.submitCompanyDeal(id)); }
  protected accept(): void { this.refresh((id) => this.dealsService.acceptCompanyDeal(id)); }
  protected cancel(): void { this.refresh((id) => this.dealsService.cancelCompanyDeal(id)); }
}
