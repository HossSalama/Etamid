import { Location } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import {
  MyDisputeSummary,
  SupportService,
  SupportTicketDetail,
  SupportTicketSummary,
} from '../../core/services/support.service';

// Support Requests (design: Support Requests.png) — the user's tickets and
// disputes in one list, tabs الكل / مفتوحة / مغلقة.
//
// WHERE THE DATA COMES FROM (no new backend — both endpoints already exist):
// - Tickets: GET /api/v1/support/tickets?status=all|open|closed (own tickets
//   only, newest first; 'open' = Open + InReview server-side).
// - Disputes: GET /api/v1/me/disputes (opened by me OR against me, newest
//   first). The API takes ONE status value and the "open" tab spans two
//   (UnderReview + AwaitingInfo), so disputes are fetched unfiltered and
//   filtered client-side per tab. Page 1, 20 items each: the design has no
//   "load more", so the list shows the newest 20 of each kind. Each dispute
//   row is labelled with its direction (opened by you / opened against you)
//   by comparing OpenedByName to the logged-in profile name — frontend-only,
//   the list DTO has no OpenedById, so no backend change was needed.
// - Tapping a ticket opens its thread here (detail + reply). Only the admin
//   can close a ticket — there is no user close action or endpoint.
//   Tapping a dispute opens the existing dispute page (/deals/:id/dispute).
// - No language reload is needed on this page: ticket subjects and deal
//   titles are stored text (created in one language), only the labels are
//   language keys, and those switch by themselves.
//
// NOT shown: the design's "تم الرد" pill — the list endpoint has no
// "who replied last" field, so pills are only Open / In review / Closed
// (tickets) and Under review / Waiting for info / Closed (disputes).
type RequestTab = 'all' | 'open' | 'closed';

interface RequestItem {
  kind: 'ticket' | 'dispute';
  id: number;
  number: string;
  title: string;
  status: string;
  createdAt: string;
  dealId?: number;
  openedByName?: string | null;
}

