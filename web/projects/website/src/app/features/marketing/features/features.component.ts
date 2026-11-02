import { Component, inject } from '@angular/core';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

@Component({
  selector: 'app-features-page',
  standalone: true,
  imports: [MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('landing_features_kicker') }}</span>
          <h1>{{ lang.t('landing_features_title') }}</h1>
          <p>{{ lang.t('landing_features_desc') }}</p>
        </section>

        <section class="marketing-section">
          <div class="marketing-card-grid">
            <article class="marketing-card">
              <span class="marketing-card-index">01</span>
              <h3>{{ lang.t('landing_feature_one_title') }}</h3>
              <p>{{ lang.t('landing_feature_one_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">02</span>
              <h3>{{ lang.t('landing_feature_two_title') }}</h3>
              <p>{{ lang.t('landing_feature_two_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">03</span>
              <h3>{{ lang.t('landing_feature_three_title') }}</h3>
              <p>{{ lang.t('landing_feature_three_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">04</span>
              <h3>{{ lang.t('page_features_four_title') }}</h3>
              <p>{{ lang.t('page_features_four_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">05</span>
              <h3>{{ lang.t('page_features_five_title') }}</h3>
              <p>{{ lang.t('page_features_five_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">06</span>
              <h3>{{ lang.t('page_features_six_title') }}</h3>
              <p>{{ lang.t('page_features_six_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">07</span>
              <h3>{{ lang.t('page_features_seven_title') }}</h3>
              <p>{{ lang.t('page_features_seven_desc') }}</p>
            </article>
            <article class="marketing-card">
              <span class="marketing-card-index">08</span>
              <h3>{{ lang.t('page_features_eight_title') }}</h3>
              <p>{{ lang.t('page_features_eight_desc') }}</p>
            </article>
          </div>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class FeaturesPageComponent {
  protected readonly lang = inject(LanguageService);
}
