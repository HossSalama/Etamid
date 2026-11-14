import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { AppUserListItem } from '../../core/models/admin.models';

/**
 * One list for both user types. The route carries the type in `data`
 * (users/companies -> 'company', users/individuals -> 'individual'),
 * so both pages share this component, its filters and its pagination.
 */
@Component({
  selector: 'app-users',
  imports: [FormsModule, RouterLink],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
})
export class UsersComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);
  private readonly route  = inject(ActivatedRoute);

  protected type          = signal<'company' | 'individual'>('company');
  protected items         = signal<AppUserListItem[]>([]);
  protected meta          = signal<ApiMeta | null>(null);
  protected loading       = signal(true);
  protected error         = signal('');
  protected page          = signal(1);
  protected filterStatus  = signal('');
  protected search        = signal('');

  protected readonly canView = this.auth.hasPermission('admin.users.view');

  protected readonly statusOptions = ['pending_verification', 'active', 'suspended'];

  ngOnInit(): void {
    this.route.data.subscribe((data) => {
      const t = data['type'] === 'individual' ? 'individual' : 'company';
      if (t !== this.type()) {
        this.type.set(t);
        this.page.set(1);
        this.filterStatus.set('');
        this.search.set('');
      }
      if (this.canView) this.load();
    });
  }

  protected get titleKey(): string {
    return this.type() === 'company' ? 'users_title_companies' : 'users_title_individuals';
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listAppUsers({
      type:   this.type(),
      status: this.filterStatus() || undefined,
      search: this.search().trim() || undefined,
      page:   this.page(),
      pageSize: 20,
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) {
          this.items.set(res.data ?? []);
          this.meta.set(res.meta ?? null);
        } else {
          this.error.set(res.message);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? this.lang.t('error_generic'));
      },
    });
  }

  protected applyFilter(): void { this.page.set(1); this.load(); }
  protected goPage(p: number): void { this.page.set(p); this.load(); }

  protected statusBadgeClass(status: string): string {
    switch (status) {
      case 'active': return 'badge-success';
      case 'suspended': return 'badge-danger';
      case 'pending_verification': return 'badge-warning';
      default: return 'badge-neutral';
    }
  }

  protected formatDate(dt: string): string {
    return new Date(dt).toLocaleString(
      this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US',
      { dateStyle: 'short', timeStyle: 'short' },
    );
  }

  protected pages(): number[] {
    const m = this.meta();
    if (!m) return [];
    return Array.from({ length: Math.min(m.totalPages, 10) }, (_, i) => i + 1);
  }
}
