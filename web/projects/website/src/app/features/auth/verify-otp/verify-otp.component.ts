import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-verify-otp',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="app-screen verify-screen">
      <!-- Top Navigation -->
      <header class="top-nav">
        <a routerLink="/auth/welcome" class="back-btn" title="Back">
          <span class="arrow">&gt;</span>
        </a>
        <div class="lang-selector" (click)="lang.toggleLanguage()" [title]="lang.otherLangLabel()">
          <span>{{ lang.langLabel() }}</span>
          <span class="chevron">˅</span>
        </div>
      </header>

      <div class="content-body">
        <!-- Logo -->
        <div class="logo-section">
          <img src="/logo.png" alt="اعْتَمِدْ" class="screen-logo" />
        </div>

        <!-- Title -->
        <h2 class="page-title">{{ lang.t('verify_title') }}</h2>
        <p class="subtitle">
          {{ lang.t('verify_desc') }}
        </p>

        @if (email()) {
          <div class="email-badge" dir="ltr">
            <span>✉️</span>
            <strong>{{ email() }}</strong>
          </div>
        }

        @if (errorMessage()) {
          <div class="alert alert-error">
            <span>⚠️</span>
            <p>{{ errorMessage() }}</p>
          </div>
        }

        @if (successMessage()) {
          <div class="alert alert-success">
            <span>✅</span>
            <p>{{ successMessage() }}</p>
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="otp-form">
          <!-- 6-Box OTP Inputs -->
          <div class="otp-boxes-row" dir="ltr">
            @for (digit of digits(); track $index) {
              <input
                [id]="'otp-' + $index"
                type="text"
                inputmode="numeric"
                pattern="[0-9]*"
                maxlength="1"
                [value]="digit"
                (input)="onDigitInput($index, $event)"
                (keydown)="onKeyDown($index, $event)"
                (paste)="onPaste($event)"
                class="otp-box"
                autocomplete="off"
              />
            }
          </div>

          <!-- Submit Button -->
          <button
            type="submit"
            class="btn btn-black"
            [disabled]="form.invalid || isLoading()"
          >
            @if (isLoading()) {
              <span>{{ lang.t('verify_submitting') }}</span>
            } @else {
              <span>{{ lang.t('verify_submit') }}</span>
            }
          </button>

          <!-- Resend Section -->
          <div class="resend-wrapper">
            @if (countdown() > 0) {
              <p class="resend-text">
                {{ lang.t('verify_didnt_receive') }}
                <span class="coral-timer">{{ lang.t('verify_resend_in') }} (00:{{ countdown() < 10 ? '0' + countdown() : countdown() }})</span>
              </p>
            } @else {
              <button
                type="button"
                class="resend-action-btn"
                (click)="onResend()"
                [disabled]="isResending()"
              >
                {{ isResending() ? '...' : lang.t('verify_resend_now') }}
              </button>
            }
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .verify-screen {
      min-height: 100vh;
      background-color: #ffffff;
      background-image: linear-gradient(to top, rgba(241, 151, 151, 0.4) 0%, rgba(255, 255, 255, 0) 65%);
    }

    .top-nav {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.5rem 1.75rem 0.5rem;

      .back-btn {
        color: #eb5757;
        font-size: 1.8rem;
        line-height: 1;
        cursor: pointer;
        display: inline-flex;
        align-items: center;
        text-decoration: none;

        .arrow {
          display: inline-block;
          font-weight: 700;
        }
      }

      .lang-selector {
        display: flex;
        align-items: center;
        gap: 0.25rem;
        color: #eb5757;
        font-weight: 700;
        font-size: 0.95rem;
        cursor: pointer;
        padding: 0.25rem 0.5rem;
        border-radius: 8px;

        &:hover {
          background-color: rgba(235, 87, 87, 0.08);
        }

        .chevron {
          font-size: 0.75rem;
        }
      }
    }

    .content-body {
      padding: 1rem 2rem 2.5rem;
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
    }

    .logo-section {
      margin: 0.5rem 0 1.25rem;

      .screen-logo {
        height: 64px;
        width: auto;
        object-fit: contain;
      }
    }

    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: #111827;
      margin-bottom: 0.5rem;
    }

    .subtitle {
      font-size: 0.875rem;
      color: #6b7280;
      line-height: 1.5;
      margin-bottom: 1.25rem;
      max-width: 320px;
    }

    .email-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      background: #ffffff;
      border: 1px solid #e5e7eb;
      padding: 0.4rem 0.85rem;
      border-radius: 20px;
      font-size: 0.85rem;
      color: #374151;
      margin-bottom: 1.5rem;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);

      strong {
        color: #111827;
      }
    }

    .alert {
      width: 100%;
      border-radius: 12px;
      padding: 0.75rem 1rem;
      font-size: 0.85rem;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1.25rem;
      box-sizing: border-box;

      p {
        margin: 0;
        text-align: start;
      }
    }

    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
    }

    .alert-success {
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
      color: #166534;
    }

    .otp-form {
      width: 100%;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1.25rem;
    }

    .otp-boxes-row {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
      width: 100%;

      .otp-box {
        width: 48px;
        height: 54px;
        border: 1.5px solid #d1d5db;
        border-radius: 12px;
        text-align: center;
        font-size: 1.5rem;
        font-weight: 800;
        color: #111827;
        background: #ffffff;
        outline: none;
        transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
        caret-color: #eb5757;

        &:focus {
          border-color: #eb5757;
          box-shadow: 0 0 0 3px rgba(235, 87, 87, 0.2);
          transform: translateY(-2px);
        }
      }
    }

    .btn {
      width: 100%;
      height: 52px;
      border-radius: 12px;
      font-size: 1rem;
      font-weight: 700;
      cursor: pointer;
      border: none;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s;
    }

    .btn-black {
      background: #111827;
      color: #ffffff;

      &:hover:not(:disabled) {
        background: #000000;
        transform: translateY(-1px);
      }

      &:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
    }

    .resend-wrapper {
      margin-top: 0.75rem;

      .resend-text {
        font-size: 0.85rem;
        color: #6b7280;

        .coral-timer {
          color: #eb5757;
          font-weight: 700;
        }
      }

      .resend-action-btn {
        background: none;
        border: none;
        color: #eb5757;
        font-weight: 700;
        font-size: 0.9rem;
        cursor: pointer;
        padding: 0.25rem 0.5rem;

        &:hover {
          text-decoration: underline;
        }
      }
    }
  `]
})
export class VerifyOtpComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  readonly lang = inject(LanguageService);

  readonly email = signal<string | null>(null);
  readonly phone = signal<string | null>(null);
  readonly isLoading = signal(false);
  readonly isResending = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly digits = signal<string[]>(['', '', '', '', '', '']);
  readonly countdown = signal(60);
  private timerRef: any = null;

  readonly form = this.fb.group({
    otp: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
  });

  ngOnInit(): void {
    this.route.queryParams.subscribe((params) => {
      if (params['email']) this.email.set(params['email']);
      if (params['phone']) this.phone.set(params['phone']);
    });
    this.startCountdown();
  }

  ngOnDestroy(): void {
    this.clearCountdown();
  }

  onDigitInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value.replace(/[^0-9]/g, '');

    const currentDigits = [...this.digits()];
    if (value.length > 0) {
      currentDigits[index] = value[value.length - 1];
      this.digits.set(currentDigits);
      if (index < 5) {
        this.focusBox(index + 1);
      }
    } else {
      currentDigits[index] = '';
      this.digits.set(currentDigits);
    }

    const combinedOtp = this.digits().join('');
    this.form.patchValue({ otp: combinedOtp });
  }

  onKeyDown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace') {
      const currentDigits = [...this.digits()];
      if (!currentDigits[index] && index > 0) {
        this.focusBox(index - 1);
      }
    }
  }

  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasted = event.clipboardData?.getData('text') || '';
    const cleanNumbers = pasted.replace(/[^0-9]/g, '').slice(0, 6);
    if (!cleanNumbers) return;

    const currentDigits = [...this.digits()];
    for (let i = 0; i < 6; i++) {
      currentDigits[i] = cleanNumbers[i] || '';
    }
    this.digits.set(currentDigits);
    this.form.patchValue({ otp: currentDigits.join('') });

    const nextIndex = Math.min(cleanNumbers.length, 5);
    this.focusBox(nextIndex);
  }

  private focusBox(index: number): void {
    const element = document.getElementById(`otp-${index}`);
    if (element) {
      (element as HTMLInputElement).focus();
      (element as HTMLInputElement).select();
    }
  }

  startCountdown(): void {
    this.countdown.set(60);
    this.clearCountdown();
    this.timerRef = setInterval(() => {
      if (this.countdown() > 0) {
        this.countdown.update((c) => c - 1);
      } else {
        this.clearCountdown();
      }
    }, 1000);
  }

  private clearCountdown(): void {
    if (this.timerRef) {
      clearInterval(this.timerRef);
      this.timerRef = null;
    }
  }

  onResend(): void {
    const phoneVal = this.phone();
    if (!phoneVal) {
      this.errorMessage.set(this.lang.t('verify_code_error'));
      return;
    }

    this.isResending.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.authService.resendOtp({ phone: phoneVal }).subscribe({
      next: (res) => {
        this.isResending.set(false);
        this.successMessage.set(this.lang.t('verify_resend_success'));
        this.startCountdown();
      },
      error: (err) => {
        this.isResending.set(false);
        this.errorMessage.set(err.error?.message || 'فشل إرسال رمز التحقق. يرجى الانتظار والمحاولة لاحقاً.');
      },
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    const otpCode = this.form.get('otp')?.value;
    const phoneVal = this.phone();

    if (!otpCode || !phoneVal) {
      this.errorMessage.set('بيانات التحقق غير مكتملة.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.verifyOtp({
      phone: phoneVal,
      code: otpCode,
    }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.router.navigate(['/home']);
      },
      error: (err) => {
        this.isLoading.set(false);
        const errs = err.error?.errors;
        if (Array.isArray(errs) && errs.length > 0) {
          this.errorMessage.set(errs.map((e: any) => e.message || e.code).join(' • '));
        } else {
          this.errorMessage.set(err.error?.message || this.lang.t('verify_code_error'));
        }
      },
    });
  }
}
