import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';
import { SupportService } from '../../../core/services/support.service';
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
            @if (error()) {
              <p class="marketing-error" role="alert">{{ error() }}</p>
            }
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
              <button type="submit" class="marketing-submit" [disabled]="sending()">{{ lang.t('page_contact_submit') }}</button>
            </form>
          }
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class ContactComponent {
  protected readonly lang = inject(LanguageService);
  private readonly support = inject(SupportService);
  protected readonly sent = signal(false);
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected onSubmit(event: Event): void {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }
    const data = new FormData(form);
    this.error.set(null);
    this.sending.set(true);
    this.support
      .sendGuestContact({
        name: String(data.get('name') ?? ''),
        channel: String(data.get('channel') ?? ''),
        subject: String(data.get('subject') ?? ''),
        message: String(data.get('message') ?? ''),
      })
      .subscribe({
        next: (res) => {
          this.sending.set(false);
          if (res.success) {
            this.sent.set(true);
          } else {
            this.error.set(res.message || this.lang.t('page_contact_error'));
          }
        },
        error: (err) => {
          this.sending.set(false);
          this.error.set(err?.error?.message || this.lang.t('page_contact_error'));
        },
      });
  }
}
