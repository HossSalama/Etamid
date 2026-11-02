import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

/**
 * Terms of Service (draft for legal review): the rules for using Etamid
 * accounts, deals and escrow. Not final legal advice; the business owner
 * should have the text reviewed before launch.
 */
@Component({
  selector: 'app-terms',
  standalone: true,
  imports: [RouterLink, MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('page_terms_kicker') }}</span>
          <h1>{{ lang.t('page_terms_title') }}</h1>
          <p>{{ lang.t('page_terms_desc') }}</p>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s1_title') }}</h2>
          <p>{{ lang.t('page_terms_s1_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s2_title') }}</h2>
          <p>{{ lang.t('page_terms_s2_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s3_title') }}</h2>
          <p>{{ lang.t('page_terms_s3_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s4_title') }}</h2>
          <p>{{ lang.t('page_terms_s4_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s5_title') }}</h2>
          <p>{{ lang.t('page_terms_s5_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s6_title') }}</h2>
          <p>{{ lang.t('page_terms_s6_desc') }}</p>
        </section>
        <section class="marketing-section">
          <h2>{{ lang.t('page_terms_s7_title') }}</h2>
          <p>{{ lang.t('page_terms_s7_desc') }}</p>
          <a routerLink="/privacy" class="marketing-text-link">{{ lang.t('page_privacy_nav') }} ←</a>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class TermsComponent {
  protected readonly lang = inject(LanguageService);
}
