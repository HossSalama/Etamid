import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  DealsService, CompanyInstallmentInput, CreateCompanyDealRequest, Counterparty,
} from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * Company deal wizard: create a draft or edit one (draft only, creator
 * only — the API enforces both). Installments must add up to the total;
 * the sum is checked live and the save button stays disabled otherwise.
 * The contract file is uploaded with purpose "contract" and its public id
 * is sent with the draft; on edit, leaving it empty keeps the old file.
 * Ordered from a company seller's shop card, the wizard prefills from the
 * router state (counterparty, title, details, total, one installment for
 * the price); the start date and the installment due date stay empty for
 * the buyer to pick, and save stays disabled until they do.
 */
@Component({
  selector: 'app-company-deal-wizard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="company-page">
      <header class="company-header-card">
        <a class="company-back" routerLink="/company-deals">→ {{ lang.t('back') }}</a>
        <h1>{{ lang.t('company_new') }}</h1>
      </header>
      <div class="company-content">
        @if (error()) {
          <p class="company-error">{{ error() }}</p>
        }
        @if (isLoading()) {
          <p>{{ lang.t('company_loading') }}</p>
        } @else {
          <section class="company-card">
            @if (!editId()) {
              <label>{{ lang.t('company_counterparty') }}
                <input type="text" [value]="counterparty()" (input)="counterparty.set($any($event.target).value)" />
              </label>
              <button type="button" class="company-btn" (click)="lookup()">{{ lang.t('company_lookup') }}</button>
              @if (counterpartyPreview(); as preview) {
                <p>{{ preview.name }} ({{ preview.code }})</p>
              }
            }
            <label>{{ lang.t('company_title_field') }}
              <input type="text" [value]="title()" (input)="title.set($any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_details_field') }}
              <input type="text" [value]="details()" (input)="details.set($any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_total_field') }}
              <input type="number" [value]="total()" (input)="total.set(+$any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_start_field') }}
              <input type="date" [value]="startDate()" (input)="startDate.set($any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_contract') }}
              <input type="file" (change)="uploadContract($any($event.target).files?.[0])" />
            </label>
            <label>{{ lang.t('company_approval_send') }}
              <input type="number" [value]="approvalSend() ?? ''" (input)="approvalSend.set($any($event.target).value === '' ? null : +$any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_approval_response') }}
              <input type="number" [value]="approvalResponse() ?? ''" (input)="approvalResponse.set($any($event.target).value === '' ? null : +$any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_approval_notes') }}
              <input type="text" [value]="approvalNotes()" (input)="approvalNotes.set($any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_penalty_percent') }}
              <input type="number" [value]="penaltyPercent() ?? ''" (input)="penaltyPercent.set($any($event.target).value === '' ? null : +$any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_penalty_grace') }}
              <input type="number" [value]="penaltyGrace() ?? ''" (input)="penaltyGrace.set($any($event.target).value === '' ? null : +$any($event.target).value)" />
            </label>
            <label>{{ lang.t('company_penalty_clauses') }}
              <input type="text" [value]="penaltyClauses()" (input)="penaltyClauses.set($any($event.target).value)" />
            </label>

            <h2>{{ lang.t('company_installments') }}</h2>
            @for (inst of installments(); track $index) {
              <div class="company-installment">
                <input type="text" [placeholder]="lang.t('company_installment_label')" [value]="inst.label ?? ''"
                  (input)="updateInstallment($index, 'label', $any($event.target).value)" />
                <input type="number" [placeholder]="lang.t('company_installment_amount')" [value]="inst.amount"
                  (input)="updateInstallment($index, 'amount', +$any($event.target).value)" />
                <input type="date" [placeholder]="lang.t('company_installment_due')" [value]="inst.dueDate"
                  (input)="updateInstallment($index, 'dueDate', $any($event.target).value)" />
                <button type="button" class="company-btn" (click)="removeInstallment($index)">{{ lang.t('company_installment_remove') }}</button>
              </div>
            }
            <button type="button" class="company-btn" (click)="addInstallment()">{{ lang.t('company_installment_add') }}</button>
            @if (!sumsMatch()) {
              <p class="company-error">{{ lang.t('company_sum_error') }}</p>
            }
            <div class="company-actions">
              <button type="button" class="company-btn" [disabled]="!canSave()" (click)="save()">{{ lang.t('company_save_draft') }}</button>
            </div>
          </section>
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
    .company-card { border: 1px solid #e5e7eb; border-radius: 18px; background: #fff; padding: 1rem 1.25rem; box-shadow: 0 8px 24px rgba(17, 24, 39, 0.04); }
    .company-card label { display: flex; flex-direction: column; gap: 0.25rem; margin-bottom: 0.75rem; }
    .company-card input { padding: 0.5rem 0.75rem; border: 1px solid #e5e7eb; border-radius: 10px; }
    .company-installment { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 0.5rem; }
    .company-actions { display: flex; gap: 0.5rem; margin-top: 1rem; }
    .company-btn { border: none; background: #eb5757; color: #fff; font-weight: 700; padding: 0.6rem 1.25rem; border-radius: 999px; cursor: pointer; }
    .company-btn:disabled { opacity: 0.5; cursor: default; }
    .company-error { color: #eb5757; font-weight: 700; }
  `],
})
export class CompanyDealWizardComponent implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly lang = inject(LanguageService);

  protected readonly isLoading = signal(false);
  protected readonly error = signal('');
  protected readonly editId = signal<number | null>(null);
  protected readonly counterparty = signal('');
  protected readonly counterpartyPreview = signal<Counterparty | null>(null);
  protected readonly title = signal('');
  protected readonly details = signal('');
  protected readonly total = signal(0);
  protected readonly startDate = signal('');
  protected readonly contractFileId = signal<string | null>(null);
  protected readonly approvalSend = signal<number | null>(null);
  protected readonly approvalResponse = signal<number | null>(null);
  protected readonly approvalNotes = signal('');
  protected readonly penaltyPercent = signal<number | null>(null);
  protected readonly penaltyGrace = signal<number | null>(null);
  protected readonly penaltyClauses = signal('');
  protected readonly installments = signal<CompanyInstallmentInput[]>([
    { label: null, amount: 0, dueDate: '' },
  ]);

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.editId.set(Number(id));
      this.isLoading.set(true);
      this.dealsService.getCompanyDeal(Number(id)).subscribe({
        next: (res) => {
          const d = res.data;
          if (d) {
            this.title.set(d.title);
            this.details.set(d.details);
            this.total.set(d.totalAmount);
            this.startDate.set(d.startDate);
            this.approvalSend.set(d.approvalSendDaysBefore);
            this.approvalResponse.set(d.approvalResponseDays);
            this.approvalNotes.set(d.approvalNotes ?? '');
            this.penaltyPercent.set(d.penaltyPercent);
            this.penaltyGrace.set(d.penaltyGraceDays);
            this.penaltyClauses.set(d.penaltyClauses ?? '');
            this.installments.set(d.installments.map((i) => ({ label: i.label, amount: i.amount, dueDate: i.dueDate })));
          }
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
    } else {
      // Ordered from a company seller's shop card: prefill the wizard with
      // the product. The company deal details cap is 200 chars, so a longer
      // product description is trimmed, like the individual form does.
      const fromProduct = (history.state as {
        fromProduct?: { sellerCode: string; name: string; description: string | null; price: number };
      }).fromProduct;
      if (fromProduct) {
        this.counterparty.set(fromProduct.sellerCode);
        this.title.set(fromProduct.name);
        this.details.set((fromProduct.description ?? '').slice(0, 200));
        this.total.set(fromProduct.price);
        this.installments.set([{ label: fromProduct.name, amount: fromProduct.price, dueDate: '' }]);
      }
    }
  }

  protected sumsMatch(): boolean {
    const sum = this.installments().reduce((acc, i) => acc + (i.amount || 0), 0);
    return this.installments().length > 0 && Math.round(sum * 100) / 100 === this.total();
  }

  protected canSave(): boolean {
    return !!this.title().trim() && !!this.details().trim() && this.total() > 0 && !!this.startDate()
      && this.installments().every((i) => i.amount > 0 && !!i.dueDate) && this.sumsMatch()
      && (this.editId() !== null || !!this.counterparty().trim());
  }

  protected lookup(): void {
    const q = this.counterparty().trim();
    if (!q) return;
    this.dealsService.lookupSeller(q).subscribe({
      next: (res) => this.counterpartyPreview.set(res.data ?? null),
      error: () => this.counterpartyPreview.set(null),
    });
  }

  protected uploadContract(file: File | undefined): void {
    if (!file) return;
    this.dealsService.uploadContractFile(file).subscribe({
      next: (res) => {
        if (res.data?.fileId) this.contractFileId.set(res.data.fileId);
      },
      error: () => this.error.set(this.lang.t('company_error')),
    });
  }

  protected addInstallment(): void {
    this.installments.set([...this.installments(), { label: null, amount: 0, dueDate: '' }]);
  }

  protected removeInstallment(index: number): void {
    this.installments.set(this.installments().filter((_, i) => i !== index));
  }

  protected updateInstallment(index: number, field: 'label' | 'amount' | 'dueDate', value: string | number): void {
    this.installments.set(this.installments().map((inst, i) => {
      if (i !== index) return inst;
      if (field === 'label') return { ...inst, label: value as string };
      if (field === 'amount') return { ...inst, amount: value as number };
      return { ...inst, dueDate: value as string };
    }));
  }

  protected save(): void {
    this.error.set('');
    const request: CreateCompanyDealRequest = {
      counterpartyPhoneOrCode: this.counterparty().trim(),
      title: this.title().trim(),
      details: this.details().trim(),
      totalAmount: this.total(),
      startDate: this.startDate(),
      contractFilePublicId: this.contractFileId(),
      approvalSendDaysBefore: this.approvalSend(),
      approvalResponseDays: this.approvalResponse(),
      approvalNotes: this.approvalNotes().trim() || null,
      penaltyPercent: this.penaltyPercent(),
      penaltyGraceDays: this.penaltyGrace(),
      penaltyClauses: this.penaltyClauses().trim() || null,
      wizardStep: 0,
      installments: this.installments(),
    };
    const editId = this.editId();
    const call = editId !== null
      ? this.dealsService.updateCompanyDeal(editId, request)
      : this.dealsService.createCompanyDeal(request);
    call.subscribe({
      next: (res) => {
        const id = res.data?.id ?? editId;
        if (id) void this.router.navigate(['/company-deals', id]);
      },
      error: () => this.error.set(this.lang.t('company_error')),
    });
  }
}
