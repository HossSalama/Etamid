import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="app-screen forgot-screen">
      <!-- Top Navigation -->
      <header class="top-nav">
        <button type="button" class="back-btn" (click)="goBack()">
          <span class="arrow">&gt;</span>
        </button>
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

        <!-- Stepper Indicator -->
        <div class="stepper-row">
          <div class="step-item" [class.active]="currentStep() === 1" [class.completed]="currentStep() > 1">
            <div class="step-circle">
              @if (currentStep() > 1) {
                <span>✓</span>
              } @else {
                <span>1</span>
              }
            </div>
            <span class="step-label">{{ lang.t('forgot_step_phone') }}</span>
          </div>

          <div class="step-line" [class.completed]="currentStep() > 1"></div>

          <div class="step-item" [class.active]="currentStep() === 2" [class.completed]="currentStep() > 2">
            <div class="step-circle">
              @if (currentStep() > 2) {
                <span>✓</span>
              } @else {
                <span>2</span>
              }
            </div>
            <span class="step-label">{{ lang.t('forgot_step_otp') }}</span>
          </div>

          <div class="step-line" [class.completed]="currentStep() > 2"></div>

          <div class="step-item" [class.active]="currentStep() === 3" [class.completed]="currentStep() > 3">
            <div class="step-circle">
              <span>3</span>
            </div>
            <span class="step-label">{{ lang.t('forgot_step_pass') }}</span>
          </div>
        </div>

        <!-- STEP 1: Phone Entry -->
        @if (currentStep() === 1) {
          <div class="step-content">
            <h2 class="page-title">{{ lang.t('forgot_title') }}</h2>
            <p class="step-subtitle">{{ lang.t('forgot_subtitle') }}</p>

            @if (errorMessage()) {
              <div class="alert alert-error">
                <span>⚠️</span>
                <p>{{ errorMessage() }}</p>
              </div>
            }

            <form [formGroup]="phoneForm" (ngSubmit)="onStep1Submit()" class="step-form">
              <div class="form-group">
                <label for="phone">{{ lang.t('login_phone_label') }}</label>
                <div class="input-wrapper">
                  <input
                    id="phone"
                    type="tel"
                    formControlName="phone"
                    placeholder="05XXXXXXXX"
                    dir="ltr"
                    autofocus
                  />
                </div>
                @if (phoneForm.get('phone')?.touched && phoneForm.get('phone')?.invalid) {
                  <small class="error-text">{{ lang.t('login_phone_error') }}</small>
                }
              </div>

              <button
                type="submit"
                class="btn btn-black"
                [disabled]="phoneForm.invalid || isLoading()"
              >
                @if (isLoading()) {
                  <span>...</span>
                } @else {
                  <span>{{ lang.t('forgot_next') }}</span>
                }
              </button>
            </form>
          </div>
        }

        <!-- STEP 2: OTP Entry -->
        @if (currentStep() === 2) {
          <div class="step-content">
            <h2 class="page-title">{{ lang.t('forgot_code_title') }}</h2>
            <p class="step-subtitle">
              {{ lang.t('forgot_code_subtitle') }}
              <span class="highlight-phone" dir="ltr">{{ phoneForm.get('phone')?.value }}</span>
            </p>

            @if (errorMessage()) {
              <div class="alert alert-error">
                <span>⚠️</span>
                <p>{{ errorMessage() }}</p>
              </div>
            }

        <form (submit)="$event.preventDefault(); onStep2Submit()" class="step-form">
              <!-- 6-Box OTP Inputs -->
              <div class="otp-boxes-row" dir="ltr">
                @for (digit of digits(); track $index) {
                  <input
                    [id]="'forgot-otp-' + $index"
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

              <button
                type="submit"
                class="btn btn-black"
                [disabled]="!isOtpComplete() || isLoading()"
              >
                @if (isLoading()) {
                  <span>...</span>
                } @else {
                  <span>{{ lang.t('forgot_next') }}</span>
                }
              </button>

              <div class="resend-wrapper">
                @if (countdown() > 0) {
                  <p class="resend-text">
                    {{ lang.t('verify_resend_in') }}
                    <span class="coral-timer">(00:{{ countdown() < 10 ? '0' + countdown() : countdown() }})</span>
                  </p>
                } @else {
                  <button
                    type="button"
                    class="resend-action-btn"
                    (click)="resendCode()"
                  >
                    {{ lang.t('verify_resend_now') }}
                  </button>
                }
              </div>
            </form>
          </div>
        }

        <!-- STEP 3: New Password Entry -->
        @if (currentStep() === 3) {
          <div class="step-content">
            <h2 class="page-title">{{ lang.t('forgot_new_pass_title') }}</h2>
            <p class="step-subtitle">{{ lang.t('forgot_new_pass_subtitle') }}</p>

            @if (errorMessage()) {
              <div class="alert alert-error">
                <span>⚠️</span>
                <p>{{ errorMessage() }}</p>
              </div>
            }

            <form [formGroup]="passwordForm" (ngSubmit)="onStep3Submit()" class="step-form">
              <!-- New Password -->
              <div class="form-group">
                <label for="newPassword">{{ lang.t('forgot_new_pass_label') }}</label>
                <div class="input-wrapper">
                  <input
                    id="newPassword"
                    [type]="showPass() ? 'text' : 'password'"
                    formControlName="newPassword"
                    placeholder="Pass#123"
                  />
                  <button
                    type="button"
                    class="toggle-btn"
                    (click)="showPass.set(!showPass())"
                    tabindex="-1"
                  >
                    {{ showPass() ? '👁️‍🗨️' : '👁️' }}
                  </button>
                </div>
                @if (passwordForm.get('newPassword')?.touched && passwordForm.get('newPassword')?.invalid) {
                  <small class="error-text">{{ lang.t('register_password_error') }}</small>
                }
              </div>

              <!-- Confirm Password -->
              <div class="form-group">
                <label for="confirmPassword">{{ lang.t('forgot_confirm_pass_label') }}</label>
                <div class="input-wrapper">
                  <input
                    id="confirmPassword"
                    [type]="showPass() ? 'text' : 'password'"
                    formControlName="confirmPassword"
                    placeholder="••••••••"
                  />
                </div>
                @if (passwordForm.hasError('mismatch') && passwordForm.get('confirmPassword')?.touched) {
                  <small class="error-text">{{ lang.t('forgot_pass_mismatch') }}</small>
                }
              </div>

              <button
                type="submit"
                class="btn btn-black"
                [disabled]="passwordForm.invalid || isLoading()"
              >
                @if (isLoading()) {
                  <span>...</span>
                } @else {
                  <span>{{ lang.t('forgot_submit_reset') }}</span>
                }
              </button>
            </form>
          </div>
        }

        <!-- SUCCESS SHEET / MODAL -->
        @if (isSuccess()) {
          <div class="modal-backdrop">
            <div class="success-bottom-sheet">
              <div class="shield-badge">
                <div class="shield-icon">🛡️</div>
                <div class="check-mark">✓</div>
              </div>
              <h3 class="success-title">{{ lang.t('forgot_success_title') }}</h3>
              <p class="success-subtitle">
                {{ lang.t('forgot_success_subtitle') }}
              </p>
              <a routerLink="/auth/login" class="btn btn-black btn-success-action">
                {{ lang.t('forgot_login_btn') }}
              </a>
            </div>
          </div>
        }

        <div class="login-footer">
          <p>{{ lang.t('forgot_remember') }} <a routerLink="/auth/login">{{ lang.t('welcome_login') }}</a></p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .forgot-screen {
      min-height: 100vh;
      background-color: #ffffff;
      background-image: linear-gradient(to top, rgba(241, 151, 151, 0.4) 0%, rgba(255, 255, 255, 0) 65%);
      position: relative;
    }

    .top-nav {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.5rem 1.75rem 0.5rem;

      .back-btn {
        background: none;
        border: none;
        color: #eb5757;
        font-size: 1.8rem;
        cursor: pointer;
        padding: 0;
        line-height: 1;

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
      padding: 0.5rem 2rem 3rem;
      display: flex;
      flex-direction: column;
      align-items: stretch;
    }

    .logo-section {
      text-align: center;
      margin: 0.5rem 0 1.25rem;

      .screen-logo {
        height: 60px;
        width: auto;
        object-fit: contain;
      }
    }

    /* Stepper Header (1 -> 2 -> 3) */
    .stepper-row {
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 2rem;
      padding: 0 1rem;
      gap: 0.5rem;
    }

    .step-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.35rem;

      .step-circle {
        width: 34px;
        height: 34px;
        border-radius: 50%;
        background: #f3f4f6;
        color: #9ca3af;
        display: flex;
        align-items: center;
        justify-content: center;
        font-weight: 700;
        font-size: 0.9rem;
        transition: all 0.3s;
      }

      .step-label {
        font-size: 0.75rem;
        color: #9ca3af;
        font-weight: 600;
      }

      &.active {
        .step-circle {
          background: #eb5757;
          color: #ffffff;
          box-shadow: 0 4px 10px rgba(235, 87, 87, 0.35);
        }

        .step-label {
          color: #eb5757;
          font-weight: 700;
        }
      }

      &.completed {
        .step-circle {
          background: #eb5757;
          color: #ffffff;
        }

        .step-label {
          color: #111827;
        }
      }
    }

    .step-line {
      flex: 1;
      height: 2px;
      background: #e5e7eb;
      margin-bottom: 1.25rem;
      transition: background 0.3s;

      &.completed {
        background: #eb5757;
      }
    }

    .step-content {
      display: flex;
      flex-direction: column;
      align-items: stretch;
    }

    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: #111827;
      text-align: center;
      margin-bottom: 0.35rem;
    }

    .step-subtitle {
      font-size: 0.875rem;
      color: #6b7280;
      text-align: center;
      margin-bottom: 1.75rem;
      line-height: 1.5;

      .highlight-phone {
        font-weight: 700;
        color: #111827;
        display: inline-block;
        margin-inline-start: 0.25rem;
      }
    }

    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      border-radius: 12px;
      padding: 0.75rem 1rem;
      color: #991b1b;
      font-size: 0.85rem;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1.25rem;

      p {
        margin: 0;
      }
    }

    .step-form {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;

      label {
        font-size: 0.9rem;
        font-weight: 700;
        color: #1f2937;
      }

      .input-wrapper {
        position: relative;
        display: flex;
        align-items: center;

        input {
          width: 100%;
          height: 48px;
          border: 1.5px solid #e5e7eb;
          border-radius: 12px;
          padding: 0 1rem;
          font-size: 0.95rem;
          background: #ffffff;
          outline: none;
          transition: border-color 0.2s, box-shadow 0.2s;
          box-sizing: border-box;

          &:focus {
            border-color: #eb5757;
            box-shadow: 0 0 0 3px rgba(235, 87, 87, 0.15);
          }

          &::placeholder {
            color: #9ca3af;
            font-size: 0.875rem;
          }
        }

        .toggle-btn {
          position: absolute;
          inset-inline-end: 0.75rem;
          background: none;
          border: none;
          cursor: pointer;
          font-size: 1.1rem;
          padding: 0.25rem;
        }
      }
    }

    .error-text {
      color: #eb5757;
      font-size: 0.75rem;
      font-weight: 600;
      margin-top: 0.15rem;
    }

    /* 6-box OTP */
    .otp-boxes-row {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
      margin: 0.5rem 0 1.25rem;

      .otp-box {
        width: 48px;
        height: 54px;
        border: 1.5px solid #d1d5db;
        border-radius: 12px;
        text-align: center;
        font-size: 1.4rem;
        font-weight: 800;
        color: #111827;
        background: #ffffff;
        outline: none;
        transition: all 0.2s;
        caret-color: #eb5757;

        &:focus {
          border-color: #eb5757;
          box-shadow: 0 0 0 3px rgba(235, 87, 87, 0.2);
          transform: translateY(-2px);
        }
      }
    }

    .resend-wrapper {
      text-align: center;
      margin-top: 0.5rem;

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
      text-decoration: none;
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

    .login-footer {
      text-align: center;
      margin-top: 2rem;

      p {
        font-size: 0.9rem;
        color: #6b7280;

        a {
          color: #eb5757;
          font-weight: 700;
          text-decoration: none;
          margin-inline-start: 0.25rem;

          &:hover {
            text-decoration: underline;
          }
        }
      }
    }

    /* Modal Sheet for Success */
    .modal-backdrop {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      display: flex;
      align-items: flex-end;
      justify-content: center;
      z-index: 1000;
      animation: fadeIn 0.3s ease;
    }

    .success-bottom-sheet {
      background: #ffffff;
      width: 100%;
      max-width: 440px;
      border-radius: 24px 24px 0 0;
      padding: 2.5rem 2rem 2.5rem;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      animation: slideUp 0.3s ease;
      box-shadow: 0 -10px 30px rgba(0, 0, 0, 0.15);

      .shield-badge {
        position: relative;
        width: 80px;
        height: 80px;
        margin-bottom: 1.25rem;
        display: flex;
        align-items: center;
        justify-content: center;

        .shield-icon {
          font-size: 4rem;
        }

        .check-mark {
          position: absolute;
          bottom: 4px;
          inset-inline-end: 4px;
          background: #10b981;
          color: white;
          width: 26px;
          height: 26px;
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
          font-weight: 900;
          font-size: 0.9rem;
          border: 2px solid white;
        }
      }

      .success-title {
        font-size: 1.4rem;
        font-weight: 800;
        color: #111827;
        margin-bottom: 0.5rem;
      }

      .success-subtitle {
        font-size: 0.9rem;
        color: #6b7280;
        line-height: 1.5;
        margin-bottom: 1.75rem;
      }

      .btn-success-action {
        width: 100%;
      }
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes slideUp {
      from { transform: translateY(100%); }
      to { transform: translateY(0); }
    }
  `]
})
export class ForgotPasswordComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  readonly lang = inject(LanguageService);

  readonly currentStep = signal<1 | 2 | 3>(1);
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly isSuccess = signal(false);
  readonly showPass = signal(false);
  private readonly resetToken = signal<string | null>(null);

  // OTP 6 digits
  readonly digits = signal<string[]>(['', '', '', '', '', '']);
  readonly countdown = signal(60);
  private timerRef: any = null;

  // Form 1: Phone
  readonly phoneForm = this.fb.group({
    phone: ['', [Validators.required, Validators.pattern(/^05[0-9]{8}$/)]],
  });

  // Form 3: Password
  readonly passwordForm = this.fb.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(64), Validators.pattern(/[^a-zA-Z0-9]/), Validators.pattern(/^\S+$/)]],
      confirmPassword: ['', [Validators.required]],
    },
    {
      validators: (group) => {
        const p1 = group.get('newPassword')?.value;
        const p2 = group.get('confirmPassword')?.value;
        return p1 === p2 ? null : { mismatch: true };
      },
    }
  );

  ngOnInit(): void {}

  ngOnDestroy(): void {
    this.clearCountdown();
  }

  goBack(): void {
    if (this.currentStep() === 3) {
      this.currentStep.set(2);
    } else if (this.currentStep() === 2) {
      this.currentStep.set(1);
      this.clearCountdown();
    } else {
      this.router.navigate(['/auth/login']);
    }
  }

  onStep1Submit(): void {
    if (this.phoneForm.invalid) return;

    const phone = this.phoneForm.get('phone')?.value;
    if (!phone) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.authService.forgotPassword({ phone }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.currentStep.set(2);
        this.startCountdown();
        setTimeout(() => this.focusBox(0), 100);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.apiError(err));
      },
    });
  }

  isOtpComplete(): boolean {
    return this.digits().every((d) => d.length === 1);
  }

  onStep2Submit(): void {
    if (!this.isOtpComplete()) return;

    const phone = this.phoneForm.get('phone')?.value;
    if (!phone) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.authService.verifyResetCode({ phone, code: this.digits().join('') }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        const token = res.data?.resetToken;
        if (!token) {
          this.errorMessage.set(this.lang.t('forgot_api_error'));
          return;
        }
        this.resetToken.set(token);
        this.currentStep.set(3);
        this.clearCountdown();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.apiError(err));
      },
    });
  }

  onStep3Submit(): void {
    if (this.passwordForm.invalid) return;

    const resetToken = this.resetToken();
    const values = this.passwordForm.getRawValue();
    if (!resetToken || !values.newPassword || !values.confirmPassword) {
      this.errorMessage.set(this.lang.t('forgot_api_error'));
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.authService.resetPassword({
      resetToken,
      newPassword: values.newPassword,
      confirmPassword: values.confirmPassword,
    }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.isSuccess.set(true);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.apiError(err));
      },
    });
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

    const nextIndex = Math.min(cleanNumbers.length, 5);
    this.focusBox(nextIndex);
  }

  private focusBox(index: number): void {
    const element = document.getElementById(`forgot-otp-${index}`);
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

  resendCode(): void {
    const phone = this.phoneForm.get('phone')?.value;
    if (!phone || this.isLoading()) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.authService.forgotPassword({ phone }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.digits.set(['', '', '', '', '', '']);
        this.startCountdown();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.apiError(err));
      },
    });
  }

  private apiError(err: any): string {
    const errors = err?.error?.errors;
    if (Array.isArray(errors) && errors.length > 0) {
      return errors.map((e: any) => e.message || e.code).join(' • ');
    }
    return err?.error?.message || this.lang.t('forgot_api_error');
  }
}