@Component({
  selector: 'app-support-requests',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="figma-screen" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <header class="figma-top-bar">
        <button type="button" class="figma-back-btn" (click)="goBack()" [attr.aria-label]="lang.t('back')">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M15 18l-6-6 6-6" />
          </svg>
        </button>
        <h1 class="figma-header-title">{{ lang.t('support_requests_title') }}</h1>
        <a routerLink="/support/contact" class="new-link">{{ lang.t('support_requests_new') }}</a>
      </header>

      @if (selectedTicket(); as ticket) {
        <!-- Ticket thread -->
        <button type="button" class="back-link" (click)="backToList()">‹ {{ lang.t('back') }}</button>

        @if (detailLoading()) {
          <p class="page-note" role="status">{{ lang.t('support_requests_loading') }}</p>
        } @else {
          <section class="figma-card">
            <div class="detail-head">
              <span class="detail-number" dir="ltr">{{ ticket.number }}</span>
              <span class="pill {{ pillClassFor('ticket', ticket.status) }}">{{ pillLabelFor('ticket', ticket.status) }}</span>
            </div>
            <h2 class="detail-title">{{ ticket.subject }}</h2>
            <p class="detail-date">{{ formatDate(ticket.createdAt) }}</p>
            @if (ticket.dealTitle) {
              <p class="detail-deal">{{ ticket.dealTitle }}</p>
            }
          </section>

          <section class="figma-card">
            <h3 class="card-section-title">💬 {{ lang.t('support_requests_messages') }}</h3>
            @if (!ticket.messages.length) {
              <p class="page-note">{{ lang.t('support_requests_no_messages') }}</p>
            }
            <div class="messages-list">
              @for (message of ticket.messages; track message.id) {
                <article class="message-item">
                  <div class="message-head">
                    <strong class="sender-name">{{ message.senderName || lang.t('support_requests_sender_' + message.senderType) }}</strong>
                    <span class="badge">{{ lang.t('support_requests_sender_' + message.senderType) }}</span>
                    <small class="message-time">{{ formatDateTime(message.createdAt) }}</small>
                  </div>
                  @if (message.message) {
                    <p class="message-body">{{ message.message }}</p>
                  }
                  @if (message.fileUrl) {
                    <div class="attachment-chip">
                      📎
                      <a [href]="message.fileUrl ?? undefined" target="_blank" rel="noopener">
                        {{ lang.t('support_requests_attachment') }} ↗
                      </a>
                    </div>
                  }
                </article>
              }
            </div>

            @if (actionError()) {
              <p class="page-error" role="alert">{{ lang.t(actionError()!) }}</p>
            }

            @if (ticket.status === 'closed') {
              <p class="closed-note">{{ lang.t('support_requests_closed_note') }}</p>
            } @else {
              <form class="reply-form" (submit)="onReplySubmit($event)">
                <textarea
                  name="message"
                  rows="3"
                  required
                  maxlength="1000"
                  [placeholder]="lang.t('support_requests_reply_placeholder')"
                  (input)="onReplyInput($event)"
                ></textarea>
                <small class="counter" dir="ltr">({{ replyLength() }}/1000)</small>
                <div class="reply-actions">
                  <button type="submit" class="btn-black" [disabled]="sending() || replyLength() === 0">
                    {{ sending() ? lang.t('support_requests_sending') : lang.t('support_requests_send') }}
                  </button>
                </div>
              </form>
            }
          </section>
        }
      } @else {
        <!-- List -->
        <div class="tabs" role="tablist">
          <button type="button" role="tab" class="tab-btn" [class.active]="tab() === 'all'" (click)="setTab('all')">
            {{ lang.t('support_requests_all') }}
          </button>
          <button type="button" role="tab" class="tab-btn" [class.active]="tab() === 'open'" (click)="setTab('open')">
            {{ lang.t('support_requests_open') }}
          </button>
          <button type="button" role="tab" class="tab-btn" [class.active]="tab() === 'closed'" (click)="setTab('closed')">
            {{ lang.t('support_requests_closed') }}
          </button>
        </div>

        @if (loading()) {
          <p class="page-note" role="status">{{ lang.t('support_requests_loading') }}</p>
        } @else if (error()) {
          <p class="page-error" role="alert">{{ lang.t('support_requests_error') }}</p>
          <button type="button" class="btn-black" (click)="load()">{{ lang.t('page_faq_retry') }}</button>
        } @else if (items().length === 0) {
          <p class="page-note" role="status">{{ lang.t('support_requests_empty') }}</p>
          <p class="page-note">{{ lang.t('support_requests_empty_hint') }}</p>
          <a routerLink="/support/contact" class="btn-black empty-cta">{{ lang.t('support_requests_new') }}</a>
        } @else {
          <div class="requests-list">
            @for (item of items(); track item.kind + '-' + item.id) {
              @if (item.kind === 'ticket') {
                <button type="button" class="request-card" (click)="openTicket(item.id)">
                  <span class="request-icon" aria-hidden="true">
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                      <polyline points="14 2 14 8 20 8" />
                      <line x1="9" y1="13" x2="15" y2="13" />
                      <line x1="9" y1="17" x2="15" y2="17" />
                    </svg>
                  </span>
                  <span class="request-body">
                    <span class="request-title">{{ item.title }}</span>
                    <span class="request-meta"><span dir="ltr">{{ item.number }}</span> · {{ formatDate(item.createdAt) }} · {{ requestMetaSuffix(item) }}</span>
                  </span>
                  <span class="pill {{ pillClassFor(item.kind, item.status) }}">{{ pillLabelFor(item.kind, item.status) }}</span>
                </button>
              } @else {
                <a class="request-card" [routerLink]="['/deals', item.dealId, 'dispute']">
                  <span class="request-icon" aria-hidden="true">
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                      <polyline points="14 2 14 8 20 8" />
                      <line x1="9" y1="13" x2="15" y2="13" />
                      <line x1="9" y1="17" x2="15" y2="17" />
                    </svg>
                  </span>
                  <span class="request-body">
                    <span class="request-title">{{ item.title }}</span>
                    <span class="request-meta"><span dir="ltr">{{ item.number }}</span> · {{ formatDate(item.createdAt) }} · {{ requestMetaSuffix(item) }}</span>
                  </span>
                  <span class="pill {{ pillClassFor(item.kind, item.status) }}">{{ pillLabelFor(item.kind, item.status) }}</span>
                </a>
              }
            }
          </div>
        }
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
      gap: 1rem;
    }

    .figma-top-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.25rem;
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
      color: #eb5757;

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

    .new-link {
      color: #eb5757;
      font-weight: 800;
      font-size: 0.85rem;
      text-decoration: none;
      white-space: nowrap;
    }

    .back-link {
      background: none;
      border: none;
      cursor: pointer;
      color: #eb5757;
      font: inherit;
      font-weight: 800;
      padding: 0;
      align-self: flex-start;
    }

    .figma-card {
      background: #ffffff;
      border-radius: 20px;
      padding: 1.15rem 1.25rem;
      border: 1px solid #f1f5f9;
      box-shadow: 0 4px 18px rgba(17, 24, 39, 0.03);
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }

    .card-section-title {
      font-size: 1rem;
      font-weight: 850;
      color: #111827;
      margin: 0;
    }

    /* Tabs: الكل / مفتوحة / مغلقة (design) */
    .tabs {
      display: flex;
      border-bottom: 1px solid #e5e7eb;
    }

    .tab-btn {
      flex: 1;
      background: none;
      border: none;
      padding: 0.75rem 0;
      font: inherit;
      font-weight: 800;
      color: #9ca3af;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      margin-bottom: -1px;
    }

    .tab-btn.active {
      color: #eb5757;
      border-bottom-color: #eb5757;
    }

    /* Request cards (design: white rounded card, icon, title+date, pill) */
    .requests-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .request-card {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      width: 100%;
      text-align: start;
      background: #ffffff;
      border: 1px solid #f1f5f9;
      border-radius: 20px;
      padding: 1rem 1.15rem;
      cursor: pointer;
      font: inherit;
      color: inherit;
      text-decoration: none;
      box-sizing: border-box;
      box-shadow: 0 4px 18px rgba(17, 24, 39, 0.03);
    }

    .request-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: #fff5f5;
      color: #eb5757;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .request-body {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }

    .request-title {
      font-weight: 850;
      color: #111827;
      font-size: 0.95rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .request-meta {
      color: #9ca3af;
      font-size: 0.8rem;
      font-weight: 600;
    }

    /* Status pills (design colors: green open, blue in review, amber replied) */
    .pill {
      flex-shrink: 0;
      padding: 0.3rem 0.8rem;
      border-radius: 999px;
      font-size: 0.78rem;
      font-weight: 800;
      white-space: nowrap;
    }

    .pill-green { background: #dcfce7; color: #15803d; }
    .pill-blue { background: #dbeafe; color: #1d4ed8; }
    .pill-amber { background: #fef3c7; color: #b45309; }
    .pill-gray { background: #f1f5f9; color: #475569; }

    .page-note {
      color: #5f6878;
      line-height: 1.8;
      text-align: center;
      margin: 0.5rem 0;
    }

    .page-error {
      padding: 1rem 1.25rem;
      border-radius: 12px;
      background: #fef2f2;
      color: #991b1b;
      font-weight: 800;
      text-align: center;
    }

    .btn-black {
      padding: 0.85rem 1.5rem;
      border: 0;
      border-radius: 999px;
      background: #111827;
      color: #fff;
      font: inherit;
      font-weight: 800;
      cursor: pointer;
      text-decoration: none;
      display: inline-block;
      text-align: center;

      &:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }
    }

    .empty-cta {
      align-self: center;
    }

    /* Ticket detail */
    .detail-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
    }

    .detail-number {
      font-weight: 800;
      color: #eb5757;
      font-size: 0.9rem;
    }

    .detail-title {
      font-size: 1.05rem;
      font-weight: 850;
      color: #111827;
      margin: 0;
      line-height: 1.4;
    }

    .detail-date {
      color: #9ca3af;
      font-size: 0.82rem;
      font-weight: 600;
      margin: 0;
    }

    .detail-deal {
      color: #6b7280;
      font-size: 0.86rem;
      margin: 0;
    }

    /* Thread: same vertical pattern as the dispute page */
    .messages-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .message-item {
      border: 1px solid #f1f5f9;
      background: #fafafa;
      border-radius: 12px;
      padding: 0.85rem 1rem;
    }

    .message-head {
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

    .badge {
      background: #f1f5f9;
      color: #475569;
      border-radius: 999px;
      padding: 0.15rem 0.6rem;
      font-size: 0.72rem;
      font-weight: 800;
    }

    .message-time {
      color: #9ca3af;
      font-size: 0.76rem;
      margin-inline-start: auto;
    }

    .message-body {
      color: #374151;
      font-size: 0.9rem;
      line-height: 1.6;
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

    .reply-form {
      display: grid;
      gap: 0.5rem;
      margin-top: 0.5rem;

      textarea {
        width: 100%;
        padding: 0.8rem 1rem;
        border: 1px solid #e5e7eb;
        border-radius: 12px;
        font: inherit;
        background: #fff;
        box-sizing: border-box;
        resize: vertical;
      }

      .counter {
        color: #5f6878;
        font-weight: 400;
        justify-self: end;
      }
    }

    .reply-actions {
      display: flex;
      gap: 0.75rem;
      justify-content: flex-end;
    }

    .btn-outline {
      padding: 0.85rem 1.25rem;
      border: 1.5px solid #e5e7eb;
      border-radius: 999px;
      background: transparent;
      color: #374151;
      font: inherit;
      font-weight: 800;
      cursor: pointer;

      &:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }
    }

    .closed-note {
      color: #5f6878;
      font-weight: 700;
      font-size: 0.9rem;
      text-align: center;
      margin: 0.5rem 0 0;
    }
  `],
})
export class SupportRequestsComponent {
  protected readonly lang = inject(LanguageService);
  private readonly support = inject(SupportService);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  private readonly auth = inject(AuthService);
  // The logged-in profile name, for the dispute direction label below.
  // Declared after `auth`: field initializers run in declaration order.
  private readonly myName = computed(() => this.auth.currentUser()?.name ?? null);

  protected readonly tab = signal<RequestTab>('all');
  protected readonly items = signal<RequestItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  protected readonly selectedTicket = signal<SupportTicketDetail | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly replyLength = signal(0);
  protected readonly sending = signal(false);
  protected readonly actionError = signal<string | null>(null);

  // Refresh the list when coming back from a thread whose ticket was closed
  // (its status pill changed); replies do not change the list, so no reload.
  private needsRefresh = false;

  constructor() {
    this.load();
  }

  protected setTab(tab: RequestTab): void {
    if (this.tab() === tab) return;
    this.tab.set(tab);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.items.set([]);
    const tab = this.tab();
    forkJoin({
      tickets: this.support.getMyTickets(tab === 'all' ? undefined : tab),
      disputes: this.support.getMyDisputes(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ tickets, disputes }) => {
          this.loading.set(false);
          const ticketItems: RequestItem[] = (
            tickets.success && tickets.data ? tickets.data : []
          ).map((t: SupportTicketSummary) => ({
            kind: 'ticket' as const,
            id: t.id,
            number: t.number,
            title: t.subject,
            status: t.status,
            createdAt: t.createdAt,
          }));
          const disputeItems: RequestItem[] = (
            disputes.success && disputes.data ? disputes.data : []
          )
            .filter((d: MyDisputeSummary) => this.disputeInTab(d.status, tab))
            .map((d: MyDisputeSummary) => ({
              kind: 'dispute' as const,
              id: d.id,
              number: d.number,
              title: d.dealTitle,
              status: d.status,
              createdAt: d.createdAt,
              dealId: d.dealId,
              openedByName: d.openedByName,
            }));
          this.items.set(
            [...ticketItems, ...disputeItems].sort(
              (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
            ),
          );
          this.error.set(!tickets.success || !disputes.success);
        },
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        },
      });
  }

  private disputeInTab(status: string, tab: RequestTab): boolean {
    if (tab === 'all') return true;
    if (tab === 'open') return status === 'under_review' || status === 'awaiting_info';
    return status === 'resolved_buyer' || status === 'resolved_seller' || status === 'closed';
  }

  // Row label (frontend-only, no backend change): the kind — ticket vs
  // dispute — and for disputes whether the user opened it or it was opened
  // against them. The list DTO carries OpenedByName but no OpenedById, so
  // the direction is a name comparison against the logged-in profile (the
  // same Name the API resolves when it builds the list). If the profile
  // name is unknown, only the kind is shown.
  protected requestMetaSuffix(item: RequestItem): string {
    if (item.kind === 'ticket') return this.lang.t('support_requests_ticket');
    const me = this.myName();
    const direction = !me
      ? ''
      : item.openedByName === me
        ? ' — ' + this.lang.t('support_requests_opened_by_you')
        : ' — ' + this.lang.t('support_requests_opened_against_you');
    return this.lang.t('support_requests_dispute') + direction;
  }

  protected pillLabelFor(kind: 'ticket' | 'dispute', status: string): string {
    if (kind === 'ticket') {
      if (status === 'in_review') return this.lang.t('support_requests_in_review');
      if (status === 'closed') return this.lang.t('support_requests_closed_pill');
      return this.lang.t('support_requests_open_pill');
    }
    if (status === 'under_review') return this.lang.t('support_requests_in_review');
    if (status === 'awaiting_info') return this.lang.t('support_requests_awaiting_info');
    return this.lang.t('support_requests_closed_pill');
  }

  protected pillClassFor(kind: 'ticket' | 'dispute', status: string): string {
    if (kind === 'ticket') {
      if (status === 'in_review') return 'pill-blue';
      if (status === 'closed') return 'pill-gray';
      return 'pill-green';
    }
    if (status === 'under_review') return 'pill-blue';
    if (status === 'awaiting_info') return 'pill-amber';
    return 'pill-gray';
  }

  // The design shows "02 May 2026" — English month names, Latin digits.
  protected formatDate(iso: string): string {
    try {
      const d = new Date(iso);
      if (isNaN(d.getTime())) return '—';
      return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    } catch {
      return '—';
    }
  }

  protected formatDateTime(iso: string): string {
    try {
      const d = new Date(iso);
      if (isNaN(d.getTime())) return '—';
      return d.toLocaleString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return '—';
    }
  }

  protected goBack(): void {
    this.location.back();
  }

  protected openTicket(id: number): void {
    this.detailLoading.set(true);
    this.actionError.set(null);
    this.support
      .getTicketDetail(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.detailLoading.set(false);
          if (res.success && res.data) this.selectedTicket.set(res.data);
          else this.actionError.set('support_requests_error');
        },
        error: () => {
          this.detailLoading.set(false);
          this.actionError.set('support_requests_error');
        },
      });
  }

  protected backToList(): void {
    this.selectedTicket.set(null);
    this.replyLength.set(0);
    this.actionError.set(null);
    if (this.needsRefresh) {
      this.needsRefresh = false;
      this.load();
    }
  }

  protected onReplyInput(event: Event): void {
    this.replyLength.set((event.target as HTMLTextAreaElement).value.length);
  }

  protected onReplySubmit(event: Event): void {
    event.preventDefault();
    const ticket = this.selectedTicket();
    const form = event.target as HTMLFormElement;
    if (!ticket || this.sending()) return;
    const message = String(new FormData(form).get('message') ?? '').trim();
    if (!message) return;
    this.actionError.set(null);
    this.sending.set(true);
    this.support.postTicketMessage(ticket.id, message).subscribe({
      next: (res) => {
        this.sending.set(false);
        if (res.success && res.data) {
          this.selectedTicket.set({ ...ticket, messages: [...ticket.messages, res.data] });
          form.reset();
          this.replyLength.set(0);
        } else {
          this.actionError.set('support_requests_action_error');
        }
      },
      error: () => {
        this.sending.set(false);
        this.actionError.set('support_requests_action_error');
      },
    });
  }
}
