import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  DealsService,
  DisputeDetail,
} from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * The dispute page for the two parties of a deal: status and reason, the
 * message thread with a send box and file attachments, mutual settlement
 * (propose / accept / reject), and the final decision when the dispute is
 * closed. Admin stays API-only in v1; guests are sent to the login page.
 * A settlement accept or an admin decision executes the money immediately
 * on the server — this page only displays the executed result.
 */
@Component({
  selector: 'app-dispute-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <main class="dispute-page">
      <header class="dispute-header-card">
        <a class="dispute-back" routerLink="/home">→ {{ lang.t('back') }}</a>
        <h1>{{ lang.t('dispute_title') }}</h1>
        @if (dispute()) {
          <p class="dispute-number">{{ dispute()!.number }} · {{ dispute()!.dealTitle }}</p>
        }
      </header>
      <div class="dispute-content">
        @if (isLoading()) {
          <p>{{ lang.t('dispute_loading') }}</p>
        } @else if (!dispute()) {
          <p>{{ lang.t('dispute_not_found') }}</p>
        } @else {
          <section class="dispute-card">
            <p>
              <strong>{{ lang.t('dispute_status') }}:</strong>
              {{ lang.t('dispute_status_' + dispute()!.status) }}
            </p>
            <p>
              <strong>{{ lang.t('dispute_deal') }}:</strong>
              {{ dispute()!.dealTitle }} · {{ dispute()!.dealAmount }} {{ dispute()!.dealCurrency }}
            </p>
            <p>
              <strong>{{ lang.t('dispute_reason') }}:</strong>
              {{ lang.isAr() ? dispute()!.reasonNameAr : dispute()!.reasonNameEn }}
            </p>
            <p>
              <strong>{{ lang.t('dispute_details') }}:</strong>
              {{ dispute()!.details }}
            </p>
            <p>
              <strong>{{ lang.t('dispute_opened_by') }}:</strong>
              {{ dispute()!.openedByName }} · <strong>{{ lang.t('dispute_against') }}:</strong>
              {{ dispute()!.againstName }}
            </p>
            @if (dispute()!.status === 'awaiting_info' && dispute()!.infoDeadlineAt) {
              <p class="dispute-deadline">
                {{ lang.t('dispute_info_deadline') }}: {{ formatDate(dispute()!.infoDeadlineAt) }}
              </p>
            }
            @if (dispute()!.status === 'closed') {
              <p class="dispute-decision">
                <strong>{{ lang.t('dispute_decision') }}:</strong>
                {{ lang.t('dispute_resolution_' + dispute()!.resolution) }}
                @if (dispute()!.resolutionNote) {
                  — {{ dispute()!.resolutionNote }}
                }
              </p>
            }
          </section>

          <section class="dispute-card">
            <h2>{{ lang.t('dispute_messages') }}</h2>
            @if (!dispute()!.messages.length) {
              <p>{{ lang.t('dispute_no_messages') }}</p>
            }
            @for (message of dispute()!.messages; track message.id) {
              <article class="dispute-message">
                <p class="dispute-message-head">
                  <strong>{{ message.senderName ?? lang.t('dispute_sender_' + message.senderType) }}</strong>
                  <span>{{ lang.t('dispute_sender_' + message.senderType) }}</span>
                  <span>{{ formatDate(message.createdAt) }}</span>
                </p>
                @if (message.message) {
                  <p class="dispute-message-body">{{ message.message }}</p>
                }
                @for (attachment of message.attachments; track attachment.id) {
                  <p>
                    <a [href]="attachment.fileUrl ?? undefined" target="_blank" rel="noopener">
                      {{ attachment.fileName }}
                    </a>
                  </p>
                }
              </article>
            }

            @if (dispute()!.availableActions.includes('send_message')) {
              <div class="dispute-send">
                <textarea
                  [(ngModel)]="messageText"
                  rows="3"
                  maxlength="1000"
                  [placeholder]="lang.t('dispute_message_placeholder')"
                ></textarea>
                <label class="dispute-file-label">
                  {{ lang.t('dispute_attach') }}
                  <input type="file" (change)="onMessageFile($event)" />
                </label>
                @if (messageFileName()) {
                  <p>{{ messageFileName() }}</p>
                }
                <button
                  type="button"
                  class="dispute-primary"
                  (click)="sendMessage()"
                  [disabled]="isSending() || (!messageText.trim() && !messageFile)"
                >
                  {{ lang.t('dispute_send') }}
                </button>
              </div>
            }
          </section>

          <section class="dispute-card">
            <h2>{{ lang.t('dispute_attachments') }}</h2>
            @if (!dispute()!.attachments.length) {
              <p>{{ lang.t('dispute_no_attachments') }}</p>
            }
            @for (attachment of dispute()!.attachments; track attachment.id) {
              <p>
                <a [href]="attachment.fileUrl ?? undefined" target="_blank" rel="noopener">
                  {{ attachment.fileName }}
                </a>
                <span> · {{ attachment.uploadedByName ?? '' }}</span>
              </p>
            }
            @if (dispute()!.availableActions.includes('send_message')) {
              <label class="dispute-file-label">
                {{ lang.t('dispute_upload') }}
                <input type="file" (change)="onStandaloneFile($event)" />
              </label>
            }
          </section>

          <section class="dispute-card">
            <h2>{{ lang.t('dispute_settlement') }}</h2>
            @if (dispute()!.openProposal) {
              <p>
                <strong>{{ lang.t('dispute_proposed_by') }}:</strong>
                {{ dispute()!.openProposal!.proposedByName }}
              </p>
              <p>
                <strong>{{ lang.t('dispute_decision') }}:</strong>
                {{ lang.t('dispute_resolution_' + dispute()!.openProposal!.resolution) }}
                @if (dispute()!.openProposal!.sellerPercent !== null) {
                  · {{ dispute()!.openProposal!.sellerPercent }}%
                }
              </p>
              @if (canDecideProposal()) {
                <div class="dispute-actions-row">
                  <button
                    type="button"
                    class="dispute-primary"
                    (click)="respondToProposal('accept')"
                    [disabled]="isProcessing()"
                  >
                    {{ lang.t('dispute_accept') }}
                  </button>
                  <button
                    type="button"
                    class="dispute-secondary"
                    (click)="respondToProposal('reject')"
                    [disabled]="isProcessing()"
                  >
                    {{ lang.t('dispute_reject') }}
                  </button>
                </div>
              }
            } @else if (dispute()!.availableActions.includes('propose_settlement')) {
              <label>{{ lang.t('dispute_propose_resolution') }}</label>
              <select [(ngModel)]="proposalResolution" class="dispute-input">
                <option value="release_seller">{{ lang.t('dispute_resolution_release_seller') }}</option>
                <option value="refund_buyer">{{ lang.t('dispute_resolution_refund_buyer') }}</option>
                <option value="partial">{{ lang.t('dispute_resolution_partial') }}</option>
              </select>
              @if (proposalResolution === 'partial') {
                <label>{{ lang.t('dispute_seller_percent') }}</label>
                <input
                  type="number"
                  [(ngModel)]="proposalPercent"
                  min="1"
                  max="99"
                  class="dispute-input"
                />
              }
              <button
                type="button"
                class="dispute-primary"
                (click)="proposeSettlement()"
                [disabled]="isProcessing()"
              >
                {{ lang.t('dispute_propose') }}
              </button>
            } @else {
              <p>{{ lang.t('dispute_no_proposal') }}</p>
            }
          </section>

          @if (error()) {
            <p class="dispute-error">{{ error() }}</p>
          }
        }
      </div>
    </main>
  `,
  styles: [`
    /* Full-bleed screen like the shop page: the header spans the whole
       width and only the inner content is constrained. Button styles are
       local: .btn/.btn-black live only inside the auth and home
       components' scoped styles. */
    .dispute-page { width: 100%; min-height: 100vh; padding: 0 0 3rem; }
    .dispute-header-card { background: #eb5757; border-radius: 0 0 28px 28px; padding: clamp(1.25rem, 3vw, 2.5rem) clamp(1rem, 5vw, 4.5rem) clamp(1.5rem, 3vw, 2.25rem); color: #fff; }
    .dispute-header-card h1 { margin: 0.5rem 0 0; color: #fff; font-size: clamp(1.8rem, 3vw, 2.6rem); }
    .dispute-back { color: #fff; text-decoration: none; font-weight: 700; }
    .dispute-number { margin: 0.5rem 0 0; opacity: 0.9; }
    .dispute-content { width: min(820px, calc(100% - 2rem)); margin: 1.5rem auto 0; display: flex; flex-direction: column; gap: 1rem; }
    .dispute-card { border: 1px solid #e5e7eb; border-radius: 18px; background: #fff; box-shadow: 0 8px 24px rgba(17, 24, 39, 0.04); padding: 1.25rem; }
    .dispute-card h2 { margin: 0 0 0.75rem; font-size: 1.15rem; }
    .dispute-card p { margin: 0.35rem 0; overflow-wrap: anywhere; }
    .dispute-deadline { color: #b45309; font-weight: 700; }
    .dispute-decision { background: #eef6ee; border-radius: 12px; padding: 0.75rem; }
    .dispute-message { border-top: 1px solid #f3f4f6; padding: 0.75rem 0; }
    .dispute-message-head { display: flex; gap: 0.75rem; flex-wrap: wrap; font-size: 0.85rem; opacity: 0.85; }
    .dispute-message-body { margin: 0.25rem 0; white-space: pre-wrap; }
    .dispute-send { display: flex; flex-direction: column; gap: 0.5rem; margin-top: 1rem; }
    .dispute-send textarea, .dispute-input { width: 100%; border: 1.5px solid #e5e7eb; border-radius: 12px; padding: 0.7rem 1rem; font: inherit; box-sizing: border-box; }
    .dispute-file-label { font-weight: 700; color: #eb5757; cursor: pointer; }
    .dispute-file-label input { display: block; margin-top: 0.25rem; font-weight: 400; color: #111827; }
    .dispute-primary { background: #111827; color: #fff; border: none; border-radius: 12px; padding: 0.7rem 1.25rem; font-weight: 700; cursor: pointer; }
    .dispute-primary:hover { background: #000; }
    .dispute-primary:disabled, .dispute-secondary:disabled { opacity: 0.5; cursor: not-allowed; }
    .dispute-secondary { background: #fff; border: 1.5px solid #eb5757; color: #eb5757; border-radius: 12px; padding: 0.7rem 1.25rem; font-weight: 700; cursor: pointer; }
    .dispute-actions-row { display: flex; gap: 0.75rem; margin-top: 0.75rem; flex-wrap: wrap; }
    .dispute-error { color: #b91c1c; font-weight: 700; }
    @media (min-width: 768px) {
      .dispute-header-card { border-radius: 0 0 36px 36px; }
    }
  `],
})
export class DisputePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  readonly dispute = signal<DisputeDetail | null>(null);
  readonly isLoading = signal(true);
  readonly isSending = signal(false);
  readonly isProcessing = signal(false);
  readonly error = signal<string | null>(null);
  readonly messageFileName = signal<string | null>(null);

  messageText = '';
  messageFile: File | null = null;
  proposalResolution: 'release_seller' | 'refund_buyer' | 'partial' = 'partial';
  proposalPercent: number | null = 60;

  private dealId = 0;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.dealId = Number(this.route.snapshot.paramMap.get('id') ?? 0);
    this.load();
  }

  formatDate(value: string | null): string {
    if (!value) return '';
    return new Date(value).toLocaleString(this.lang.isAr() ? 'ar-SA' : 'en-US');
  }

  canDecideProposal(): boolean {
    const current = this.dispute();
    if (!current?.openProposal) return false;
    return current.openProposal.proposedById !== this.auth.currentUser()?.id;
  }

  onMessageFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.item(0) ?? null;
    this.messageFile = file;
    this.messageFileName.set(file?.name ?? null);
  }

  sendMessage(): void {
    const current = this.dispute();
    if (!current || this.isSending()) return;
    const text = this.messageText.trim();
    if (!text && !this.messageFile) return;
    this.isSending.set(true);
    this.error.set(null);
    const file = this.messageFile;
    if (!file) {
      this.postMessage(text);
      return;
    }
    this.dealsApi.uploadDisputeFile(file).subscribe({
      next: (upload) => {
        if (!upload.success || !upload.data) {
          this.isSending.set(false);
          this.error.set(this.lang.t('dispute_action_failed'));
          return;
        }
        this.postMessage(text, upload.data.fileId);
      },
      error: () => {
        this.isSending.set(false);
        this.error.set(this.lang.t('dispute_action_failed'));
      },
    });
  }

  onStandaloneFile(event: Event): void {
    const current = this.dispute();
    const file = (event.target as HTMLInputElement).files?.item(0) ?? null;
    if (!current || !file) return;
    this.isSending.set(true);
    this.error.set(null);
    this.dealsApi.uploadDisputeFile(file).subscribe({
      next: (upload) => {
        if (!upload.success || !upload.data) {
          this.isSending.set(false);
          this.error.set(this.lang.t('dispute_action_failed'));
          return;
        }
        this.dealsApi.addDisputeAttachment(current.dealId, upload.data.fileId).subscribe({
          next: (response) => {
            this.isSending.set(false);
            if (!response.success) {
              this.error.set(this.lang.t('dispute_action_failed'));
              return;
            }
            this.load();
          },
          error: () => {
            this.isSending.set(false);
            this.error.set(this.lang.t('dispute_action_failed'));
          },
        });
      },
      error: () => {
        this.isSending.set(false);
        this.error.set(this.lang.t('dispute_action_failed'));
      },
    });
  }

  proposeSettlement(): void {
    const current = this.dispute();
    if (!current || this.isProcessing()) return;
    if (this.proposalResolution === 'partial'
      && (this.proposalPercent === null || this.proposalPercent < 1 || this.proposalPercent > 99)) {
      this.error.set(this.lang.t('dispute_action_failed'));
      return;
    }
    this.isProcessing.set(true);
    this.error.set(null);
    this.dealsApi
      .proposeSettlement(
        current.dealId,
        this.proposalResolution,
        this.proposalResolution === 'partial' ? this.proposalPercent ?? undefined : undefined,
      )
      .subscribe({
        next: (response) => {
          this.isProcessing.set(false);
          if (!response.success) {
            this.error.set(this.lang.t('dispute_action_failed'));
            return;
          }
          this.load();
        },
        error: () => {
          this.isProcessing.set(false);
          this.error.set(this.lang.t('dispute_action_failed'));
        },
      });
  }

  respondToProposal(action: 'accept' | 'reject'): void {
    const current = this.dispute();
    if (!current?.openProposal || this.isProcessing()) return;
    this.isProcessing.set(true);
    this.error.set(null);
    const call = action === 'accept'
      ? this.dealsApi.acceptSettlement(current.dealId, current.openProposal.id)
      : this.dealsApi.rejectSettlement(current.dealId, current.openProposal.id);
    call.subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.error.set(this.lang.t('dispute_action_failed'));
          return;
        }
        this.load();
      },
      error: () => {
        this.isProcessing.set(false);
        this.error.set(this.lang.t('dispute_action_failed'));
      },
    });
  }

  private postMessage(text: string, filePublicId?: string): void {
    const current = this.dispute();
    if (!current) return;
    this.dealsApi.sendDisputeMessage(current.dealId, text, filePublicId).subscribe({
      next: (response) => {
        this.isSending.set(false);
        if (!response.success) {
          this.error.set(this.lang.t('dispute_action_failed'));
          return;
        }
        this.messageText = '';
        this.messageFile = null;
        this.messageFileName.set(null);
        this.load();
      },
      error: () => {
        this.isSending.set(false);
        this.error.set(this.lang.t('dispute_action_failed'));
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.dealsApi.getDispute(this.dealId).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) {
          this.dispute.set(null);
          return;
        }
        this.dispute.set(response.data);
      },
      error: () => {
        this.isLoading.set(false);
        this.dispute.set(null);
      },
    });
  }
}
