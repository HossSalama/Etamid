import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminAuthService } from '../../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../../core/services/admin-language.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly auth   = inject(AdminAuthService);
  private readonly router = inject(Router);
  protected readonly lang = inject(AdminLanguageService);

  protected email     = '';
  protected password  = '';
  protected loading   = signal(false);
  protected errorMsg  = signal('');

  protected submit(): void {
    if (this.loading()) return;
    this.errorMsg.set('');

    if (!this.email.trim() || !this.password) {
      this.errorMsg.set(this.lang.t('error_generic'));
      return;
    }

    this.loading.set(true);
    this.auth.login(this.email.trim(), this.password).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          const code = res.errors?.[0]?.code ?? '';
          this.errorMsg.set(this.mapErrorCode(code, res.message));
          return;
        }
        // twoFactorRequired is handled inside auth service tap; check the result
        if (res.data?.twoFactorRequired && res.data.challenge) {
          this.router.navigate(['/login/2fa'], {
            state: {
              challengeToken:   res.data.challenge.challengeToken,
              expiresInSeconds: res.data.challenge.expiresInSeconds,
            },
          });
        } else {
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const code = err.error?.errors?.[0]?.code ?? '';
        this.errorMsg.set(this.mapErrorCode(code, err.error?.message));
      },
    });
  }

  private mapErrorCode(code: string, fallback?: string): string {
    switch (code) {
      case 'ADMIN_INVALID_CREDENTIALS': return this.lang.t('login_invalid');
      case 'ADMIN_ACCOUNT_LOCKED':      return this.lang.t('login_locked');
      case 'RATE_LIMITED':              return this.lang.t('login_rate_limited');
      default: return fallback ?? this.lang.t('error_generic');
    }
  }

  protected toggleLang(): void {
    this.lang.toggle();
  }
}
