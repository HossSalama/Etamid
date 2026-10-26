import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-welcome',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="app-screen welcome-screen">
      <!-- Top Navigation -->
      <header class="top-nav">
        <a routerLink="/home" class="skip-btn">{{ lang.t('skip') }}</a>
        <div class="lang-selector" (click)="lang.toggleLanguage()" [title]="lang.otherLangLabel()">
          <span>{{ lang.langLabel() }}</span>
          <span class="chevron">˅</span>
        </div>
      </header>

      <!-- Center Logo -->
      <main class="center-content">
        <div class="logo-wrapper">
          <img src="/logo.png" alt="اعْتَمِدْ | etamid" class="main-logo" />
          <p class="tagline">{{ lang.t('welcome_tagline') }}</p>
        </div>
      </main>

      <!-- Bottom Actions -->
      <footer class="bottom-actions">
        <div class="account-choice-heading">
          <h1>{{ lang.t('welcome_choose_account_title') }}</h1>
          <p>{{ lang.t('welcome_choose_account_desc') }}</p>
        </div>
        <a routerLink="/auth/register-individual" class="btn btn-black">
          {{ lang.t('welcome_create_individual') }}
        </a>
        <a routerLink="/auth/register-company" class="btn btn-outline-coral">
          {{ lang.t('welcome_create_company') }}
        </a>
        <div class="login-wrapper">
          <span class="have-account">{{ lang.t('welcome_already_have_account') }}</span>
          <a routerLink="/auth/login" class="login-link">
            {{ lang.t('welcome_login') }}
          </a>
        </div>
      </footer>
    </div>
  `,
  styles: [`
    .welcome-screen {
      justify-content: space-between;
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
        font-size: 1.05rem;
        cursor: pointer;
        transition: opacity 0.2s;

        &:hover {
          opacity: 0.8;
        }
      }

      .lang-selector {
        color: #eb5757;
        font-weight: 700;
        font-size: 1rem;
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.25rem 0.5rem;
        border-radius: 8px;
        transition: background-color 0.2s;

        &:hover {
          background-color: rgba(235, 87, 87, 0.08);
        }

        .chevron {
          font-size: 0.75rem;
          transform: translateY(1px);
        }
      }
    }

    .center-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      flex: 1;
      padding: 1rem;

      .logo-wrapper {
        text-align: center;
        max-width: 280px;

        .main-logo {
          width: 100%;
          max-width: 200px;
          height: auto;
          object-fit: contain;
          filter: drop-shadow(0 2px 8px rgba(0, 0, 0, 0.04));
        }

        .tagline {
          font-size: 0.85rem;
          color: #6b7280;
          margin-top: 1rem;
          font-weight: 500;
          line-height: 1.5;
        }
      }
    }

    .bottom-actions {
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
      padding: 1.5rem 2rem 2.5rem;

      .btn {
        width: 100%;
        height: 52px;
        border-radius: 12px;
        font-size: 1.05rem;
        font-weight: 700;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        text-decoration: none;
        transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);

        &:active {
          transform: scale(0.98);
        }
      }

      .btn-black {
        background-color: #111827;
        color: #ffffff;
        box-shadow: 0 4px 12px rgba(17, 24, 39, 0.15);

        &:hover {
          background-color: #000000;
        }
      }

      .btn-outline-coral {
        background-color: #ffffff;
        color: #eb5757;
        border: 1.5px solid #eb5757;

        &:hover {
          background-color: #fff5f5;
        }
      }

      .login-wrapper {
        text-align: center;
        margin-top: 0.5rem;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.35rem;

        .have-account {
          font-size: 0.9rem;
          color: #6b7280;
        }

        .login-link {
          color: #eb5757;
          font-size: 0.95rem;
          font-weight: 700;
          text-decoration: none;

          &:hover {
            text-decoration: underline;
          }
        }
      }
    }
  `]
})
export class WelcomeComponent {
  readonly lang = inject(LanguageService);
}
