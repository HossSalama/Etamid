import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

@Component({
  selector: 'app-about',
  standalone: true,
  imports: [RouterLink, MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('page_about_kicker') }}</span>
          <h1>{{ lang.t('page_about_title') }}</h1>
          <p>{{ lang.t('page_about_desc') }}</p>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_about_what_title') }}</h2>
          <p>{{ lang.t('page_about_what_desc') }}</p>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_about_mission_title') }}</h2>
          <p>{{ lang.t('page_about_mission_desc') }}</p>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_about_for_title') }}</h2>
          <div class="marketing-card-grid marketing-card-grid-two">
            <article class="marketing-card">
              <h3>{{ lang.t('page_about_for_individual_title') }}</h3>
              <p>{{ lang.t('page_about_for_individual_desc') }}</p>
              <a routerLink="/auth/register-individual" class="marketing-text-link">
                {{ lang.t('welcome_create_individual') }} ←
              </a>
            </article>
            <article class="marketing-card">
              <h3>{{ lang.t('page_about_for_company_title') }}</h3>
              <p>{{ lang.t('page_about_for_company_desc') }}</p>
              <a routerLink="/auth/register-company" class="marketing-text-link">
                {{ lang.t('welcome_create_company') }} ←
              </a>
            </article>
          </div>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_about_values_title') }}</h2>
          <div class="marketing-card-grid">
            <article class="marketing-card">
              <span class="marketing-card-index">01</span>
              <h3>{{ lang.t('page_about_value_one_title') }}</h3>
              <p>{{ lang.t('page_about_value_one_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">02</span>
              <h3>{{ lang.t('page_about_value_two_title') }}</h3>
              <p>{{ lang.t('page_about_value_two_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">03</span>
              <h3>{{ lang.t('page_about_value_three_title') }}</h3>
              <p>{{ lang.t('page_about_value_three_desc') }}</p>
            </article>
          </div>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class AboutComponent {
  protected readonly lang = inject(LanguageService);
}
