import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { SupportTicketSummary } from '../../core/models/admin.models';

type TicketFilter = 'all' | 'open' | 'in_review' | 'closed';

@Component({
  selector: 'app-support-tickets',
  imports: [RouterLink],
  templateUrl: './support-tickets.component.html',
  styleUrl: './support-tickets.component.scss',
})
export class SupportTicketsComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items   = signal<SupportTicketSummary[]>([]);
  protected meta    = signal<ApiMeta | null>(null);
  protected loading = signal(true);
  protected error   = signal('');
  protected page    = signal(1);
  protected filter  = signal<TicketFilter>('all');

  protected readonly filters: TicketFilter[] = ['all', 'open', 'in_review', 'closed'];

  protected readonly canView = this.auth.hasPermission('admin.support.manage');

  ngOnInit(): void {
    if (this.canView) this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listSupportTickets({ status: this.filter(), page: this.page(), pageSize: 20 }).subscribe({
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

  protected setFilter(f: TicketFilter): void {
    if (this.filter() === f) return;
    this.filter.set(f);
    this.page.set(1);
    this.load();
  }

  protected goPage(p: number): void { this.page.set(p); this.load(); }

  protected statusBadge(status: string): string {
    switch (status) {
      case 'open':      return 'badge badge-warning';
      case 'in_review': return 'badge badge-info';
      case 'closed':    return 'badge badge-neutral';
      default: return 'badge badge-neutral';
    }
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'open':      return this.lang.t('sup_status_open');
      case 'in_review': return this.lang.t('sup_status_in_review');
      case 'closed':    return this.lang.t('sup_status_closed');
      default: return status;
    }
  }

  protected filterLabel(f: TicketFilter): string {
    switch (f) {
      case 'all':       return this.lang.t('sup_filter_all');
      case 'open':      return this.lang.t('sup_filter_open');
      case 'in_review': return this.lang.t('sup_filter_in_review');
      case 'closed':    return this.lang.t('sup_filter_closed');
    }
  }

  protected formatDate(dt: string | null | undefined): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }

  protected pages(): number[] {
    const m = this.meta();
    if (!m) return [];
    return Array.from({ length: m.totalPages }, (_, i) => i + 1);
  }
}
