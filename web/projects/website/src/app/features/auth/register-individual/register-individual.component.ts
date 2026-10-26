import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-register-individual',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="app-screen register-screen">
      <!-- Top Navigation -->
      <header class="top-nav">
        <a routerLink="/home" class="skip-btn">{{ lang.t('skip') }}</a>
        <div class="lang-selector" (click)="lang.toggleLanguage()" [title]="lang.otherLangLabel()">
          <span>{{ lang.langLabel() }}</span>
          <span class="chevron">˅</span>
        </div>
      </header>

      <div class="content-body auth-layout">
        <section class="auth-form-panel">
        <!-- Logo -->
        <div class="logo-section">
          <img src="/logo.png" alt="اعْتَمِدْ" class="screen-logo" />
        </div>

        <!-- Title -->
        <h2 class="page-title">{{ lang.t('register_ind_title') }}</h2>

        @if (errorMessage()) {
          <div class="alert alert-error">
            <span>⚠️</span>
            <p>{{ errorMessage() }}</p>
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="register-form">
          <!-- Username -->
          <div class="form-group">
            <label for="username">{{ lang.t('register_username_label') }}</label>
            <div class="input-wrapper">
              <input
                id="username"
                type="text"
                formControlName="username"
                [placeholder]="lang.t('register_username_placeholder')"
              />
            </div>
            @if (form.get('username')?.touched && form.get('username')?.invalid) {
              <small class="error-text">{{ lang.t('register_username_error') }}</small>
            }
          </div>

          <!-- Phone -->
          <div class="form-group">
            <label for="phone">{{ lang.t('register_phone_label') }}</label>
            <div class="input-wrapper">
              <input
                id="phone"
                type="tel"
                formControlName="phone"
                placeholder="05XXXXXXXX"
                dir="ltr"
              />
            </div>
            @if (form.get('phone')?.touched && form.get('phone')?.invalid) {
              <small class="error-text">{{ lang.t('register_phone_error') }}</small>
            }
          </div>

          <!-- Email -->
          <div class="form-group">
            <label for="email">{{ lang.t('register_email_label') }}</label>
            <div class="input-wrapper">
              <input
                id="email"
                type="email"
                formControlName="email"
                placeholder="name@domain.com"
                dir="ltr"
              />
            </div>
            @if (form.get('email')?.touched && form.get('email')?.invalid) {
              <small class="error-text">{{ lang.t('register_email_error') }}</small>
            }
          </div>

          <!-- Password -->
          <div class="form-group">
            <label for="password">{{ lang.t('register_password_label') }}</label>
            <div class="input-wrapper">
              <input
                id="password"
                type="password"
                formControlName="password"
                [placeholder]="lang.t('register_password_placeholder')"
              />
            </div>
            @if (form.get('password')?.touched && form.get('password')?.invalid) {
              <small class="error-text">{{ lang.t('register_password_error') }}</small>
            }
          </div>

          <!-- Terms Checkbox -->
          <div class="terms-group">
            <label class="terms-label">
              <input type="checkbox" formControlName="acceptTerms" />
              <span>{{ lang.t('register_terms_agree') }} <a href="javascript:void(0)" class="coral-link">{{ lang.t('register_terms_link') }}</a></span>
            </label>
            @if (form.get('acceptTerms')?.touched && form.get('acceptTerms')?.invalid) {
              <small class="error-text">{{ lang.t('register_terms_error') }}</small>
            }
          </div>

          <!-- Submit Button -->
          <button
            type="submit"
            class="btn btn-black"
            [disabled]="form.invalid || isLoading()"
          >
            @if (isLoading()) {
              <span>{{ lang.t('register_submitting') }}</span>
            } @else {
              <span>{{ lang.t('register_submit') }}</span>
            }
          </button>
        </form>

        <div class="login-footer">
          <p>{{ lang.t('register_has_account') }} <a routerLink="/auth/login">{{ lang.t('welcome_login') }}</a></p>
        </div>
        </section>
        <aside class="auth-brand-panel">
          <div class="brand-mark">E</div>
          <span class="brand-kicker">ETAMID · اعتمد</span>
          <h1>{{ lang.t('home_guest_title') }}</h1>
          <p>{{ lang.t('home_guest_desc') }}</p>
          <div class="brand-decoration" aria-hidden="true"><span>✓</span></div>
        </aside>
      </div>
    </div>
  `,
  styles: [`
    .register-screen {
      min-height: 100vh;
      background-color: #ffffff;
      background-image: linear-gradient(to top, rgba(241, 151, 151, 0.4) 0%, rgba(255, 255, 255, 0) 65%);
    }

    .top-nav {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.5rem 1.75rem 0.5rem;

      .skip-btn {
        color: #eb5757;
        font-weight: 700;
        font-size: 1rem;
        text-decoration: none;
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
      margin: 1rem 0 1.25rem;

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
      text-align: center;
      margin-bottom: 1.5rem;
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

    .register-form {
      display: flex;
      flex-direction: column;
      gap: 1.1rem;
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
      }
    }

    .terms-group {
      margin-top: 0.25rem;
    }

    .terms-label {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      font-size: 0.85rem;
      color: #4b5563;
      cursor: pointer;

      input[type="checkbox"] {
        width: 18px;
        height: 18px;
        accent-color: #eb5757;
        cursor: pointer;
      }

      .coral-link {
        color: #eb5757;
        text-decoration: underline;
        font-weight: 600;
      }
    }

    .error-text {
      color: #eb5757;
      font-size: 0.75rem;
      font-weight: 600;
      margin-top: 0.15rem;
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
      margin-top: 0.5rem;
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
      margin-top: 1.5rem;

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
  `]
})
export class RegisterIndividualComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    phone: ['', [Validators.required, Validators.pattern(/^05[0-9]{8}$/)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6), Validators.pattern(/[^a-zA-Z0-9]/)]],
    acceptTerms: [false, [Validators.requiredTrue]],
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const val = this.form.getRawValue();
    this.authService.registerIndividual({
      username: val.username!,
      phone: val.phone!,
      email: val.email!,
      password: val.password!,
      acceptTerms: val.acceptTerms!,
    }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.router.navigate(['/auth/verify-otp'], {
          queryParams: { phone: val.phone, email: val.email },
        });
      },
      error: (err) => {
        this.isLoading.set(false);
        const errs = err.error?.errors;
        if (Array.isArray(errs) && errs.length > 0) {
          const msg = errs.map((e: any) => e.message || e.code).join(' • ');
          this.errorMessage.set(msg);
        } else {
          this.errorMessage.set(err.error?.message || 'حدث خطأ أثناء إنشاء الحساب، يرجى المحاولة مرة أخرى.');
        }
      },
    });
  }
}
