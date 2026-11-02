import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';
import { MarketingShellComponent } from '../marketing-shell.component';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [RouterLink, MarketingShellComponent],
  template: `
    <app-marketing-shell>
      <div class="marketing-page">
        <section class="marketing-hero">
          <span class="marketing-kicker">{{ lang.t('page_contact_kicker') }}</span>
          <h1>{{ lang.t('page_contact_title') }}</h1>
          <p>{{ lang.t('page_contact_desc') }}</p>
        </section>

        <section class="marketing-section">
          <div class="marketing-card-grid marketing-card-grid-two">
            <article class="marketing-card">
              <h3>{{ lang.t('page_contact_account_title') }}</h3>
              <p>{{ lang.t('page_contact_account_desc') }}</p>
              <a routerLink="/auth/login" class="marketing-text-link">{{ lang.t('welcome_login') }} ←</a>
            </article>
            <article class="marketing-card">
              <h3>{{ lang.t('page_contact_company_title') }}</h3>
              <p>{{ lang.t('page_contact_company_desc') }}</p>
              <a routerLink="/auth/register-company" class="marketing-text-link">
                {{ lang.t('welcome_create_company') }} ←
              </a>
            </article>
          </div>
        </section>

        <section class="marketing-section">
          <h2>{{ lang.t('page_contact_form_title') }}</h2>
          @if (sent()) {
            <p class="marketing-success" role="status">{{ lang.t('page_contact_sent') }}</p>
          } @else {
            <!-- TODO: wire this form to a support endpoint or support mailbox
                 before launch; today it only validates and confirms locally. -->
            <form class="marketing-form" (submit)="onSubmit($event)">
              <label>
                <span>{{ lang.t('page_contact_name') }}</span>
                <input type="text" name="name" required maxlength="150" />
              </label>
              <label>
                <span>{{ lang.t('page_contact_channel') }}</span>
                <input type="text" name="channel" required maxlength="254" />
              </label>
              <label>
                <span>{{ lang.t('page_contact_subject') }}</span>
                <input type="text" name="subject" required maxlength="150" />
              </label>
              <label>
                <span>{{ lang.t('page_contact_message') }}</span>
                <textarea name="message" rows="5" required maxlength="2000"></textarea>
              </label>
              <button type="submit" class="marketing-submit">{{ lang.t('page_contact_submit') }}</button>
            </form>
          }
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class ContactComponent {
  protected readonly lang = inject(LanguageService);
  protected readonly sent = signal(false);

  protected onSubmit(event: Event): void {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    if (form.checkValidity()) {
      this.sent.set(true);
    } else {
      form.reportValidity();
    }
  }
}
