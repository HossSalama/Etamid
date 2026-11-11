import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DealsService, CompanyDealDetail, LookupItem } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * Company deal detail: the deal, its installments with their actions.
 */
@Component({
  selector: 'app-company-deal-detail',
  standalone: true,
  imports: [RouterLink, AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen company-page">
      <app-header
        [title]="deal()?.title || lang.t('company_title')"
        [badge]="deal() ? lang.t('company_status_' + deal()!.status) : ''"
        [badgeType]="deal() ? getStatusBadgeType(deal()!.status) : 'neutral'"
        backUrl="/company-deals"
      />

      <main class="app-page-shell">
        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('company_loading') }}</p>
          </div>
        } @else if (!deal()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p class="company-error">{{ lang.t('company_error') }}</p>
            <a routerLink="/company-deals" class="btn btn-outline-coral" style="margin-top: 1rem;">
              {{ lang.t('back') }}
            </a>
          </div>
        } @else if (deal(); as d) {
          <!-- Deal Overview Card -->
          <section class="app-card detail-main-card">
            <div class="detail-top-row">
              <div>
                <span class="detail-party-label">{{ d.counterpartyName }}</span>
                <div class="company-amount-block">
                  <span class="company-total">{{ d.totalAmount.toLocaleString() }}</span>
                  <span class="company-currency">{{ d.currency }}</span>
                </div>
              </div>
              <span class="badge" [class]="'badge-' + getStatusBadgeType(d.status)">
                {{ lang.t('company_status_' + d.status) }}
              </span>
            </div>

            <p class="detail-description">{{ d.details }}</p>

            @if (d.hasContract && d.contractFileUrl) {
              <div class="contract-box">
                <span>📄 {{ lang.t('company_contract') }}</span>
                <a [href]="d.contractFileUrl" target="_blank" rel="noopener" class="btn btn-sm btn-outline-coral">
                  {{ lang.t('company_contract') }} ↗
                </a>
              </div>
            }

            @if (d.rejectNote) {
              <div class="badge badge-danger" style="margin-top: 0.5rem; width: 100%; padding: 0.6rem 0.8rem; justify-content: flex-start;">
                ⚠️ {{ d.rejectNote }}
              </div>
            }

            @if (error()) {
              <div class="badge badge-danger" style="margin-top: 0.5rem; width: 100%; padding: 0.6rem 0.8rem; justify-content: flex-start;">
                ⚠️ {{ error() }}
              </div>
            }

            <!-- Main Actions -->
            <div class="company-actions">
              @if (d.availableActions.includes('edit')) {
                <a class="btn btn-outline-coral" [routerLink]="['/company-deals', d.id, 'edit']">
                  {{ lang.t('company_edit') }}
                </a>
              }
              @if (d.availableActions.includes('submit')) {
                <button type="button" class="btn btn-black" (click)="submit()">
                  {{ lang.t('company_submit') }}
                </button>
              }
              @if (d.availableActions.includes('accept')) {
                <button type="button" class="btn btn-black" (click)="accept()">
                  {{ lang.t('company_accept') }}
                </button>
              }
              @if (d.availableActions.includes('reject')) {
                <div class="reject-row">
                  <select #rejectReason class="form-select">
                    @for (reason of rejectReasons(); track reason.id) {
                      <option [value]="reason.id">{{ reason.name }}</option>
                    }
                  </select>
                  <button type="button" class="btn btn-outline-coral" style="color: #b91c1c !important; border-color: #fecaca;" (click)="reject(+rejectReason.value)">
                    {{ lang.t('company_reject') }}
                  </button>
                </div>
              }
              @if (d.availableActions.includes('cancel')) {
                <button type="button" class="btn btn-outline-coral" (click)="cancel()">
                  {{ lang.t('company_cancel') }}
                </button>
              }
            </div>
          </section>

          <!-- Installments Section -->
          <h2 class="section-heading">{{ lang.t('company_installments') }}</h2>
          <div class="installments-list">
            @for (inst of d.installments; track inst.id) {
              <section class="app-card installment-card">
                <div class="installment-header">
                  <div>
                    <strong class="installment-title">{{ inst.label ?? (lang.isAr() ? 'الدفعة ' + inst.order : 'Installment ' + inst.order) }}</strong>
                    <span class="installment-due">📅 {{ inst.dueDate }}</span>
                  </div>
                  <div class="installment-price-block">
                    <span class="installment-amount">{{ inst.amount.toLocaleString() }}</span>
                    <span class="company-currency">{{ d.currency }}</span>
                  </div>
                </div>

                <div class="installment-status-row">
                  <span class="badge" [class]="inst.isFunded ? 'badge-success' : 'badge-neutral'">
                    {{ inst.isFunded ? lang.t('company_funded') : lang.t('company_not_funded') }}
                  </span>
                  <span class="badge badge-info">
                    {{ lang.t('company_inst_' + inst.status) }}
                  </span>
                </div>

                @if (inst.penaltyAmount) {
                  <p class="penalty-text">⚠️ {{ lang.t('company_penalty_applied') }}: {{ inst.penaltyAmount }} ({{ inst.penaltyStatus }})</p>
                }
                @if (inst.rejectionReason) {
                  <p class="penalty-text">⚠️ {{ inst.rejectionReason }}</p>
                }
                @if (inst.approvalDeadlineAt) {
                  <p class="deadline-text">⏳ {{ inst.approvalDeadlineAt }}</p>
                }

                <div class="company-actions">
                  @if (inst.availableActions.includes('request_approval')) {
                    <button type="button" class="btn btn-sm btn-black" (click)="requestApproval(inst.id)">
                      {{ lang.t('company_request_approval') }}
                    </button>
                  }
                  @if (inst.availableActions.includes('approve')) {
                    <button type="button" class="btn btn-sm btn-coral" (click)="approve(inst.id)">
                      {{ lang.t('company_approve') }}
                    </button>
                  }
                  @if (inst.availableActions.includes('reject')) {
                    <div class="reject-installment-box">
                      <input #reasonInput type="text" class="form-input" style="min-height: 38px; font-size: 0.85rem;" [placeholder]="lang.t('company_reject_reason')" />
                      <button type="button" class="btn btn-sm btn-outline-coral" (click)="rejectInstallment(inst.id, reasonInput.value)">
                        {{ lang.t('company_reject_installment') }}
                      </button>
                    </div>
                  }
                  @if (d.role === 'counterparty' && inst.status !== 'paid' && !inst.isFunded) {
                    <button type="button" class="btn btn-black" (click)="fund(inst.id)">
                      {{ lang.t('company_fund') }}
                    </button>
                  }
                  @if (fundSessionUrl()) {
                    <a class="btn btn-coral" [href]="fundSessionUrl()" target="_blank" rel="noopener">
                      {{ lang.t('company_fund') }} ↗
                    </a>
                  }
                </div>
              </section>
            }
          </div>

          <!-- Penalties Section -->
          @if (d.penalties.length) {
            <h2 class="section-heading">{{ lang.t('company_penalty') }}</h2>
            @for (penalty of d.penalties; track penalty.id) {
              <section class="app-card penalty-card">
                <p><strong>{{ penalty.penaltyAmount }} {{ d.currency }}</strong> · {{ penalty.status }} · {{ lang.t('company_days_late') }}: {{ penalty.daysLate }}</p>
                @if (penalty.waiverReason) {
                  <p class="penalty-reason">{{ penalty.waiverReason }}</p>
                }
              </section>
            }
          }
        }
      </main>

      <app-bottom-nav activeNav="company" />
    </div>
  `,
  styles: [`
    .detail-main-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .detail-top-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }
    .detail-party-label {
      color: #6b7280;
      font-size: 0.88rem;
      font-weight: 700;
      display: block;
      margin-bottom: 0.2rem;
    }
    .company-amount-block {
      display: flex;
      align-items: baseline;
      gap: 0.35rem;
    }
    .company-total {
      font-size: 1.6rem;
      font-weight: 850;
      color: #111827;
    }
    .company-currency {
      color: #6b7280;
      font-size: 0.85rem;
      font-weight: 700;
    }
    .detail-description {
      color: #374151;
      font-size: 0.95rem;
      line-height: 1.6;
      margin: 0;
    }
    .contract-box {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      padding: 0.75rem 1rem;
      font-weight: 700;
    }
    .company-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      align-items: center;
      margin-top: 0.5rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;
    }
    .reject-row {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .section-heading {
      font-size: 1.25rem;
      font-weight: 850;
      color: #111827;
      margin: 0.5rem 0 0;
    }
    .installments-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .installment-card {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .installment-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }
    .installment-title {
      font-size: 1.05rem;
      color: #111827;
      display: block;
    }
    .installment-due {
      color: #9ca3af;
      font-size: 0.8rem;
    }
    .installment-price-block {
      display: flex;
      align-items: baseline;
      gap: 0.25rem;
    }
    .installment-amount {
      font-size: 1.25rem;
      font-weight: 850;
      color: #111827;
    }
    .installment-status-row {
      display: flex;
      gap: 0.5rem;
    }
    .reject-installment-box {
      display: flex;
      gap: 0.4rem;
      align-items: center;
      width: 100%;
    }
    .penalty-text {
      color: #b91c1c;
      font-size: 0.85rem;
      font-weight: 700;
      margin: 0;
    }
    .deadline-text {
      color: #6b7280;
      font-size: 0.82rem;
      margin: 0;
    }
    .penalty-card {
      font-size: 0.9rem;
      color: #374151;
    }
    .penalty-reason {
      color: #6b7280;
      font-size: 0.82rem;
      margin-top: 0.25rem;
    }
    .form-select, .form-input {
      padding: 0.5rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 10px;
      font-family: inherit;
      outline: none;
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

  private dealId = 0;

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

  protected getStatusBadgeType(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    switch (status) {
      case 'active':
      case 'completed':
        return 'success';
      case 'pending':
        return 'warning';
      case 'rejected':
      case 'cancelled':
        return 'danger';
      default:
        return 'neutral';
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

  protected reject(reasonId: number): void {
    this.refresh((id) => this.dealsService.rejectCompanyDeal(id, reasonId));
  }

  protected requestApproval(installmentId: number): void {
    this.refresh((id) => this.dealsService.requestInstallmentApproval(id, installmentId));
  }

  protected approve(installmentId: number): void {
    this.refresh((id) => this.dealsService.approveInstallment(id, installmentId));
  }

  protected rejectInstallment(installmentId: number, reason: string): void {
    if (!reason.trim()) {
      this.error.set(this.lang.t('company_error'));
      return;
    }
    this.refresh((id) => this.dealsService.rejectInstallment(id, installmentId, reason.trim()));
  }

  protected fund(installmentId: number): void {
    this.error.set('');
    this.fundSessionUrl.set('');
    this.dealsService.fundInstallment(this.dealId, installmentId).subscribe({
      next: (res) => {
        if (res.data?.redirectUrl) this.fundSessionUrl.set(res.data.redirectUrl);
        this.load();
      },
      error: () => this.error.set(this.lang.t('company_error')),
    });
  }
}
