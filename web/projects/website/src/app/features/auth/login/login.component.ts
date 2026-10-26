import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="app-screen login-screen">
      <!-- Top Navigation -->
      <header class="top-nav">
        <a routerLink="/home" class="skip-btn">{{ lang.t('skip') }}</a>
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
        <h2 class="page-title">{{ lang.t('login_title') }}</h2>

        @if (errorMessage()) {
          <div class="alert alert-error">
            <span>⚠️</span>
            <p>{{ errorMessage() }}</p>
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="login-form">
          <!-- Phone Field -->
          <div class="form-group">
            <label for="phone">{{ lang.t('login_phone_label') }}</label>
            <div class="input-wrapper">
              <input
                id="phone"
                type="tel"
                formControlName="phone"
                [placeholder]="lang.t('login_phone_placeholder')"
                dir="ltr"
              />
            </div>
            @if (form.get('phone')?.touched && form.get('phone')?.invalid) {
              <small class="error-text">{{ lang.t('login_phone_error') }}</small>
            }
          </div>

          <!-- Password Field -->
          <div class="form-group">
            <label for="password">{{ lang.t('login_password_label') }}</label>
            <div class="input-wrapper password-wrapper">
              <input
                id="password"
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
                [placeholder]="lang.t('login_password_placeholder')"
              />
              <button
                type="button"
                class="toggle-btn"
                (click)="showPassword.set(!showPassword())"
                tabindex="-1"
              >
                {{ showPassword() ? '👁️‍🗨️' : '👁️' }}
              </button>
            </div>
            <div class="forgot-wrapper">
              <a routerLink="/auth/forgot-password" class="forgot-link">{{ lang.t('login_forgot_password') }}</a>
            </div>
          </div>

          <!-- Submit Button -->
          <button
            type="submit"
            class="btn btn-black"
            [disabled]="form.invalid || isLoading()"
          >
            @if (isLoading()) {
              <span>{{ lang.t('login_logging_in') }}</span>
            } @else {
              <span>{{ lang.t('login_submit') }}</span>
            }
          </button>
        </form>

        <div class="signup-footer">
          <p>{{ lang.t('login_no_account') }} <a routerLink="/auth/register-individual">{{ lang.t('login_register_now') }}</a></p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-screen {
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
        cursor: pointer;
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
      padding: 1.5rem 2rem 3rem;
      display: flex;
      flex-direction: column;
      align-items: stretch;
    }

    .logo-section {
      text-align: center;
      margin-bottom: 2rem;

      .screen-logo {
        height: 68px;
        width: auto;
        object-fit: contain;
      }
    }

    .page-title {
      font-size: 1.6rem;
      font-weight: 800;
      color: #111827;
      text-align: center;
      margin-bottom: 1.75rem;
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

    .login-form {
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
          height: 50px;
          border: 1.5px solid #e5e7eb;
          border-radius: 12px;
          padding: 0 1rem;
          font-size: 1rem;
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
            font-size: 0.9rem;
          }
        }
      }

      .password-wrapper {
        .toggle-btn {
          position: absolute;
          inset-inline-end: 0.75rem;
          background: none;
          border: none;
          cursor: pointer;
          font-size: 1.1rem;
          padding: 0.25rem;
          display: flex;
          align-items: center;
        }
      }

      .forgot-wrapper {
        margin-top: 0.35rem;

        .forgot-link {
          font-size: 0.85rem;
          color: #eb5757;
          font-weight: 600;
          text-decoration: none;

          &:hover {
            text-decoration: underline;
          }
        }
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
      font-size: 1.05rem;
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

    .signup-footer {
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
  `]
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal(false);

  readonly form = this.fb.group({
    phone: ['', [Validators.required, Validators.pattern(/^05[0-9]{8}$/)]],
    password: ['', [Validators.required]],
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const val = this.form.getRawValue();
    this.authService.login({
      phone: val.phone!,
      password: val.password!,
    }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.router.navigate(['/home']);
      },
      error: (err) => {
        this.isLoading.set(false);
        const errs = err.error?.errors;
        if (Array.isArray(errs) && errs.length > 0) {
          const msg = errs.map((e: any) => e.message || e.code).join(' • ');
          this.errorMessage.set(msg);
        } else {
          this.errorMessage.set(err.error?.message || 'بيانات الدخول غير صحيحة، يرجى المحاولة مرة أخرى.');
        }
      },
    });
  }
}
