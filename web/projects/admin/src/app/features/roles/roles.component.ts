import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { Role } from '../../core/models/admin.models';

@Component({
  selector: 'app-roles',
  imports: [RouterLink, FormsModule],
  templateUrl: './roles.component.html',
  styleUrl: './roles.component.scss',
})
export class RolesComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items       = signal<Role[]>([]);
  protected loading     = signal(true);
  protected error       = signal('');

  protected showCreate  = signal(false);
  protected createName  = signal('');
  protected createDesc  = signal('');
  protected createLoading = signal(false);
  protected createError   = signal('');

  protected readonly canManage    = this.auth.hasPermission('admin.roles.manage');
  protected readonly isSuperAdmin = this.auth.isSuperAdmin();

  ngOnInit(): void { if (this.canManage) this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.api.listRoles().subscribe({
      next: (res) => { this.loading.set(false); if (res.success) this.items.set(res.data ?? []); else this.error.set(res.message); },
      error: (err: HttpErrorResponse) => { this.loading.set(false); this.error.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected openCreate(): void { this.showCreate.set(true); this.createName.set(''); this.createDesc.set(''); this.createError.set(''); }
  protected closeCreate(): void { this.showCreate.set(false); this.createLoading.set(false); }

  protected submitCreate(): void {
    if (this.createLoading() || !this.createName().trim()) { this.createError.set(this.lang.t('error_generic')); return; }
    this.createLoading.set(true);
    this.api.createRole({ name: this.createName().trim(), description: this.createDesc().trim() || undefined }).subscribe({
      next: (res) => {
        this.createLoading.set(false);
        if (res.success) { this.closeCreate(); this.load(); }
        else {
          const code = res.errors?.[0]?.code ?? '';
          this.createError.set(code === 'ADMIN_ROLE_NAME_EXISTS' ? this.lang.t('rol_name_exists') : (res.message ?? this.lang.t('error_generic')));
        }
      },
      error: (err: HttpErrorResponse) => { this.createLoading.set(false); this.createError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }
}
