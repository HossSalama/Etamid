import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { Dispute } from '../../core/models/admin.models';

@Component({
  selector: 'app-disputes',
  imports: [RouterLink],
  templateUrl: './disputes.component.html',
  styleUrl: './disputes.component.scss',
})
export class DisputesComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items   = signal<Dispute[]>([]);
  protected meta    = signal<ApiMeta | null>(null);
  protected loading = signal(true);
  protected error   = signal('');
  protected page    = signal(1);

  protected readonly canView = this.auth.hasPermission('admin.disputes.view');

  ngOnInit(): void {
    if (this.canView) this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listDisputes({ page: this.page(), pageSize: 20 }).subscribe({
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

  protected goPage(p: number): void { this.page.set(p); this.load(); }

  protected statusBadge(status: string): string {
    switch (status) {
      case 'under_review':    return 'badge badge-warning';
      case 'awaiting_info':   return 'badge badge-info';
      case 'resolved_buyer':  return 'badge badge-success';
      case 'resolved_seller': return 'badge badge-purple';
      case 'closed':          return 'badge badge-neutral';
      default: return 'badge badge-neutral';
    }
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'under_review':    return this.lang.t('dis_status_under_review');
      case 'awaiting_info':   return this.lang.t('dis_status_awaiting_info');
      case 'resolved_buyer':  return this.lang.t('dis_status_resolved_buyer');
      case 'resolved_seller': return this.lang.t('dis_status_resolved_seller');
      case 'closed':          return this.lang.t('dis_status_closed');
      default: return status;
    }
  }

  protected formatDate(dt: string): string {
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }

  protected pages(): number[] {
    const m = this.meta();
    if (!m) return [];
    return Array.from({ length: m.totalPages }, (_, i) => i + 1);
  }
}
