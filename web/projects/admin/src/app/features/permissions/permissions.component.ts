import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { Permission } from '../../core/models/admin.models';

@Component({
  selector: 'app-permissions',
  imports: [],
  templateUrl: './permissions.component.html',
  styleUrl: './permissions.component.scss',
})
export class PermissionsComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items   = signal<Permission[]>([]);
  protected loading = signal(true);
  protected error   = signal('');

  protected readonly canManage = this.auth.hasPermission('admin.roles.manage');

  ngOnInit(): void { if (this.canManage) this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.api.listPermissions().subscribe({
      next: (res) => { this.loading.set(false); if (res.success) this.items.set(res.data ?? []); else this.error.set(res.message); },
      error: (err: HttpErrorResponse) => { this.loading.set(false); this.error.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }
}
