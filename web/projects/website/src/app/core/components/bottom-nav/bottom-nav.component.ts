import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { LanguageService } from '../../services/language.service';

export type NavTab = 'home' | 'deals' | 'shop' | 'company' | 'wallet' | 'profile';

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  template: `
    <nav class="bottom-nav-bar" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'" aria-label="Main Navigation">
      <button
        type="button"
        class="nav-item"
        [class.active]="activeNav === 'home'"
        (click)="onSelect('home')"
        [attr.aria-current]="activeNav === 'home' ? 'page' : null"
      >
        <span class="nav-icon" aria-hidden="true">🏠</span>
        <span class="nav-label">{{ lang.t('home_nav_home') }}</span>
      </button>

      <button
        type="button"
        class="nav-item"
        [class.active]="activeNav === 'deals'"
        (click)="onSelect('deals')"
        [attr.aria-current]="activeNav === 'deals' ? 'page' : null"
      >
        <span class="nav-icon" aria-hidden="true">📑</span>
        <span class="nav-label">{{ lang.t('home_nav_deals') }}</span>
      </button>

      <button
        type="button"
        class="nav-item"
        [class.active]="activeNav === 'shop'"
        (click)="onSelect('shop')"
        [attr.aria-current]="activeNav === 'shop' ? 'page' : null"
      >
        <span class="nav-icon" aria-hidden="true">🛍️</span>
        <span class="nav-label">{{ lang.t('shop_nav') }}</span>
      </button>

      <button
        type="button"
        class="nav-item"
        [class.active]="activeNav === 'company'"
        (click)="onSelect('company')"
        [attr.aria-current]="activeNav === 'company' ? 'page' : null"
      >
        <span class="nav-icon" aria-hidden="true">🏢</span>
        <span class="nav-label">{{ lang.t('company_nav') }}</span>
      </button>

      <button
        type="button"
        class="nav-item"
        [class.active]="activeNav === 'wallet'"
        (click)="onSelect('wallet')"
        [attr.aria-current]="activeNav === 'wallet' ? 'page' : null"
      >
        <span class="nav-icon" aria-hidden="true">💳</span>
        <span class="nav-label">{{ lang.t('home_nav_wallet') }}</span>
      </button>

      <button
        type="button"
        class="nav-item"
        [class.active]="activeNav === 'profile'"
        (click)="onSelect('profile')"
        [attr.aria-current]="activeNav === 'profile' ? 'page' : null"
      >
        <span class="nav-icon" aria-hidden="true">
          @if (authService.currentUser()?.avatarUrl) {
            <img
              class="nav-avatar-img"
              [src]="authService.currentUser()?.avatarUrl"
              [alt]="lang.t('profile_photo')"
            />
          } @else {
            👤
          }
        </span>
        <span class="nav-label">{{ lang.t('home_nav_profile') }}</span>
      </button>
    </nav>
  `,
})
export class BottomNavComponent {
  @Input() activeNav: NavTab = 'home';
  @Output() readonly navChange = new EventEmitter<NavTab>();

  protected readonly lang = inject(LanguageService);
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  onSelect(tab: NavTab): void {
    if (tab === 'shop') {
      void this.router.navigate(['/shop']);
      return;
    }
    if (tab === 'company') {
      void this.router.navigate(['/company-deals']);
      return;
    }

    // Tabs that live on the dashboard: home, deals, wallet, profile
    const currentUrl = this.router.url.split('?')[0];
    if (currentUrl === '/home' || currentUrl === '/') {
      this.navChange.emit(tab);
    } else {
      void this.router.navigate(['/home'], { queryParams: { tab } });
    }
  }
}
