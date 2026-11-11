import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { Deal } from '../../core/models/admin.models';

@Component({
  selector: 'app-deals',
  imports: [FormsModule],
  templateUrl: './deals.component.html',
  styleUrl: './deals.component.scss',
})
export class DealsComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items         = signal<Deal[]>([]);
  protected meta          = signal<ApiMeta | null>(null);
  protected loading       = signal(true);
  protected error         = signal('');
  protected page          = signal(1);
  protected filterStatus  = signal('');

  protected readonly canView = this.auth.hasPermission('admin.deals.view');

  protected readonly statusOptions = [
    'pending_acceptance',
    'funded',
    'inspection',
    'completed',
    'cancelled',
    'disputed',
    'refunded',
  ];

  ngOnInit(): void {
    if (this.canView) this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listDeals({
      status:   this.filterStatus() || undefined,
      page:     this.page(),
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

  protected formatDate(dt: string): string {
    return new Date(dt).toLocaleString(
      this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US',
      { dateStyle: 'short', timeStyle: 'short' },
    );
  }

  protected formatAmount(amount: number, currency: string): string {
    return new Intl.NumberFormat(
      this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US',
      { style: 'currency', currency, maximumFractionDigits: 2 },
    ).format(amount);
  }

  protected pages(): number[] {
    const m = this.meta();
    if (!m) return [];
    return Array.from({ length: Math.min(m.totalPages, 10) }, (_, i) => i + 1);
  }
}
