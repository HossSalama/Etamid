import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { LanguageService } from '../../services/language.service';
import { NotificationsService } from '../../services/notifications.service';

@Component({
  selector: 'app-header',
  standalone: true,
  template: `
    <header class="app-header-card" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <div class="app-header-top">
        <div class="app-header-start">
          @if (showBack) {
            <button
              type="button"
              class="app-header-back-btn"
              (click)="onBack()"
              [attr.aria-label]="lang.t('back')"
            >
              <span class="back-arrow" aria-hidden="true">{{ lang.isAr() ? '→' : '←' }}</span>
              <span class="back-text">{{ lang.t('back') }}</span>
            </button>
          }

          <div class="app-header-title-wrap">
            <h1 class="app-header-title">{{ title }}</h1>
            @if (subtitle || badge) {
              <div class="app-header-sub-row">
                @if (subtitle) {
                  <span class="app-header-subtitle">{{ subtitle }}</span>
                }
                @if (badge) {
                  <span class="badge" [class]="'badge-' + (badgeType || 'neutral')">{{ badge }}</span>
                }
              </div>
            }
          </div>
        </div>

        <div class="app-header-actions">
          <ng-content select="[header-actions]"></ng-content>

          @if (showNotifications && authService.isAuthenticated()) {
            <button
              type="button"
              class="app-header-icon-btn"
              (click)="openNotifications()"
              [title]="lang.t('notif_title')"
            >
              <span aria-hidden="true">🔔</span>
              @if (hasUnread()) {
                <span class="unread-dot" aria-label="Unread notifications"></span>
              }
            </button>
          }

          @if (showLang) {
            <button
              type="button"
              class="app-header-icon-btn lang-btn"
              (click)="lang.toggleLanguage()"
              [title]="lang.otherLangLabel()"
            >
              <span>{{ lang.currentLang() === 'ar' ? 'EN' : 'ع' }}</span>
            </button>
          }

          @if (showLogout && authService.isAuthenticated()) {
            <button
              type="button"
              class="app-header-icon-btn logout-btn"
              (click)="logout()"
              title="تسجيل الخروج"
            >
              <span aria-hidden="true">🚪</span>
            </button>
          }
        </div>
      </div>

      <ng-content select="[header-bottom]"></ng-content>
    </header>
  `,
})
export class AppHeaderComponent implements OnInit {
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() badge = '';
  @Input() badgeType: 'success' | 'warning' | 'danger' | 'info' | 'neutral' = 'neutral';
  @Input() backUrl = '/home';
  @Input() showBack = true;
  @Input() showNotifications = true;
  @Input() showLang = true;
  @Input() showLogout = false;
  @Output() readonly back = new EventEmitter<void>();

  protected readonly lang = inject(LanguageService);
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificationsApi = inject(NotificationsService);

  readonly hasUnread = signal(false);

  ngOnInit(): void {
    if (this.showNotifications && this.authService.isAuthenticated()) {
      this.notificationsApi.getUnreadCount().subscribe({
        next: (res) => {
          this.hasUnread.set((res.data?.count ?? 0) > 0);
        },
        error: () => {},
      });
    }
  }

  onBack(): void {
    if (this.back.observed) {
      this.back.emit();
      return;
    }
    if (this.backUrl) {
      void this.router.navigateByUrl(this.backUrl);
    } else if (globalThis.history.length > 1) {
      globalThis.history.back();
    } else {
      void this.router.navigate(['/home']);
    }
  }

  openNotifications(): void {
    void this.router.navigate(['/notifications']);
  }

  logout(): void {
    this.authService.logout();
    void this.router.navigate(['/auth/welcome']);
  }
}
