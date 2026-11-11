import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  DealsService,
  DisputeDetail,
} from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * The dispute page for the two parties of a deal.
 */
@Component({
  selector: 'app-dispute-page',
  standalone: true,
  imports: [FormsModule, RouterLink, AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen dispute-page">
      <app-header
        [title]="lang.t('dispute_title')"
        [subtitle]="dispute() ? (dispute()!.number + ' · ' + dispute()!.dealTitle) : ''"
        [badge]="dispute() ? lang.t('dispute_status_' + dispute()!.status) : ''"
        [badgeType]="dispute()?.status === 'closed' ? 'success' : 'warning'"
        backUrl="/home?tab=deals"
      />

      <main class="app-page-shell" style="max-width: 840px;">
        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('dispute_loading') }}</p>
          </div>
        } @else if (!dispute()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('dispute_not_found') }}</p>
            <a routerLink="/home" class="btn btn-outline-coral" style="margin-top: 1rem;">{{ lang.t('back') }}</a>
          </div>
        } @else {
          <!-- Dispute Overview Card -->
          <section class="app-card dispute-card">
            <div class="dispute-meta-grid">
              <div class="meta-item">
                <span class="meta-label">{{ lang.t('dispute_deal') }}</span>
                <strong class="meta-value">{{ dispute()!.dealTitle }} · {{ dispute()!.dealAmount }} {{ dispute()!.dealCurrency }}</strong>
              </div>
              <div class="meta-item">
                <span class="meta-label">{{ lang.t('dispute_reason') }}</span>
                <strong class="meta-value">{{ lang.isAr() ? dispute()!.reasonNameAr : dispute()!.reasonNameEn }}</strong>
              </div>
              <div class="meta-item">
                <span class="meta-label">{{ lang.t('dispute_opened_by') }}</span>
                <strong class="meta-value">{{ dispute()!.openedByName }}</strong>
              </div>
              <div class="meta-item">
                <span class="meta-label">{{ lang.t('dispute_against') }}</span>
                <strong class="meta-value">{{ dispute()!.againstName }}</strong>
              </div>
            </div>

            <div class="dispute-details-box">
              <span class="meta-label">{{ lang.t('dispute_details') }}:</span>
              <p class="dispute-text">{{ dispute()!.details }}</p>
            </div>

            @if (dispute()!.status === 'awaiting_info' && dispute()!.infoDeadlineAt) {
              <div class="badge badge-warning" style="padding: 0.5rem 0.8rem; font-size: 0.85rem; width: fit-content;">
                ⏳ {{ lang.t('dispute_info_deadline') }}: {{ formatDate(dispute()!.infoDeadlineAt) }}
              </div>
            }

            @if (dispute()!.status === 'closed') {
              <div class="dispute-decision-box">
                <strong>⚖️ {{ lang.t('dispute_decision') }}:</strong>
                <span>{{ lang.t('dispute_resolution_' + dispute()!.resolution) }}</span>
                @if (dispute()!.resolutionNote) {
                  <p class="decision-note">{{ dispute()!.resolutionNote }}</p>
                }
              </div>
            }
          </section>

          <!-- Messages Thread -->
          <section class="app-card dispute-card">
            <h2 class="card-title">💬 {{ lang.t('dispute_messages') }}</h2>
            @if (!dispute()!.messages.length) {
              <p style="color: #6b7280; font-size: 0.9rem;">{{ lang.t('dispute_no_messages') }}</p>
            }
            <div class="messages-list">
              @for (message of dispute()!.messages; track message.id) {
                <article class="dispute-message-item">
                  <div class="dispute-message-head">
                    <strong class="sender-name">{{ message.senderName ?? lang.t('dispute_sender_' + message.senderType) }}</strong>
                    <span class="badge badge-neutral">{{ lang.t('dispute_sender_' + message.senderType) }}</span>
                    <small class="message-time">🕒 {{ formatDate(message.createdAt) }}</small>
                  </div>
                  @if (message.message) {
                    <p class="dispute-message-body">{{ message.message }}</p>
                  }
                  @for (attachment of message.attachments; track attachment.id) {
                    <div class="attachment-chip">
                      📎
                      <a [href]="attachment.fileUrl ?? undefined" target="_blank" rel="noopener">
                        {{ attachment.fileName }} ↗
                      </a>
                    </div>
                  }
                </article>
              }
            </div>

            @if (dispute()!.availableActions.includes('send_message')) {
              <div class="dispute-send-box">
                <textarea
                  [(ngModel)]="messageText"
                  rows="3"
                  maxlength="1000"
                  class="dispute-textarea"
                  [placeholder]="lang.t('dispute_message_placeholder')"
                ></textarea>
                <div class="dispute-send-actions">
                  <label class="btn btn-sm btn-outline-gray file-attach-label">
                    📎 {{ lang.t('dispute_attach') }}
                    <input type="file" class="visually-hidden" (change)="onMessageFile($event)" />
                  </label>
                  @if (messageFileName()) {
                    <span class="badge badge-neutral">{{ messageFileName() }}</span>
                  }
                  <button
                    type="button"
                    class="btn btn-black"
                    (click)="sendMessage()"
                    [disabled]="isSending() || (!messageText.trim() && !messageFile)"
                  >
                    {{ lang.t('dispute_send') }}
                  </button>
                </div>
              </div>
            }
          </section>

          <!-- Attachments -->
          <section class="app-card dispute-card">
            <h2 class="card-title">📁 {{ lang.t('dispute_attachments') }}</h2>
            @if (!dispute()!.attachments.length) {
              <p style="color: #6b7280; font-size: 0.9rem;">{{ lang.t('dispute_no_attachments') }}</p>
            }
            <div class="attachments-list">
              @for (attachment of dispute()!.attachments; track attachment.id) {
                <div class="attachment-item">
                  <span>📄</span>
                  <a [href]="attachment.fileUrl ?? undefined" target="_blank" rel="noopener" class="attachment-link">
                    {{ attachment.fileName }} ↗
                  </a>
                  <small class="attachment-uploader">({{ attachment.uploadedByName ?? '' }})</small>
                </div>
              }
            </div>
            @if (dispute()!.availableActions.includes('send_message')) {
              <label class="btn btn-sm btn-outline-coral file-attach-label" style="margin-top: 0.75rem; width: fit-content;">
                + {{ lang.t('dispute_upload') }}
                <input type="file" class="visually-hidden" (change)="onStandaloneFile($event)" />
              </label>
            }
          </section>

          <!-- Settlement -->
          <section class="app-card dispute-card">
            <h2 class="card-title">🤝 {{ lang.t('dispute_settlement') }}</h2>
            @if (dispute()!.openProposal) {
              <div class="proposal-box">
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
                      class="btn btn-coral"
                      (click)="respondToProposal('accept')"
                      [disabled]="isProcessing()"
                    >
                      {{ lang.t('dispute_accept') }}
                    </button>
                    <button
                      type="button"
                      class="btn btn-outline-coral"
                      style="color: #b91c1c !important; border-color: #fecaca;"
                      (click)="respondToProposal('reject')"
                      [disabled]="isProcessing()"
                    >
                      {{ lang.t('dispute_reject') }}
                    </button>
                  </div>
                }
              </div>
            } @else if (dispute()!.availableActions.includes('propose_settlement')) {
              <div class="propose-form">
                <label class="form-label">{{ lang.t('dispute_propose_resolution') }}</label>
                <select [(ngModel)]="proposalResolution" class="form-select">
                  <option value="release_seller">{{ lang.t('dispute_resolution_release_seller') }}</option>
                  <option value="refund_buyer">{{ lang.t('dispute_resolution_refund_buyer') }}</option>
                  <option value="partial">{{ lang.t('dispute_resolution_partial') }}</option>
                </select>
                @if (proposalResolution === 'partial') {
                  <label class="form-label" style="margin-top: 0.5rem;">{{ lang.t('dispute_seller_percent') }} (%)</label>
                  <input
                    type="number"
                    [(ngModel)]="proposalPercent"
                    min="1"
                    max="99"
                    class="form-input"
                  />
                }
                <button
                  type="button"
                  class="btn btn-black"
                  style="margin-top: 0.75rem;"
                  (click)="proposeSettlement()"
                  [disabled]="isProcessing()"
                >
                  {{ lang.t('dispute_propose') }}
                </button>
              </div>
            } @else {
              <p style="color: #6b7280; font-size: 0.9rem;">{{ lang.t('dispute_no_proposal') }}</p>
            }
          </section>

          @if (error()) {
            <div class="badge badge-danger" style="width: 100%; padding: 0.6rem 0.8rem;">
              ⚠️ {{ error() }}
            </div>
          }
        }
      </main>

      <app-bottom-nav activeNav="deals" />
    </div>
  `,
  styles: [`
    .dispute-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .card-title {
      font-size: 1.15rem;
      font-weight: 850;
      color: #111827;
      margin: 0;
    }
    .dispute-meta-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 0.75rem;
    }
    .meta-item {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }
    .meta-label {
      font-size: 0.82rem;
      color: #6b7280;
      font-weight: 600;
    }
    .meta-value {
      font-size: 0.95rem;
      color: #111827;
    }
    .dispute-details-box {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      padding: 0.85rem 1rem;
    }
    .dispute-text {
      color: #374151;
      font-size: 0.9rem;
      line-height: 1.5;
      margin: 0.25rem 0 0;
    }
    .dispute-decision-box {
      background: #ecfdf5;
      border: 1px solid #a7f3d0;
      color: #065f46;
      border-radius: 12px;
      padding: 0.85rem 1rem;
      font-size: 0.92rem;
    }
    .decision-note {
      margin: 0.35rem 0 0;
      font-size: 0.85rem;
    }
    .messages-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .dispute-message-item {
      border: 1px solid #f1f5f9;
      background: #fafafa;
      border-radius: 12px;
      padding: 0.85rem 1rem;
    }
    .dispute-message-head {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.35rem;
      flex-wrap: wrap;
    }
    .sender-name {
      font-size: 0.9rem;
      color: #111827;
    }
    .message-time {
      color: #9ca3af;
      font-size: 0.76rem;
      margin-inline-start: auto;
    }
    .dispute-message-body {
      color: #374151;
      font-size: 0.9rem;
      line-height: 1.5;
      margin: 0;
      white-space: pre-wrap;
    }
    .attachment-chip {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      background: #fff;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 0.25rem 0.6rem;
      font-size: 0.8rem;
      margin-top: 0.5rem;
      color: #eb5757;
      a { color: inherit; font-weight: 700; }
    }
    .dispute-send-box {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-top: 0.5rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;
    }
    .dispute-textarea {
      width: 100%;
      border: 1px solid #d1d5db;
      border-radius: 12px;
      padding: 0.7rem 0.95rem;
      font-family: inherit;
      outline: none;
      resize: vertical;
      &:focus {
        border-color: #eb5757;
        box-shadow: 0 0 0 3px rgba(235, 87, 87, 0.14);
      }
    }
    .dispute-send-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      justify-content: flex-end;
    }
    .file-attach-label {
      cursor: pointer;
      position: relative;
    }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      border: 0;
    }
    .attachments-list {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .attachment-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.9rem;
    }
    .attachment-link {
      color: #eb5757;
      font-weight: 700;
      &:hover { text-decoration: underline; }
    }
    .attachment-uploader {
      color: #9ca3af;
      font-size: 0.8rem;
    }
    .proposal-box {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      padding: 1rem;
    }
    .dispute-actions-row {
      display: flex;
      gap: 0.65rem;
      margin-top: 0.75rem;
    }
    .form-select, .form-input {
      width: 100%;
      padding: 0.65rem 0.85rem;
      border: 1px solid #d1d5db;
      border-radius: 10px;
      font-family: inherit;
      outline: none;
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
  proposalResolution: 'release_seller' | 'refund_buyer' | 'partial' = 'release_seller';
  proposalPercent: number | null = null;

  private dealId = 0;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isFinite(id) || id <= 0) {
      void this.router.navigate(['/home']);
      return;
    }
    this.dealId = id;
    this.load();
  }

  load(): void {
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

  onMessageFile(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0] ?? null;
    this.messageFile = file;
    this.messageFileName.set(file?.name ?? null);
  }

  onStandaloneFile(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0] ?? null;
    if (!file) return;
    this.dealsApi.uploadDisputeFile(file).subscribe({
      next: (uploadRes) => {
        const fileId = uploadRes.data?.fileId;
        if (!fileId) return;
        this.dealsApi.addDisputeAttachment(this.dealId, fileId).subscribe({
          next: () => this.load(),
          error: (err: unknown) => this.error.set(this.errorKey(err)),
        });
      },
      error: (err: unknown) => this.error.set(this.errorKey(err)),
    });
  }

  sendMessage(): void {
    const text = this.messageText.trim();
    if (!text && !this.messageFile) return;
    this.isSending.set(true);
    this.error.set(null);

    const sendWithFileId = (fileId: string | null) => {
      this.dealsApi.sendDisputeMessage(this.dealId, text, fileId ?? undefined).subscribe({
        next: (response) => {
          this.isSending.set(false);
          if (!response.success) {
            this.error.set(this.errorKey(response));
            return;
          }
          this.messageText = '';
          this.messageFile = null;
          this.messageFileName.set(null);
          this.load();
        },
        error: (err: unknown) => {
          this.isSending.set(false);
          this.error.set(this.errorKey(err));
        },
      });
    };

    if (this.messageFile) {
      this.dealsApi.uploadDisputeFile(this.messageFile).subscribe({
        next: (uploadRes) => sendWithFileId(uploadRes.data?.fileId ?? null),
        error: (err: unknown) => {
          this.isSending.set(false);
          this.error.set(this.errorKey(err));
        },
      });
    } else {
      sendWithFileId(null);
    }
  }

  canDecideProposal(): boolean {
    const proposal = this.dispute()?.openProposal;
    if (!proposal) return false;
    const myId = this.auth.currentUser()?.id;
    return myId !== undefined && proposal.proposedById !== myId;
  }

  respondToProposal(decision: 'accept' | 'reject'): void {
    const proposal = this.dispute()?.openProposal;
    if (!proposal) return;
    this.isProcessing.set(true);
    this.error.set(null);
    const call = decision === 'accept'
      ? this.dealsApi.acceptSettlement(this.dealId, proposal.id)
      : this.dealsApi.rejectSettlement(this.dealId, proposal.id);

    call.subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.error.set(this.errorKey(response));
          return;
        }
        this.load();
      },
      error: (err: unknown) => {
        this.isProcessing.set(false);
        this.error.set(this.errorKey(err));
      },
    });
  }

  proposeSettlement(): void {
    this.isProcessing.set(true);
    this.error.set(null);
    const percent = this.proposalResolution === 'partial' ? (this.proposalPercent ?? undefined) : undefined;
    this.dealsApi.proposeSettlement(this.dealId, this.proposalResolution, percent).subscribe({
      next: (response) => {
        this.isProcessing.set(false);
        if (!response.success) {
          this.error.set(this.errorKey(response));
          return;
        }
        this.load();
      },
      error: (err: unknown) => {
        this.isProcessing.set(false);
        this.error.set(this.errorKey(err));
      },
    });
  }

  formatDate(isoString: string | null | undefined): string {
    if (!isoString) return '';
    try {
      const date = new Date(isoString);
      return date.toLocaleDateString(this.lang.isAr() ? 'ar-SA' : 'en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return isoString;
    }
  }

  private errorKey(error: unknown): string {
    const envelope = error as { error?: { errors?: Array<{ code?: string }> }; errors?: Array<{ code?: string }> };
    const code = envelope?.error?.errors?.[0]?.code ?? envelope?.errors?.[0]?.code;
    return code ?? 'dispute_error';
  }
}
