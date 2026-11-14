import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { AppUserDetail } from '../../core/models/admin.models';

@Component({
  selector: 'app-user-detail',
  imports: [],
  templateUrl: './user-detail.component.html',
  styleUrl: './user-detail.component.scss',
})
export class UserDetailComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected user              = signal<AppUserDetail | null>(null);
  protected loading           = signal(true);
  protected error             = signal('');
  protected deleteLoading     = signal(false);
  protected deleteError       = signal('');
  protected showDeleteConfirm = signal(false);

  protected readonly canView   = this.auth.hasPermission('admin.users.view');
  protected readonly canManage = this.auth.hasPermission('admin.users.manage');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/users/companies']); return; }
    if (!this.canView) { this.loading.set(false); return; }
    this.load(id);
  }

  private get userId(): number { return this.user()?.id ?? 0; }

  protected load(id: number): void {
    this.loading.set(true);
    this.api.getAppUser(id).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) this.user.set(res.data);
        else this.error.set(res.message);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? this.lang.t('error_generic'));
      },
    });
  }

  protected statusBadgeClass(status: string): string {
    switch (status) {
      case 'active': return 'badge-success';
      case 'suspended': return 'badge-danger';
      case 'pending_verification': return 'badge-warning';
      default: return 'badge-neutral';
    }
  }

  protected backPath(): string {
    return this.user()?.type === 'individual' ? '/users/individuals' : '/users/companies';
  }

  protected back(): void { this.router.navigate([this.backPath()]); }

  protected confirmDelete(): void { this.showDeleteConfirm.set(true); }
  protected cancelDelete(): void { this.showDeleteConfirm.set(false); }

  protected doDelete(): void {
    if (this.deleteLoading()) return;
    this.deleteLoading.set(true); this.deleteError.set('');
    this.api.deleteAppUser(this.userId).subscribe({
      next: (res) => {
        this.deleteLoading.set(false);
        if (res.success) { this.router.navigate([this.backPath()]); }
        else {
          this.deleteError.set(res.message ?? this.lang.t('error_generic'));
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

  protected formatDate(dt: string | null): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(
      this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US',
      { dateStyle: 'medium', timeStyle: 'short' },
    );
  }
}
