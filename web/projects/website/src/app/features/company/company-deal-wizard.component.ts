import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  DealsService, CompanyInstallmentInput, CreateCompanyDealRequest, Counterparty,
} from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * Company deal wizard: create a draft or edit one.
 */
@Component({
  selector: 'app-company-deal-wizard',
  standalone: true,
  imports: [AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen company-page">
      <app-header
        [title]="editId() ? lang.t('company_edit') : lang.t('company_new')"
        backUrl="/company-deals"
      />

      <main class="app-page-shell">
        @if (error()) {
          <div class="app-card" style="background: #fef2f2; border-color: #fecaca; color: #b91c1c; font-weight: 700;">
            ⚠️ {{ error() }}
          </div>
        }

        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('company_loading') }}</p>
          </div>
        } @else {
          <form class="app-card wizard-card" (submit)="$event.preventDefault()">
            <!-- Counterparty -->
            @if (!editId()) {
              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_counterparty') }}</label>
                <div class="wizard-lookup-row">
                  <input
                    type="text"
                    class="wizard-input"
                    [placeholder]="lang.t('deal_counterparty_placeholder')"
                    [value]="counterparty()"
                    (input)="counterparty.set($any($event.target).value)"
                  />
                  <button type="button" class="btn btn-outline-coral" (click)="lookup()">
                    {{ lang.t('company_lookup') }}
                  </button>
                </div>
                @if (counterpartyPreview(); as preview) {
                  <div class="badge badge-success" style="margin-top: 0.5rem; width: fit-content; padding: 0.4rem 0.8rem;">
                    ✓ {{ preview.name }} ({{ preview.code }})
                  </div>
                }
              </div>
            }

            <!-- Deal Title & Details -->
            <div class="wizard-field">
              <label class="wizard-label">{{ lang.t('company_title_field') }}</label>
              <input
                type="text"
                class="wizard-input"
                [value]="title()"
                (input)="title.set($any($event.target).value)"
                maxlength="150"
              />
            </div>

            <div class="wizard-field">
              <label class="wizard-label">{{ lang.t('company_details_field') }}</label>
              <textarea
                class="wizard-textarea"
                rows="3"
                [value]="details()"
                (input)="details.set($any($event.target).value)"
                maxlength="200"
              ></textarea>
            </div>

            <!-- Amount & Start Date -->
            <div class="wizard-grid-two">
              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_total_field') }} ({{ lang.t('home_sar') }})</label>
                <input
                  type="number"
                  class="wizard-input"
                  dir="ltr"
                  [value]="total()"
                  (input)="total.set(+$any($event.target).value)"
                />
              </div>

              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_start_field') }}</label>
                <input
                  type="date"
                  class="wizard-input"
                  [value]="startDate()"
                  (input)="startDate.set($any($event.target).value)"
                />
              </div>
            </div>

            <!-- Contract File -->
            <div class="wizard-field">
              <label class="wizard-label">{{ lang.t('company_contract') }}</label>
              <input
                type="file"
                class="wizard-input"
                accept=".pdf,image/png,image/jpeg"
                (change)="uploadContract($any($event.target).files?.[0])"
              />
              @if (contractFileId()) {
                <span class="badge badge-success" style="margin-top: 0.35rem; width: fit-content;">✓ {{ lang.t('addresses_saved') }}</span>
              }
            </div>

            <!-- Approval Settings -->
            <h3 class="wizard-subtitle">{{ lang.t('company_approval_notes') }}</h3>
            <div class="wizard-grid-two">
              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_approval_send') }}</label>
                <input
                  type="number"
                  class="wizard-input"
                  [value]="approvalSend() ?? ''"
                  (input)="approvalSend.set($any($event.target).value === '' ? null : +$any($event.target).value)"
                />
              </div>
              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_approval_response') }}</label>
                <input
                  type="number"
                  class="wizard-input"
                  [value]="approvalResponse() ?? ''"
                  (input)="approvalResponse.set($any($event.target).value === '' ? null : +$any($event.target).value)"
                />
              </div>
            </div>

            <div class="wizard-field">
              <label class="wizard-label">{{ lang.t('company_approval_notes') }}</label>
              <input
                type="text"
                class="wizard-input"
                [value]="approvalNotes()"
                (input)="approvalNotes.set($any($event.target).value)"
              />
            </div>

            <!-- Penalties -->
            <h3 class="wizard-subtitle">{{ lang.t('company_penalty_clauses') }}</h3>
            <div class="wizard-grid-two">
              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_penalty_percent') }} (%)</label>
                <input
                  type="number"
                  class="wizard-input"
                  [value]="penaltyPercent() ?? ''"
                  (input)="penaltyPercent.set($any($event.target).value === '' ? null : +$any($event.target).value)"
                />
              </div>
              <div class="wizard-field">
                <label class="wizard-label">{{ lang.t('company_penalty_grace') }}</label>
                <input
                  type="number"
                  class="wizard-input"
                  [value]="penaltyGrace() ?? ''"
                  (input)="penaltyGrace.set($any($event.target).value === '' ? null : +$any($event.target).value)"
                />
              </div>
            </div>

            <div class="wizard-field">
              <label class="wizard-label">{{ lang.t('company_penalty_clauses') }}</label>
              <input
                type="text"
                class="wizard-input"
                [value]="penaltyClauses()"
                (input)="penaltyClauses.set($any($event.target).value)"
              />
            </div>

            <!-- Installments -->
            <div class="wizard-installments-section">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem;">
                <h3 class="wizard-subtitle" style="margin: 0;">{{ lang.t('company_installments') }}</h3>
                <button type="button" class="btn btn-sm btn-outline-coral" (click)="addInstallment()">
                  + {{ lang.t('company_installment_add') }}
                </button>
              </div>

              @for (inst of installments(); track $index) {
                <div class="installment-row">
                  <input
                    type="text"
                    class="wizard-input"
                    [placeholder]="lang.t('company_installment_label')"
                    [value]="inst.label ?? ''"
                    (input)="updateInstallment($index, 'label', $any($event.target).value)"
                  />
                  <input
                    type="number"
                    class="wizard-input"
                    dir="ltr"
                    [placeholder]="lang.t('company_installment_amount')"
                    [value]="inst.amount"
                    (input)="updateInstallment($index, 'amount', +$any($event.target).value)"
                  />
                  <input
                    type="date"
                    class="wizard-input"
                    [placeholder]="lang.t('company_installment_due')"
                    [value]="inst.dueDate"
                    (input)="updateInstallment($index, 'dueDate', $any($event.target).value)"
                  />
                  @if (installments().length > 1) {
                    <button
                      type="button"
                      class="btn btn-sm btn-outline-coral"
                      style="color: #b91c1c !important; border-color: #fecaca;"
                      (click)="removeInstallment($index)"
                      [title]="lang.t('company_installment_remove')"
                    >
                      ✕
                    </button>
                  }
                </div>
              }

              @if (!sumsMatch()) {
                <div class="badge badge-danger" style="margin-top: 0.5rem; padding: 0.4rem 0.8rem;">
                  ⚠️ {{ lang.t('company_sum_error') }}
                </div>
              }
            </div>

            <div class="wizard-submit-row">
              <button
                type="button"
                class="btn btn-black btn-lg"
                style="width: 100%;"
                [disabled]="!canSave()"
                (click)="save()"
              >
                {{ lang.t('company_save_draft') }}
              </button>
            </div>
          </form>
        }
      </main>

      <app-bottom-nav activeNav="company" />
    </div>
  `,
  styles: [`
    .wizard-card {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      max-width: 840px;
      margin-inline: auto;
      width: 100%;
    }
    .wizard-field {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }
    .wizard-label {
      font-size: 0.88rem;
      font-weight: 750;
      color: #374151;
    }
    .wizard-input, .wizard-textarea {
      width: 100%;
      min-height: 46px;
      border: 1px solid #d1d5db;
      border-radius: 12px;
      padding: 0.7rem 0.95rem;
      font-family: inherit;
      font-size: 0.95rem;
      outline: none;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
      background: #fff;
      &:focus {
        border-color: #eb5757;
        box-shadow: 0 0 0 3px rgba(235, 87, 87, 0.14);
      }
    }
    .wizard-textarea {
      min-height: 90px;
      resize: vertical;
    }
    .wizard-lookup-row {
      display: flex;
      gap: 0.5rem;
    }
    .wizard-grid-two {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 1rem;
    }
    .wizard-subtitle {
      font-size: 1.1rem;
      font-weight: 850;
      color: #111827;
      margin: 0.75rem 0 0.25rem;
      padding-bottom: 0.4rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .wizard-installments-section {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 14px;
      padding: 1.25rem;
    }
    .installment-row {
      display: grid;
      grid-template-columns: minmax(140px, 1.5fr) minmax(100px, 1fr) minmax(140px, 1fr) auto;
      gap: 0.5rem;
      align-items: center;
      margin-bottom: 0.65rem;
    }
    .wizard-submit-row {
      margin-top: 1rem;
      padding-top: 1rem;
      border-top: 1px solid #f1f5f9;
    }
    @media (max-width: 600px) {
      .installment-row {
        grid-template-columns: 1fr;
      }
    }
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
