import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../core/services/language.service';

/**
 * Shared public shell for the marketing pages (About, Pricing, FAQ, Contact,
 * Features, How it works): top navigation, a closing call-to-action and a
 * footer that links to every public page. Page content is projected inside.
 */
@Component({
  selector: 'app-marketing-shell',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="marketing-shell">
      <header class="marketing-nav">
        <a routerLink="/home" class="marketing-brand" aria-label="Etamid home">
          <img src="/logo.png" alt="اعتمد" />
        </a>
        <nav class="marketing-links" aria-label="Main navigation">
          <a routerLink="/features">{{ lang.t('landing_nav_features') }}</a>
          <a routerLink="/how-it-works">{{ lang.t('landing_nav_steps') }}</a>
          <a routerLink="/about">{{ lang.t('page_about_nav') }}</a>
          <a routerLink="/pricing">{{ lang.t('page_pricing_nav') }}</a>
          <a routerLink="/faq">{{ lang.t('page_faq_nav') }}</a>
          <a routerLink="/contact">{{ lang.t('page_contact_nav') }}</a>
        </nav>
        <div class="marketing-nav-actions">
          <button class="marketing-language" type="button" (click)="lang.toggleLanguage()">
            {{ lang.langLabel() }} <span>⌄</span>
          </button>
          <a routerLink="/auth/login" class="marketing-login">{{ lang.t('welcome_login') }}</a>
          <a routerLink="/auth/welcome" class="marketing-nav-cta">{{ lang.t('landing_nav_register') }}</a>
        </div>
      </header>

      <main class="marketing-main">
        <ng-content></ng-content>
        <section class="marketing-cta">
          <div>
            <span>{{ lang.t('landing_bottom_kicker') }}</span>
            <h2>{{ lang.t('landing_bottom_title') }}</h2>
            <p>{{ lang.t('landing_bottom_desc') }}</p>
          </div>
          <a routerLink="/auth/welcome">{{ lang.t('landing_primary_cta') }} <span aria-hidden="true">←</span></a>
        </section>
      </main>

      <footer class="marketing-footer">
        <a routerLink="/home" class="marketing-footer-brand"><img src="/logo.png" alt="اعتمد" /></a>
        <nav class="marketing-footer-links" aria-label="Footer navigation">
          <a routerLink="/features">{{ lang.t('landing_nav_features') }}</a>
          <a routerLink="/how-it-works">{{ lang.t('landing_nav_steps') }}</a>
          <a routerLink="/about">{{ lang.t('page_about_nav') }}</a>
          <a routerLink="/pricing">{{ lang.t('page_pricing_nav') }}</a>
          <a routerLink="/faq">{{ lang.t('page_faq_nav') }}</a>
          <a routerLink="/contact">{{ lang.t('page_contact_nav') }}</a>
          <a routerLink="/terms">{{ lang.t('page_terms_nav') }}</a>
          <a routerLink="/privacy">{{ lang.t('page_privacy_nav') }}</a>
        </nav>
        <span>{{ lang.t('landing_footer_note') }}</span>
        <a routerLink="/auth/login">{{ lang.t('welcome_login') }}</a>
      </footer>
    </div>
  `,
})
export class MarketingShellComponent {
  protected readonly lang = inject(LanguageService);
}
