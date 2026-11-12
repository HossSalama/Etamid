import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { Permission, Role } from '../../core/models/admin.models';

@Component({
  selector: 'app-role-detail',
  imports: [FormsModule],
  templateUrl: './role-detail.component.html',
  styleUrl: './role-detail.component.scss',
})
export class RoleDetailComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected role           = signal<Role | null>(null);
  protected allPermissions = signal<Permission[]>([]);
  protected loading        = signal(true);
  protected error          = signal('');
  protected selectedPerms  = signal<string[]>([]);
  protected permsLoading   = signal(false);
  protected permsError     = signal('');
  protected permsSuccess   = signal(false);
  protected deleteLoading  = signal(false);
  protected deleteError    = signal('');
  protected showDeleteConfirm = signal(false);

  protected readonly isSuperAdmin = this.auth.isSuperAdmin();

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/roles']); return; }
    this.load(id);
    this.api.listPermissions().subscribe({ next: (r) => { if (r.success) this.allPermissions.set(r.data ?? []); } });
  }

  private get roleId(): number { return this.role()?.id ?? 0; }

  protected load(id?: number): void {
    const tid = id ?? this.roleId;
    this.loading.set(true);
    this.api.getRole(tid).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) { this.role.set(res.data); this.selectedPerms.set(res.data?.permissions ?? []); }
        else this.error.set(res.message);
      },
      error: (err: HttpErrorResponse) => { this.loading.set(false); this.error.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected isPermChecked(code: string): boolean { return this.selectedPerms().includes(code); }
  protected togglePerm(code: string): void {
    const p = this.selectedPerms();
    this.selectedPerms.set(p.includes(code) ? p.filter(x => x !== code) : [...p, code]);
  }

  protected savePermissions(): void {
    if (this.permsLoading()) return;
    this.permsLoading.set(true); this.permsError.set(''); this.permsSuccess.set(false);
    this.api.setRolePermissions(this.roleId, this.selectedPerms()).subscribe({
      next: (res) => {
        this.permsLoading.set(false);
        if (res.success) { this.role.set(res.data); this.permsSuccess.set(true); }
        else {
          const code = res.errors?.[0]?.code ?? '';
          this.permsError.set(code === 'ADMIN_ROLE_PROTECTED' ? this.lang.t('rol_protected') : (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.permsLoading.set(false); this.permsError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected confirmDelete(): void { this.showDeleteConfirm.set(true); }
  protected cancelDelete():  void { this.showDeleteConfirm.set(false); }

  protected doDelete(): void {
    if (this.deleteLoading()) return;
    this.deleteLoading.set(true); this.deleteError.set('');
    this.api.deleteRole(this.roleId).subscribe({
      next: (res) => {
        this.deleteLoading.set(false);
        if (res.success) { this.router.navigate(['/roles']); }
        else {
          const code = res.errors?.[0]?.code ?? '';
          this.deleteError.set(
            code === 'ADMIN_ROLE_PROTECTED' ? this.lang.t('rol_protected') :
            code === 'ADMIN_ROLE_IN_USE' ? this.lang.t('rol_in_use') :
            (res.message ?? this.lang.t('error_generic')));
          this.showDeleteConfirm.set(false);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.deleteLoading.set(false);
        this.deleteError.set(err.error?.message ?? this.lang.t('error_generic'));
        this.showDeleteConfirm.set(false);
      },
    });
  }

  protected back(): void { this.router.navigate(['/roles']); }
}
