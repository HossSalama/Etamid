import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { Admin, Role } from '../../core/models/admin.models';

@Component({
  selector: 'app-admins',
  imports: [RouterLink, FormsModule],
  templateUrl: './admins.component.html',
  styleUrl: './admins.component.scss',
})
export class AdminsComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items          = signal<Admin[]>([]);
  protected meta           = signal<ApiMeta | null>(null);
  protected loading        = signal(true);
  protected error          = signal('');
  protected statusFilter   = signal('');
  protected page           = signal(1);

  protected showCreate     = signal(false);
  protected createName     = signal('');
  protected createEmail    = signal('');
  protected createPass     = signal('');
  protected createRoleIds  = signal<number[]>([]);
  protected createLoading  = signal(false);
  protected createError    = signal('');
  protected availableRoles = signal<Role[]>([]);

  protected readonly canManage    = this.auth.hasPermission('admin.roles.manage');
  protected readonly isSuperAdmin = this.auth.isSuperAdmin();

  ngOnInit(): void {
    if (this.canManage) { this.load(); this.loadRoles(); }
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listAdmins({ status: this.statusFilter() || undefined, page: this.page(), pageSize: 20 }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) { this.items.set(res.data ?? []); this.meta.set(res.meta ?? null); }
        else this.error.set(res.message);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? this.lang.t('error_generic'));
      },
    });
  }

  protected loadRoles(): void {
    this.api.listRoles().subscribe({ next: (res) => { if (res.success) this.availableRoles.set(res.data ?? []); } });
  }

  protected applyFilter(): void { this.page.set(1); this.load(); }
  protected goPage(p: number): void { this.page.set(p); this.load(); }

  protected openCreate(): void {
    this.showCreate.set(true);
    this.createName.set(''); this.createEmail.set(''); this.createPass.set('');
    this.createRoleIds.set([]); this.createError.set('');
  }

  protected closeCreate(): void { this.showCreate.set(false); this.createLoading.set(false); }

  protected isRoleChecked(id: number): boolean { return this.createRoleIds().includes(id); }

  protected toggleRole(id: number): void {
    const ids = this.createRoleIds();
    this.createRoleIds.set(ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]);
  }

  protected submitCreate(): void {
    if (this.createLoading()) return;
    if (!this.createName().trim() || !this.createEmail().trim() || !this.createPass()) {
      this.createError.set(this.lang.t('error_generic')); return;
    }
    this.createLoading.set(true);
    this.api.createAdmin({
      name: this.createName().trim(), email: this.createEmail().trim(),
      password: this.createPass(), roleIds: this.createRoleIds(),
    }).subscribe({
      next: (res) => {
        this.createLoading.set(false);
        if (res.success) { this.closeCreate(); this.load(); }
        else {
          const code = res.errors?.[0]?.code ?? '';
          this.createError.set(
            code === 'ADMIN_EMAIL_EXISTS' ? this.lang.t('adm_email_exists') :
            code === 'ADMIN_WEAK_PASSWORD' ? this.lang.t('adm_weak_password') :
            (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => {
        this.createLoading.set(false);
        const code = err.error?.errors?.[0]?.code ?? '';
        this.createError.set(
          code === 'ADMIN_EMAIL_EXISTS' ? this.lang.t('adm_email_exists') :
          code === 'ADMIN_WEAK_PASSWORD' ? this.lang.t('adm_weak_password') :
          (err.error?.message ?? this.lang.t('error_generic')));
      },
    });
  }

  protected pages(): number[] {
    const m = this.meta();
    if (!m) return [];
    return Array.from({ length: m.totalPages }, (_, i) => i + 1);
  }

  protected formatDate(dt: string | null): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'short', timeStyle: 'short' });
  }
}
