import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminAuthService } from '../../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../../core/services/admin-language.service';

@Component({
  selector: 'app-verify-2fa',
  imports: [FormsModule],
  templateUrl: './verify-2fa.component.html',
  styleUrl: './verify-2fa.component.scss',
})
export class Verify2faComponent implements OnInit {
  private readonly auth   = inject(AdminAuthService);
  private readonly router = inject(Router);
  protected readonly lang = inject(AdminLanguageService);

  protected code            = '';
  protected loading         = signal(false);
  protected errorMsg        = signal('');
  private challengeToken    = '';
  private expiresInSeconds  = 0;

  ngOnInit(): void {
    const state = this.router.lastSuccessfulNavigation()?.extras?.state as
      | { challengeToken?: string; expiresInSeconds?: number }
      | undefined;

    if (!state?.challengeToken) {
      this.router.navigate(['/login']);
      return;
    }
    this.challengeToken   = state.challengeToken;
    this.expiresInSeconds = state.expiresInSeconds ?? 300;
  }

  protected submit(): void {
    if (this.loading()) return;
    this.errorMsg.set('');

    const trimmed = this.code.replace(/\s/g, '');
    if (trimmed.length !== 6) {
      this.errorMsg.set(this.lang.t('verify_2fa_invalid'));
      return;
    }

    this.loading.set(true);
    this.auth.verify2fa(this.challengeToken, trimmed).subscribe({
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
      case 'ADMIN_ACCOUNT_LOCKED':        return this.lang.t('login_locked');
      default: return fallback ?? this.lang.t('error_generic');
    }
  }

  protected toggleLang(): void {
    this.lang.toggle();
  }
}
