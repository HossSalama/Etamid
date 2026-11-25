import { Location } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { skip } from 'rxjs';
import { LanguageService } from '../../core/services/language.service';
import { SupportService, TicketSubject } from '../../core/services/support.service';

// Registered-user Contact Us, like the design: WhatsApp button on top, then
// "or send us a message" with a subject dropdown and a 0/200 message box.
// Sending creates a support ticket (POST /api/v1/support/contact) — the
// guest form at /contact is a different page (guest message, no ticket).
@Component({
  selector: 'app-contact-us',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="figma-screen" [attr.dir]="lang.isAr() ? 'rtl' : 'ltr'">
      <header class="figma-top-bar">
        <button type="button" class="figma-back-btn" (click)="goBack()" [attr.aria-label]="lang.t('back')">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M15 18l-6-6 6-6" />
          </svg>
        </button>
        <h1 class="figma-header-title">{{ lang.t('support_contact_title') }}</h1>
        <div class="figma-top-spacer"></div>
      </header>

      <p class="contact-intro">{{ lang.t('support_contact_desc') }}</p>

      @if (loading()) {
        <p class="contact-note" role="status">{{ lang.t('support_contact_loading') }}</p>
      } @else if (loadError()) {
        <p class="contact-error" role="alert">{{ lang.t('support_contact_error') }}</p>
        <button type="button" class="btn-black" (click)="load()">{{ lang.t('page_faq_retry') }}</button>
      } @else if (sentTicket()) {
        <div class="success-card" role="status">
          <p class="success-title">{{ lang.t('page_contact_sent') }}</p>
          <p class="success-line">{{ lang.t('support_contact_ticket_label') }}: <strong dir="ltr">{{ sentTicket()!.number }}</strong></p>
          <p class="success-line">{{ lang.t('support_contact_ticket_status') }}</p>
          <button type="button" class="btn-black" (click)="sendAnother()">
            {{ lang.t('support_contact_new') }}
          </button>
          <a routerLink="/support/requests" class="btn-black success-link">
            {{ lang.t('support_requests_view_requests') }}
          </a>
        </div>
      } @else {
        @if (whatsapp()) {
          <section class="whatsapp-card">
            <p>{{ lang.t('support_contact_whatsapp_hint') }}</p>
            <button type="button" class="btn-whatsapp-wide" (click)="openWhatsApp()">
              <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                <path d="M12.031 6.172c-3.181 0-5.767 2.586-5.768 5.766-.001 1.298.38 2.27 1.019 3.287l-.582 2.128 2.182-.573c.978.58 1.911.928 3.145.929 3.178 0 5.767-2.587 5.768-5.766.001-3.187-2.575-5.77-5.764-5.771zm3.392 8.244c-.144.405-.837.774-1.17.824-.299.045-.677.063-1.092-.069-.252-.08-.575-.187-.988-.365-1.739-.751-2.874-2.502-2.961-2.617-.087-.116-.708-.94-.708-1.793s.448-1.273.607-1.446c.159-.173.346-.217.462-.217l.332.007c.106.005.249-.04.39.298.144.347.491 1.2.534 1.287.043.087.072.188.014.304-.058.116-.087.188-.173.289l-.26.304c-.087.086-.177.18-.076.354.101.174.449.741.964 1.201.662.591 1.221.774 1.394.86.174.086.275.072.376-.043.101-.116.433-.506.549-.68.116-.173.231-.145.39-.086.159.058 1.011.477 1.184.564.173.087.289.13.332.203.043.072.043.419-.101.824z"/>
              </svg>
              {{ lang.t('support_contact_whatsapp_btn') }}
            </button>
          </section>

          <div class="divider" aria-hidden="true"><span></span>{{ lang.t('support_contact_or') }}<span></span></div>
        }

        @if (error()) {
          <p class="contact-error" role="alert">{{ error() }}</p>
        }
        <form class="contact-form" (submit)="onSubmit($event)">
          <label>
            <span>{{ lang.t('support_contact_subject') }}</span>
            <select name="subjectId" required>
              <option value="" disabled selected>{{ lang.t('support_contact_subject_placeholder') }}</option>
              @for (subject of subjects(); track subject.id) {
                <option [value]="subject.id">{{ subject.name }}</option>
              }
            </select>
          </label>
          <label>
            <span>{{ lang.t('support_contact_message') }}</span>
            <textarea
              name="message"
              rows="5"
              required
              maxlength="200"
              [placeholder]="lang.t('support_contact_message_placeholder')"
              (input)="onMessageInput($event)"
            ></textarea>
            <small class="counter" dir="ltr">({{ messageLength() }}/200)</small>
          </label>
          <button type="submit" class="btn-black" [disabled]="sending() || messageLength() === 0">
            {{ sending() ? lang.t('support_contact_sending') : lang.t('support_contact_submit') }}
          </button>
        </form>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      background-color: #f8f9fa;
      min-height: 100vh;
      font-family: 'Cairo', system-ui, -apple-system, sans-serif;
    }

    .figma-screen {
      max-width: 440px;
      margin: 0 auto;
      min-height: 100vh;
      background-color: #f8f9fa;
      padding: 1.25rem 1rem 3.5rem;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
    }

    .figma-top-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1.25rem;
      position: sticky;
      top: 0;
      background: #f8f9fa;
      z-index: 10;
      padding: 0.25rem 0;
    }

    .figma-back-btn {
      background: none;
      border: none;
      cursor: pointer;
      padding: 0.35rem;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
      color: #eb5757;

      &:hover {
        background: #fee2e2;
      }
    }

    .figma-header-title {
      font-size: 1.25rem;
      font-weight: 850;
      color: #eb5757;
      margin: 0;
      text-align: center;
      flex: 1;
    }

    .figma-top-spacer {
      width: 32px;
    }

    .contact-intro {
      margin: 0 0 1.5rem;
      color: #5f6878;
      line-height: 1.8;
      text-align: center;
    }

    .contact-note {
      color: #5f6878;
      line-height: 1.8;
      text-align: center;
    }

    .contact-error {
      padding: 1rem 1.25rem;
      border-radius: 12px;
      background: #fef2f2;
      color: #991b1b;
      font-weight: 800;
    }

    .whatsapp-card {
      padding: 1.25rem;
      border: 1px solid #e5e7eb;
      border-radius: 20px;
      background: #fff;
      text-align: center;

      p {
        margin: 0 0 1rem;
        color: #374151;
        line-height: 1.8;
      }
    }

    .btn-whatsapp-wide {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      width: 100%;
      padding: 0.8rem 1rem;
      border: 1px solid #25d366;
      border-radius: 999px;
      background: #e9fbf0;
      color: #128c4b;
      font: inherit;
      font-weight: 800;
      cursor: pointer;
    }

    .divider {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin: 1.5rem 0;
      color: #5f6878;
      font-weight: 800;

      span {
        flex: 1;
        height: 1px;
        background: #e5e7eb;
      }
    }

    .contact-form {
      display: grid;
      gap: 1rem;

      label {
        display: grid;
        gap: 0.4rem;
        font-weight: 800;
      }

      select,
      textarea {
        width: 100%;
        padding: 0.8rem 1rem;
        border: 1px solid #e5e7eb;
        border-radius: 12px;
        font: inherit;
        background: #fff;
        box-sizing: border-box;
      }

      textarea {
        resize: vertical;
      }

      .counter {
        color: #5f6878;
        font-weight: 400;
        justify-self: end;
      }
    }

    .btn-black {
      padding: 0.85rem 1.5rem;
      border: 0;
      border-radius: 999px;
      background: #111827;
      color: #fff;
      font: inherit;
      font-weight: 800;
      cursor: pointer;

      &:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }
    }

    .success-card {
      padding: 1.5rem;
      border: 1px solid #bbf7d0;
      border-radius: 20px;
      background: #ecfdf5;
      text-align: center;

      .success-title {
        margin: 0 0 0.75rem;
        color: #065f46;
        font-weight: 900;
        font-size: 1.1rem;
      }

      .success-line {
        margin: 0.25rem 0;
        color: #374151;
      }

      .btn-black {
        margin-top: 1rem;
      }

      a.btn-black {
        display: inline-block;
        text-decoration: none;
      }
    }
  `],
})
export class ContactUsComponent implements OnInit {
  protected readonly lang = inject(LanguageService);
  private readonly support = inject(SupportService);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  // toObservable() needs an injection context (constructor / field
  // initializer) — calling it in ngOnInit throws at runtime (NG0203) and
  // aborts the first render. Field initializers run in that context.
  private readonly langChanges$ = toObservable(this.lang.currentLang);

  protected readonly subjects = signal<TicketSubject[]>([]);
  protected readonly whatsapp = signal('');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly messageLength = signal(0);
  protected readonly sentTicket = signal<{ number: string } | null>(null);

  ngOnInit(): void {
    this.load();
    // Subjects come from the API localized by Accept-Language: reload them
    // when the language changes (same bug class as the FAQ page, 23 Nov
    // 2026). Only the dropdown is refreshed — not the message being typed,
    // not a ticket that was already sent, and not the WhatsApp number
    // (language-neutral). skip(1) ignores the initial emission: load()
    // above already fetched once.
    this.langChanges$
      .pipe(skip(1), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.reloadSubjects());
  }

  private reloadSubjects(): void {
    this.support.getTicketSubjects().subscribe({
      next: (res) => {
        if (res.success && res.data) this.subjects.set(res.data.subjects);
      },
      error: () => {
        // Keep the old list: a failed reload must not blank the dropdown.
      },
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.sentTicket.set(null);
    // One call returns both the subjects and the WhatsApp number
    // ({ supportWhatsapp, subjects }) — no separate /api/v1/config call.
    this.support.getTicketSubjects().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.subjects.set(res.data.subjects);
          if (res.data.supportWhatsapp) this.whatsapp.set(res.data.supportWhatsapp);
        } else {
          this.loadError.set(true);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  protected goBack(): void {
    this.location.back();
  }

  protected openWhatsApp(): void {
    const phone = this.whatsapp().replace(/^\+/, '');
    globalThis.open(`https://wa.me/${phone}`, '_blank');
  }

  protected onMessageInput(event: Event): void {
    this.messageLength.set((event.target as HTMLTextAreaElement).value.length);
  }

  protected sendAnother(): void {
    this.sentTicket.set(null);
    this.messageLength.set(0);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }
    const data = new FormData(form);
    const subjectId = Number(data.get('subjectId'));
    const message = String(data.get('message') ?? '').trim();
    if (!subjectId || !message) return;
    this.error.set(null);
    this.sending.set(true);
    this.support.sendContact({ subjectId, message }).subscribe({
      next: (res) => {
        this.sending.set(false);
        if (res.success && res.data) {
          this.sentTicket.set({ number: res.data.number });
        } else {
          this.error.set(res.errors?.[0]?.message || this.lang.t('support_contact_error'));
        }
      },
      error: (err) => {
        this.sending.set(false);
        this.error.set(err?.error?.errors?.[0]?.message || this.lang.t('support_contact_error'));
      },
    });
  }
}
