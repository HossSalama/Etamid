import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AppNotification,
  NotificationsService,
} from '../../core/services/notifications.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * In-app notifications list: newest first.
 */
@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen notifications-page">
      <app-header [title]="lang.t('notif_title')" backUrl="/home">
        <div header-actions>
          @if (unreadCount() > 0) {
            <button
              type="button"
              class="btn btn-pill mark-read-btn"
              (click)="markAllRead()"
              [disabled]="isMarkingAll()"
            >
              {{ lang.t('notif_mark_all_read') }}
            </button>
          }
        </div>
      </app-header>

      <main class="app-page-shell" style="max-width: 780px;">
        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('notif_loading') }}</p>
          </div>
        } @else if (loadError()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center; color: #b91c1c;">
            <p>{{ lang.t('notif_error') }}</p>
          </div>
        } @else if (!items().length) {
          <div class="app-card empty-notifications-box" style="padding: 3rem 1.5rem; text-align: center;">
            <span style="font-size: 2.5rem; display: block; margin-bottom: 0.5rem;" aria-hidden="true">🔔</span>
            <h4 style="font-size: 1.1rem; color: #111827; margin-bottom: 0.35rem;">{{ lang.t('notif_empty_title') }}</h4>
            <p style="color: #6b7280; font-size: 0.9rem;">{{ lang.t('notif_empty_desc') }}</p>
          </div>
        } @else {
          <div class="app-card notifications-card">
            <ul class="notifications-list">
              @for (item of items(); track item.id) {
                <li>
                  <button
                    type="button"
                    class="notification-row"
                    [class.notification-unread]="!item.isRead"
                    (click)="openNotification(item)"
                  >
                    <span class="notification-dot" aria-hidden="true"></span>
                    <span class="notification-text">
                      <strong class="notification-title">{{ item.title }}</strong>
                      <span class="notification-body">{{ item.body }}</span>
                      <small class="notification-date">🕒 {{ item.createdAt }}</small>
                    </span>
                  </button>
                </li>
              }
            </ul>

            @if (hasMore()) {
              <div style="text-align: center; padding-top: 1.25rem;">
                <button type="button" class="btn btn-outline-coral" (click)="loadMore()" [disabled]="isLoadingMore()">
                  {{ isLoadingMore() ? lang.t('notif_loading') : lang.t('notif_load_more') }}
                </button>
              </div>
            }
          </div>
        }
      </main>

      <app-bottom-nav activeNav="home" />
    </div>
  `,
  styles: [`
    .mark-read-btn {
      background: #ffffff;
      color: #eb5757 !important;
      font-weight: 850;
      min-height: 38px;
      padding: 0.4rem 0.9rem;
      font-size: 0.82rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
      &:hover {
        background: #fdf2f2;
      }
    }
    .notifications-card {
      padding: 0.5rem 1rem 1.25rem;
    }
    .notifications-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .notification-row {
      display: flex;
      gap: 0.85rem;
      width: 100%;
      text-align: start;
      background: none;
      border: none;
      border-bottom: 1px solid #f1f5f9;
      padding: 1rem 0.5rem;
      cursor: pointer;
      border-radius: 12px;
      transition: background 0.15s ease;
      font-family: inherit;
      &:hover {
        background: #f9fafb;
      }
    }
    .notification-unread {
      background: #fff5f5;
      &:hover {
        background: #fee2e2;
      }
    }
    .notification-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #eb5757;
      margin-top: 0.55rem;
      flex-shrink: 0;
      opacity: 0;
    }
    .notification-unread .notification-dot {
      opacity: 1;
    }
    .notification-text {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      flex: 1;
    }
    .notification-title {
      font-weight: 800;
      font-size: 0.95rem;
      color: #111827;
    }
    .notification-body {
      font-size: 0.88rem;
      color: #4b5563;
      line-height: 1.5;
    }
    .notification-date {
      font-size: 0.76rem;
      color: #9ca3af;
      margin-top: 0.2rem;
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
