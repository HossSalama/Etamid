import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  AppNotification,
  NotificationsService,
} from '../../core/services/notifications.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * In-app notifications (Layer 1): the list from GET /notifications, newest
 * first. Tapping a notification marks it read and opens the screen its
 * links point at — a dispute goes to the dispute page, a company deal to
 * its detail page, everything else to the dashboard (deals and the wallet
 * live there; there is no /deals/:id route). Opening the list never marks
 * anything read; only a tap (or "mark all read") does.
 */
@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="notifications-page">
      <header class="notifications-header-card">
        <a class="notifications-back" routerLink="/home">→ {{ lang.t('back') }}</a>
        <h1>{{ lang.t('notif_title') }}</h1>
        <div class="notifications-header-actions">
          @if (unreadCount() > 0) {
            <button class="btn btn-black" (click)="markAllRead()" [disabled]="isMarkingAll()">
              {{ lang.t('notif_mark_all_read') }}
            </button>
          }
        </div>
      </header>
      <div class="notifications-content">
        @if (isLoading()) {
          <p>{{ lang.t('notif_loading') }}</p>
        } @else if (loadError()) {
          <p>{{ lang.t('notif_error') }}</p>
        } @else if (!items().length) {
          <div class="empty-notifications-box">
            <span class="empty-icon">🔔</span>
            <h4>{{ lang.t('notif_empty_title') }}</h4>
            <p>{{ lang.t('notif_empty_desc') }}</p>
          </div>
        } @else {
          <ul class="notifications-list">
            @for (item of items(); track item.id) {
              <li>
                <button
                  class="notification-row"
                  [class.notification-unread]="!item.isRead"
                  (click)="openNotification(item)"
                >
                  <span class="notification-dot" aria-hidden="true"></span>
                  <span class="notification-text">
                    <span class="notification-title">{{ item.title }}</span>
                    <span class="notification-body">{{ item.body }}</span>
                    <span class="notification-date">{{ item.createdAt }}</span>
                  </span>
                </button>
              </li>
            }
          </ul>
          @if (hasMore()) {
            <button class="btn btn-black" (click)="loadMore()" [disabled]="isLoadingMore()">
              {{ isLoadingMore() ? lang.t('notif_loading') : lang.t('notif_load_more') }}
            </button>
          }
        }
      </div>
    </main>
  `,
  styles: [`
    .notifications-page {
      max-width: 720px;
      margin: 0 auto;
      padding: 1rem;
    }
    .notifications-header-card {
      background: #ffffff;
      border-radius: 16px;
      padding: 1.25rem;
      margin-bottom: 1rem;
    }
    .notifications-back {
      color: #6b7280;
      text-decoration: none;
      font-size: 0.85rem;
    }
    .notifications-header-card h1 {
      margin: 0.5rem 0;
      font-size: 1.25rem;
    }
    .notifications-header-actions {
      display: flex;
      justify-content: flex-end;
    }
    .notifications-content {
      background: #ffffff;
      border-radius: 16px;
      padding: 1.25rem;
    }
    .empty-notifications-box {
      text-align: center;
      padding: 2.5rem 1.5rem;
    }
    .empty-icon {
      font-size: 2.5rem;
      display: block;
      margin-bottom: 0.5rem;
    }
    .notifications-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .notification-row {
      display: flex;
      gap: 0.75rem;
      width: 100%;
      text-align: start;
      background: none;
      border: none;
      border-bottom: 1px solid #f1f5f9;
      padding: 0.85rem 0.25rem;
      cursor: pointer;
    }
    .notification-unread {
      background: #fef3f2;
    }
    .notification-dot {
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 50%;
      background: #eb5757;
      margin-top: 0.45rem;
      flex-shrink: 0;
      visibility: hidden;
    }
    .notification-unread .notification-dot {
      visibility: visible;
    }
    .notification-text {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }
    .notification-title {
      font-weight: 700;
      font-size: 0.95rem;
      color: #111827;
    }
    .notification-body {
      font-size: 0.85rem;
      color: #4b5563;
      line-height: 1.45;
    }
    .notification-date {
      font-size: 0.75rem;
      color: #9ca3af;
    }
  `],
})
export class NotificationsPageComponent implements OnInit {
  private readonly notificationsApi = inject(NotificationsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);

  readonly items = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);
  readonly isLoading = signal(true);
  readonly isLoadingMore = signal(false);
  readonly isMarkingAll = signal(false);
  readonly loadError = signal(false);
  readonly hasMore = signal(false);

  private page = 1;
  private readonly pageSize = 20;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.load();
  }

  markAllRead(): void {
    if (this.isMarkingAll()) return;
    this.isMarkingAll.set(true);
    this.notificationsApi.markAllRead().subscribe({
      next: (response) => {
        this.isMarkingAll.set(false);
        if (!response.success) return;
        this.items.update((list) => list.map((n) => ({ ...n, isRead: true })));
        this.unreadCount.set(0);
      },
      error: () => {
        this.isMarkingAll.set(false);
      },
    });
  }

  openNotification(item: AppNotification): void {
    if (!item.isRead) {
      this.notificationsApi.markRead(item.id).subscribe({
        next: (response) => {
          if (!response.success) return;
          this.items.update((list) =>
            list.map((n) => (n.id === item.id ? { ...n, isRead: true } : n)),
          );
          this.unreadCount.update((c) => Math.max(0, c - 1));
        },
        error: () => {},
      });
    }
    if (item.disputeId !== null && item.dealId !== null) {
      void this.router.navigate(['/deals', item.dealId, 'dispute']);
    } else if (item.companyDealId !== null) {
      void this.router.navigate(['/company-deals', item.companyDealId]);
    } else {
      void this.router.navigate(['/home']);
    }
  }

  loadMore(): void {
    if (this.isLoadingMore() || !this.hasMore()) return;
    this.isLoadingMore.set(true);
    this.page += 1;
    this.notificationsApi.getNotifications(this.page, this.pageSize).subscribe({
      next: (response) => {
        this.isLoadingMore.set(false);
        if (!response.success || !response.data) return;
        this.items.update((list) => [...list, ...response.data]);
        this.hasMore.set(response.data.length === this.pageSize);
      },
      error: () => {
        this.isLoadingMore.set(false);
        this.page -= 1;
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadError.set(false);
    this.notificationsApi.getNotifications(this.page, this.pageSize).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) {
          this.loadError.set(true);
          return;
        }
        this.items.set(response.data);
        this.hasMore.set(response.data.length === this.pageSize);
        this.loadUnreadCount();
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set(true);
      },
    });
  }

  private loadUnreadCount(): void {
    this.notificationsApi.getUnreadCount().subscribe({
      next: (response) => {
        if (response.success && response.data) this.unreadCount.set(response.data.count);
      },
      error: () => {},
    });
  }
}
