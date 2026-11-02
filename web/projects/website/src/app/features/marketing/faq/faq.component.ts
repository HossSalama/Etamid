import { Component, inject } from '@angular/core';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

@Component({
  selector: 'app-faq',
  standalone: true,
  imports: [MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('page_faq_kicker') }}</span>
          <h1>{{ lang.t('page_faq_title') }}</h1>
          <p>{{ lang.t('page_faq_desc') }}</p>
        </section>

        <section class="marketing-section">
          <div class="marketing-faq">
            <details><summary>{{ lang.t('page_faq_q1') }}</summary><p>{{ lang.t('page_faq_a1') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q2') }}</summary><p>{{ lang.t('page_faq_a2') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q3') }}</summary><p>{{ lang.t('page_faq_a3') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q4') }}</summary><p>{{ lang.t('page_faq_a4') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q5') }}</summary><p>{{ lang.t('page_faq_a5') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q6') }}</summary><p>{{ lang.t('page_faq_a6') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q7') }}</summary><p>{{ lang.t('page_faq_a7') }}</p></details>
            <details><summary>{{ lang.t('page_faq_q8') }}</summary><p>{{ lang.t('page_faq_a8') }}</p></details>
          </div>
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class FaqComponent {
  protected readonly lang = inject(LanguageService);
}
