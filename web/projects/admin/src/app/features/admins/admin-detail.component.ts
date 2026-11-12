import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { Admin, Role } from '../../core/models/admin.models';

@Component({
  selector: 'app-admin-detail',
  imports: [FormsModule],
  templateUrl: './admin-detail.component.html',
  styleUrl: './admin-detail.component.scss',
})
export class AdminDetailComponent implements OnInit {
  protected readonly api    = inject(AdminApiService);
  protected readonly auth   = inject(AdminAuthService);
  protected readonly lang   = inject(AdminLanguageService);
  private readonly route    = inject(ActivatedRoute);
  private readonly router   = inject(Router);

  protected admin          = signal<Admin | null>(null);
  protected loading        = signal(true);
  protected error          = signal('');
  protected actionLoading  = signal(false);
  protected actionError    = signal('');
  protected actionSuccess  = signal('');

  protected availableRoles = signal<Role[]>([]);
  protected selectedRoleIds= signal<number[]>([]);
  protected showRolesModal = signal(false);
  protected rolesLoading   = signal(false);
  protected rolesError     = signal('');

  protected readonly isSuperAdmin = this.auth.isSuperAdmin();
  protected readonly canManage    = this.auth.hasPermission('admin.roles.manage');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/admins']); return; }
    this.load(id);
    this.api.listRoles().subscribe({ next: (r) => { if (r.success) this.availableRoles.set(r.data ?? []); } });
  }

  private get adminId(): number { return this.admin()?.id ?? 0; }

  protected load(id?: number): void {
    const tid = id ?? this.adminId;
    this.loading.set(true);
    this.api.getAdmin(tid).subscribe({
      next: (res) => { this.loading.set(false); if (res.success) this.admin.set(res.data); else this.error.set(res.message); },
      error: (err: HttpErrorResponse) => { this.loading.set(false); this.error.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected doAction(action: 'disable' | 'enable' | 'reset-2fa'): void {
    if (this.actionLoading()) return;
    this.actionLoading.set(true); this.actionError.set(''); this.actionSuccess.set('');
    const obs = action === 'disable' ? this.api.disableAdmin(this.adminId)
      : action === 'enable' ? this.api.enableAdmin(this.adminId)
      : this.api.reset2faAdmin(this.adminId);
    obs.subscribe({
      next: (res) => {
        this.actionLoading.set(false);
        if (res.success) { this.admin.set(res.data); this.actionSuccess.set('✓'); }
        else {
          const code = res.errors?.[0]?.code ?? '';
          this.actionError.set(
            code === 'ADMIN_CANNOT_DISABLE_SELF' ? this.lang.t('adm_cannot_disable_self') :
            code === 'ADMIN_LAST_SUPER_ADMIN' ? this.lang.t('adm_last_super_admin') :
            (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.actionLoading.set(false); this.actionError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected openRolesModal(): void {
    const a = this.admin();
    if (!a) return;
    const roleIds = this.availableRoles().filter(r => a.roles.includes(r.name)).map(r => r.id);
    this.selectedRoleIds.set(roleIds);
    this.rolesError.set('');
    this.showRolesModal.set(true);
  }

  protected closeRolesModal(): void { this.showRolesModal.set(false); this.rolesLoading.set(false); }
  protected isRoleChecked(id: number): boolean { return this.selectedRoleIds().includes(id); }
  protected toggleRole(id: number): void {
    const ids = this.selectedRoleIds();
    this.selectedRoleIds.set(ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]);
  }

  protected saveRoles(): void {
    if (this.rolesLoading()) return;
    this.rolesLoading.set(true); this.rolesError.set('');
    this.api.setAdminRoles(this.adminId, this.selectedRoleIds()).subscribe({
      next: (res) => {
        this.rolesLoading.set(false);
        if (res.success) { this.admin.set(res.data); this.closeRolesModal(); }
        else {
          const code = res.errors?.[0]?.code ?? '';
          this.rolesError.set(code === 'ADMIN_LAST_SUPER_ADMIN' ? this.lang.t('adm_last_super_admin') : (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.rolesLoading.set(false); this.rolesError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected back(): void { this.router.navigate(['/admins']); }

  protected formatDate(dt: string | null): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }
}
