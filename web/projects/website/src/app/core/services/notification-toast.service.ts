import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import {
  AppNotification,
  NotificationsService,
} from './notifications.service';

/**
 * In-app notification toast (Layer 1, frontend only — no Push, no SignalR).
 *
 * Polls GET /notifications (page 1, size 5) every 30 seconds while the user
 * is authenticated. The first poll after login only records the ids of the
 * unread notifications as "seen": notifications that predate the session
 * never toast. Later polls toast each newly arrived unread notification once:
 * the newest first, one at a time, each for 5 seconds. The seen set lives
 * only in memory, so a toast is never shown twice in the same session.
 * Polling skips while the tab is hidden and resumes when it becomes visible.
 * While the user is on /notifications the toast is suppressed (they are
 * already looking at the list) but the new ids are still marked seen.
 */
@Injectable({ providedIn: 'root' })
export class NotificationToastService {
  private readonly auth = inject(AuthService);
  private readonly notificationsApi = inject(NotificationsService);
  private readonly router = inject(Router);

  static readonly PollIntervalMs = 30_000;
  static readonly ToastDurationMs = 5_000;
  static readonly MaxQueued = 3;

  private readonly seenIds = new Set<number>();
  private readonly queue: AppNotification[] = [];
  private readonly _current = signal<AppNotification | null>(null);
  readonly current = this._current.asReadonly();

  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private dismissTimer: ReturnType<typeof setTimeout> | null = null;
  private baselineDone = false;
  private baselineAtMs = 0;
  private pollInFlight = false;
  private visibilityListener: (() => void) | null = null;
  private started = false;

  start(): void {
    if (this.started) return;
    this.started = true;
    this.pollTimer = setInterval(() => this.poll(), NotificationToastService.PollIntervalMs);
    this.visibilityListener = () => {
      if (!document.hidden) void this.poll();
    };
    document.addEventListener('visibilitychange', this.visibilityListener);
    void this.poll();
  }

  stop(): void {
    this.started = false;
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
    if (this.visibilityListener !== null) {
      document.removeEventListener('visibilitychange', this.visibilityListener);
      this.visibilityListener = null;
    }
    this.clearDismissTimer();
    this.queue.length = 0;
    this._current.set(null);
  }

  /** Tap the toast: mark it read (errors ignored, like the notifications page) then open its screen. */
  openCurrent(): void {
    const item = this._current();
    if (!item) return;
    this.dismissCurrent();
    if (!item.isRead) {
      this.notificationsApi.markRead(item.id).subscribe({ error: () => {} });
    }
    if (item.disputeId !== null && item.dealId !== null) {
      void this.router.navigate(['/deals', item.dealId, 'dispute']);
    } else if (item.companyDealId !== null) {
      void this.router.navigate(['/company-deals', item.companyDealId]);
    } else {
      void this.router.navigate(['/home']);
    }
  }

  /** Close (×): dismiss without marking read and without navigating. */
  dismissCurrent(): void {
    this.clearDismissTimer();
    this._current.set(null);
    this.showNext();
  }

  private poll(): void {
    if (!this.auth.isAuthenticated()) {
      // Logged out: forget the session so a later login re-baselines.
      this.baselineDone = false;
      this.baselineAtMs = 0;
      this.seenIds.clear();
      this.queue.length = 0;
      this._current.set(null);
      this.clearDismissTimer();
      return;
    }
    if (document.hidden || this.pollInFlight) return;
    this.pollInFlight = true;
    this.notificationsApi.getNotifications(1, 5).subscribe({
      next: (response) => {
        this.pollInFlight = false;
        if (!response.success || !response.data) return;
        const unread = response.data.filter((n) => !n.isRead);
        if (!this.baselineDone) {
          for (const n of unread) this.seenIds.add(n.id);
          // Cutoff as well as the seen set: unread items beyond page 1 were
          // never baselined, so only toast items created after this moment.
          this.baselineAtMs = Date.now();
          this.baselineDone = true;
          return;
        }
        // Newest first: the API already returns newest first.
        const fresh = unread.filter(
          (n) => !this.seenIds.has(n.id) && Date.parse(n.createdAt) > this.baselineAtMs,
        );
        for (const n of fresh) this.seenIds.add(n.id);
        if (fresh.length === 0) return;
        if (this.isOnNotificationsPage()) return;
        this.queue.push(...fresh);
        // Keep at most MaxQueued waiting (newest first); drop older extras.
        while (this.queue.length > NotificationToastService.MaxQueued) {
          this.queue.pop();
        }
        this.showNext();
      },
      error: () => {
        this.pollInFlight = false;
      },
    });
  }

  private showNext(): void {
    if (this._current() !== null) return;
    const next = this.queue.shift();
    if (!next) return;
    this._current.set(next);
    this.clearDismissTimer();
    this.dismissTimer = setTimeout(() => this.dismissCurrent(), NotificationToastService.ToastDurationMs);
  }

  private clearDismissTimer(): void {
    if (this.dismissTimer !== null) {
      clearTimeout(this.dismissTimer);
      this.dismissTimer = null;
    }
  }

  private isOnNotificationsPage(): boolean {
    return this.router.url.split('?')[0] === '/notifications';
  }
}
