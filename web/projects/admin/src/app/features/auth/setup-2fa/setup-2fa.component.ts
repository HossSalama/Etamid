import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminAuthService } from '../../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../../core/services/admin-language.service';

/**
 * Mandatory-2FA setup page (public, reached only from login when the admin has
 * not enabled 2FA yet). Step 1 fetches the secret with the login setup-challenge,
 * step 2 enables 2FA with the authenticator code and signs the admin in.
 */
@Component({
  selector: 'app-setup-2fa',
  imports: [FormsModule],
  templateUrl: './setup-2fa.component.html',
  styleUrl: './setup-2fa.component.scss',
})
export class Setup2faComponent implements OnInit {
  private readonly auth   = inject(AdminAuthService);
  private readonly router = inject(Router);
  protected readonly lang = inject(AdminLanguageService);

  protected secret        = signal('');
  protected otpAuthUri    = signal('');
  protected code          = '';
  protected setupLoading  = signal(true);
  protected loading       = signal(false);
  protected errorMsg      = signal('');
  private challengeToken  = '';
  private expiresInSeconds = 0;

  ngOnInit(): void {
    const state = this.router.lastSuccessfulNavigation()?.extras?.state as
      | { challengeToken?: string; expiresInSeconds?: number }
      | undefined;

    if (!state?.challengeToken) {
      this.router.navigate(['/login']);
      return;
    }
    this.challengeToken    = state.challengeToken;
    this.expiresInSeconds  = state.expiresInSeconds ?? 300;

    this.auth.setup2faWithChallenge(this.challengeToken).subscribe({
      next: (res) => {
        this.setupLoading.set(false);
        if (res.success && res.data) {
          this.secret.set(res.data.secret);
          this.otpAuthUri.set(res.data.otpAuthUri);
        } else {
          const code = res.errors?.[0]?.code ?? '';
          this.errorMsg.set(this.mapError(code, res.message));
        }
      },
      error: (err: HttpErrorResponse) => {
        this.setupLoading.set(false);
        const code = err.error?.errors?.[0]?.code ?? '';
        this.errorMsg.set(this.mapError(code, err.error?.message));
      },
    });
  }

  protected submit(): void {
    if (this.loading() || !this.secret()) return;
    this.errorMsg.set('');

    const trimmed = this.code.replace(/\s/g, '');
    if (trimmed.length !== 6) {
      this.errorMsg.set(this.lang.t('verify_2fa_invalid'));
      return;
    }

    this.loading.set(true);
    this.auth.enable2faWithChallenge(this.challengeToken, trimmed).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) {
          this.router.navigate(['/dashboard']);
        } else {
          const code = res.errors?.[0]?.code ?? '';
          this.errorMsg.set(this.mapError(code, res.message));
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const code = err.error?.errors?.[0]?.code ?? '';
        this.errorMsg.set(this.mapError(code, err.error?.message));
      },
    });
  }

  protected backToLogin(): void {
    this.router.navigate(['/login']);
  }

  private mapError(code: string, fallback?: string): string {
    switch (code) {
      case 'ADMIN_2FA_CODE_INVALID':      return this.lang.t('verify_2fa_invalid');
      case 'ADMIN_2FA_CHALLENGE_INVALID': return this.lang.t('verify_2fa_invalid_challenge');
      case 'ADMIN_2FA_SETUP_REQUIRED':    return this.lang.t('prof_setup_required');
      case 'ADMIN_ACCOUNT_LOCKED':        return this.lang.t('login_locked');
      default: return fallback ?? this.lang.t('error_generic');
    }
  }

  protected toggleLang(): void {
    this.lang.toggle();
  }
}
