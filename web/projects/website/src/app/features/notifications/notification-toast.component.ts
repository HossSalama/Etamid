import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NotificationToastService } from '../../core/services/notification-toast.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * In-app notification toast (Layer 1, frontend only).
 *
 * Rendered once in app.html so it works on every page. Shows at most one
 * toast at a time, at the top of the viewport, for 5 seconds. Tapping it
 * marks the notification read and opens its screen; the close button
 * dismisses it without marking read or navigating. The bell and the
 * /notifications page are untouched — the toast is only a fleeting heads-up.
 */
@Component({
  selector: 'app-notification-toast',
  standalone: true,
  template: `
    @if (toast.current(); as item) {
      <div class="notification-toast" role="status">
        <button class="notification-toast-body" (click)="toast.openCurrent()">
          <span class="notification-toast-icon" aria-hidden="true">🔔</span>
          <span class="notification-toast-text">
            <span class="notification-toast-title">{{ item.title }}</span>
            <span class="notification-toast-message">{{ item.body }}</span>
            <span class="notification-toast-hint">{{ lang.t('notif_toast_open') }}</span>
          </span>
        </button>
        <button
          class="notification-toast-close"
          type="button"
          (click)="toast.dismissCurrent()"
          aria-label="×"
        >
          ×
        </button>
      </div>
    }
  `,
  styles: [
    `
      .notification-toast {
        position: fixed;
        top: 0.75rem;
        left: 50%;
        transform: translateX(-50%);
        z-index: 1200;
        width: min(92vw, 560px);
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        background: #ffffff;
        border-radius: 16px;
        padding: 0.75rem 1rem;
        box-shadow: 0 8px 30px rgba(0, 0, 0, 0.18);
      }
      .notification-toast-body {
        flex: 1;
        display: flex;
        gap: 0.75rem;
        align-items: flex-start;
        padding: 0;
        border: 0;
        background: transparent;
        cursor: pointer;
        text-align: start;
        font: inherit;
        color: inherit;
      }
      .notification-toast-icon {
        font-size: 1.4rem;
        line-height: 1.4;
      }
      .notification-toast-text {
        display: flex;
        flex-direction: column;
        gap: 0.15rem;
        min-width: 0;
      }
      .notification-toast-title {
        font-weight: 700;
        font-size: 0.95rem;
      }
      .notification-toast-message {
        font-size: 0.85rem;
        color: #4b5563;
      }
      .notification-toast-hint {
        font-size: 0.75rem;
        color: #9ca3af;
      }
      .notification-toast-close {
        flex-shrink: 0;
        border: 0;
        background: transparent;
        cursor: pointer;
        font-size: 1.25rem;
        line-height: 1;
        padding: 0.25rem;
        color: #6b7280;
      }
    `,
  ],
})
export class NotificationToastComponent implements OnInit, OnDestroy {
  protected readonly toast = inject(NotificationToastService);
  protected readonly lang = inject(LanguageService);

  ngOnInit(): void {
    this.toast.start();
  }

  ngOnDestroy(): void {
    this.toast.stop();
  }
}
