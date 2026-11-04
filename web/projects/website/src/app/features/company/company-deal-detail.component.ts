import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DealsService, CompanyDealDetail, LookupItem } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * Company deal detail: the deal, its installments with their actions
 * (request approval, approve, reject, fund) and its penalties. Actions are
 * shown from the deal's availableActions and each installment's own list,
 * so the UI never offers an action the API would refuse.
 */
@Component({
  selector: 'app-company-deal-detail',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="company-page">
      <header class="company-header-card">
        <a class="company-back" routerLink="/company-deals">→ {{ lang.t('back') }}</a>
        @if (deal(); as d) {
          <h1>{{ d.title }}</h1>
        }
      </header>
      <div class="company-content">
        @if (isLoading()) {
          <p>{{ lang.t('company_loading') }}</p>
        } @else if (!deal()) {
          <p>{{ lang.t('company_error') }}</p>
        } @else if (deal(); as d) {
          <section class="company-card">
            <p><strong>{{ lang.t('company_status_' + d.status) }}</strong> · {{ d.counterpartyName }}</p>
            <p class="company-total">{{ d.totalAmount }} {{ d.currency }}</p>
            <p>{{ d.details }}</p>
            @if (d.hasContract && d.contractFileUrl) {
              <p><a [href]="d.contractFileUrl" target="_blank" rel="noopener">{{ lang.t('company_contract') }}</a></p>
            }
            @if (d.rejectNote) {
              <p>{{ d.rejectNote }}</p>
            }
            @if (error()) {
              <p class="company-error">{{ error() }}</p>
            }
            <div class="company-actions">
              @if (d.availableActions.includes('edit')) {
                <a class="company-btn" [routerLink]="['/company-deals', d.id, 'edit']">{{ lang.t('company_edit') }}</a>
              }
              @if (d.availableActions.includes('submit')) {
                <button type="button" class="company-btn" (click)="submit()">{{ lang.t('company_submit') }}</button>
              }
              @if (d.availableActions.includes('accept')) {
                <button type="button" class="company-btn" (click)="accept()">{{ lang.t('company_accept') }}</button>
              }
              @if (d.availableActions.includes('reject')) {
                <select #rejectReason>
                  @for (reason of rejectReasons(); track reason.id) {
                    <option [value]="reason.id">{{ reason.name }}</option>
                  }
                </select>
                <button type="button" class="company-btn" (click)="reject(+rejectReason.value)">{{ lang.t('company_reject') }}</button>
              }
              @if (d.availableActions.includes('cancel')) {
                <button type="button" class="company-btn" (click)="cancel()">{{ lang.t('company_cancel') }}</button>
              }
            </div>
          </section>

          <h2>{{ lang.t('company_installments') }}</h2>
          @for (inst of d.installments; track inst.id) {
            <section class="company-card">
              <p><strong>{{ inst.label ?? inst.order }}</strong> · {{ inst.amount }} {{ d.currency }}</p>
              <p>{{ inst.dueDate }} · {{ lang.t('company_inst_' + inst.status) }}</p>
              <p>{{ inst.isFunded ? lang.t('company_funded') : lang.t('company_not_funded') }}</p>
              @if (inst.penaltyAmount) {
                <p>{{ lang.t('company_penalty_applied') }}: {{ inst.penaltyAmount }} ({{ inst.penaltyStatus }})</p>
              }
              @if (inst.rejectionReason) {
                <p>{{ inst.rejectionReason }}</p>
              }
              @if (inst.approvalDeadlineAt) {
                <p>{{ inst.approvalDeadlineAt }}</p>
              }
              <div class="company-actions">
                @if (inst.availableActions.includes('request_approval')) {
                  <button type="button" class="company-btn" (click)="requestApproval(inst.id)">{{ lang.t('company_request_approval') }}</button>
                }
                @if (inst.availableActions.includes('approve')) {
                  <button type="button" class="company-btn" (click)="approve(inst.id)">{{ lang.t('company_approve') }}</button>
                }
                @if (inst.availableActions.includes('reject')) {
                  <input #reasonInput type="text" [placeholder]="lang.t('company_reject_reason')" />
                  <button type="button" class="company-btn" (click)="rejectInstallment(inst.id, reasonInput.value)">{{ lang.t('company_reject_installment') }}</button>
                }
                @if (d.role === 'counterparty' && inst.status !== 'paid' && !inst.isFunded) {
                  <button type="button" class="company-btn" (click)="fund(inst.id)">{{ lang.t('company_fund') }}</button>
                }
                @if (fundSessionUrl()) {
                  <a class="company-btn" [href]="fundSessionUrl()" target="_blank" rel="noopener">{{ fundSessionUrl() }}</a>
                }
              </div>
            </section>
          }

          @if (d.penalties.length) {
            <h2>{{ lang.t('company_penalty') }}</h2>
            @for (penalty of d.penalties; track penalty.id) {
              <section class="company-card">
                <p>{{ penalty.penaltyAmount }} · {{ penalty.status }} · {{ lang.t('company_days_late') }}: {{ penalty.daysLate }}</p>
                @if (penalty.waiverReason) {
                  <p>{{ penalty.waiverReason }}</p>
                }
              </section>
            }
          }
        }
      </div>
    </main>
  `,
  styles: [`
    .company-page { width: 100%; min-height: 100vh; padding: 0 0 3rem; }
    .company-header-card { background: #eb5757; border-radius: 0 0 28px 28px; padding: clamp(1.25rem, 3vw, 2.5rem) clamp(1rem, 5vw, 4.5rem) clamp(1.5rem, 3vw, 2.25rem); color: #fff; }
    .company-header-card h1 { margin: 0.5rem 0 0; color: #fff; font-size: clamp(1.8rem, 3vw, 2.6rem); }
    .company-back { color: #fff; text-decoration: none; font-weight: 700; }
    .company-content { width: min(1280px, calc(100% - 2rem)); margin: 1.5rem auto 0; }
    .company-card { border: 1px solid #e5e7eb; border-radius: 18px; background: #fff; padding: 1rem 1.25rem; margin-bottom: 1rem; box-shadow: 0 8px 24px rgba(17, 24, 39, 0.04); }
    .company-total { font-weight: 800; font-size: 1.1rem; }
    .company-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.75rem; align-items: center; }
    .company-btn { display: inline-block; border: none; background: #eb5757; color: #fff; font-weight: 700; padding: 0.6rem 1.25rem; border-radius: 999px; cursor: pointer; text-decoration: none; }
    .company-error { color: #eb5757; font-weight: 700; }
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
