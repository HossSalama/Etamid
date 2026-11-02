import { Component, inject } from '@angular/core';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

@Component({
  selector: 'app-how-it-works-page',
  standalone: true,
  imports: [MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('landing_steps_kicker') }}</span>
          <h1>{{ lang.t('landing_steps_title') }}</h1>
          <p>{{ lang.t('landing_steps_desc') }}</p>
        </section>

        <section class="marketing-section">
          <ol class="marketing-steps">
            <li>
              <span class="marketing-step-index">01</span>
              <h3>{{ lang.t('landing_step_one_title') }}</h3>
              <p>{{ lang.t('landing_step_one_desc') }}</p>
            </li>
            <li>
              <span class="marketing-step-index">02</span>
              <h3>{{ lang.t('landing_step_two_title') }}</h3>
              <p>{{ lang.t('landing_step_two_desc') }}</p>
            </li>
            <li>
              <span class="marketing-step-index">03</span>
              <h3>{{ lang.t('page_steps_three_title') }}</h3>
              <p>{{ lang.t('page_steps_three_desc') }}</p>
            </li>
            <li>
              <span class="marketing-step-index">04</span>
              <h3>{{ lang.t('page_steps_four_title') }}</h3>
              <p>{{ lang.t('page_steps_four_desc') }}</p>
            </li>
            <li>
              <span class="marketing-step-index">05</span>
              <h3>{{ lang.t('page_steps_five_title') }}</h3>
              <p>{{ lang.t('page_steps_five_desc') }}</p>
            </li>
            <li>
              <span class="marketing-step-index">06</span>
              <h3>{{ lang.t('page_steps_six_title') }}</h3>
              <p>{{ lang.t('page_steps_six_desc') }}</p>
            </li>
          </ol>
          <p class="marketing-note">{{ lang.t('page_steps_note') }}</p>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class HowItWorksPageComponent {
  protected readonly lang = inject(LanguageService);
}
