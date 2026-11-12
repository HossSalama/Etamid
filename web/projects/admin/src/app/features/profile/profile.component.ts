import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';

@Component({
  selector: 'app-profile',
  imports: [FormsModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected readonly admin = this.auth.currentAdmin;

  // 2FA Setup
  protected setupSecret  = signal('');
  protected setupUri     = signal('');
  protected setupLoading = signal(false);
  protected setupError   = signal('');
  protected showSetup    = signal(false);

  // 2FA Enable
  protected enableCode    = signal('');
  protected enableLoading = signal(false);
  protected enableError   = signal('');
  protected enableSuccess = signal(false);

  // 2FA Disable
  protected disablePassword = signal('');
  protected disableCode     = signal('');
  protected disableLoading  = signal(false);
  protected disableError    = signal('');
  protected disableSuccess  = signal(false);
  protected showDisable     = signal(false);

  protected doSetup(): void {
    if (this.setupLoading()) return;
    this.setupLoading.set(true); this.setupError.set('');
    this.auth.setup2fa().subscribe({
      next: (res) => {
        this.setupLoading.set(false);
        if (res.success && res.data) {
          this.setupSecret.set(res.data.secret);
          this.setupUri.set(res.data.otpAuthUri);
          this.showSetup.set(true);
        } else {
          const code = res.errors?.[0]?.code ?? '';
          this.setupError.set(code === 'ADMIN_2FA_ALREADY_ENABLED' ? this.lang.t('prof_already_enabled') : (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.setupLoading.set(false); this.setupError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected doEnable(): void {
    const code = this.enableCode().replace(/\s/g, '');
    if (this.enableLoading() || code.length !== 6) return;
    this.enableLoading.set(true); this.enableError.set(''); this.enableSuccess.set(false);
    this.auth.enable2fa(code).subscribe({
      next: (res) => {
        this.enableLoading.set(false);
        if (res.success) { this.enableSuccess.set(true); this.showSetup.set(false); this.enableCode.set(''); }
        else {
          const c = res.errors?.[0]?.code ?? '';
          this.enableError.set(
            c === 'ADMIN_2FA_CODE_INVALID'    ? this.lang.t('prof_code_invalid') :
            c === 'ADMIN_2FA_SETUP_REQUIRED'  ? this.lang.t('prof_setup_required') :
            c === 'ADMIN_2FA_ALREADY_ENABLED' ? this.lang.t('prof_already_enabled') :
            (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.enableLoading.set(false); this.enableError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected openDisable(): void { this.showDisable.set(true); this.disablePassword.set(''); this.disableCode.set(''); this.disableError.set(''); }
  protected closeDisable(): void { this.showDisable.set(false); this.disableLoading.set(false); }

  protected doDisable(): void {
    const code = this.disableCode().replace(/\s/g, '');
    if (this.disableLoading() || !this.disablePassword() || code.length !== 6) return;
    this.disableLoading.set(true); this.disableError.set(''); this.disableSuccess.set(false);
    this.auth.disable2fa(this.disablePassword(), code).subscribe({
      next: (res) => {
        this.disableLoading.set(false);
        if (res.success) { this.disableSuccess.set(true); this.closeDisable(); }
        else {
          const c = res.errors?.[0]?.code ?? '';
          this.disableError.set(
            c === 'ADMIN_2FA_CODE_INVALID'    ? this.lang.t('prof_code_invalid') :
            c === 'ADMIN_INVALID_CREDENTIALS' ? this.lang.t('prof_invalid_creds') :
            c === 'ADMIN_2FA_NOT_ENABLED'     ? this.lang.t('prof_not_enabled') :
            (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.disableLoading.set(false); this.disableError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected formatDate(dt: string | null): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }
}
