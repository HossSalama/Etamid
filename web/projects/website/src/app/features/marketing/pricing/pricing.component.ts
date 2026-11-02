import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [RouterLink, MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('page_pricing_kicker') }}</span>
          <h1>{{ lang.t('page_pricing_title') }}</h1>
          <p>{{ lang.t('page_pricing_desc') }}</p>
        </section>

        <section class="marketing-section">
          <div class="marketing-card-grid">
            <article class="marketing-card marketing-price-card">
              <h3>{{ lang.t('page_pricing_individual_title') }}</h3>
              <p class="marketing-price">{{ lang.t('page_pricing_individual_price') }}</p>
              <p class="marketing-price-note">{{ lang.t('page_pricing_individual_note') }}</p>
              <ul>
                <li>{{ lang.t('page_pricing_individual_f1') }}</li>
                <li>{{ lang.t('page_pricing_individual_f2') }}</li>
                <li>{{ lang.t('page_pricing_individual_f3') }}</li>
              </ul>
              <a routerLink="/auth/register-individual" class="marketing-card-cta">
                {{ lang.t('welcome_create_individual') }}
              </a>
            </article>
            <article class="marketing-card marketing-price-card marketing-price-featured">
              <h3>{{ lang.t('page_pricing_company_title') }}</h3>
              <p class="marketing-price">{{ lang.t('page_pricing_company_price') }}</p>
              <p class="marketing-price-note">{{ lang.t('page_pricing_company_note') }}</p>
              <ul>
                <li>{{ lang.t('page_pricing_company_f1') }}</li>
                <li>{{ lang.t('page_pricing_company_f2') }}</li>
                <li>{{ lang.t('page_pricing_company_f3') }}</li>
              </ul>
              <a routerLink="/auth/register-company" class="marketing-card-cta">
                {{ lang.t('welcome_create_company') }}
              </a>
            </article>
            <article class="marketing-card marketing-price-card">
              <h3>{{ lang.t('page_pricing_custom_title') }}</h3>
              <p class="marketing-price">{{ lang.t('page_pricing_custom_price') }}</p>
              <p class="marketing-price-note">{{ lang.t('page_pricing_custom_note') }}</p>
              <ul>
                <li>{{ lang.t('page_pricing_custom_f1') }}</li>
                <li>{{ lang.t('page_pricing_custom_f2') }}</li>
                <li>{{ lang.t('page_pricing_custom_f3') }}</li>
              </ul>
              <a routerLink="/contact" class="marketing-card-cta">{{ lang.t('page_contact_nav') }}</a>
            </article>
          </div>
          <p class="marketing-note">{{ lang.t('page_pricing_note') }}</p>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class PricingComponent {
  protected readonly lang = inject(LanguageService);
}
