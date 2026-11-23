import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { combineLatest, Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { LanguageService } from '../../../core/services/language.service';
import { Faq, SupportService } from '../../../core/services/support.service';
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
          <div class="faq-search" role="search">
            <span class="faq-search-icon" aria-hidden="true">⌕</span>
            <input
              #searchInput
              type="search"
              [placeholder]="lang.t('page_faq_search')"
              [attr.aria-label]="lang.t('page_faq_search')"
              (input)="onSearch($event)"
            />
          </div>

          @if (loading()) {
            <p class="marketing-note" role="status">{{ lang.t('page_faq_loading') }}</p>
          } @else if (error()) {
            <p class="marketing-error" role="alert">{{ lang.t('page_faq_error') }}</p>
            <button type="button" class="marketing-submit" (click)="reload()">
              {{ lang.t('page_faq_retry') }}
            </button>
          } @else if (faqs().length === 0) {
            <p class="marketing-note" role="status">{{ lang.t('page_faq_no_results') }}</p>
            @if (query()) {
              <button type="button" class="marketing-submit" (click)="clearSearch(searchInput)">
                {{ lang.t('page_faq_clear_search') }}
              </button>
            }
          } @else {
            <div class="marketing-faq">
              @for (faq of faqs(); track faq.id; let i = $index) {
                <details [open]="$first">
                  <summary>{{ i + 1 }}. {{ faq.question }}</summary>
                  <p>{{ faq.answer }}</p>
                </details>
              }
            </div>
          }
        </section>
      </div>
    </app-marketing-shell>
  `,
})
export class FaqComponent implements OnInit {
  protected readonly lang = inject(LanguageService);
  private readonly support = inject(SupportService);
  private readonly destroyRef = inject(DestroyRef);
  // toObservable() needs an injection context (constructor / field
  // initializer) — calling it in ngOnInit throws at runtime (NG0203) and
  // aborts the first render, which blanked the whole page. Field
  // initializers run in that context, so the observable is created here
  // and subscribed to in ngOnInit.
  private readonly langChanges$ = toObservable(this.lang.currentLang);

  protected readonly faqs = signal<Faq[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly query = signal('');

  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    // The FAQ list comes from the API localized by Accept-Language, so it
    // must reload when the language changes — not only when the search text
    // changes. combineLatest re-fetches with the current query on every
    // language switch (a switch after load left Arabic questions under an
    // English UI, 23 Nov 2026).
    combineLatest([
      this.search$.pipe(debounceTime(350), distinctUntilChanged()),
      this.langChanges$,
    ])
      .pipe(
        switchMap(([q]) => {
          this.loading.set(true);
          this.error.set(false);
          return this.support.getFaqs(q);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          this.faqs.set(res.success && res.data ? res.data : []);
          this.error.set(!res.success);
        },
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        },
      });
    this.search$.next('');
  }

  protected onSearch(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.query.set(value);
    this.search$.next(value);
  }

  protected clearSearch(input: HTMLInputElement): void {
    input.value = '';
    this.query.set('');
    this.search$.next('');
  }

  protected reload(): void {
    this.search$.next(this.query());
  }
}
