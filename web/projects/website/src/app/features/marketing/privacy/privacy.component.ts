import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

/**
 * Privacy Policy (draft for legal review): what Etamid collects, why, what
 * the other party sees, and what rights the user has over their data.
 */
@Component({
  selector: 'app-privacy',
  standalone: true,
  imports: [RouterLink, MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('page_privacy_kicker') }}</span>
          <h1>{{ lang.t('page_privacy_title') }}</h1>
          <p>{{ lang.t('page_privacy_desc') }}</p>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s1_title') }}</h2>
          <p>{{ lang.t('page_privacy_s1_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s2_title') }}</h2>
          <p>{{ lang.t('page_privacy_s2_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s3_title') }}</h2>
          <p>{{ lang.t('page_privacy_s3_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s4_title') }}</h2>
          <p>{{ lang.t('page_privacy_s4_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s5_title') }}</h2>
          <p>{{ lang.t('page_privacy_s5_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s6_title') }}</h2>
          <p>{{ lang.t('page_privacy_s6_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_privacy_s7_title') }}</h2>
          <p>{{ lang.t('page_privacy_s7_desc') }}</p>
          <a routerLink="/terms" class="marketing-text-link">{{ lang.t('page_terms_nav') }} ←</a>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class PrivacyComponent {
  protected readonly lang = inject(LanguageService);
}
